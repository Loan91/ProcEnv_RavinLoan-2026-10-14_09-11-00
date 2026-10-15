using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Champ de rochers : GÉNÉRER -> FILTRER -> MODIFIER -> INSTANCIER.
///
/// Justification des bornes (exercice de consolidation) :
/// - Échelle 0.5 à 2 : des petits cailloux jusqu'aux gros blocs, sans extrêmes
///   (en dessous de 0.5 on ne voit plus le rocher, au-dessus de 2 il écrase les autres).
/// - Rotation : uniquement autour de Y (0 à 360). Un rocher posé au sol ne doit pas
///   être penché dans tous les sens, sinon il semble flotter ou s'enfoncer.
/// - Jitter 1 : avec une case de 2.5 m, le décalage reste inférieur à la case,
///   donc la grille se casse sans que les points se mélangent trop.
/// - Rayon anti-chevauchement 0.7 : environ la demi-diagonale d'un cube de 1 m.
/// </summary>
public class ScatterGenerator : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public GameObject prefab;
        [Min(0f)] public float weight = 1f;
    }

    [Header("Aléa")]
    public int seed = 1;

    [Header("Zone et grille")]
    public Vector2 size = new Vector2(60f, 60f);
    [Min(0.5f)] public float cellSize = 2.5f;
    [Min(1f)] public float fieldRadius = 28f;   // rayon du disque gardé par le nouvel opérateur

    [Header("Règles : amas")]
    [Range(0f, 1f)] public float minDensity = 0.5f;
    [Range(0.01f, 0.3f)] public float noiseFrequency = 0.08f;

    [Header("Règles : variation")]
    [Range(0f, 2f)] public float jitter = 1f;
    public Vector2 scaleRange = new Vector2(0.5f, 2f);
    [Range(0.1f, 2f)] public float pruneRadius = 0.7f;

    [Header("Contenu")]
    public List<Entry> entries = new List<Entry>();

    [Header("Debug")]
    public bool drawGizmos = true;
    public bool logCounts = false;

    // Valeurs nommées (pas de nombres "magiques")
    const string RootName = "Generated";
    const int ChannelNoiseX = 100;
    const int ChannelNoiseZ = 101;
    const int ChannelPrefab = 4;
    const float NoiseOffsetMax = 500f;
    const float HalfHeight = 0.5f;        // les prefabs font 1 m et ont leur pivot au centre
    const float GizmoRadius = 0.25f;

    List<SpawnPoint> lastPoints = new List<SpawnPoint>();

    // GÉNÉRER -> FILTRER -> MODIFIER
    public List<SpawnPoint> BuildPoints()
    {
        // décalage du bruit : dérivé de la seed, donc reproductible
        var offset = new Vector2(
            PointOps.Range(seed, ChannelNoiseX, 0f, NoiseOffsetMax),
            PointOps.Range(seed, ChannelNoiseZ, 0f, NoiseOffsetMax));

        var pts = PointOps.Grid(size, cellSize, seed, Vector3.zero);
        Log("Grille", pts);

        pts = PointOps.FilterByDistanceFromCenter(pts, Vector3.zero, fieldRadius);
        Log("Après disque", pts);

        PointOps.NoiseDensity(pts, noiseFrequency, offset);
        pts = PointOps.FilterDensity(pts, minDensity);
        Log("Après densité", pts);

        PointOps.Randomize(pts, jitter, scaleRange.x, scaleRange.y);
        pts = PointOps.SelfPrune(pts, pruneRadius);
        Log("Après anti-chevauchement", pts);
        return pts;
    }

    void Log(string etape, List<SpawnPoint> pts)
    {
        if (logCounts) Debug.Log(etape + " : " + pts.Count);
    }

    // INSTANCIER
    [ContextMenu("Generate")]
    public void Generate()
    {
        Clear();
        if (entries.Count == 0 || entries[0].prefab == null)
        {
            Debug.LogWarning("Ajoutez au moins une entrée avec un prefab.", this);
            return;
        }

        lastPoints = BuildPoints();

        var root = new GameObject(RootName).transform;
        root.SetParent(transform, false);

        var weights = new List<float>();
        foreach (var e in entries) weights.Add(e.weight);

        foreach (var p in lastPoints)
        {
            int index = PointOps.PickWeighted(weights, PointOps.Hash01(p.seed, ChannelPrefab));
            var prefab = entries[index].prefab;
            if (prefab == null) continue;

            var go = Instantiate(prefab, root);

            // On relève le rocher de la moitié de sa hauteur pour qu'il soit posé sur le sol
            var pos = p.position;
            pos.y = p.scale.y * HalfHeight;

            go.transform.localPosition = pos;
            go.transform.localRotation = p.rotation;
            go.transform.localScale = p.scale;
        }
        Debug.Log($"[{name}] seed {seed} : {lastPoints.Count} objets générés.");
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        var t = transform.Find(RootName);
        if (t == null) return;
        if (Application.isPlaying) Destroy(t.gameObject);
        else DestroyImmediate(t.gameObject);
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        foreach (var p in BuildPoints())
        {
            Gizmos.color = Color.Lerp(Color.red, Color.green, p.density);
            Gizmos.DrawWireSphere(transform.TransformPoint(p.position), GizmoRadius * p.scale.x);
        }
    }
}