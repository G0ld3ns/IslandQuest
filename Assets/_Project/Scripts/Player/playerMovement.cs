using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

public class GridMover : MonoBehaviour
{
    [Header("Grid")]
    public Transform grid;
    public Vector2Int gridSize = new Vector2Int(20, 20);
    public float cellSize = 1f;
    public bool clampToBounds = true;

    [Header("Collision")]
    public LayerMask obstacleMask;             
    [Range(0.1f, 1f)] public float cellOccupancy = 0.9f; // 0.9 = beveik visas langelis
    public float testHeight = 2f;

    [Header("Visual (child)")]
    public Transform visual;                 // parent su baze + modeliu
    public Vector3 fixPitchRoll = Vector3.zero;
    public Vector3 modelOffset = Vector3.zero;
    public bool autoCenterOnStart = true;

    [Header("Movement")]
    public float moveDuration = 0.22f;       // 0.18–0.30
    public float hopHeight = 0.35f;
    public bool bobVisualOnly = true;        // hop tik vizualui – kamera nejitterina
    public LayerMask groundMask = ~0;

    // ---- private ----
    Vector2Int gridPos;
    float gridY;
    bool isMoving;
    Quaternion baseFixRot;

    void Awake()
    {
        if (!grid) { Debug.LogError("Assign Grid"); enabled = false; return; }

        // auto-read iš GridGizmo, jei yra
        var giz = grid.GetComponent<GridGizmo>();
        if (giz) { gridSize = giz.size; cellSize = giz.cellSize; }

        baseFixRot = Quaternion.Euler(fixPitchRoll);
        gridY = grid.position.y;

        // centruojam vizualą pagal render bounds (patogu, jei pivot ne per vidurį)
        if (visual && autoCenterOnStart)
        {
            var rends = visual.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                Vector3 localCenter = visual.InverseTransformPoint(b.center);
                modelOffset += new Vector3(-localCenter.x, 0f, -localCenter.z);
            }
            visual.localPosition = modelOffset;
        }

        // start pozicija ir facing
        gridPos = WorldToGrid(transform.position);
        transform.position = GridToWorld(gridPos);
        ApplyFacing(Vector2Int.up);
    }

    void Update()
    {
        // kai nestojam – laikom vizualą ant bazės
        if (!isMoving && bobVisualOnly && visual)
            visual.localPosition = modelOffset;

        if (isMoving) return;

        var kb = Keyboard.current; if (kb == null) return;
        Vector2Int dir = Vector2Int.zero;
        if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) dir = Vector2Int.up;
        else if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) dir = Vector2Int.down;
        else if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) dir = Vector2Int.left;
        else if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) dir = Vector2Int.right;

        if (dir != Vector2Int.zero) TryMove(dir);
    }

    void TryMove(Vector2Int dir)
    {
        if (isMoving) return;

        Vector2Int next = gridPos + dir;

        if (clampToBounds &&
            (next.x < 0 || next.x >= gridSize.x || next.y < 0 || next.y >= gridSize.y))
            return;

        // ❗ jei kita ląstelė užblokuota – niekur neinam
        if (IsCellBlocked(next)) return;

        ApplyFacing(dir);
        StartCoroutine(HopTo(next));
    }


    void ApplyFacing(Vector2Int dir)
    {
        float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg; // W=+Z, D=+X ...
        if (visual) visual.localRotation = Quaternion.Euler(0f, yaw, 0f) * baseFixRot;
        else transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    IEnumerator HopTo(Vector2Int next)
    {
        isMoving = true;

        // užfiksuojam startinį langelį, kad animacija būtų deterministinė
        Vector2Int startGrid = gridPos;

        Vector3 from = GridToWorld(startGrid);  // su ground aukščiu
        Vector3 to = GridToWorld(next);
        float baseY = from.y;                  // hopo metu root lieka lygiam Y

        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            float k = Mathf.Clamp01(elapsed / moveDuration);

            // root juda tik XZ – be jitter
            Vector3 pos = Vector3.Lerp(from, to, k);
            pos.y = baseY;
            transform.position = pos;

            // hop tik vizualui
            if (bobVisualOnly && visual)
            {
                float yArc = Mathf.Sin(k * Mathf.PI) * hopHeight;
                visual.localPosition = modelOffset + new Vector3(0f, yArc, 0f);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // nusileidimas: vienas ground snap
        Vector3 landed = to;
        if (TryGroundHit(landed, out var hit)) landed.y = hit.point.y;
        else landed.y = gridY;

        transform.position = landed;

        // grąžinam vizualą į bazę
        if (bobVisualOnly && visual)
            visual.localPosition = modelOffset;

        // tik dabar atnaujinam grid koordinatę
        gridPos = next;
        isMoving = false;
    }

    // ----- helpers -----
    Vector3 GridOrigin() => new Vector3(
        grid.position.x - gridSize.x * 0.5f * cellSize,
        gridY,
        grid.position.z - gridSize.y * 0.5f * cellSize
    );

    bool IsCellBlocked(Vector2Int cell)
    {
        // langelio centro pasaulio koordinatė
        Vector3 center = GridToWorld(cell);

        // padarom „dėžę“ per XZ, o Y užkeliame per pusę testHeight
        float half = (cellSize * cellOccupancy) * 0.5f;
        Vector3 halfExtents = new Vector3(half, testHeight * 0.5f, half);
        Vector3 boxCenter = new Vector3(center.x, center.y + halfExtents.y, center.z);

        // kad neatsimuštume į patį žaidėją
        int mask = obstacleMask & ~(1 << gameObject.layer);

        // jei kas nors su Obstacle layer yra toje dėžėje – laikom užblokuota
        return Physics.CheckBox(boxCenter, halfExtents, Quaternion.identity, mask, QueryTriggerInteraction.Ignore);
    }

    bool TryGroundHit(Vector3 at, out RaycastHit hit)
    {
        int castMask = groundMask & ~(1 << gameObject.layer); // neatsimušti į save
        return Physics.Raycast(at + Vector3.up * 5f, Vector3.down, out hit, 50f, castMask, QueryTriggerInteraction.Ignore);
    }

    // Patogu naudoti ir be generatoriaus (pvz., per UI/respawn)
    public void TeleportTo(Vector2Int cell)
    {
        gridPos = cell;
        transform.position = GridToWorld(gridPos);
        if (visual) visual.localPosition = modelOffset;
        isMoving = false;
    }

    Vector3 GridToWorld(Vector2Int gp)
    {
        Vector3 o = GridOrigin();
        Vector3 p = o + new Vector3((gp.x + 0.5f) * cellSize, gridY, (gp.y + 0.5f) * cellSize);

        if (TryGroundHit(p, out var hit)) p.y = hit.point.y;
        else p.y = gridY;

        return p;
    }

    Vector2Int WorldToGrid(Vector3 world)
    {
        Vector3 o = GridOrigin();
        float gx = (world.x - o.x) / cellSize - 0.5f;
        float gy = (world.z - o.z) / cellSize - 0.5f;
        return new Vector2Int(Mathf.RoundToInt(gx), Mathf.RoundToInt(gy));
    }
}
