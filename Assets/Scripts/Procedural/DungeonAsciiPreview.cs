using UnityEngine;

public class DungeonAsciiPreview : MonoBehaviour
{
    public int seed = 1;

    [ContextMenu("Print")]
    void Print()
    {
        var layout = new DungeonLayout(40, 40, seed);
        layout.Generate(20, 10, 15, 200, 0.2f);
        Debug.Log("Seed " + seed + " - connecté : " + layout.IsFullyConnected() + "\n" + layout.ToAscii());
    }
}