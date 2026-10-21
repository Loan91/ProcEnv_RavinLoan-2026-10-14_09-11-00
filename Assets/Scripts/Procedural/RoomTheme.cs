using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RoomTheme", menuName = "ProcEnv/Room Theme")]
public class RoomTheme : ScriptableObject
{
    [System.Serializable]
    public class PropEntry
    {
        public GameObject prefab;
        [Min(0f)] public float weight = 1f;
    }

    public string themeName = "Stockage";

    [Header("Props")]
    public List<PropEntry> wallProps = new List<PropEntry>();
    public List<PropEntry> centerProps = new List<PropEntry>();
    [Range(0f, 1f)] public float wallDensity = 0.5f;
    [Range(0f, 1f)] public float centerDensity = 0.12f;

    [Header("Lumière")]
    public Color lightColor = Color.white;
    public float lightIntensity = 1.2f;
    public float lightRange = 10f;

    public GameObject Pick(List<PropEntry> list, float r01)
    {
        if (list == null || list.Count == 0) return null;
        var weights = new List<float>();
        foreach (var e in list) weights.Add(e.weight);
        return list[PointOps.PickWeighted(weights, r01)].prefab;
    }
}