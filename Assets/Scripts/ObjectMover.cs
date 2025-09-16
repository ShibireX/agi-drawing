using UnityEngine;

public class ObjectMover : MonoBehaviour
{
    private float speed;    // Movement speed
    private Renderer rend;  // Renderer reference for color change

    public void Initialize(float moveSpeed)
    {
        speed = moveSpeed;

        // Cache the Renderer so we can change color later
        rend = GetComponent<Renderer>();

        // Default color (white)
        if (rend != null)
        {
            rend.material.color = Color.white;
        }
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

    private void OnMouseDown()
    {
        // Left-click detection
        if (Input.GetMouseButtonDown(0))
        {
            if (rend != null)
            {
                rend.material.color = Color.green;
            }
        }
    }
}
