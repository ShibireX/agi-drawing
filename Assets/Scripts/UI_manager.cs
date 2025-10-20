using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;


public class UI_manager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI text_321;
    [SerializeField] private Sprite pauseSprite;
    [SerializeField] private Sprite playSprite;
    [SerializeField] private GameObject buttonPlay;
    [SerializeField] private List<Image> galleryImages; 
    private int currentGalleryIndex = 0;

    [SerializeField] private Image sandTimerUp;
    [SerializeField] private Image sandTimerDown;
    [SerializeField] private Paint.CanvasPainter canvasPainter;

    public int totalTime = 10; // seconds
    private int currentTime;
    private bool isTimerRunning = false;
    private Coroutine timerCoroutine;
    private float sandStep; 

    private void Start()
    {
        UpdateTimerText(totalTime);
        currentTime = totalTime;
        text_321.gameObject.SetActive(false);

        ResetSandTimer();

        sandStep = 1.0f / totalTime; 
    }

    public void ToggleTimer()
    {
        if (!isTimerRunning)
        {
            StartTimer();
        }
        else
        {
            StopTimer();
        }
    }

    private void StartTimer()
    {
        if (currentTime == totalTime)
        {
            StartCoroutine(StartCountdownSequence());
        }
        else
        {
            // Resume timer
            buttonPlay.GetComponent<Image>().sprite = pauseSprite;
            timerCoroutine = StartCoroutine(TimerCountdown());
            isTimerRunning = true;
        }
    }

    private IEnumerator StartCountdownSequence()
    {
        text_321.gameObject.SetActive(true);
        string[] countdownTexts = { "3", "2", "1", "Go!" };

        foreach (string count in countdownTexts)
        {
            text_321.text = count;
            yield return StartCoroutine(AnimateCountdownText());
        }

        text_321.gameObject.SetActive(false);

        // start main timer
        buttonPlay.GetComponent<Image>().sprite = pauseSprite;
        timerCoroutine = StartCoroutine(TimerCountdown());
        isTimerRunning = true;
    }

    private IEnumerator AnimateCountdownText()
    {
        float duration = 1f;
        float elapsed = 0f;
        float scaleUp = 1.1f;
        float scaleDown = 0.9f;

        Vector3 originalScale = text_321.transform.localScale;
        Vector3 startScale = originalScale * scaleUp;
        Vector3 endScale = originalScale * scaleDown;

        // Start at slightly larger scale
        text_321.transform.localScale = startScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            text_321.transform.localScale = Vector3.Lerp(startScale, endScale, t);
            yield return null;
        }

        // Restore to original scale
        text_321.transform.localScale = originalScale;
    }

    private void StopTimer()
    {
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
        }

        buttonPlay.GetComponent<Image>().sprite = playSprite;
        isTimerRunning = false;
    }

    private IEnumerator TimerCountdown()
    {
        while (currentTime > 0)
        {
            UpdateTimerText(currentTime);
            yield return new WaitForSeconds(1f);
            currentTime--;

            sandTimerUp.fillAmount = sandTimerUp.fillAmount - sandStep;
            sandTimerDown.fillAmount = sandTimerDown.fillAmount + sandStep;
        }

        UpdateTimerText(0);
        currentTime = totalTime;

        // show "Artwork Completed!" for 3 seconds
        yield return StartCoroutine(ShowArtworkCompletedMessage());
        StopTimer();
    }

    private IEnumerator ShowArtworkCompletedMessage()
    {
        text_321.gameObject.SetActive(true);
        text_321.text = "Artwork Completed!";
        text_321.transform.localScale = Vector3.one;

        float duration = 0.8f;
        float elapsed = 0f;
        float scaleUp = 0.6f;
        float scaleDown = 0.5f;

        Vector3 originalScale = text_321.transform.localScale;
        Debug.Log(originalScale);
        Vector3 startScale = originalScale * scaleUp;
        Vector3 endScale = originalScale * scaleDown;

        // Start at slightly larger scale
        text_321.transform.localScale = startScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            text_321.transform.localScale = Vector3.Lerp(startScale, endScale, t);
            yield return null;
        }

        yield return new WaitForSeconds(3f);

        ResetSandTimer();
        text_321.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
        text_321.gameObject.SetActive(false);

        TakeScreenshot();

        //canvasPainter.ClearCanvas();
    }


    private void UpdateTimerText(int timeInSeconds)
    {
        int minutes = timeInSeconds / 60;
        int seconds = timeInSeconds % 60;
        timerText.text = $"{minutes:00}:{seconds:00}";
    }

    private void ResetSandTimer()
    {
        sandTimerUp.fillAmount = 1f;
        sandTimerDown.fillAmount = 0f;
    }

    private void TakeScreenshot()
    {
        StartCoroutine(CaptureAndShowScreenshot());
    }

    private IEnumerator CaptureAndShowScreenshot()
    {
        yield return new WaitForEndOfFrame();

        // save screenshot as a textrue
        Texture2D fullTex = ScreenCapture.CaptureScreenshotAsTexture();

        int width = fullTex.width;
        int height = fullTex.height;

        // parameters to crop current screenshot
        int cropX = Mathf.RoundToInt(width * 0.2f);
        int cropY = Mathf.RoundToInt(height * 0.2f);
        int cropWidth = Mathf.RoundToInt(width * 0.6f);
        int cropHeight = Mathf.RoundToInt(height * 0.6f);

        // crop
        Color[] pixels = fullTex.GetPixels(cropX, cropY, cropWidth, cropHeight);
        Texture2D croppedTex = new Texture2D(cropWidth, cropHeight, TextureFormat.RGB24, false);
        croppedTex.SetPixels(pixels);
        croppedTex.Apply();

        // create a Sprite from the cropped texture
        Sprite screenshotSprite = Sprite.Create(
            croppedTex,
            new Rect(0, 0, cropWidth, cropHeight),
            new Vector2(0.5f, 0.5f)
        );

        // update the gallery image
        if (galleryImages != null && galleryImages.Count > 0)
        {
            Image targetImage = galleryImages[currentGalleryIndex];
            targetImage.sprite = screenshotSprite;
            //targetImage.preserveAspect = true;

            currentGalleryIndex = (currentGalleryIndex + 1) % galleryImages.Count;
        }

        // Cleanup memory
        Destroy(fullTex);

        canvasPainter.ClearCanvas();
    }

 }
