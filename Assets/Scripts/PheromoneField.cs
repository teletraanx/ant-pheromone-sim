// PheromoneField.cs
// Attach to a Quad lying on the XZ plane. Use two copies: Home (blue) and Food (green).
// Rotate X = 90° (faces up)

using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshRenderer))]
public class PheromoneField : MonoBehaviour
{
    [Header("Grid / World")]
    public int width = 128, height = 128;
    public float worldSize = 45f;            // square on XZ centered at this transform
    public bool autoAlignToXZ = true;        // scales to worldSize and rotates flat

    [Header("Field Dynamics")]
    [Min(0f)] public float diffusion = 0.1f; // D
    [Min(0f)] public float evaporation = 0.01f; // ρ

    [Header("Rendering")]
    public Material fieldMaterial;           // Unlit/Transparent (URP: Universal Render Pipeline/Unlit, Transparent)
    public Color tint = Color.green;         
    public float displayScale = 5f;          
    public int updateEveryNFrames = 2;

    // --- internals ---
    float[,] a, b;                           
    Texture2D tex;
    MeshRenderer mr;
    Material mat;                            
    Color[] pixels;

    void Awake()
    {
        mr = GetComponent<MeshRenderer>();
        InitIfNeeded();
        SetupRendererMaterial();             
    }

    void OnValidate()
    {
        mr = GetComponent<MeshRenderer>();
        InitIfNeeded();
        SetupRendererMaterial();
        UpdateTexture();
    }

    void Update()
    {
        float dt = Application.isPlaying ? Time.deltaTime : 0.016f;
        StepField(dt);
        if (Time.frameCount % Mathf.Max(1, updateEveryNFrames) == 0) UpdateTexture();
    }

    public void Deposit(Vector3 worldPos, float amount)
    {
        var (x, y) = WorldToGrid(worldPos);
        if ((uint)x < (uint)width && (uint)y < (uint)height) a[x, y] += amount;
    }

    public float Sample(Vector3 worldPos)
    {
        var (x, y) = WorldToGrid(worldPos);
        return ((uint)x < (uint)width && (uint)y < (uint)height) ? a[x, y] : 0f;
    }

    public void Clear()
    {
        System.Array.Clear(a, 0, a.Length);
        System.Array.Clear(b, 0, b.Length);
        UpdateTexture();
    }

    // Wipes a circular/elliptical area of scent (when food is eaten)
    public void ClearArea(Vector3 worldPos, float radius)
    {
        var (cx, cy) = WorldToGrid(worldPos);
        float radX = radius * width / worldSize;   
        float radY = radius * height / worldSize;   
        int minX = Mathf.Max(0, Mathf.FloorToInt(cx - radX));
        int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(cx + radX));
        int minY = Mathf.Max(0, Mathf.FloorToInt(cy - radY));
        int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(cy + radY));

        float invRadX2 = 1f / (radX * radX + 1e-6f);
        float invRadY2 = 1f / (radY * radY + 1e-6f);

        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x - cx, dy = y - cy;
                if (dx * dx * invRadX2 + dy * dy * invRadY2 <= 1f) a[x, y] = 0f;
            }
    }

    void InitIfNeeded()
    {
        width = Mathf.Max(2, width);
        height = Mathf.Max(2, height);

        if (a == null || a.GetLength(0) != width || a.GetLength(1) != height)
        {
            a = new float[width, height];
            b = new float[width, height];
            pixels = new Color[width * height];
        }

        if (tex == null || tex.width != width || tex.height != height)
        {
            tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
        }

        if (autoAlignToXZ)
        {
            transform.localScale = new Vector3(worldSize, worldSize, 1f);
            var e = transform.eulerAngles; e.x = 90f; transform.eulerAngles = e; 
        }
    }

    void SetupRendererMaterial()
    {
        if (!fieldMaterial)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) shader = Shader.Find("Unlit/Transparent");
            fieldMaterial = new Material(shader) { renderQueue = 3000 }; // Transparent
        }

        if (Application.isPlaying)
        {
            if (mr.material == null || mr.material == mr.sharedMaterial)
                mr.material = new Material(fieldMaterial);
            mat = mr.material;
        }
        else
        {
            mr.sharedMaterial = fieldMaterial;
            mat = mr.sharedMaterial;
        }

        if (mat != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);   
            else mat.mainTexture = tex;              
        }
    }

    (int x, int y) WorldToGrid(Vector3 p)
    {
        Vector3 local = p - transform.position;
        float half = worldSize * 0.5f;
        float u = Mathf.InverseLerp(-half, half, local.x);
        float v = Mathf.InverseLerp(-half, half, local.z);
        int gx = Mathf.FloorToInt(u * width);
        int gy = Mathf.FloorToInt(v * height);
        return (Mathf.Clamp(gx, 0, width - 1), Mathf.Clamp(gy, 0, height - 1));
    }

    void StepField(float dt)
    {
        if (dt <= 0f) return;

        for (int y = 1; y < height - 1; y++)
            for (int x = 1; x < width - 1; x++)
            {
                float c = a[x, y];
                float lap = a[x - 1, y] + a[x + 1, y] + a[x, y - 1] + a[x, y + 1] - 4f * c;
                float next = c + dt * (diffusion * lap) - dt * (evaporation * c);
                b[x, y] = next > 0f ? next : 0f;
            }

        for (int x = 0; x < width; x++) { b[x, 0] = a[x, 0]; b[x, height - 1] = a[x, height - 1]; }
        for (int y = 0; y < height; y++) { b[0, y] = a[0, y]; b[width - 1, y] = a[width - 1, y]; }

        var t = a; a = b; b = t;
    }

    void UpdateTexture()
    {
        int i = 0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float v = Mathf.Clamp01(a[x, y] * displayScale);
                pixels[i++] = new Color(tint.r * v, tint.g * v, tint.b * v, v); 
            }
        tex.SetPixels(pixels);
        tex.Apply(false, false);
    }
}
