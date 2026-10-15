using UnityEngine;

/// <summary>Peint les calques du terrain selon la pente, l'altitude et du bruit.
/// Ordre des calques : 0 = herbe, 1 = roche, 2 = terre.</summary>
[RequireComponent(typeof(Terrain))]
public class TerrainPainter : MonoBehaviour
{
    public int seed = 1;

    [Header("Roche (pente en degrés)")]
    public float rockSlopeStart = 28f;
    public float rockSlopeEnd = 42f;

    [Header("Terre")]
    [Range(0f, 1f)] public float lowLevel = 0.10f;      // altitude normalisée sous laquelle on trouve de la terre
    [Range(0f, 0.3f)] public float lowBlend = 0.05f;
    public float patchFrequency = 6f;                   // taches de terre dans l'herbe
    [Range(0f, 1f)] public float patchStart = 0.60f;
    [Range(0f, 1f)] public float patchEnd = 0.75f;

    [ContextMenu("Paint")]
    public void Paint()
    {
        var terrain = GetComponent<Terrain>();
        var td = terrain.terrainData;
        if (td.alphamapLayers < 3)
        {
            Debug.LogError("Il faut au moins 3 Terrain Layers (herbe, roche, terre) sur le terrain.", this);
            return;
        }

        int w = td.alphamapWidth, h = td.alphamapHeight, layers = td.alphamapLayers;
        var maps = new float[h, w, layers];
        float ox = PointOps.Range(seed, 400, 0f, 1000f), oy = PointOps.Range(seed, 401, 0f, 1000f);

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float nx = x / (float)(w - 1), ny = y / (float)(h - 1);
                float height01 = td.GetInterpolatedHeight(nx, ny) / td.size.y;
                float slope = td.GetSteepness(nx, ny);

                float rock = PointOps.Smooth(rockSlopeStart, rockSlopeEnd, slope);
                float low = 1f - PointOps.Smooth(lowLevel, lowLevel + lowBlend, height01);
                float patch = PointOps.Smooth(patchStart, patchEnd, Mathf.PerlinNoise(nx * patchFrequency + ox, ny * patchFrequency + oy));

                float dirt = Mathf.Clamp01(Mathf.Max(low, patch)) * (1f - rock);
                float grass = Mathf.Max(0f, 1f - rock - dirt);

                maps[y, x, 0] = grass;
                maps[y, x, 1] = rock;
                maps[y, x, 2] = dirt;      // la somme vaut 1 par construction
            }

        td.SetAlphamaps(0, 0, maps);
    }
}