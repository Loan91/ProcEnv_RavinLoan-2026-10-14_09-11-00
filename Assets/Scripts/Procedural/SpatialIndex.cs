using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mémorise des disques (x, z, rayon) pour répondre vite à "cet emplacement est-il libre ?".
/// Sert à l'exclusion entre couches (grands arbres d'abord, petits éléments ensuite) et autour des routes.
/// </summary>
public class SpatialIndex
{
    struct Disc { public float x, z, r; }

    readonly float cell;
    readonly Dictionary<Vector2Int, List<Disc>> grid = new Dictionary<Vector2Int, List<Disc>>();
    float maxRadius;

    public SpatialIndex(float cellSize = 4f) { cell = Mathf.Max(0.1f, cellSize); }

    Vector2Int Key(float x, float z)
    {
        return new Vector2Int(Mathf.FloorToInt(x / cell), Mathf.FloorToInt(z / cell));
    }

    public void Add(Vector3 p, float radius)
    {
        var k = Key(p.x, p.z);
        if (!grid.TryGetValue(k, out var list)) { list = new List<Disc>(); grid[k] = list; }
        list.Add(new Disc { x = p.x, z = p.z, r = radius });
        if (radius > maxRadius) maxRadius = radius;
    }

    public bool IsFree(Vector3 p, float radius)
    {
        int reach = Mathf.CeilToInt((radius + maxRadius) / cell);
        var k = Key(p.x, p.z);
        for (int dx = -reach; dx <= reach; dx++)
            for (int dz = -reach; dz <= reach; dz++)
            {
                if (!grid.TryGetValue(new Vector2Int(k.x + dx, k.y + dz), out var list)) continue;
                foreach (var d in list)
                {
                    float ex = p.x - d.x, ez = p.z - d.z;
                    float min = radius + d.r;
                    if (ex * ex + ez * ez < min * min) return false;
                }
            }
        return true;
    }
}