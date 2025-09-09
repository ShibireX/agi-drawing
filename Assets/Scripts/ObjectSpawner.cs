using UnityEngine;

public class ObjectSpawner : MonoBehaviour
{
    public GameObject objectPrefab;     // The prefab to spawn
    public Transform cameraTransform;   // Reference to camera
    public float spawnDistance = 30f;   // How far away objects spawn (z-axis)
    public float minSpawnInterval = 1f; // Minimum time between spawns
    public float maxSpawnInterval = 3f; // Maximum time between spawns
    public float moveSpeed = 10f;       // Speed objects move towards camera
    public float xRange = 5f;           // How far left/right objects can spawn

    private void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    private System.Collections.IEnumerator SpawnLoop()
    {
        while (true)
        {
            SpawnObject();
            float waitTime = Random.Range(minSpawnInterval, maxSpawnInterval);
            yield return new WaitForSeconds(waitTime);
        }
    }

    private void SpawnObject()
    {
        float randomX = Random.Range(-xRange, xRange);
        Vector3 spawnPos = new Vector3(
            randomX,
            cameraTransform.position.y,
            cameraTransform.position.z + spawnDistance
        );

        GameObject obj = Instantiate(objectPrefab, spawnPos, Quaternion.identity);
        obj.AddComponent<ObjectMover>().Initialize(moveSpeed);
    }
}
