using UnityEngine;

[RequireComponent(typeof(Terrain))]
public class TerrainGenerator : MonoBehaviour
{
    [Header("Aléa")]
    public int seed = 1;

    [Header("Terrain")]
    [Tooltip("Doit valoir 2^n + 1 : 257, 513, 1025...")]
    public int heightmapResolution = 513;
    public Vector3 terrainSize = new Vector3(1000f, 150f, 1000f);

    [Header("Bruit fractal (fBm)")]
    public float frequency = 3f;
    [Range(1, 8)] public int octaves = 5;
    [Range(0.1f, 0.9f)] public float persistence = 0.5f;
    public float lacunarity = 2f;
    [Tooltip("> 1 : vallées plates et sommets marqués")]
    public float redistribution = 1.6f;

    [Header("Masque en île (optionnel)")]
    public bool islandMask = false;
    [Range(0.1f, 0.95f)] public float islandStart = 0.55f;

    [Header("Érosion thermique (simple)")]
    public int erosionIterations = 0;
    public float talus = 0.004f;
    [Range(0f, 0.5f)] public float erosionStrength = 0.3f;

    // Hauteur normalisée (0..1) en un point (x01, y01) du terrain
    public float HeightAt(float x01, float y01, Vector2[] offsets)
    {
        float amp = 1f, freq = frequency, sum = 0f, norm = 0f;
        for (int o = 0; o < octaves; o++)
        {
            sum += amp * Mathf.PerlinNoise(offsets[o].x + x01 * freq, offsets[o].y + y01 * freq);
            norm += amp;
            amp *= persistence;
            freq *= lacunarity;
        }
        float h = Mathf.Pow(sum / norm, redistribution);

        if (islandMask)
        {
            float d = Mathf.Max(Mathf.Abs(x01 * 2f - 1f), Mathf.Abs(y01 * 2f - 1f));   // 0 au centre, 1 au bord
            h *= 1f - PointOps.Smooth(islandStart, 1f, d);
        }
        return Mathf.Clamp01(h);
    }

    public float[,] BuildHeights(int res)
    {
        // un décalage différent par octave, tous dérivés de la seed
        var offsets = new Vector2[octaves];
        for (int o = 0; o < octaves; o++)
            offsets[o] = new Vector2(PointOps.Range(seed, 200 + o, 0f, 1000f), PointOps.Range(seed, 300 + o, 0f, 1000f));

        var h = new float[res, res];
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
                h[y, x] = HeightAt(x / (float)(res - 1), y / (float)(res - 1), offsets);   // attention : tableau [y, x]

        if (erosionIterations > 0) ThermalErosion(h, erosionIterations, talus, erosionStrength);
        return h;
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        var terrain = GetComponent<Terrain>();
        var td = terrain.terrainData;
        td.heightmapResolution = heightmapResolution;   // à régler AVANT SetHeights
        td.size = terrainSize;
        td.SetHeights(0, 0, BuildHeights(td.heightmapResolution));
        Debug.Log("[Terrain] seed " + seed + " générée (" + td.heightmapResolution + "²).");
    }

    // Les pentes trop raides s'éboulent : on déplace de la matière vers le voisin le plus bas
    static void ThermalErosion(float[,] h, int iterations, float talus, float strength)
    {
        int n = h.GetLength(0);
        int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
        for (int it = 0; it < iterations; it++)
            for (int y = 1; y < n - 1; y++)
                for (int x = 1; x < n - 1; x++)
                {
                    float maxDiff = 0f; int bx = 0, by = 0;
                    for (int d = 0; d < 4; d++)
                    {
                        float diff = h[y, x] - h[y + dy[d], x + dx[d]];
                        if (diff > maxDiff) { maxDiff = diff; bx = x + dx[d]; by = y + dy[d]; }
                    }
                    if (maxDiff > talus)
                    {
                        float move = strength * (maxDiff - talus) * 0.5f;
                        h[y, x] -= move;
                        h[by, bx] += move;
                    }
                }
    }
}