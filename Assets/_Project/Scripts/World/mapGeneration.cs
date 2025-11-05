using System.Collections.Generic;
using UnityEngine;

public class MapGeneration : MonoBehaviour
{
    [Header("Grid")]
    public GridGizmo gridGizmo;
    [Header("Prefabs")]
    public GameObject[] wallPrefabs;

    [Header("Origin")]
    [SerializeField] bool originIsCenter = false;
    [Header("SpawnProtection")]
    public Vector2Int spawnSafe = new Vector2Int(2, 2);
    [Header("Wall Box")]
    public int boxSize = 3;                 // 3x3
    [Range(0, 1)] public float boxSpawnChance = 0.8f;
    public int minTilesInBox = 3;           // min. užpildytų langelių kiekis boxe
    public int maxTilesInBox = 9;           // max (3x3 -> 9)
    public int separation = 1;

    [Header("Chest")]
    public GameObject chestPrefab;
    public int chestBlockSize = 3;   // tvirtinam 3x3
    Vector2Int chestCenterCell;

    [Header("Prefabs Height")]
    public float yOffSet = 0.05f;
    public bool generateOnPlay = true;

    private Vector3 gridCorner;
    private Vector2Int gridSize;
    private float gridCellSize;

    bool[,] occupied;
    bool[,] blocked;
    void Start()
    {
        if (generateOnPlay)
            GenerateMap();
    }

    [ContextMenu("Generate Map")]
    public void GenerateMap()
    {
        if (gridGizmo == null)
        {
            Debug.LogError("Grid not found!");
            return;
        }

        gridSize = gridGizmo.size;
        gridCellSize = gridGizmo.cellSize;
        gridCorner = GetGridOrigin();

        occupied = new bool[gridSize.x, gridSize.y];
        blocked = new bool[gridSize.x, gridSize.y];

        for (int x = 0; x < Mathf.Min(spawnSafe.x, gridSize.x); x++)
            for (int y = 0; y < Mathf.Min(spawnSafe.y, gridSize.y); y++)
                blocked[x, y] = true;

        ClearPreviousObjects();

        for (int ax = 0; ax <= gridSize.x - boxSize; ax++)
        {
            for (int ay = 0; ay <= gridSize.y - boxSize; ay++)
            {
                // šansai praleisti boxą
                if (Random.value > boxSpawnChance) continue;

                // Jei šito boxo apimtis + 1 langelio kraštas jau blokuota – praleidžiam
                if (RegionOverlapsBlocked(ax, ay, boxSize, separation)) continue;

                // Kiek langelių pildysim šiame boxe
                int need = Mathf.Clamp(Random.Range(minTilesInBox, maxTilesInBox + 1), 0, boxSize * boxSize);

                // Susirinkam visus 3×3 lokalius langelius ir iš jų atsitiktinai parenkam “need”
                ReserveTopBandChestBlock();
                var cells = new List<Vector2Int>(boxSize * boxSize);
                for (int dx = 0; dx < boxSize; dx++)
                    for (int dy = 0; dy < boxSize; dy++)
                        cells.Add(new Vector2Int(ax + dx, ay + dy));

                FisherYatesShuffle(cells);

                for (int i = 0; i < need; i++)
                {
                    var c = cells[i];
                    occupied[c.x, c.y] = true;
                }

                // Pažymim blokavimo zoną: pats boxas + separation žiedas aplink
                MarkBlockedWithMargin(ax, ay, boxSize, separation);
            }
        }

        // Suinstancinam tik “occupied” langelius
        BuildWalls();
        PlaceChest();

        Debug.Log("Map generated");

    }

    bool RegionOverlapsBlocked(int ax, int ay, int bSize, int margin)
    {
        int minX = Mathf.Max(0, ax - margin);
        int minY = Mathf.Max(0, ay - margin);
        int maxX = Mathf.Min(gridSize.x - 1, ax + bSize - 1 + margin);
        int maxY = Mathf.Min(gridSize.y - 1, ay + bSize - 1 + margin);

        for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
                if (blocked[x, y]) return true;

        return false;
    }

    void MarkBlockedWithMargin(int ax, int ay, int bSize, int margin)
    {
        int minX = Mathf.Max(0, ax - margin);
        int minY = Mathf.Max(0, ay - margin);
        int maxX = Mathf.Min(gridSize.x - 1, ax + bSize - 1 + margin);
        int maxY = Mathf.Min(gridSize.y - 1, ay + bSize - 1 + margin);

        for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
                blocked[x, y] = true;
    }

    void BuildWalls()
    {
        for (int x = 0; x < gridSize.x; x++)
            for (int y = 0; y < gridSize.y; y++)
            {
                if (!occupied[x, y]) continue;
                if (wallPrefabs == null || wallPrefabs.Length == 0) continue;

                Vector3 pos = gridCorner + new Vector3((x + 0.5f) * gridCellSize, yOffSet, (y + 0.5f) * gridCellSize);
                GameObject prefab = wallPrefabs[Random.Range(0, wallPrefabs.Length)];
                Quaternion rot = Quaternion.Euler(0, 90 * Random.Range(0, 4), 0);

                Instantiate(prefab, pos, rot, transform);
            }
    }



    void ReserveTopBandChestBlock()
    {
        int size = Mathf.Max(3, chestBlockSize);   // laikomės 3x3
        size = 3;

        // centro eilė – antra nuo viršaus
        int cy = gridSize.y - 2;
        if (cy < 1) { Debug.LogWarning("Map per žemas chest juostai."); return; }

        // stulpelis turi turėti vietos 3x3 blokui: 1..size.x-2
        int cx = Random.Range(1, Mathf.Max(2, gridSize.x - 1) - 1); // [1, size.x-2)

        // rezervuojam 3x3 aplink (cx, cy): x∈[cx-1..cx+1], y∈[cy-1..cy+1]
        for (int x = cx - 1; x <= cx + 1; x++)
        {
            if (x < 0 || x >= gridSize.x) continue;
            for (int y = cy - 1; y <= cy + 1; y++)
            {
                if (y < 0 || y >= gridSize.y) continue;
                blocked[x, y] = true;   // mūsų 3x3 – be box’ų
                occupied[x, y] = false; // jei kas nors jau pažymėta – nuvalom
            }
        }

        chestCenterCell = new Vector2Int(cx, cy);
    }

    void PlaceChest()
    {
        if (!chestPrefab) return;

        Vector3 pos = gridCorner + new Vector3(
            (chestCenterCell.x + 0.5f) * gridCellSize,
            yOffSet,
            (chestCenterCell.y + 0.5f) * gridCellSize
        );

        Instantiate(chestPrefab, pos, Quaternion.identity, transform);
    }

    void FisherYatesShuffle(List<Vector2Int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void ClearPreviousObjects()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
    }
    Vector3 GetGridOrigin()
    {
        if (originIsCenter)
        {
            return new Vector3(
                transform.position.x - gridSize.x * 0.5f * gridCellSize,
                transform.position.y,
                transform.position.z - gridSize.y * 0.5f * gridCellSize
            );
        }
        else
        {
            return transform.position;
        }
    }
}
