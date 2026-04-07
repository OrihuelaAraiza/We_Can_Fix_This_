using UnityEngine;

public class BoxSpawner : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject boxPrefab;

    [Header("Spawn")]
    public Transform[] spawnPoints;
    public float spawnIntervalSeconds = 25f;
    public int maxBoxesAlive = 12;
    public bool spawnOnStart = true;

    [Header("Optional")]
    public bool createPickUpSnapIfMissing = true;
    public Vector3 pickUpSnapLocalPos = Vector3.zero;

    private float timer = 0f;

    void Start()
    {
        if (spawnOnStart)
            SpawnBox();
    }

    void Update()
    {
        if (boxPrefab == null) return;
        if (spawnPoints == null || spawnPoints.Length == 0) return;

        timer += Time.deltaTime;
        if (timer >= spawnIntervalSeconds)
        {
            timer = 0f;
            SpawnBox();
        }
    }

    void SpawnBox()
    {
        int currentBoxes = FindObjectsOfType<BoxItem>().Length;
        if (currentBoxes >= maxBoxesAlive) return;

        Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
        GameObject go = Instantiate(boxPrefab, sp.position, sp.rotation);

        BoxItem box = go.GetComponent<BoxItem>();
        if (box == null) box = go.AddComponent<BoxItem>();

        if (createPickUpSnapIfMissing && box.pickUpSnap == null)
        {
            Transform t = go.transform.Find("PickUpSnap");
            if (t == null)
            {
                GameObject snap = new GameObject("PickUpSnap");
                snap.transform.SetParent(go.transform);
                snap.transform.localPosition = pickUpSnapLocalPos;
                snap.transform.localRotation = Quaternion.identity;
                box.pickUpSnap = snap.transform;
            }
            else
            {
                box.pickUpSnap = t;
            }
        }
    }
}