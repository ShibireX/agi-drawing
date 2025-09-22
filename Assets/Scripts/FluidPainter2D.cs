// Attach to Main Camera (Built-in). LMB to paint.
// 1..6 colors, E = white, R = reset, P = toggle pigment mode.

using UnityEngine;

[RequireComponent(typeof(Camera))]
public class FluidPainter2D : MonoBehaviour
{
    [Header("Compute Shader & Display")]
    public ComputeShader fluidCS;
    public Material displayMaterial; // Unlit/DisplayDye

    [Header("Resolution")]
    public int width = 1024;
    public int height = 1024;

    [Header("Simulation Params")]
    [Range(0.90f, 1.0f)] public float velocityDissipation = 0.995f;
    [Range(0.90f, 1.0f)] public float dyeDissipation = 0.997f; // avoid infinite buildup
    public float gridScale = 1.0f;
    [Range(1, 200)] public int pressureIterations = 60;

    [Header("Brush")]
    public float brushRadiusPixels = 24f;
    public Color brushColor = Color.red;
    public float brushStrength = 3000f; // scales mouse velocity

    [Header("Mixing")]
    public bool usePigment = true;
    [Range(0.02f, 0.6f)] public float pigmentStrength = 0.12f; // τ per stroke unit
    [Range(0.1f, 4f)] public float pigmentDensity = 1.2f;      // display density
    public KeyCode togglePigmentKey = KeyCode.P;

    RenderTexture velA, velB, dyeA, dyeB, pressureA, pressureB, divergence;
    int kAdvect, kDiv, kJacobi, kSubgrad, kBrush, kAdvectDye;

    Vector2 prevMouseUV;
    bool hadPrev = false;

    void Start()
    {
        kAdvect    = fluidCS.FindKernel("Advect");
        kDiv       = fluidCS.FindKernel("ComputeDivergence");
        kJacobi    = fluidCS.FindKernel("Jacobi");
        kSubgrad   = fluidCS.FindKernel("SubtractGradient");
        kBrush     = fluidCS.FindKernel("ApplyBrush");
        kAdvectDye = fluidCS.FindKernel("AdvectDye");

        velA = MakeRT(width, height, RenderTextureFormat.RGFloat);
        velB = MakeRT(width, height, RenderTextureFormat.RGFloat);
        dyeA = MakeRT(width, height, RenderTextureFormat.ARGBHalf);
        dyeB = MakeRT(width, height, RenderTextureFormat.ARGBHalf);
        pressureA = MakeRT(width, height, RenderTextureFormat.RFloat);
        pressureB = MakeRT(width, height, RenderTextureFormat.RFloat);
        divergence = MakeRT(width, height, RenderTextureFormat.RFloat);

        ClearRT(velA, Color.black); ClearRT(velB, Color.black);
        ClearRT(dyeA, Color.clear); ClearRT(dyeB, Color.clear);
        ClearRT(pressureA, Color.black); ClearRT(pressureB, Color.black);
        ClearRT(divergence, Color.black);
    }

    RenderTexture MakeRT(int w, int h, RenderTextureFormat fmt)
    {
        var rt = new RenderTexture(w, h, 0, fmt, RenderTextureReadWrite.Linear)
        {
            enableRandomWrite = true,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        rt.Create();
        return rt;
    }

    void ClearRT(RenderTexture rt, Color c)
    {
        var active = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, c);
        RenderTexture.active = active;
    }

    void OnDestroy()
    {
        velA?.Release(); velB?.Release();
        dyeA?.Release(); dyeB?.Release();
        pressureA?.Release(); pressureB?.Release();
        divergence?.Release();
    }

    void Update()
    {
        HandleHotkeys();

        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);

        // 1) Advect velocity
        SetCommon(dt);
        fluidCS.SetTexture(kAdvect, "_VelocityRead", velA);
        fluidCS.SetTexture(kAdvect, "_VelocityWrite", velB);
        Dispatch(kAdvect);
        Swap(ref velA, ref velB);

        // 2) Brush
        Vector2 mouseUV;
        bool doBrush = GetMouseUV(out mouseUV);
        Vector2 brushVelUV = Vector2.zero;

        if (doBrush)
        {
            if (hadPrev) brushVelUV = (mouseUV - prevMouseUV) / Mathf.Max(dt, 1e-6f);
            prevMouseUV = mouseUV; hadPrev = true;

            SetCommon(dt, applyBrush: 1, brushPosUV: mouseUV, brushVelUV: brushVelUV * brushStrength);
            fluidCS.SetFloat("brushRadius", brushRadiusPixels);
            fluidCS.SetVector("brushColor", (Vector4)brushColor);
            fluidCS.SetFloat("pigmentStrength", pigmentStrength);

            fluidCS.SetTexture(kBrush, "_VelocityRead", velA);
            fluidCS.SetTexture(kBrush, "_VelocityWrite", velB);
            fluidCS.SetTexture(kBrush, "_DyeRead", dyeA);
            fluidCS.SetTexture(kBrush, "_DyeWrite", dyeB);
            Dispatch(kBrush);
            Swap(ref velA, ref velB);
            Swap(ref dyeA, ref dyeB);
        }
        else
        {
            hadPrev = false;
        }

        // 3) Divergence
        SetCommon(dt);
        fluidCS.SetTexture(kDiv, "_VelocityRead", velA);
        fluidCS.SetTexture(kDiv, "_Divergence", divergence);
        Dispatch(kDiv);

        // 4) Pressure solve
        ClearRT(pressureA, Color.black);
        for (int i = 0; i < pressureIterations; i++)
        {
            fluidCS.SetTexture(kJacobi, "_PressureRead", pressureA);
            fluidCS.SetTexture(kJacobi, "_PressureWrite", pressureB);
            fluidCS.SetTexture(kJacobi, "_Divergence", divergence);
            Dispatch(kJacobi);
            Swap(ref pressureA, ref pressureB);
        }

        // 5) Subtract gradient
        SetCommon(dt);
        fluidCS.SetTexture(kSubgrad, "_VelocityRead", velA);
        fluidCS.SetTexture(kSubgrad, "_VelocityWrite", velB);
        fluidCS.SetTexture(kSubgrad, "_PressureRead", pressureA);
        Dispatch(kSubgrad);
        Swap(ref velA, ref velB);

        // 6) Advect dye/τ
        SetCommon(dt);
        fluidCS.SetTexture(kAdvectDye, "_VelocityRead", velA);
        fluidCS.SetTexture(kAdvectDye, "_DyeRead", dyeA);
        fluidCS.SetTexture(kAdvectDye, "_DyeWrite", dyeB);
        Dispatch(kAdvectDye);
        Swap(ref dyeA, ref dyeB);

        // Present
        if (displayMaterial != null)
        {
            displayMaterial.mainTexture = dyeA;
            displayMaterial.SetFloat("_PigmentMode", usePigment ? 1f : 0f);
            displayMaterial.SetFloat("_Density", pigmentDensity);
        }
    }

    void SetCommon(float dt, int applyBrush = 0, Vector2 brushPosUV = default, Vector2 brushVelUV = default)
    {
        fluidCS.SetFloats("invResolution", 1f / width, 1f / height);
        fluidCS.SetFloat("dt", dt);
        fluidCS.SetFloat("dissipation", velocityDissipation);
        fluidCS.SetFloat("dyeDissipation", dyeDissipation);
        fluidCS.SetFloat("gridScale", gridScale);
        fluidCS.SetInt("applyBrush", applyBrush);
        fluidCS.SetInt("usePigment", usePigment ? 1 : 0);
        fluidCS.SetFloats("brushPos", brushPosUV.x, brushPosUV.y);
        fluidCS.SetFloats("brushVel", brushVelUV.x, brushVelUV.y);
    }

    void Dispatch(int kernel)
    {
        int gx = Mathf.CeilToInt(width / 8.0f);
        int gy = Mathf.CeilToInt(height / 8.0f);
        fluidCS.Dispatch(kernel, gx, gy, 1);
    }

    void Swap<T>(ref T a, ref T b) { (a, b) = (b, a); }

    bool GetMouseUV(out Vector2 uv)
    {
        uv = Vector2.zero;
        if (!Input.GetMouseButton(0)) return false;
        var mp = Input.mousePosition;
        uv = new Vector2(mp.x / Screen.width, mp.y / Screen.height);
        return true;
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (displayMaterial != null && displayMaterial.mainTexture != null)
            Graphics.Blit(null, dest, displayMaterial);
        else
            Graphics.Blit(src, dest);
    }

    void HandleHotkeys()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) brushColor = Color.red;
        if (Input.GetKeyDown(KeyCode.Alpha2)) brushColor = Color.green;
        if (Input.GetKeyDown(KeyCode.Alpha3)) brushColor = Color.blue;
        if (Input.GetKeyDown(KeyCode.Alpha4)) brushColor = new Color(1f, 1f, 0f); // Yellow
        if (Input.GetKeyDown(KeyCode.Alpha5)) brushColor = new Color(1f, 0f, 1f); // Magenta
        if (Input.GetKeyDown(KeyCode.Alpha6)) brushColor = new Color(0f, 1f, 1f); // Cyan
        if (Input.GetKeyDown(KeyCode.E))      brushColor = Color.white;

        if (Input.GetKeyDown(KeyCode.R))
        {
            ClearRT(dyeA, Color.clear);
            ClearRT(dyeB, Color.clear);
        }
        if (Input.GetKeyDown(togglePigmentKey))
            usePigment = !usePigment;
    }
}
