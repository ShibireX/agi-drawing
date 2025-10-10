using System.Collections;
using UnityEngine;
using TMPro;

public class SaveButton : MonoBehaviour
{
    [Tooltip("The TextMeshProUGUI object used to display the save message.")]
    public TextMeshProUGUI saveMessageText;

    [Tooltip("Duration in seconds for how long the message is visible.")]
    public float displayDuration = 3f;

    private Coroutine hideCoroutine;

    public void ShowSaveMessage()
    {
        if (saveMessageText == null)
        {
            Debug.LogWarning("[SaveButton] No saveMessageText assigned.");
            return;
        }

        saveMessageText.text = "Painting saved to Gallery";
        saveMessageText.gameObject.SetActive(true);

        // Restart the timer if pressed multiple times
        if (hideCoroutine != null)
            StopCoroutine(hideCoroutine);

        hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);
        saveMessageText.gameObject.SetActive(false);
    }
}
