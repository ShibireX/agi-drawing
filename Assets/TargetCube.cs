using UnityEngine;

public class TargetCube : MonoBehaviour
{
    [Tooltip("Tag your projectile prefab with this. Leave blank to accept any Rigidbody hit.")]
    public string projectileTag = "Projectile";

    [Tooltip("Optional: a tiny pop on death.")]
    public float popScale = 1.2f;
    public float popTime = 0.05f;

    bool _dying;

    void OnTriggerEnter(Collider other)
    {
        if (_dying) return;

        bool isProjectile =
            (!string.IsNullOrEmpty(projectileTag) && other.CompareTag(projectileTag))
            || other.attachedRigidbody != null; // fallback: any rigidbody

        if (isProjectile)
        {
            // quick pop and die
            _dying = true;
            if (popTime > 0f)
                StartCoroutine(PopAndDestroy());
            else
                Destroy(gameObject);
        }
    }

    System.Collections.IEnumerator PopAndDestroy()
    {
        Vector3 start = transform.localScale;
        Vector3 end = start * popScale;
        float t = 0f;
        while (t < popTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / popTime);
            transform.localScale = Vector3.Lerp(start, end, k);
            yield return null;
        }
        Destroy(gameObject);
    }
}