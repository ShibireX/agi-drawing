using UnityEngine;

public class PaintSpawner : MonoBehaviour
{
    [Header("Paint Settings")]
    public GameObject paintSpherePrefab;   
    public float speedThreshold = 0.1f;    // Min speed to start painting
    public float spawnInterval = 0.1f;    // Time between spawns
    public float spawnForceMultiplier = 1f; // Controls how much velocity spheres inherit

    private Vector3 lastPosition;
    private float spawnTimer;

    void Start()
    {
        lastPosition = transform.position;
        spawnTimer = 0f;
    }

    void Update()
    {
        // Calculate movement speed and velocity vector
        Vector3 velocity = (transform.position - lastPosition) / Time.deltaTime;
        float speed = velocity.magnitude;
        lastPosition = transform.position;

        spawnTimer -= Time.deltaTime;

        // Spawn spheres only if moving fast enough and timer expired
        if (speed > speedThreshold && spawnTimer <= 0f)
        {
            SpawnPaintSphere(velocity);
            spawnTimer = spawnInterval;
        }
    }

    void SpawnPaintSphere(Vector3 velocity)
    {
        GameObject sphere = Instantiate(paintSpherePrefab, transform.position, Quaternion.identity);

        // Apply velocity if sphere has a Rigidbody
        Rigidbody rb = sphere.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = velocity * spawnForceMultiplier;
        }
    }
}
