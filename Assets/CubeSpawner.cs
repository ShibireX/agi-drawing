using UnityEngine;

public class CubeSpawner : MonoBehaviour
{
    public float spawnInterval = 3f;
    public float spawnDistance = 3f;
    public Vector3 cubeScale = new Vector3(0.3f, 0.3f, 0.3f);
    public float cubeLifetime = 10f;
    [Tooltip("Optional custom prefab; if null, we'll make a Primitive cube.")]
    public GameObject cubePrefab;

    float _t;

    void Update()
    {
        _t += Time.deltaTime;
        if (_t >= spawnInterval)
        {
            _t = 0f;
            SpawnOne();
        }
    }

    void SpawnOne()
    {
        var cam = Camera.main;
        if (!cam) { Debug.LogWarning("CubeSpawner: No Camera.main"); return; }

        Vector3 pos = cam.transform.position + cam.transform.forward * spawnDistance;
        Quaternion rot = Quaternion.LookRotation(-cam.transform.forward, cam.transform.up); // face camera

        GameObject go;
        if (cubePrefab)
        {
            go = Instantiate(cubePrefab, pos, rot);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetPositionAndRotation(pos, rot);
        }

        go.transform.localScale = cubeScale;

        // Ensure collider is trigger so a Rigidbody projectile can pass and notify
        var col = go.GetComponent<Collider>();
        if (!col) col = go.AddComponent<BoxCollider>();
        col.isTrigger = true;

        // Marker/logic for hit detection
        if (!go.TryGetComponent<TargetCube>(out _))
            go.AddComponent<TargetCube>();

        // Clean up after a while
        if (cubeLifetime > 0f) Destroy(go, cubeLifetime);
    }
}