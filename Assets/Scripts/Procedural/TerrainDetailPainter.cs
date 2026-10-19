using UnityEngine;

/// <summary>
/// Remplit le système de DÉTAILS du Terrain (herbe, fleurs, petits cailloux) par règles :
/// pente, altitude, bruit et poids des calques de terrain. Aucun GameObject n'est créé :
/// le Terrain dessine lui-même les détails, de façon instanciée et à très faible coût.
///
/// Mise en place :
///  1. Sélectionner le Terrain > outil "Paint Details" > "Edit Details" > ajouter les détails
///     (Add Grass Texture pour l'herbe, Add Detail Mesh pour cailloux et fleurs).
///  2. L'ORDRE de la liste des détails du Terrain doit correspondre à l'ordre de 'rules' ci-dessous.
///  3. Menu du composant > Paint Details.
/// </summary>
[RequireComponent(typeof(Terrain))]
public class TerrainDetailPainter : MonoBehaviour
{
    [System.Serializable]
    public class DetailRule
    {
        public string layerName = "Herbe";

        [Header("Où")]
        [Range(0f, 90f)] public float minSlope = 0f;
        [Range(0f, 90f)] public float maxSlope = 30f;
        [Range(0f, 1f)] public float minHeight = 0.05f;
        [Range(0f, 1f)] public float maxHeight = 0.7f;
        [Tooltip("Calque de terrain requis (0 = herbe, 1 = roche, 2 = terre) ; -1 pour ignorer")]
        public int requiredTerrainLayer = 0;
        [Range(0f, 1f)] public float minLayerWeight = 0.5f;

        [Header("Combien")]
        [Tooltip("Basse fréquence = grandes taches ; haute fréquence = petits amas")]
        public float noiseFrequency = 12f;
        [Range(0f, 1f)] public float noiseThreshold = 0.35f;
        [Range(0, 16)] public int maxDensity = 8;
        [Tooltip("Variation aléatoire de la densité d'une cellule à l'autre (0 = aucune)")]
        [Range(0f, 1f)] public float randomness = 0.3f;
    }

    public int seed = 1;

    [Header("Résolution des détails")]
    [Tooltip("Puissance de 2 recommandée ; plus élevé = plus fin mais plus lourd")]
    public int detailResolution = 1024;
    [Tooltip("Taille d'un bloc de rendu en cellules (8 à 64 typique)")]
    public int resolutionPerPatch = 16;

    [Header("Affichage")]
    [Tooltip("Distance (m) au-delà de laquelle les détails ne sont plus dessinés")]
    public float detailDistance = 80f;
    [Range(0f, 1f)] public float densityScale = 1f;

    [Header("Une règle par détail du Terrain, dans le MÊME ORDRE")]
    public DetailRule[] rules = new DetailRule[0];

    [Header("Routes à dégager (facultatif)")]
    public RoadTool[] roads;

    [ContextMenu("Paint Details")]
    public void Paint()
    {
        var terrain = GetComponent<Terrain>();
        var td = terrain.terrainData;
        int protoCount = td.detailPrototypes.Length;
        if (protoCount == 0)
        {
            Debug.LogWarning("Aucun détail défini : Terrain > Paint Details > Edit Details > Add…", this);
            return;
        }
        if (rules.Length != protoCount)
            Debug.LogWarning("Le nombre de règles (" + rules.Length + ") diffère du nombre de détails (" + protoCount + "). Seules les premières correspondances sont peintes.", this);

        td.SetDetailResolution(detailResolution, resolutionPerPatch);
        int res = td.detailWidth;

        // On lit les calques de terrain une seule fois
        float[,,] alpha = td.GetAlphamaps(0, 0, td.alphamapWidth, td.alphamapHeight);
        int aw = td.alphamapWidth, ah = td.alphamapHeight, layers = td.alphamapLayers;

        int count = Mathf.Min(rules.Length, protoCount);
        for (int i = 0; i < count; i++)
        {
            var r = rules[i];
            var map = new int[res, res];
            float noiseOffset = PointOps.Range(seed, 800 + i, 0f, 1000f);
            long painted = 0;

            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float nx = x / (float)(res - 1), ny = y / (float)(res - 1);

                    float slope = td.GetSteepness(nx, ny);
                    if (slope < r.minSlope || slope > r.maxSlope) continue;

                    float h01 = td.GetInterpolatedHeight(nx, ny) / td.size.y;
                    if (h01 < r.minHeight || h01 > r.maxHeight) continue;

                    if (r.requiredTerrainLayer >= 0 && r.requiredTerrainLayer < layers)
                    {
                        int ax = Mathf.Clamp(Mathf.RoundToInt(nx * (aw - 1)), 0, aw - 1);
                        int ay = Mathf.Clamp(Mathf.RoundToInt(ny * (ah - 1)), 0, ah - 1);
                        if (alpha[ay, ax, r.requiredTerrainLayer] < r.minLayerWeight) continue;
                    }

                    float n = Mathf.PerlinNoise(nx * r.noiseFrequency + noiseOffset, ny * r.noiseFrequency + noiseOffset);
                    if (n < r.noiseThreshold) continue;

                    // plus on s'enfonce dans une tache, plus c'est dense ; un peu d'aléa stable par cellule
                    float strength = Mathf.Clamp01((n - r.noiseThreshold) / Mathf.Max(0.0001f, 1f - r.noiseThreshold));
                    float jitter = 1f - r.randomness * PointOps.Hash01(PointOps.CombineSeed(seed, x, y), i);
                    int d = Mathf.RoundToInt(r.maxDensity * strength * jitter);
                    if (d > 0) { map[y, x] = d; painted++; }
                }

            ClearAroundRoads(td, terrain, map, res);
            td.SetDetailLayer(0, 0, i, map);   // le tableau s'indexe [y, x]
            Debug.Log("[Détails] " + r.layerName + " : " + painted + " cellules peintes.");
        }

        terrain.detailObjectDistance = detailDistance;
        terrain.detailObjectDensity = densityScale;
    }

    // Aucun détail sur la route ni dans sa marge
    void ClearAroundRoads(TerrainData td, Terrain terrain, int[,] map, int res)
    {
        if (roads == null) return;
        Vector3 org = terrain.transform.position, size = td.size;
        float cell = size.x / (res - 1);

        foreach (var road in roads)
        {
            if (road == null) continue;
            float radius = road.roadWidth * 0.5f + road.clearance;
            int rpx = Mathf.CeilToInt(radius / cell);

            foreach (var p in road.SamplePolyline())
            {
                int cx = Mathf.RoundToInt((p.x - org.x) / size.x * (res - 1));
                int cy = Mathf.RoundToInt((p.z - org.z) / size.z * (res - 1));
                for (int y = Mathf.Max(0, cy - rpx); y <= Mathf.Min(res - 1, cy + rpx); y++)
                    for (int x = Mathf.Max(0, cx - rpx); x <= Mathf.Min(res - 1, cx + rpx); x++)
                    {
                        float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) * cell;
                        if (dist <= radius) map[y, x] = 0;
                    }
            }
        }
    }

    [ContextMenu("Clear Details")]
    public void ClearDetails()
    {
        var td = GetComponent<Terrain>().terrainData;
        int res = td.detailWidth;
        for (int i = 0; i < td.detailPrototypes.Length; i++) td.SetDetailLayer(0, 0, i, new int[res, res]);
    }
}