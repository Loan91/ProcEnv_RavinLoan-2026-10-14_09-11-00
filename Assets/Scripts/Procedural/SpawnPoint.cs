using UnityEngine;

[System.Serializable]
public struct SpawnPoint
{
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;
    public float density;   // 0 à 1 : validité du point
    public int seed;        // graine propre au point (stable, dérivée de sa cellule)

    public SpawnPoint(Vector3 position, int seed)
    {
        this.position = position;
        this.seed = seed;
        rotation = Quaternion.identity;
        scale = Vector3.one;
        density = 1f;
    }
}