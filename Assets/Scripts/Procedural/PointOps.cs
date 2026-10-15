using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Opérations sur des listes de points : le "mini PCG" de ce cours.
/// Schéma : GÉNÉRER -> FILTRER -> MODIFIER -> INSTANCIER.
/// </summary>
public static class PointOps
{
    // ---------- Hasard reproductible (sans état) ----------

    /// <summary>Renvoie un nombre dans [0,1[ qui ne dépend que de (seed, channel).</summary>
    public static float Hash01(int seed, int channel)
    {
        unchecked
        {
            uint s = (uint)seed + (uint)channel * 0x9E3779B9u;
            uint state = s * 747796405u + 2891336453u;
            uint word = ((state >> (int)((state >> 28) + 4u)) ^ state) * 277803737u;
            word = (word >> 22) ^ word;
            return (word & 0xFFFFFFu) / 16777216f;
        }
    }

    /// <summary>Mélange la seed globale et des coordonnées entières.</summary>
    public static int CombineSeed(int seed, int a, int b)
    {
        unchecked { return (seed * 73856093) ^ (a * 19349663) ^ (b * 83492791); }
    }

    /// <summary>
    /// Transition douce de 0 à 1 entre edge0 et edge1 (comme le smoothstep des shaders).
    /// Attention : Mathf.SmoothStep d'Unity a une signature différente (from, to, t) : ne pas l'utiliser ici.
    /// </summary>
    public static float Smooth(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    public static float Range(int seed, int channel, float min, float max)
        => min + Hash01(seed, channel) * (max - min);

    // ---------- 1. GÉNÉRER ----------

    /// <summary>Grille régulière centrée sur 'center'. Chaque point reçoit une seed stable.</summary>
    public static List<SpawnPoint> Grid(Vector2 size, float cellSize, int seed, Vector3 center)
    {
        var pts = new List<SpawnPoint>();
        int nx = Mathf.FloorToInt(size.x / cellSize);
        int nz = Mathf.FloorToInt(size.y / cellSize);
        var origin = center + new Vector3(-size.x * 0.5f, 0f, -size.y * 0.5f);
        for (int x = 0; x < nx; x++)
            for (int z = 0; z < nz; z++)
            {
                var pos = origin + new Vector3((x + 0.5f) * cellSize, 0f, (z + 0.5f) * cellSize);
                pts.Add(new SpawnPoint(pos, CombineSeed(seed, x, z)));
            }
        return pts;
    }

    // ---------- 2. FILTRER ----------

    public static List<SpawnPoint> FilterDensity(List<SpawnPoint> pts, float minDensity)
    {
        var res = new List<SpawnPoint>(pts.Count);
        foreach (var p in pts)
            if (p.density >= minDensity) res.Add(p);
        return res;
    }

    /// <summary>
    /// Garde uniquement les points situés dans un disque (vu de dessus, sur le plan XZ).
    /// Fonction pure : ne modifie pas la liste d'entrée, renvoie une nouvelle liste.
    /// </summary>
    public static List<SpawnPoint> FilterByDistanceFromCenter(List<SpawnPoint> pts, Vector3 center, float radius)
    {
        var res = new List<SpawnPoint>(pts.Count);
        float radiusSqr = radius * radius;   // on compare les carrés : pas besoin de racine carrée

        foreach (var p in pts)
        {
            float dx = p.position.x - center.x;
            float dz = p.position.z - center.z;
            if (dx * dx + dz * dz <= radiusSqr) res.Add(p);
        }
        return res;
    }

    /// <summary>Supprime les points qui se chevauchent. Les plus grands sont gardés en priorité.</summary>
    public static List<SpawnPoint> SelfPrune(List<SpawnPoint> pts, float baseRadius)
    {
        var kept = new List<SpawnPoint>();
        if (pts.Count == 0) return kept;

        var sorted = new List<SpawnPoint>(pts);
        sorted.Sort((a, b) =>
        {
            int c = b.scale.x.CompareTo(a.scale.x);
            return c != 0 ? c : a.seed.CompareTo(b.seed);   // départage stable
        });

        float cell = Mathf.Max(0.01f, baseRadius * sorted[0].scale.x * 2f);
        var grid = new Dictionary<Vector2Int, List<int>>();

        foreach (var p in sorted)
        {
            float r = baseRadius * p.scale.x;
            var c = new Vector2Int(Mathf.FloorToInt(p.position.x / cell), Mathf.FloorToInt(p.position.z / cell));
            bool free = true;

            for (int dx = -1; dx <= 1 && free; dx++)
                for (int dz = -1; dz <= 1 && free; dz++)
                {
                    if (!grid.TryGetValue(new Vector2Int(c.x + dx, c.y + dz), out var list)) continue;
                    foreach (int i in list)
                    {
                        var q = kept[i];
                        float ddx = p.position.x - q.position.x;
                        float ddz = p.position.z - q.position.z;
                        float minDist = r + baseRadius * q.scale.x;
                        if (ddx * ddx + ddz * ddz < minDist * minDist) { free = false; break; }
                    }
                }

            if (!free) continue;
            if (!grid.TryGetValue(c, out var cellList)) { cellList = new List<int>(); grid[c] = cellList; }
            cellList.Add(kept.Count);
            kept.Add(p);
        }
        return kept;
    }

    // ---------- 3. MODIFIER ----------

    /// <summary>Bruit de Perlin (cohérent dans l'espace) multiplié à la densité.</summary>
    public static void NoiseDensity(List<SpawnPoint> pts, float frequency, Vector2 offset)
    {
        for (int i = 0; i < pts.Count; i++)
        {
            var p = pts[i];
            // +1000 : PerlinNoise est symétrique autour de 0, on reste dans les positifs
            float n = Mathf.PerlinNoise((p.position.x + 1000f) * frequency + offset.x,
                                        (p.position.z + 1000f) * frequency + offset.y);
            p.density *= n;
            pts[i] = p;
        }
    }

    /// <summary>Décalage, rotation en Y et échelle aléatoires, tirés de la seed de CHAQUE point.</summary>
    public static void Randomize(List<SpawnPoint> pts, float jitter, float scaleMin, float scaleMax)
    {
        for (int i = 0; i < pts.Count; i++)
        {
            var p = pts[i];
            p.position += new Vector3(Range(p.seed, 0, -jitter, jitter), 0f, Range(p.seed, 1, -jitter, jitter));
            p.rotation = Quaternion.Euler(0f, Range(p.seed, 2, 0f, 360f), 0f);
            float s = Range(p.seed, 3, scaleMin, scaleMax);
            p.scale = new Vector3(s, s, s);
            pts[i] = p;
        }
    }

    // ---------- Aide au choix pondéré ----------

    /// <summary>Choisit un indice selon des poids, à partir d'un tirage r01 dans [0,1[.</summary>
    public static int PickWeighted(IList<float> weights, float r01)
    {
        float total = 0f;
        for (int i = 0; i < weights.Count; i++) total += Mathf.Max(0f, weights[i]);
        if (total <= 0f) return 0;
        float r = r01 * total;
        for (int i = 0; i < weights.Count; i++)
        {
            r -= Mathf.Max(0f, weights[i]);
            if (r < 0f) return i;
        }
        return weights.Count - 1;
    }
}