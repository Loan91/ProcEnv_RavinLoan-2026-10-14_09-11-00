using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class VegetationLayer
{
    public string layerName = "Arbres";
    public List<PrefabEntry> entries = new List<PrefabEntry>();

    [Header("Distribution")]
    [Min(1f)] public float cellSize = 6f;                 // plus petit = plus dense
    public Vector2 scaleRange = new Vector2(0.8f, 1.3f);
    public float exclusionRadius = 2f;                    // rayon à l'échelle 1

    [Header("Règles")]
    [Range(0f, 1f)] public float minHeight = 0.10f;       // altitude normalisée
    [Range(0f, 1f)] public float maxHeight = 0.70f;
    [Range(0f, 90f)] public float minSlope = 0f;          // degrés (rochers : > 0)
    [Range(0f, 90f)] public float maxSlope = 30f;         // degrés
    public float noiseFrequency = 0.01f;                  // basse fréquence = grands bosquets
    [Range(0f, 1f)] public float minDensity = 0.5f;
    [Range(0f, 1f)] public float alignToSlope = 0f;       // 0 = vertical (arbre), 1 = suit la pente (rocher)
}