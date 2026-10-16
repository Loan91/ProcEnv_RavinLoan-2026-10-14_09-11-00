using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Biome", menuName = "ProcEnv/Biome")]

public class BiomeDefinition : ScriptableObject
{
    public string biomeName = "Forêt";
    [TextArea] public string description;

    [Header("Où ce biome existe")]
    [Range(0f, 1f)] public float minHeight = 0f;
    [Range(0f, 1f)] public float maxHeight = 1f;
    [Range(0f, 90f)] public float minSlope = 0f;
    [Range(0f, 90f)] public float maxSlope = 90f;
    [Range(0f, 1f)] public float minMoisture = 0f;
    [Range(0f, 1f)] public float maxMoisture = 1f;

    [Header("Ce qu'on y trouve")]
    public List<VegetationLayer> layers = new List<VegetationLayer>();

    public bool Accepts(float height01, float slope, float moisture)
    {
        return height01 >= minHeight && height01 <= maxHeight &&
               slope >= minSlope && slope <= maxSlope &&
               moisture >= minMoisture && moisture <= maxMoisture;
    }
}