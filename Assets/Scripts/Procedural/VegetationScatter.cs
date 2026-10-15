using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Terrain))]
public class VegetationScatter : MonoBehaviour
{
    public int seed = 1;

    [Header("Biomes (par ordre de PRIORITÉ : le premier qui accepte un point gagne)")]
    public List<BiomeDefinition> biomes = new List<BiomeDefinition>();

    [Header("Humidité (bruit basse fréquence)")]
    public float moistureFrequency = 3f;

    [Header("Transitions")]
    [Tooltip("Plus grand = frontières plus floues entre biomes")]
    [Range(0f, 0.5f)] public float transitionWidth = 0.12f;

    [Header("Exclusions")]
    public RoadTool[] roads;

    const string RootName = "Vegetation";

    [ContextMenu("Generate")]
    public void Generate()
    {
        Clear();
        var terrain = GetComponent<Terrain>();
        var td = terrain.terrainData;
        Vector3 org = terrain.transform.position;
        Vector3 size = td.size;

        var root = new GameObject(RootName).transform;
        root.SetParent(transform, false);

        // 1. Zones interdites : les routes réservent leur emprise en premier
        var occupied = new SpatialIndex(4f);
        if (roads != null) foreach (var r in roads) if (r != null) r.ReserveIn(occupied);

        float mox = PointOps.Range(seed, 500, 0f, 1000f), moy = PointOps.Range(seed, 501, 0f, 1000f);
        int total = 0;

        // 2. Chaque couche de chaque biome, du plus grand au plus petit (ordre de la liste)
        for (int b = 0; b < biomes.Count; b++)
        {
            if (biomes[b] == null) continue;
            for (int l = 0; l < biomes[b].layers.Count; l++)
            {
                var layer = biomes[b].layers[l];
                if (layer.entries.Count == 0) continue;

                var pts = PointOps.Grid(new Vector2(size.x, size.z), layer.cellSize,
                                        PointOps.CombineSeed(seed, b, l), org + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f));
                PointOps.Randomize(pts, layer.cellSize * 0.4f, layer.scaleRange.x, layer.scaleRange.y);
                pts.Sort((p, q) => q.scale.x.CompareTo(p.scale.x));   // les plus grands d'abord

                float noiseOffset = PointOps.Range(seed, 600 + b * 10 + l, 0f, 1000f);
                var weights = new List<float>();
                foreach (var e in layer.entries) weights.Add(e.weight);

                foreach (var p in pts)
                {
                    float nx = (p.position.x - org.x) / size.x, nz = (p.position.z - org.z) / size.z;
                    if (nx < 0f || nx > 1f || nz < 0f || nz > 1f) continue;

                    float height01 = td.GetInterpolatedHeight(nx, nz) / size.y;
                    float slope = td.GetSteepness(nx, nz);
                    float moisture = Mathf.PerlinNoise(nx * moistureFrequency + mox, nz * moistureFrequency + moy);

                    // frontière irrégulière : chaque point "triche" un peu sur ses mesures
                    float fuzz = (PointOps.Hash01(p.seed, 30) - 0.5f) * 2f * transitionWidth;
                    if (BiomeAt(height01 + fuzz * 0.5f, slope, moisture + fuzz) != b) continue;

                    // règles propres à la couche
                    if (height01 < layer.minHeight || height01 > layer.maxHeight) continue;
                    if (slope < layer.minSlope || slope > layer.maxSlope) continue;
                    float cluster = Mathf.PerlinNoise(p.position.x * layer.noiseFrequency + noiseOffset,
                                                      p.position.z * layer.noiseFrequency + noiseOffset);
                    if (cluster < layer.minDensity) continue;

                    // exclusion : pas de chevauchement avec ce qui est déjà posé
                    float radius = layer.exclusionRadius * p.scale.x;
                    var pos = new Vector3(p.position.x, terrain.SampleHeight(p.position) + org.y, p.position.z);
                    if (!occupied.IsFree(pos, radius)) continue;
                    occupied.Add(pos, radius);

                    var prefab = layer.entries[PointOps.PickWeighted(weights, PointOps.Hash01(p.seed, 4))].prefab;
                    if (prefab == null) continue;

                    Quaternion yaw = p.rotation;
                    Quaternion slopeRot = Quaternion.FromToRotation(Vector3.up, td.GetInterpolatedNormal(nx, nz)) * yaw;
                    var go = Instantiate(prefab, root);
                    go.transform.position = pos;
                    go.transform.rotation = Quaternion.Slerp(yaw, slopeRot, layer.alignToSlope);
                    go.transform.localScale = p.scale;
                    total++;
                }
            }
        }
        Debug.Log("[Végétation] seed " + seed + " : " + total + " objets.");
    }

    // Premier biome (dans l'ordre de priorité) qui accepte ce point ; -1 si aucun
    int BiomeAt(float height01, float slope, float moisture)
    {
        for (int i = 0; i < biomes.Count; i++)
            if (biomes[i] != null && biomes[i].Accepts(height01, slope, moisture)) return i;
        return -1;
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        var t = transform.Find(RootName);
        if (t == null) return;
        if (Application.isPlaying) Destroy(t.gameObject);
        else DestroyImmediate(t.gameObject);
    }
}