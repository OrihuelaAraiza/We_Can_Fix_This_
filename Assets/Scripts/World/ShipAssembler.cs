using UnityEngine;

[DefaultExecutionOrder(-100)]
public class ShipAssembler : MonoBehaviour
{
    [Header("Palette")]
    [SerializeField] Color cFloor  = new Color(0.13f, 0.15f, 0.20f);
    [SerializeField] Color cWall   = new Color(0.18f, 0.20f, 0.26f);
    [SerializeField] Color cAccent = new Color(0.20f, 0.50f, 0.90f);

    [Header("Settings")]
    [SerializeField] bool buildOnStart = true;

    // Dimensiones fijas
    const float CX = 20f; // centro ancho
    const float CZ = 20f; // centro largo
    const float AW = 10f; // brazo ancho
    const float AL = 12f; // brazo largo
    const float WH = 3.5f; // altura pared
    const float WT = 0.4f; // grosor pared
    const float FH = 0.4f; // grosor piso

    void Start() { if (buildOnStart) Assemble(); }

    [ContextMenu("Assemble Now")]
    public void Assemble()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        BuildCenter();
        BuildNorth();
        BuildSouth();
        BuildEast();
        BuildWest();
        BuildAccents();
        PositionStations();

        Debug.Log("[ShipAssembler] Nave ensamblada sin paredes bloqueantes.");
    }

    // ── Centro 20x20 — abierto en los 4 lados donde conectan los brazos
    void BuildCenter()
    {
        var g = Zone("Center", 0, 0);
        Floor(g, 0, 0, CX, CZ);

        // Norte y Sur: tienen hueco en el centro de AW ancho
        WallWithGap(g, "W_N",  0,  CZ/2f, CX, true,  AW);
        WallWithGap(g, "W_S",  0, -CZ/2f, CX, true,  AW);
        // Este y Oeste: tienen hueco en el centro de AW ancho
        WallWithGap(g, "W_E",  CX/2f, 0, CZ, false, AW);
        WallWithGap(g, "W_W", -CX/2f, 0, CZ, false, AW);
    }

    // ── Brazo Norte — abierto en el sur (conecta con centro)
    void BuildNorth()
    {
        float posZ = CZ/2f + AL/2f;
        var g = Zone("North", 0, posZ);
        Floor(g, 0, posZ, AW, AL);

        Wall(g, "W_N",  0,        posZ + AL/2f, AW, true);  // frente: cerrado
        Wall(g, "W_E",  AW/2f,    posZ,         AL, false); // lado: cerrado
        Wall(g, "W_W", -AW/2f,    posZ,         AL, false); // lado: cerrado
        // Sur: ABIERTO — no se crea pared
    }

    // ── Brazo Sur — abierto en el norte
    void BuildSouth()
    {
        float posZ = -(CZ/2f + AL/2f);
        var g = Zone("South", 0, posZ);
        Floor(g, 0, posZ, AW, AL);

        Wall(g, "W_S",  0,        posZ - AL/2f, AW, true);
        Wall(g, "W_E",  AW/2f,    posZ,         AL, false);
        Wall(g, "W_W", -AW/2f,    posZ,         AL, false);
        // Norte: ABIERTO
    }

    // ── Brazo Este — abierto en el oeste
    void BuildEast()
    {
        float posX = CX/2f + AL/2f;
        var g = Zone("East", posX, 0);
        Floor(g, posX, 0, AL, AW);

        Wall(g, "W_E",  posX + AL/2f, 0,        AW, false);
        Wall(g, "W_N",  posX,         AW/2f,    AL, true);
        Wall(g, "W_S",  posX,        -AW/2f,    AL, true);
        // Oeste: ABIERTO
    }

    // ── Brazo Oeste — abierto en el este
    void BuildWest()
    {
        float posX = -(CX/2f + AL/2f);
        var g = Zone("West", posX, 0);
        Floor(g, posX, 0, AL, AW);

        Wall(g, "W_W",  posX - AL/2f, 0,        AW, false);
        Wall(g, "W_N",  posX,         AW/2f,    AL, true);
        Wall(g, "W_S",  posX,        -AW/2f,    AL, true);
        // Este: ABIERTO
    }

    // ── Acentos decorativos
    void BuildAccents()
    {
        var g = new GameObject("Accents");
        g.transform.SetParent(transform);
        MakeCube("Acc_H", g.transform, 0, 0.43f, 0, CX, 0.05f, 0.3f, cAccent);
        MakeCube("Acc_V", g.transform, 0, 0.43f, 0, 0.3f, 0.05f, CZ, cAccent);
    }

    // ─── Station Placement ────────────────────────────────────────────

    void PositionStations()
    {
        MoveStation("Station_Gravity",         0f,  1f,  16f);
        MoveStation("Station_Hull",            0f,  1f, -16f);
        MoveStation("Station_Communications", 16f,  1f,   0f);
        MoveStation("Station_Energy",        -16f,  1f,   0f);
    }

    void MoveStation(string stationName, float x, float y, float z)
    {
        var obj = GameObject.Find(stationName);
        if (obj != null)
        {
            obj.transform.position = new Vector3(x, y, z);
            Debug.Log($"[ShipAssembler] {stationName} → ({x},{y},{z})");
        }
        else
            Debug.LogWarning($"[ShipAssembler] No encontró: {stationName}");
    }

    // ─── Helpers ──────────────────────────────────────────────────────

    // Pared sólida sin hueco
    void Wall(GameObject parent, string id,
              float wx, float wz, float length, bool alongX)
    {
        float wallY = FH/2f + WH/2f;
        float sx = alongX ? length : WT;
        float sz = alongX ? WT     : length;
        MakeCube(id, parent.transform, wx, wallY, wz, sx, WH, sz, cWall);
    }

    // Pared con hueco central (para las 4 paredes del centro)
    void WallWithGap(GameObject parent, string id,
                     float wx, float wz,
                     float totalLen, bool alongX, float gapW)
    {
        float sideLen = (totalLen - gapW) / 2f;
        float wallY   = FH/2f + WH/2f;
        float offset  = gapW/2f + sideLen/2f;

        if (alongX) // pared corre en X
        {
            float sx = sideLen; float sz = WT;
            MakeCube(id+"_L", parent.transform,
                wx - offset, wallY, wz, sx, WH, sz, cWall);
            MakeCube(id+"_R", parent.transform,
                wx + offset, wallY, wz, sx, WH, sz, cWall);
        }
        else // pared corre en Z
        {
            float sx = WT; float sz = sideLen;
            MakeCube(id+"_L", parent.transform,
                wx, wallY, wz - offset, sx, WH, sz, cWall);
            MakeCube(id+"_R", parent.transform,
                wx, wallY, wz + offset, sx, WH, sz, cWall);
        }
    }

    void Floor(GameObject parent, float x, float z, float sx, float sz)
    {
        MakeCube("Floor", parent.transform, x, FH/2f, z,
                 sx, FH, sz, cFloor, isFloor: true);
    }

    GameObject Zone(string id, float x, float z)
    {
        var g = new GameObject("Zone_" + id);
        g.transform.SetParent(transform);
        g.transform.localPosition = Vector3.zero;
        return g;
    }

    void MakeCube(string id, Transform parent,
                  float x, float y, float z,
                  float sx, float sy, float sz,
                  Color color, bool isFloor = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = id;
        go.transform.SetParent(parent);
        go.transform.position   = new Vector3(x, y, z);
        go.transform.localScale = new Vector3(sx, sy, sz);
        if (isFloor) go.layer = LayerMask.NameToLayer("Ground");
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        go.GetComponent<Renderer>().material = mat;
    }
}
