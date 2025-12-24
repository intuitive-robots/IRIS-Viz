using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

using IRIS.Node;        // IRISXRNode, IRISService<Req,Resp>
using IRIS.Utilities;  // Subscriber<T>

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

[Serializable]
public struct SubscribePointCloudStreamRequest
{
    public int camId;     // e.g., 1
    public string topic;  // e.g., "RGBD/Cam1"
    public string url;    // e.g., "tcp://10.0.0.5:56423"
}

public class MultiZmqFrameReceiver : MonoBehaviour
{
    [Header("Logging")]
    [Tooltip("Log when (re)subscribing to topics")]
    public bool logSubscribe = true;

    [Tooltip("Log every received/parsed packet (verbose)")]
    public bool logPackets = false;

    [SerializeField] private GameObject objectToActivate;
    private bool _pendingActivate = false;

    // camId -> latest decoded packet
    private readonly ConcurrentDictionary<int, MultiFramePacket> _latest = new();

    private class SubEntry
    {
        public Subscriber<byte[]> sub;
        public string url;
        public string topic;
    }

    // camId -> subscriber info
    private readonly Dictionary<int, SubEntry> _subs = new();

    // IRIS service handle so we can unregister on destroy
    private IRISService<SubscribePointCloudStreamRequest, string> _subscribeSvc;

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

        // Register IRIS service: SimPublisher calls this with {camId, topic, url}
        _subscribeSvc = new IRISService<SubscribePointCloudStreamRequest, string>(
            "SubscribePointCloudStream",
            HandleSubscribePointCloudStream
        );
    }

    private void OnDestroy()
    {
        try { _subscribeSvc?.Unregister(); } catch { }

        foreach (var kv in _subs)
        {
            try { kv.Value.sub?.Unsubscribe(); } catch { }
            // NOTE: IRIS.Utilities.Subscriber<T> has no Dispose(); Unsubscribe() is sufficient.
        }

        _subs.Clear();
        _latest.Clear();
    }

    // =================== IRIS Service handler ===================

    private string HandleSubscribePointCloudStream(SubscribePointCloudStreamRequest req)
    {
        if (req.camId <= 0 || string.IsNullOrEmpty(req.topic) || string.IsNullOrEmpty(req.url))
        {
            Debug.LogWarning("[MultiZmqFrameReceiver] SubscribePointCloudStream: bad request");
            return "bad-request";
        }

        RegisterStream(req.camId, req.url, req.topic);
        _pendingActivate = true;
        return "ok";
    }

    private void Update()
    {
        if (!_pendingActivate)
            return;

        // Clear the flag first to avoid repeated activation
        _pendingActivate = false;

        if (objectToActivate != null && !objectToActivate.activeSelf)
        {
            objectToActivate.SetActive(true);
        }
    }

    // =================== Public API (used by renderer) ===================

    public bool TryGetLatest(int camId, out MultiFramePacket pkt)
        => _latest.TryGetValue(camId, out pkt);

    public int[] ActiveCameraIds()
        => _latest.Keys.OrderBy(k => k).ToArray();

    public List<MultiFramePacket> SnapshotAll()
        => _latest.Values.OrderBy(v => v.camId).ToList();

    // Optional: manual wiring from another script/Inspector
    public void RegisterStream(int camId, string url, string topic)
        => EnsureSubscribed(camId, topic, url);

    // =================== ZMQ SUB management ===================

    private void EnsureSubscribed(int camId, string topic, string url)
    {
        if (_subs.TryGetValue(camId, out var existing))
        {
            // Already correct?
            if (existing.url == url && existing.topic == topic)
                return;

            // Sender restarted / port or topic changed → clean up and rewire
            try { existing.sub?.Unsubscribe(); } catch { }
            _subs.Remove(camId);
        }

        var sub = new Subscriber<byte[]>(
            topic,
            data => OnFrameBytes(camId, data)
        );

        sub.StartSubscription(url, topic);
        _subs[camId] = new SubEntry { sub = sub, url = url, topic = topic };

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
                $"rgb={(pkt.rgbBytes?.Length ?? 0)} depth={(pkt.depthBytes?.Length ?? 0)} " +
                $"intr={pkt.hasIntr} pose={pkt.hasPose}"
            );
        }
    }

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

            // RGB + Depth
            if (msg.Length < o + rgbLen + depthLen)
                return false;

            byte[] rgb = null;
            if (rgbLen > 0)
            {
                rgb = new byte[rgbLen];
                Buffer.BlockCopy(msg, o, rgb, 0, rgbLen);
                o += rgbLen;
            }

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
