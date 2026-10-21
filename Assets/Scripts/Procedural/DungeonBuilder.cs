using System.Collections.Generic;
using UnityEngine;

public class DungeonBuilder : MonoBehaviour
{
    [Header("Aléa")]
    public int seed = 1;

    [Header("Plan")]
    public int gridWidth = 40;
    public int gridHeight = 40;
    public int roomCount = 8;
    public int minRoomSize = 4;
    public int maxRoomSize = 9;
    [Range(0f, 1f)] public float loopChance = 0.2f;
    public int maxTries = 200;

    [Header("Kit modulaire (laisser vide = cubes de secours)")]
    public float tileSize = 4f;
    public float wallHeight = 3f;
    public GameObject floorPrefab;
    public GameObject wallPrefab;
    public GameObject ceilingPrefab;
    public GameObject pillarPrefab;
    public GameObject doorPrefab;

    [Header("Garnissage")]
    public List<RoomTheme> themes = new List<RoomTheme>();
    public float wallPropInset = 0.6f;
    public bool addLights = true;

    [Header("Entrée vers l'extérieur (côté -Z du donjon)")]
    public bool addEntrance = true;

    public DungeonLayout Layout { get; private set; }

    const string RootName = "Dungeon";

    // Direction d : (dx, dz) et rotation en Y du mur qui ferme ce côté
    static readonly int[] DX = { 0, 1, 0, -1 };
    static readonly int[] DZ = { 1, 0, -1, 0 };
    static readonly float[] YAW = { 0f, 90f, 180f, 270f };

    [ContextMenu("Generate")]
    public void Generate()
    {
        Clear();

        Layout = new DungeonLayout(gridWidth, gridHeight, seed);
        Layout.Generate(roomCount, minRoomSize, maxRoomSize, maxTries, loopChance);
        if (addEntrance) Layout.AddEntrance();

        var root = new GameObject(RootName).transform;
        root.SetParent(transform, false);

        BuildGeometry(root);
        for (int i = 0; i < Layout.rooms.Count; i++) Populate(Layout.rooms[i], i, root);

        Debug.Log("[Dungeon] seed " + seed + " : " + Layout.rooms.Count + " salles, connecté = " + Layout.IsFullyConnected());
    }

    // Affiche le plan dans la Console : très utile pour comprendre un bug de plan
    [ContextMenu("Print Layout")]
    public void PrintLayout()
    {
        var l = new DungeonLayout(gridWidth, gridHeight, seed);
        l.Generate(roomCount, minRoomSize, maxRoomSize, maxTries, loopChance);
        if (addEntrance) l.AddEntrance();
        Debug.Log(l.ToAscii());
    }

    // Teste la connectivité sur de nombreuses seeds (sans rien instancier)
    [ContextMenu("Validate 200 Seeds")]
    public void ValidateSeeds()
    {
        int failures = 0;
        for (int s = 1; s <= 200; s++)
        {
            var l = new DungeonLayout(gridWidth, gridHeight, s);
            l.Generate(roomCount, minRoomSize, maxRoomSize, maxTries, loopChance);
            if (addEntrance) l.AddEntrance();
            if (!l.IsFullyConnected()) { failures++; Debug.LogWarning("Seed non connectée : " + s); }
        }
        Debug.Log("[Dungeon] validation terminée : " + failures + " échec(s) sur 200 seeds.");
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        var t = transform.Find(RootName);
        if (t == null) return;
        if (Application.isPlaying) Destroy(t.gameObject);
        else DestroyImmediate(t.gameObject);
    }

    Vector3 CellCenter(int x, int y) { return new Vector3(x * tileSize, 0f, y * tileSize); }

    // ---------- Géométrie : les murs sont DÉDUITS des sols ----------
    void BuildGeometry(Transform root)
    {
        var corners = new HashSet<Vector2Int>();
        float half = tileSize * 0.5f;

        for (int x = 0; x < Layout.width; x++)
            for (int y = 0; y < Layout.height; y++)
            {
                if (!Layout.IsOpen(x, y)) continue;
                Vector3 c = CellCenter(x, y);

                Place(floorPrefab, "Floor", c + new Vector3(0f, -0.1f, 0f), Quaternion.identity,
                      new Vector3(tileSize, 0.2f, tileSize), root);
                Place(ceilingPrefab, "Ceiling", c + new Vector3(0f, wallHeight + 0.1f, 0f), Quaternion.identity,
                      new Vector3(tileSize, 0.2f, tileSize), root);

                for (int d = 0; d < 4; d++)
                {
                    int nx = x + DX[d], ny = y + DZ[d];
                    Vector3 edge = c + new Vector3(DX[d] * half, 0f, DZ[d] * half);
                    Quaternion rot = Quaternion.Euler(0f, YAW[d], 0f);

                    bool isEntrance = Layout.HasEntrance && x == Layout.EntranceX && y == 0 && d == 2;

                    if (isEntrance)
                    {
                        // l'ouverture vers l'extérieur : un cadre de porte à la place du mur (ou rien si pas de prefab)
                        if (doorPrefab != null) Place(doorPrefab, "Entrance", edge, rot, Vector3.one, root);
                    }
                    else if (!Layout.IsOpen(nx, ny))
                    {
                        Place(wallPrefab, "Wall", edge + new Vector3(0f, wallHeight * 0.5f, 0f), rot,
                              new Vector3(tileSize, wallHeight, 0.2f), root);
                    }
                    else if (Layout.cells[x, y] == CellType.Corridor && Layout.cells[nx, ny] == CellType.Room && doorPrefab != null)
                    {
                        // jonction couloir -> salle : un cadre de porte
                        Place(doorPrefab, "Door", edge, rot, Vector3.one, root);
                    }
                }

                // coins : un pilier là où les 4 cellules autour ne sont pas toutes ouvertes
                for (int cx = x; cx <= x + 1; cx++)
                    for (int cy = y; cy <= y + 1; cy++)
                    {
                        bool allOpen = Layout.IsOpen(cx, cy) && Layout.IsOpen(cx - 1, cy) &&
                                       Layout.IsOpen(cx, cy - 1) && Layout.IsOpen(cx - 1, cy - 1);
                        if (!allOpen) corners.Add(new Vector2Int(cx, cy));
                    }
            }

        foreach (var k in corners)
        {
            float pillarY = pillarPrefab != null ? 0f : wallHeight * 0.5f;
            var pos = new Vector3((k.x - 0.5f) * tileSize, pillarY, (k.y - 0.5f) * tileSize);
            Place(pillarPrefab, "Pillar", pos, Quaternion.identity, new Vector3(0.4f, wallHeight, 0.4f), root);
        }
    }

    // Instancie le prefab du kit, ou un cube de secours à l'échelle demandée
    void Place(GameObject prefab, string label, Vector3 localPos, Quaternion localRot, Vector3 fallbackScale, Transform parent)
    {
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab, parent);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent, false);
            go.transform.localScale = fallbackScale;
        }
        go.name = label;
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
    }

    // ---------- Garnissage d'une salle ----------
    void Populate(RoomRect r, int index, Transform root)
    {
        if (themes.Count == 0) return;
        var theme = themes[Mathf.Min(themes.Count - 1, (int)(PointOps.Hash01(seed, 1000 + index) * themes.Count))];
        if (theme == null) return;

        var holder = new GameObject("Room " + index + " (" + theme.themeName + ")").transform;
        holder.SetParent(root, false);

        // Passages à préserver : cellules du bord qui touchent un couloir, et leurs voisines
        var blocked = new HashSet<Vector2Int>();
        for (int x = r.x; x < r.x + r.w; x++)
            for (int y = r.y; y < r.y + r.h; y++)
            {
                bool onBorder = x == r.x || y == r.y || x == r.x + r.w - 1 || y == r.y + r.h - 1;
                if (!onBorder) continue;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + DX[d], ny = y + DZ[d];
                    bool outside = nx < r.x || ny < r.y || nx >= r.x + r.w || ny >= r.y + r.h;
                    if (outside && Layout.IsOpen(nx, ny))
                    {
                        blocked.Add(new Vector2Int(x, y));
                        for (int e = 0; e < 4; e++) blocked.Add(new Vector2Int(x + DX[e], y + DZ[e]));
                    }
                }
            }

        for (int x = r.x; x < r.x + r.w; x++)
            for (int y = r.y; y < r.y + r.h; y++)
            {
                if (blocked.Contains(new Vector2Int(x, y))) continue;
                int cellSeed = PointOps.CombineSeed(seed, x, y);
                Vector3 c = CellCenter(x, y);

                // quelle(s) paroi(s) touche cette cellule ?
                int wallDir = -1;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + DX[d], ny = y + DZ[d];
                    if (nx < r.x || ny < r.y || nx >= r.x + r.w || ny >= r.y + r.h) { wallDir = d; break; }
                }

                if (wallDir >= 0)
                {
                    if (PointOps.Hash01(cellSeed, 10) > theme.wallDensity) continue;
                    var prefab = theme.Pick(theme.wallProps, PointOps.Hash01(cellSeed, 11));
                    if (prefab == null) continue;
                    // posé contre la paroi, regardant vers l'intérieur de la salle
                    Vector3 toWall = new Vector3(DX[wallDir], 0f, DZ[wallDir]);
                    var go = Instantiate(prefab, holder);
                    go.transform.localPosition = c + toWall * (tileSize * 0.5f - wallPropInset);
                    go.transform.localRotation = Quaternion.Euler(0f, YAW[(wallDir + 2) % 4], 0f);
                }
                else
                {
                    if (PointOps.Hash01(cellSeed, 20) > theme.centerDensity) continue;
                    var prefab = theme.Pick(theme.centerProps, PointOps.Hash01(cellSeed, 21));
                    if (prefab == null) continue;
                    var go = Instantiate(prefab, holder);
                    go.transform.localPosition = c;
                    go.transform.localRotation = Quaternion.Euler(0f, PointOps.Range(cellSeed, 22, 0f, 360f), 0f);
                }
            }

        if (addLights)
        {
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(holder, false);
            lightGo.transform.localPosition = CellCenter(r.CenterX, r.CenterY) + new Vector3(0f, wallHeight - 0.5f, 0f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = theme.lightColor;
            l.intensity = theme.lightIntensity;
            l.range = theme.lightRange;
        }
    }
}