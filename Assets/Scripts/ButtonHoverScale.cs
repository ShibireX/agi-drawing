using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Vector3 originalScale;
    private Vector3 targetScale;
    private bool isHovered = false;

    [SerializeField] private float scaleFactor = 1.05f;
    [SerializeField] private float speed = 10f;

    void Start()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
    }

    void Update()
    {
        // Smoothly interpolate towards target scale
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * speed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        targetScale = originalScale * scaleFactor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        targetScale = originalScale;
    }
}
