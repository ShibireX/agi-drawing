using UnityEngine;

public class AutoDestroy : MonoBehaviour
{
    public float lifetime = 2f; // how long the object stays alive

    void Start()
    {
        Destroy(gameObject, lifetime);
    }
}
