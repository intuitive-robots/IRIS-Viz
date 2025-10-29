// Assets/Pointclouds/MultiZmqFrameReceiver.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IRIS.Node;            // IRIS
using IRIS.Utilities;      // IRIS helpers

[Serializable]
public struct MultiFramePacket
{
    public int camId;
    public int width, height;
    public float fx, fy, cx, cy;
    public bool hasIntr, hasPose;
    public Matrix4x4 pose;         // T_wc (row-major)
    public byte[] rgbBytes;        // JPEG
    public byte[] depthBytes;      // width*height*2 (u16 LE)
    public bool IsValid => width > 0 && height > 0 && rgbBytes != null && depthBytes != null;
}

public class MultiZmqFrameReceiver : MonoBehaviour
{
    [Header("IRIS Subscriber (connect to Python PUB)")]
    public string host = "127.0.0.1";
    public int[] ports = new int[] { 5555, 5556 };
    public bool logConnections = true;
    public bool logPackets = false;

    private class Worker
    {
        public int port;
        public string url;
        public Subscriber<byte[]> sub;
    }

    private readonly List<Worker> workers = new();
    private readonly ConcurrentDictionary<int, MultiFramePacket> latest = new();

    // Packet layout (little-endian)
    const uint  MAGIC    = 0xABCD1234;
    const ushort FLAG_POSE = 1;
    const ushort FLAG_INTR = 2;

    void Start()
    {
        var iris = IRISXRNode.Instance;
        if (iris == null)
        {
            Debug.LogError("[IRIS] IRISXRNode is not in the scene. Add the IRISXRNode component before running.");
            enabled = false;
            return;
        }

        if (ports == null || ports.Length == 0)
        {
            Debug.LogError("[MultiZmqFrameReceiver/IRIS] No ports configured.");
            enabled = false;
            return;
        }

        foreach (var p in ports.Distinct())
            StartWorker(p);
    }

    void OnDestroy()
    {
        foreach (var w in workers.ToArray())
            StopWorker(w);
    }

    void StartWorker(int port)
    {
        var w = new Worker
        {
            port = port,
            url  = $"tcp://{host}:{port}"
        };

        // Topic string is informational; helper subscribes to "" internally.
        w.sub = new Subscriber<byte[]>("rgbd/frames", OnBytes);
        w.sub.StartSubscription(w.url);

        if (logConnections) Debug.Log($"[IRIS-Receiver] SUB {w.url}");
        workers.Add(w);
    }

    void StopWorker(Worker w)
    {
        try { w.sub?.Unsubscribe(); } catch {}
    }

    // Called by IRIS subscriber on Unity's update spin
    void OnBytes(byte[] msg)
    {
        if (TryParsePacket(msg, out var pkt) && pkt.IsValid)
        {
            latest.AddOrUpdate(pkt.camId, pkt, (k, _) => pkt);
            if (logPackets)
            {
                Debug.Log($"[IRIS-Receiver] cam={pkt.camId} {pkt.width}x{pkt.height} " +
                          $"rgb={pkt.rgbBytes?.Length} depth={pkt.depthBytes?.Length} " +
                          $"intr={pkt.hasIntr} pose={pkt.hasPose}");
            }
        }
    }

    bool TryParsePacket(byte[] msg, out MultiFramePacket pkt)
    {
        pkt = default;
        try
        {
            int o = 0;
            if (msg.Length < 4) return false;
            if (ReadUInt32LE(msg, o) != MAGIC) return false; o += 4;

            if (msg.Length < o + 2 + 2 + 4 + 8 + 4*6) return false;
            ushort version = ReadUInt16LE(msg, o); o += 2;
            ushort flags   = ReadUInt16LE(msg, o); o += 2;
            int camId      = ReadInt32LE(msg,  o); o += 4;
            ulong ts_us    = ReadUInt64LE(msg, o); o += 8;
            int width      = ReadInt32LE(msg,  o); o += 4;
            int height     = ReadInt32LE(msg,  o); o += 4;
            int rgbLen     = ReadInt32LE(msg,  o); o += 4;
            int depthLen   = ReadInt32LE(msg,  o); o += 4;

            float fx=0, fy=0, cx=0, cy=0;
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

            if (rgbLen < 0 || depthLen < 0) return false;
            if (msg.Length < o + rgbLen + depthLen) return false;

            var rgb = new byte[rgbLen];
            Buffer.BlockCopy(msg, o, rgb, 0, rgbLen); o += rgbLen;

            var depth = new byte[depthLen];
            Buffer.BlockCopy(msg, o, depth, 0, depthLen); o += depthLen;

            bool hasPose = (flags & FLAG_POSE) != 0;
            Matrix4x4 pose = Matrix4x4.identity;
            if (hasPose)
            {
                if (msg.Length < o + 64) return false;
                pose.m00 = ReadFloatLE(msg, o + 0);  pose.m01 = ReadFloatLE(msg, o + 4);
                pose.m02 = ReadFloatLE(msg, o + 8);  pose.m03 = ReadFloatLE(msg, o + 12);
                pose.m10 = ReadFloatLE(msg, o + 16); pose.m11 = ReadFloatLE(msg, o + 20);
                pose.m12 = ReadFloatLE(msg, o + 24); pose.m13 = ReadFloatLE(msg, o + 28);
                pose.m20 = ReadFloatLE(msg, o + 32); pose.m21 = ReadFloatLE(msg, o + 36);
                pose.m22 = ReadFloatLE(msg, o + 40); pose.m23 = ReadFloatLE(msg, o + 44);
                pose.m30 = ReadFloatLE(msg, o + 48); pose.m31 = ReadFloatLE(msg, o + 52);
                pose.m32 = ReadFloatLE(msg, o + 56); pose.m33 = ReadFloatLE(msg, o + 60);
                o += 64;
            }

            pkt = new MultiFramePacket
            {
                camId = camId,
                width = width, height = height,
                fx = fx, fy = fy, cx = cx, cy = cy,
                hasIntr = hasIntr, hasPose = hasPose, pose = pose,
                rgbBytes = rgb, depthBytes = depth
            };
            return true;
        }
        catch { return false; }
    }

    // ——— Public API used by MultiCamPointCloudRenderer ———
    public int[] ActiveCameraIds() => latest.Keys.OrderBy(k => k).ToArray();
    public bool TryGetLatest(int camId, out MultiFramePacket pkt) => latest.TryGetValue(camId, out pkt);
    public List<MultiFramePacket> SnapshotAll() => latest.Values.OrderBy(v => v.camId).ToList();

    // ——— little-endian readers (no unsafe) ———
    static ushort ReadUInt16LE(byte[] b, int o) => (ushort)(b[o] | (b[o+1] << 8));
    static uint   ReadUInt32LE(byte[] b, int o) => (uint)(b[o] | (b[o+1] << 8) | (b[o+2] << 16) | (b[o+3] << 24));
    static int    ReadInt32LE (byte[] b, int o) => (int)ReadUInt32LE(b,o);
    static ulong  ReadUInt64LE(byte[] b, int o) => (ulong)ReadUInt32LE(b,o) | ((ulong)ReadUInt32LE(b,o+4) << 32);
    static float  ReadFloatLE (byte[] b, int o)
    {
        if (BitConverter.IsLittleEndian) return BitConverter.ToSingle(b, o);
        var tmp = new byte[4];
        Buffer.BlockCopy(b, o, tmp, 0, 4);
        Array.Reverse(tmp);
        return BitConverter.ToSingle(tmp, 0);
    }
}
