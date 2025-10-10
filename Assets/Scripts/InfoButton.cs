using UnityEngine;
using TMPro;

public class InfoButton : MonoBehaviour
{
    [Tooltip("The panel that contains the info text.")]
    public GameObject infoPanel;

    [Tooltip("The TextMeshProUGUI component for displaying info text.")]
    public TextMeshProUGUI infoText;

    private bool isVisible = false;

    public void ToggleInfoPanel()
    {
        if (infoPanel == null || infoText == null)
        {
            Debug.LogWarning("[InfoButton] Missing references.");
            return;
        }

        isVisible = !isVisible;
        infoPanel.SetActive(isVisible);

        if (isVisible)
        {
            infoText.text = "[INFORMATION PLACEHOLDER]";
        }
    }
}
