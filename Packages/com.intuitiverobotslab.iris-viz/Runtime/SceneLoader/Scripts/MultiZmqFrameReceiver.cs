using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

using IRIS.Node;        // IRISXRNode, UnityMainThreadDispatcher
using IRIS.Utilities;  // Subscriber<T>, UnityPortSet, MsgUtils, NodeInfo
using NetMQ;
using NetMQ.Sockets;

[Serializable]
public struct MultiFramePacket
{
    public int camId;
    public int width, height;

    public float fx, fy, cx, cy;
    public bool hasIntr, hasPose;
    public Matrix4x4 pose;    // T_wc (row-major)

    public byte[] rgbBytes;   // JPEG
    public byte[] depthBytes; // width*height*2 (uint16 LE)

    public bool IsValid =>
        width > 0 &&
        height > 0 &&
        depthBytes != null &&
        depthBytes.Length >= width * height * 2;
}

public class MultiZmqFrameReceiver : MonoBehaviour
{
    [Header("Topics")]
    [Tooltip("Prefix used by Streamer.py topics, e.g. 'RGBD/Cam' -> 'RGBD/Cam1'")]
    public string topicPrefix = "RGBD/Cam";

    [Tooltip("Camera IDs to subscribe to (topics = prefix + id)")]
    public int[] cameraIds = new[] { 1 };

    [Header("Discovery")]
    [Tooltip("Use IRIS multicast + GetNodeInfo to auto-wire SimPublisher topics")]
    public bool autoDiscoverSimPublisher = true;

    [Header("Debug Logging")]
    [Tooltip("Log discovery beacons and NodeInfo results")]
    public bool logDiscovery = false;

    [Tooltip("Log when (re)subscribing to topics")]
    public bool logSubscribe = true;

    [Tooltip("Log every received/parsed packet (verbose)")]
    public bool logPackets = false;

    // camId -> latest decoded packet
    private readonly ConcurrentDictionary<int, MultiFramePacket> _latest = new();

    private class SubEntry
    {
        public Subscriber<byte[]> sub;
        public string url;
    }

    // camId -> subscriber info
    private readonly Dictionary<int, SubEntry> _subs = new();

    // Discovery
    private UdpClient _mcClient;
    private CancellationTokenSource _discCts;

    // PacketV2 constants (must match Python PacketV2Writer)
    private const uint MAGIC = 0xABCD1234;
    private const ushort FLAG_POSE = 1;
    private const ushort FLAG_INTR = 2;

    private void Start()
    {
        if (IRISXRNode.Instance == null)
        {
            Debug.LogError("[MultiZmqFrameReceiver] Missing IRISXRNode in scene.");
            enabled = false;
            return;
        }

        if (cameraIds == null || cameraIds.Length == 0)
            cameraIds = new[] { 1 };

        if (autoDiscoverSimPublisher)
        {
            StartDiscovery();
        }
        else if (logDiscovery)
        {
            Debug.Log("[MultiZmqFrameReceiver] Auto-discovery disabled; expecting manual wiring.");
        }
    }

    private void OnDestroy()
    {
        StopDiscovery();

        foreach (var kv in _subs)
        {
            try { kv.Value.sub.Unsubscribe(); } catch { }
        }

        _subs.Clear();
        _latest.Clear();
    }

    // =================== Discovery ===================

    private void StartDiscovery()
    {
        if (_discCts != null)
            return;

        _discCts = new CancellationTokenSource();
        var token = _discCts.Token;

        Task.Run(async () =>
        {
            try
            {
                _mcClient = new UdpClient();
                _mcClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _mcClient.Client.Bind(new IPEndPoint(IPAddress.Any, UnityPortSet.DISCOVERY));
                _mcClient.JoinMulticastGroup(IPAddress.Parse("239.255.10.10"));

                if (logDiscovery)
                    Debug.Log("[MultiZmqFrameReceiver] Listening on 239.255.10.10:7720 for IRIS nodes...");

                while (!token.IsCancellationRequested)
                {
                    var recvTask = _mcClient.ReceiveAsync();
                    var completed = await Task.WhenAny(recvTask, Task.Delay(1000, token));
                    if (completed != recvTask)
                        continue; // timeout / check cancel

                    var result = recvTask.Result;
                    string remoteIP = result.RemoteEndPoint.Address.ToString();
                    string s = Encoding.UTF8.GetString(result.Buffer);

                    // IRIS beacon: nodeID(36) + nodeInfoID(36) + servicePort(ascii)
                    if (s.Length < 72)
                        continue;

                    if (!int.TryParse(s.Substring(72), out int servicePort))
                        continue;

                    if (logDiscovery)
                        Debug.Log($"[MultiZmqFrameReceiver] Beacon from {remoteIP}:{servicePort}");

                    TryFetchNodeInfoAndWire(remoteIP, servicePort);
                }
            }
            catch (ObjectDisposedException)
            {
                // Normal during shutdown
            }
            catch (Exception e)
            {
                if (logDiscovery)
                    Debug.LogWarning($"[MultiZmqFrameReceiver] Discovery error: {e.Message}");
            }
        }, token);
    }

    private void StopDiscovery()
    {
        if (_discCts == null)
            return;

        _discCts.Cancel();

        try
        {
            _mcClient?.DropMulticastGroup(IPAddress.Parse("239.255.10.10"));
        }
        catch { }

        try
        {
            _mcClient?.Close();
        }
        catch { }

        _mcClient = null;

        _discCts.Dispose();
        _discCts = null;
    }

    private void TryFetchNodeInfoAndWire(string remoteIP, int servicePort)
    {
        Task.Run(() =>
        {
            string svcUrl = $"tcp://{remoteIP}:{servicePort}";

            try
            {
                using (var req = new RequestSocket())
                {
                    req.Connect(svcUrl);

                    // IRIS service: ["GetNodeInfo", <payload>]
                    var msg = new NetMQMessage();
                    msg.Append("GetNodeInfo");
                    msg.Append(MsgUtils.String2Bytes("")); // empty payload
                    req.SendMultipartMessage(msg);

                    if (!req.TryReceiveFrameBytes(TimeSpan.FromMilliseconds(1000), out var data) ||
                        data == null || data.Length == 0)
                    {
                        if (logDiscovery)
                            Debug.LogWarning($"[MultiZmqFrameReceiver] GetNodeInfo timeout from {svcUrl}");
                        return;
                    }

                    NodeInfo ni;
                    try
                    {
                        ni = MsgUtils.BytesDeserialize2Object<NodeInfo>(data);
                    }
                    catch (Exception ex)
                    {
                        if (logDiscovery)
                            Debug.LogWarning($"[MultiZmqFrameReceiver] Invalid NodeInfo from {svcUrl}: {ex.Message}");
                        return;
                    }

                    if (ni.topicDict == null || ni.topicDict.Count == 0)
                        return;

                    // For each camera we care about, wire if topic exists
                    foreach (int camId in cameraIds)
                    {
                        string topic = $"{topicPrefix}{camId}";
                        if (!ni.topicDict.TryGetValue(topic, out int topicPort))
                            continue;

                        string url = $"tcp://{remoteIP}:{topicPort}";

                        UnityMainThreadDispatcher.Instance.Enqueue(() =>
                        {
                            EnsureSubscribed(camId, topic, url);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                if (logDiscovery)
                    Debug.LogWarning($"[MultiZmqFrameReceiver] GetNodeInfo failed from {svcUrl}: {ex.Message}");
            }
        });
    }

    private void EnsureSubscribed(int camId, string topic, string url)
    {
        if (_subs.TryGetValue(camId, out var existing))
        {
            if (existing.url == url)
                return;

            // Port changed (e.g. sender restarted) → clean up and rewire
            try { existing.sub.Unsubscribe(); } catch { }
            _subs.Remove(camId);
        }

        var sub = new Subscriber<byte[]>(
            topic,
            data => OnFrameBytes(camId, data)
        );

        sub.StartSubscription(url, topic);
        _subs[camId] = new SubEntry { sub = sub, url = url };

        if (logSubscribe)
            Debug.Log($"[MultiZmqFrameReceiver] Cam{camId}: {topic} @ {url}");
    }

    // =================== Frame handling ===================

    private void OnFrameBytes(int camIdFromTopic, byte[] msg)
    {
        if (msg == null || msg.Length == 0)
            return;

        if (!TryParsePacket(msg, out var pkt))
        {
            if (logPackets)
                Debug.LogWarning($"[MultiZmqFrameReceiver] Parse failed (Cam{camIdFromTopic})");
            return;
        }

        if (pkt.camId <= 0)
            pkt.camId = camIdFromTopic;

        if (!pkt.IsValid)
        {
            if (logPackets)
            {
                int dlen = pkt.depthBytes != null ? pkt.depthBytes.Length : 0;
                Debug.LogWarning($"[MultiZmqFrameReceiver] Invalid pkt Cam{pkt.camId}: {pkt.width}x{pkt.height} depth={dlen}");
            }
            return;
        }

        _latest[pkt.camId] = pkt;

        if (logPackets)
        {
            Debug.Log(
                $"[MultiZmqFrameReceiver] Cam{pkt.camId} {pkt.width}x{pkt.height} " +
                $"rgb={pkt.rgbBytes?.Length ?? 0} depth={pkt.depthBytes?.Length ?? 0} " +
                $"intr={pkt.hasIntr} pose={pkt.hasPose}"
            );
        }
    }

    // =================== Public API ===================

    public bool TryGetLatest(int camId, out MultiFramePacket pkt)
        => _latest.TryGetValue(camId, out pkt);

    public int[] ActiveCameraIds()
        => _latest.Keys.OrderBy(k => k).ToArray();

    public List<MultiFramePacket> SnapshotAll()
        => _latest.Values.OrderBy(v => v.camId).ToList();

    // =================== PacketV2 parsing ===================

    private bool TryParsePacket(byte[] msg, out MultiFramePacket pkt)
    {
        pkt = default;

        try
        {
            int o = 0;
            if (msg.Length < 4) return false;

            uint magic = ReadUInt32LE(msg, o); o += 4;
            if (magic != MAGIC) return false;

            if (msg.Length < o + 2 + 2 + 4 + 8 + 4 + 4 + 4 + 4)
                return false;

            ushort version = ReadUInt16LE(msg, o); o += 2;
            ushort flags = ReadUInt16LE(msg, o); o += 2;
            int camId = ReadInt32LE(msg, o); o += 4;
            ulong ts_us = ReadUInt64LE(msg, o); o += 8;
            int width = ReadInt32LE(msg, o); o += 4;
            int height = ReadInt32LE(msg, o); o += 4;
            int rgbLen = ReadInt32LE(msg, o); o += 4;
            int depthLen = ReadInt32LE(msg, o); o += 4;

            if (width <= 0 || height <= 0 || rgbLen < 0 || depthLen < 0)
                return false;

            // Intrinsics
            float fx = 0, fy = 0, cx = 0, cy = 0;
            bool hasIntr = (flags & FLAG_INTR) != 0;
            if (hasIntr)
            {
                if (msg.Length < o + 16) return false;
                fx = ReadFloatLE(msg, o + 0);
                fy = ReadFloatLE(msg, o + 4);
                cx = ReadFloatLE(msg, o + 8);
                cy = ReadFloatLE(msg, o + 12);
                o += 16;
            }

            // RGB
            if (msg.Length < o + rgbLen + depthLen)
                return false;

            byte[] rgb = null;
            if (rgbLen > 0)
            {
                rgb = new byte[rgbLen];
                Buffer.BlockCopy(msg, o, rgb, 0, rgbLen);
                o += rgbLen;
            }

            // Depth
            byte[] depth = null;
            if (depthLen > 0)
            {
                depth = new byte[depthLen];
                Buffer.BlockCopy(msg, o, depth, 0, depthLen);
                o += depthLen;
            }

            // Pose
            bool hasPose = (flags & FLAG_POSE) != 0;
            Matrix4x4 pose = Matrix4x4.identity;
            if (hasPose)
            {
                if (msg.Length < o + 16 * 4) return false;
                pose = ReadMatrix4x4LE(msg, o);
                o += 16 * 4;
            }

            pkt = new MultiFramePacket
            {
                camId = camId,
                width = width,
                height = height,
                fx = fx,
                fy = fy,
                cx = cx,
                cy = cy,
                hasIntr = hasIntr,
                hasPose = hasPose,
                pose = pose,
                rgbBytes = rgb,
                depthBytes = depth
            };

            return true;
        }
        catch
        {
            return false;
        }
    }

    // =================== LE helpers ===================

    private static ushort ReadUInt16LE(byte[] b, int o)
        => (ushort)(b[o] | (b[o + 1] << 8));

    private static uint ReadUInt32LE(byte[] b, int o)
        => (uint)(b[o] |
                  (b[o + 1] << 8) |
                  (b[o + 2] << 16) |
                  (b[o + 3] << 24));

    private static int ReadInt32LE(byte[] b, int o)
        => (int)ReadUInt32LE(b, o);

    private static ulong ReadUInt64LE(byte[] b, int o)
    {
        uint lo = ReadUInt32LE(b, o);
        uint hi = ReadUInt32LE(b, o + 4);
        return ((ulong)hi << 32) | lo;
    }

    private static float ReadFloatLE(byte[] b, int o)
    {
        if (BitConverter.IsLittleEndian)
            return BitConverter.ToSingle(b, o);

        var tmp = new byte[4];
        Buffer.BlockCopy(b, o, tmp, 0, 4);
        Array.Reverse(tmp);
        return BitConverter.ToSingle(tmp, 0);
    }

    private static Matrix4x4 ReadMatrix4x4LE(byte[] b, int o)
    {
        Matrix4x4 m = new Matrix4x4();
        for (int r = 0; r < 4; ++r)
        {
            for (int c = 0; c < 4; ++c)
            {
                m[r, c] = ReadFloatLE(b, o);
                o += 4;
            }
        }
        return m;
    }
}
