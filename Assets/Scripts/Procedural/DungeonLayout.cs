using System.Collections.Generic;

public enum CellType : byte { Empty = 0, Room = 1, Corridor = 2 }

public struct RoomRect
{
    public int x, y, w, h;
    public int CenterX { get { return x + w / 2; } }
    public int CenterY { get { return y + h / 2; } }
}

/// <summary>
/// Plan d'un donjon sur grille. Aucune dépendance à Unity : on peut le tester seul.
/// Les cellules sont indexées [x, y] ; dans Unity, y correspond à l'axe Z.
/// </summary>
public class DungeonLayout
{
    public readonly int width, height;
    public readonly CellType[,] cells;
    public readonly List<RoomRect> rooms = new List<RoomRect>();
    public bool HasEntrance { get; private set; }
    public int EntranceX { get; private set; }     // cellule d'entrée : y = 0, côté -Z
    readonly System.Random rng;

    public DungeonLayout(int width, int height, int seed)
    {
        this.width = width;
        this.height = height;
        cells = new CellType[width, height];
        rng = new System.Random(seed);   // flux dédié : même seed, même plan
    }

    public void Generate(int roomCount, int minSize, int maxSize, int maxTries, float loopChance)
    {
        PlaceRooms(roomCount, minSize, maxSize, maxTries);
        ConnectRooms(loopChance);
    }

    // ---------- 1. Placer les salles (tirage avec rejet) ----------
    void PlaceRooms(int roomCount, int minSize, int maxSize, int maxTries)
    {
        for (int attempt = 0; attempt < maxTries && rooms.Count < roomCount; attempt++)
        {
            int w = rng.Next(minSize, maxSize + 1);
            int h = rng.Next(minSize, maxSize + 1);
            if (width - w - 1 < 1 || height - h - 1 < 1) continue;   // salle trop grande pour la grille
            int x = rng.Next(1, width - w);        // 1 .. width-w-1
            int y = rng.Next(1, height - h);

            if (Overlaps(x, y, w, h)) continue;    // rejet

            var room = new RoomRect { x = x, y = y, w = w, h = h };
            rooms.Add(room);
            for (int i = x; i < x + w; i++)
                for (int j = y; j < y + h; j++)
                    cells[i, j] = CellType.Room;
        }
    }

    // Deux rectangles se chevauchent (avec une marge de 1 cellule) si leurs intervalles se recoupent sur X ET sur Y
    bool Overlaps(int x, int y, int w, int h)
    {
        foreach (var r in rooms)
        {
            if (x < r.x + r.w + 1 && x + w + 1 > r.x &&
                y < r.y + r.h + 1 && y + h + 1 > r.y) return true;
        }
        return false;
    }

    // ---------- 2. Relier les salles ----------
    void ConnectRooms(float loopChance)
    {
        // chaînage : chaque salle est reliée à la précédente => connectivité garantie
        for (int i = 1; i < rooms.Count; i++)
        {
            Corridor(rooms[i - 1], rooms[i]);
        }
            

        // boucles supplémentaires : des alternatives, pas de la connectivité
        for (int i = 0; i < rooms.Count; i++)
        {
            if (rng.NextDouble() >= loopChance) continue;
            int j = rng.Next(0, rooms.Count);
            if (j != i && j != i - 1 && j != i + 1) Corridor(rooms[i], rooms[j]);
        }
    }

    // Couloir en L : un segment horizontal puis vertical (ou l'inverse, au hasard)
    void Corridor(RoomRect a, RoomRect b)
    {
        int x1 = a.CenterX, y1 = a.CenterY, x2 = b.CenterX, y2 = b.CenterY;
        bool horizontalFirst = rng.Next(0, 2) == 0;

        List<int> pathA = BuildPath(x1, y1, x2, y2, horizontalFirst);
        List<int> pathB = BuildPath(x1, y1, x2, y2, !horizontalFirst);

        List<int> chosen = pathA;
        if (!IsClean(pathA) && IsClean(pathB)) chosen = pathB;   // sinon on garde A (rare)

        foreach (int id in chosen) Carve(id / height, id % height);
    }

    List<int> BuildPath(int x1, int y1, int x2, int y2, bool horizontalFirst)
    {
        var path = new List<int>();
        if (horizontalFirst)
        {
            AddLine(path, x1, y1, x2, y1);
            AddLine(path, x2, y1, x2, y2);
        }
        else
        {
            AddLine(path, x1, y1, x1, y2);
            AddLine(path, x1, y2, x2, y2);
        }
        return path;
    }

    void AddLine(List<int> path, int xa, int ya, int xb, int yb)
    {
        int dx = xb > xa ? 1 : (xb < xa ? -1 : 0);
        int dy = yb > ya ? 1 : (yb < ya ? -1 : 0);
        int x = xa, y = ya;
        path.Add(x * height + y);
        while (x != xb || y != yb)
        {
            x += dx;
            y += dy;
            path.Add(x * height + y);
        }
    }

    bool IsClean(List<int> path)
    {
        var inPath = new HashSet<int>(path);
        int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };

        foreach (int id in path)
        {
            int x = id / height, y = id % height;
            if (cells[x, y] != CellType.Empty) continue;   // déjà ouvert : rien à creuser ici

            for (int d = 0; d < 4; d++)
            {
                int nx = x + dx[d], ny = y + dy[d];
                if (!InBounds(nx, ny)) continue;
                if (cells[nx, ny] != CellType.Empty && !inPath.Contains(nx * height + ny))
                    return false;   // il colle un couloir ou une salle voisine
            }
        }
        return true;
    }

    void Carve(int x, int y)
    {
        if (cells[x, y] == CellType.Empty) cells[x, y] = CellType.Corridor;   // ne jamais écraser une salle
    }

    /// <summary>Creuse un couloir de la première salle jusqu'au bord de grille (y = 0) : c'est la sortie vers l'extérieur.</summary>
    public void AddEntrance()
    {
        if (rooms.Count == 0) return;
        int x = rooms[0].CenterX;
        for (int y = rooms[0].y - 1; y >= 0; y--) Carve(x, y);
        HasEntrance = true;
        EntranceX = x;
    }

    // ---------- 3. Requêtes et validation ----------
    public bool InBounds(int x, int y) { return x >= 0 && y >= 0 && x < width && y < height; }

    public bool IsOpen(int x, int y) { return InBounds(x, y) && cells[x, y] != CellType.Empty; }

    public int CountOpen()
    {
        int n = 0;
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (cells[x, y] != CellType.Empty) n++;
        return n;
    }

    /// <summary>Remplissage par diffusion (flood fill) depuis la première salle.</summary>
    public bool IsFullyConnected()
    {
        if (rooms.Count == 0) return false;
        var visited = new bool[width, height];
        var stack = new Stack<int>();
        stack.Push(rooms[0].CenterX * height + rooms[0].CenterY);
        int count = 0;
        int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };

        while (stack.Count > 0)
        {
            int id = stack.Pop();
            int x = id / height, y = id % height;
            if (!IsOpen(x, y) || visited[x, y]) continue;
            visited[x, y] = true;
            count++;
            for (int d = 0; d < 4; d++) stack.Push((x + dx[d]) * height + (y + dy[d]));   // hors limites : rejeté par IsOpen
        }
        return count == CountOpen();
    }

    /// <summary>Représentation texte : . vide, # salle, + couloir, E entrée. Pratique pour déboguer dans la Console.</summary>
    public string ToAscii()
    {
        var sb = new System.Text.StringBuilder();
        for (int y = height - 1; y >= 0; y--)      // y croissant vers le haut, comme une carte vue de dessus
        {
            for (int x = 0; x < width; x++)
            {
                if (HasEntrance && x == EntranceX && y == 0) { sb.Append('E'); continue; }
                var c = cells[x, y];
                sb.Append(c == CellType.Empty ? '.' : (c == CellType.Room ? '#' : '+'));
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }
}