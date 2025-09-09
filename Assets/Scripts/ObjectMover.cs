using UnityEngine;

public class ObjectMover : MonoBehaviour
{
    private float speed;

    public void Initialize(float moveSpeed)
    {
        speed = moveSpeed;
    }

    void Update()
    {
        // Move straight towards -z (towards the camera)
        transform.position += Vector3.back * speed * Time.deltaTime;

        // Destroy once behind the camera
        if (transform.position.z < -0.5f)
        {
            Destroy(gameObject);
        }
    }
}
