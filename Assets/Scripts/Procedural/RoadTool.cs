using UnityEngine;
using UnityEngine.Splines;

/// <summary>Une route décrite par une spline : aplanit le terrain, le peint, réserve son emprise et reçoit des props.</summary>
public class RoadTool : MonoBehaviour
{
    public SplineContainer spline;
    public Terrain terrain;

    [Header("Géométrie")]
    public float roadWidth = 6f;
    public float shoulder = 6f;                 // largeur de la transition avec le terrain
    public float clearance = 2f;                // marge sans végétation de chaque côté
    [Range(100, 1000)] public int samples = 400;
    [Range(1, 60)] public int smoothWindow = 15;
    public int dirtLayerIndex = 2;

    [Header("Props le long de la route")]
    public GameObject propPrefab;
    public float propSpacing = 20f;
    public float propOffset = 4f;
    public int seed = 1;

    const string RootName = "RoadProps";

    // Points de la route dans le monde
    public Vector3[] SamplePolyline()
    {
        var pts = new Vector3[samples];
        for (int i = 0; i < samples; i++)
            pts[i] = spline.EvaluatePosition(i / (float)(samples - 1));
        return pts;
    }

    // L'emprise de la route est interdite à la végétation
    public void ReserveIn(SpatialIndex index)
    {
        foreach (var p in SamplePolyline()) index.Add(p, roadWidth * 0.5f + clearance);
    }

    [ContextMenu("Apply To Terrain")]
    public void ApplyToTerrain()
    {
        var td = terrain.terrainData;
        Vector3 org = terrain.transform.position, size = td.size;
        var pts = SamplePolyline();

        // 1. Hauteurs cibles : celles du terrain sous la route, lissées pour éviter les vagues
        var raw = new float[pts.Length];
        for (int i = 0; i < pts.Length; i++) raw[i] = terrain.SampleHeight(pts[i]) / size.y;
        var target = new float[pts.Length];
        for (int i = 0; i < pts.Length; i++)
        {
            float sum = 0f; int n = 0;
            for (int k = -smoothWindow; k <= smoothWindow; k++)
            {
                int j = Mathf.Clamp(i + k, 0, pts.Length - 1);
                sum += raw[j]; n++;
            }
            target[i] = sum / n;
        }

        // 2. Estampage dans la heightmap : on tire chaque pixel proche vers la hauteur cible
        int res = td.heightmapResolution;
        float[,] h = td.GetHeights(0, 0, res, res);
        float pxSize = size.x / (res - 1);
        float reach = roadWidth * 0.5f + shoulder;
        int rpx = Mathf.CeilToInt(reach / pxSize);

        for (int i = 0; i < pts.Length; i++)
        {
            int cx = Mathf.RoundToInt((pts[i].x - org.x) / size.x * (res - 1));
            int cy = Mathf.RoundToInt((pts[i].z - org.z) / size.z * (res - 1));
            for (int y = Mathf.Max(0, cy - rpx); y <= Mathf.Min(res - 1, cy + rpx); y++)
                for (int x = Mathf.Max(0, cx - rpx); x <= Mathf.Min(res - 1, cx + rpx); x++)
                {
                    float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) * pxSize;
                    float wgt = 1f - PointOps.Smooth(roadWidth * 0.5f, reach, dist);   // 1 sur la route, 0 au bord de la transition
                    if (wgt > 0f) h[y, x] = Mathf.Lerp(h[y, x], target[i], wgt);
                }
        }
        td.SetHeights(0, 0, h);

        // 3. Peinture de la terre sous la route
        if (td.alphamapLayers > dirtLayerIndex)
        {
            int aw = td.alphamapWidth, ah = td.alphamapHeight;
            float[,,] a = td.GetAlphamaps(0, 0, aw, ah);
            float apx = size.x / (aw - 1);
            float paint = roadWidth * 0.5f + 1.5f;
            int arpx = Mathf.CeilToInt(paint / apx);

            for (int i = 0; i < pts.Length; i++)
            {
                int cx = Mathf.RoundToInt((pts[i].x - org.x) / size.x * (aw - 1));
                int cy = Mathf.RoundToInt((pts[i].z - org.z) / size.z * (ah - 1));
                for (int y = Mathf.Max(0, cy - arpx); y <= Mathf.Min(ah - 1, cy + arpx); y++)
                    for (int x = Mathf.Max(0, cx - arpx); x <= Mathf.Min(aw - 1, cx + arpx); x++)
                    {
                        float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) * apx;
                        float wgt = 1f - PointOps.Smooth(roadWidth * 0.5f - 1f, paint, dist);
                        if (wgt <= 0f) continue;
                        for (int l = 0; l < td.alphamapLayers; l++)
                            a[y, x, l] = Mathf.Lerp(a[y, x, l], l == dirtLayerIndex ? 1f : 0f, wgt);   // la somme reste égale à 1
                    }
            }
            td.SetAlphamaps(0, 0, a);
        }
        Debug.Log("[Route] terrain aplani et peint le long de la spline.");
    }

    [ContextMenu("Place Props")]
    public void PlaceProps()
    {
        ClearProps();
        if (propPrefab == null) return;
        var root = new GameObject(RootName).transform;
        root.SetParent(transform, false);

        var pts = SamplePolyline();
        float acc = 0f, next = propSpacing * 0.5f;
        int n = 0;
        for (int i = 1; i < pts.Length - 1; i++)
        {
            acc += Vector3.Distance(pts[i - 1], pts[i]);
            if (acc < next) continue;
            next += propSpacing;

            Vector3 tangent = (pts[i + 1] - pts[i - 1]).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;

            for (int side = -1; side <= 1; side += 2)
            {
                int ps = PointOps.CombineSeed(seed, i, side);
                if (PointOps.Hash01(ps, 0) < 0.2f) continue;                     // 20 % de manques : moins artificiel
                float along = PointOps.Range(ps, 1, -1.5f, 1.5f);                // petit décalage le long de la route
                Vector3 pos = pts[i] + right * side * propOffset + tangent * along;
                pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;

                var go = Instantiate(propPrefab, root);
                go.transform.position = pos;
                go.transform.rotation = Quaternion.LookRotation(-right * side, Vector3.up);   // tourné vers la route
                n++;
            }
        }
        Debug.Log("[Route] " + n + " props placés.");
    }

    [ContextMenu("Clear Props")]
    public void ClearProps()
    {
        var t = transform.Find(RootName);
        if (t == null) return;
        if (Application.isPlaying) Destroy(t.gameObject);
        else DestroyImmediate(t.gameObject);
    }
}