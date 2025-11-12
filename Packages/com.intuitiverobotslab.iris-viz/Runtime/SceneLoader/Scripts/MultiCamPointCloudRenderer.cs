using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.Rendering; // AsyncGPUReadback

public class MultiCamPointCloudRenderer : MonoBehaviour
{
    [Header("Data Source")]
    public MultiZmqFrameReceiver receiver;

    [Header("Compute / Visuals")]
    public ComputeShader pointCloudCompute;     // must define: #pragma kernel CSMain
    [SerializeField] string kernelName = "CSMain";
    public VisualEffect vfx;
    [Range(0.001f, 0.2f)] public float pointSizeWorld = 0.01f;
    public int vfxCapacity = 2_000_000;

    [Header("Culling (meters)")]
    public float scale = 1.0f;
    public float cullMin = 0.10f;
    public float cullMax = 8.0f;
    public float xCull  = 0.0f;  // 0 disables
    public float yCull  = 0.0f;  // 0 disables
    public bool  doFrustumTest = true;

    [Header("Image ↔ Unity flips (match previous pipeline)")]
    public bool flipPosX = false;
    public bool flipPosY = true;     // default ON
    public bool flipRgbX = false;
    public bool flipRgbY = true;     // default ON

    [Header("Poses")]
    public bool usePacketPosesIfPresent = true;

    // ---- internals ----
    int csKernel = -1;

    class CamRes
    {
        public int camId, width, height, capacity;
        public Texture2D rgbTexture;
        public ComputeBuffer depthU32;
    }
    readonly Dictionary<int, CamRes> cams = new();

    // Ping-pong outputs: 0 = write this frame, 1 = read for VFX; then swap.
    const int BUF_COUNT = 2;
    GraphicsBuffer[] positions = new GraphicsBuffer[BUF_COUNT];   // float3 (12B)
    GraphicsBuffer[] colors    = new GraphicsBuffer[BUF_COUNT];   // float4 (16B)
    ComputeBuffer[]  validCount   = new ComputeBuffer[BUF_COUNT]; // uint[1]
    ComputeBuffer[]  visibleCount = new ComputeBuffer[BUF_COUNT]; // uint[1]
    int writeBuf = 0, readBuf = 1;
    int totalCapacity = 0;

    // Count via async readback (Unity 6 friendly)
    AsyncGPUReadbackRequest _countReq;
    bool _countPending = false;
    int  _lastVisibleForVFX = 0;
    bool _vfxSeeded = false;

    // Temp CPU depth
    uint[]   tmpDepthU32;
    ushort[] tmpDepthU16;

    // Reusable zero for counter reset (avoid per-frame alloc)
    static readonly uint[] ZERO_UINT1 = new uint[1] { 0u };

    // VFX property IDs
    static readonly int ID_PointSizeWorld = Shader.PropertyToID("PointSizeWorld");
    static readonly int ID_Count          = Shader.PropertyToID("Count");
    static readonly int ID_Positions      = Shader.PropertyToID("Positions");
    static readonly int ID_Colors         = Shader.PropertyToID("Colors");

    void OnEnable()
    {
        ResolveKernel();
        if (vfx != null) { vfx.Reinit(); vfx.Play(); }
    }

    void Start()
    {
        ResolveKernel();
        if (vfx != null) { vfx.Reinit(); vfx.Play(); }
    }

    void OnDestroy()
    {
        foreach (var c in cams.Values)
        {
            if (c.rgbTexture != null) Destroy(c.rgbTexture);
            c.depthU32?.Release();
        }
        for (int i = 0; i < BUF_COUNT; i++)
        {
            positions[i]?.Dispose();
            colors[i]?.Dispose();
            validCount[i]?.Release();
            visibleCount[i]?.Release();
        }
    }

    void ResolveKernel()
    {
        csKernel = -1;
        if (pointCloudCompute == null) return;
        try { csKernel = pointCloudCompute.FindKernel(kernelName); }
        catch { csKernel = -1; }
    }

    void Update()
    {
        if (receiver == null || pointCloudCompute == null || csKernel < 0) return;

        var frames = receiver.SnapshotAll();
        if (frames == null || frames.Count == 0) return;

        // Ensure per-cam resources & overall capacity
        int requiredTotal = 0;
        foreach (var pkt in frames)
        {
            var cr = GetOrCreateCam(pkt.camId, pkt.width, pkt.height);
            requiredTotal += cr.capacity;
        }
        if (requiredTotal > totalCapacity)
        {
            AllocateOutputs(requiredTotal);
            if (vfx != null) { vfx.Reinit(); vfx.Play(); }
        }

        // Reset counters for WRITE buffer
        validCount[writeBuf].SetData(ZERO_UINT1);
        visibleCount[writeBuf].SetData(ZERO_UINT1);

        // Bind WRITE buffers once this frame
        pointCloudCompute.SetBuffer(csKernel, "Positions",     positions[writeBuf]);
        pointCloudCompute.SetBuffer(csKernel, "Colors",        colors[writeBuf]);
        pointCloudCompute.SetBuffer(csKernel, "_ValidCount",   validCount[writeBuf]);
        pointCloudCompute.SetBuffer(csKernel, "_VisibleCount", visibleCount[writeBuf]);

        // Upload & dispatch per camera
        foreach (var pkt in frames)
        {
            var cr = cams[pkt.camId];

            // RGB upload (JPEG path)
            cr.rgbTexture.LoadImage(pkt.rgbBytes);

            // Depth upload (u16 → u32 → GPU)
            int count = cr.capacity;
            EnsureTempDepth(count);
            Buffer.BlockCopy(pkt.depthBytes, 0, tmpDepthU16, 0, count * 2);
            for (int i = 0; i < count; i++) tmpDepthU32[i] = tmpDepthU16[i];
            cr.depthU32.SetData(tmpDepthU32, 0, 0, count);

            // Per-camera uniforms
            pointCloudCompute.SetBuffer(csKernel, "depthBuffer", cr.depthU32);
            pointCloudCompute.SetInt("_Width",  cr.width);
            pointCloudCompute.SetInt("_Height", cr.height);

            pointCloudCompute.SetFloat("_Fx", pkt.hasIntr ? pkt.fx : 0f);
            pointCloudCompute.SetFloat("_Fy", pkt.hasIntr ? pkt.fy : 0f);
            pointCloudCompute.SetFloat("_Cx", pkt.hasIntr ? pkt.cx : 0f);
            pointCloudCompute.SetFloat("_Cy", pkt.hasIntr ? pkt.cy : 0f);

            pointCloudCompute.SetFloat("_Scale",     scale);
            pointCloudCompute.SetFloat("_CullMinZ",  cullMin);
            pointCloudCompute.SetFloat("_CullMaxZ",  cullMax);
            pointCloudCompute.SetFloat("_CullX",     xCull);
            pointCloudCompute.SetFloat("_CullY",     yCull);

            Matrix4x4 pose = (usePacketPosesIfPresent && pkt.hasPose)
                             ? pkt.pose
                             : Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            pointCloudCompute.SetMatrix("_PoseMatrix", pose);

            var cam = Camera.main;
            if (cam != null)
            {
                Matrix4x4 VP = cam.projectionMatrix * cam.worldToCameraMatrix;
                pointCloudCompute.SetMatrix("_VP", VP);
            }

            pointCloudCompute.SetInt("_FlipPosX",  flipPosX ? 1 : 0);
            pointCloudCompute.SetInt("_FlipPosY",  flipPosY ? 1 : 0);
            pointCloudCompute.SetInt("_FlipRgbX",  flipRgbX ? 1 : 0);
            pointCloudCompute.SetInt("_FlipRgbY",  flipRgbY ? 1 : 0);

            pointCloudCompute.SetTexture(csKernel, "_ColorTex", cr.rgbTexture);
            pointCloudCompute.SetInt("_UseColorTex", 1);
            pointCloudCompute.SetInt("_DoFrustum",   doFrustumTest ? 1 : 0);

            int tgx = Mathf.Max(1, (cr.width  + 7) / 8);
            int tgy = Mathf.Max(1, (cr.height + 7) / 8);
            pointCloudCompute.Dispatch(csKernel, tgx, tgy, 1);
        }

        // Async readback for just-written visible count (Unity 6 safe)
        if (SystemInfo.supportsAsyncGPUReadback && !_countPending)
        {
            _countPending = true;
            _countReq = AsyncGPUReadback.Request(visibleCount[writeBuf], (req) =>
            {
                if (!req.hasError)
                {
                    var data = req.GetData<uint>();
                    _lastVisibleForVFX = (int)data[0];
                }
                _countPending = false;
            });
        }

        // Swap ping-pong
        int tmpIdx = readBuf; readBuf = writeBuf; writeBuf = tmpIdx;

        // Bind READ buffers and drive VFX
        if (vfx != null)
        {
            vfx.SetGraphicsBuffer(ID_Positions, positions[readBuf]);
            vfx.SetGraphicsBuffer(ID_Colors,    colors[readBuf]);

            int countForVFX = Mathf.Clamp(_lastVisibleForVFX, 0,
                                Mathf.Min(vfxCapacity, totalCapacity));
            vfx.SetInt(ID_Count, countForVFX);
            vfx.SetFloat(ID_PointSizeWorld, pointSizeWorld);

            // Seed once when points become available (no manual interaction needed)
            if (!_vfxSeeded && countForVFX > 0)
            {
                vfx.Reinit();
                // rebind after Reinit (properties reset)
                vfx.SetGraphicsBuffer(ID_Positions, positions[readBuf]);
                vfx.SetGraphicsBuffer(ID_Colors,    colors[readBuf]);
                vfx.SetInt(ID_Count, countForVFX);
                vfx.SetFloat(ID_PointSizeWorld, pointSizeWorld);
                vfx.Play();
                _vfxSeeded = true;
            }
        }
    }

    CamRes GetOrCreateCam(int camId, int w, int h)
    {
        if (!cams.TryGetValue(camId, out var cr))
        {
            cr = new CamRes { camId = camId };
            cams[camId] = cr;
        }
        cr.width  = Mathf.Max(1, w);
        cr.height = Mathf.Max(1, h);
        int cap = cr.width * cr.height;

        if (cr.rgbTexture == null || cr.rgbTexture.width != cr.width || cr.rgbTexture.height != cr.height)
        {
            if (cr.rgbTexture != null) Destroy(cr.rgbTexture);
            cr.rgbTexture = new Texture2D(cr.width, cr.height, TextureFormat.RGBA32, false, false);
            cr.rgbTexture.filterMode = FilterMode.Bilinear;   // smoother sampling
            cr.rgbTexture.wrapMode   = TextureWrapMode.Clamp; // avoid edge artifacts
        }
        if (cr.depthU32 == null || cr.depthU32.count < cap)
        {
            cr.depthU32?.Release();
            cr.depthU32 = new ComputeBuffer(cap, sizeof(uint), ComputeBufferType.Structured);
        }

        cr.capacity = cap;
        return cr;
    }

    void AllocateOutputs(int requiredTotalCapacity)
    {
        totalCapacity = Mathf.Max(1, requiredTotalCapacity);

        for (int i = 0; i < BUF_COUNT; i++)
        {
            positions[i]?.Dispose();
            colors[i]?.Dispose();
            validCount[i]?.Release();
            visibleCount[i]?.Release();

            positions[i]    = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalCapacity, sizeof(float) * 3);
            colors[i]       = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalCapacity, sizeof(float) * 4);
            validCount[i]   = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Structured);
            visibleCount[i] = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Structured);
        }

        writeBuf = 0; readBuf = 1;
        _lastVisibleForVFX = 0;
        _countPending = false;
        _vfxSeeded = false;

        if (vfx != null)
        {
            vfx.SetGraphicsBuffer(ID_Positions, positions[readBuf]);
            vfx.SetGraphicsBuffer(ID_Colors,    colors[readBuf]);
            vfx.SetFloat(ID_PointSizeWorld, pointSizeWorld);
            vfx.SetInt(ID_Count, 0);
        }
    }

    void EnsureTempDepth(int count)
    {
        if (tmpDepthU16 == null || tmpDepthU16.Length < count) tmpDepthU16 = new ushort[count];
        if (tmpDepthU32 == null || tmpDepthU32.Length < count) tmpDepthU32 = new uint[count];
    }
    public int RenderedPointCountDebug
    {
    get { return _lastVisibleForVFX; }
    }

}