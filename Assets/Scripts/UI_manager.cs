using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;


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
    [SerializeField] private ImuUdpLogger imuUdpLogger;
    [SerializeField] private GameObject characterGameObject;

    [Header("Audio")]
    [SerializeField] private AudioSource countdownAudioSource;
    [SerializeField] private AudioSource backgroundMusicAudioSource;
    [SerializeField] private AudioSource whistleAudioSource;
    [SerializeField] private AudioSource surprisedUtterance;

    [Header("Screenshot Settings")]
    [SerializeField] private string screenshotFolderName = "Drawings";
    [SerializeField] private bool useCustomPath = false;
    [SerializeField] private string customSavePath = "";
    [Header("Canvas Crop Settings (0.0 to 1.0)")]
    [SerializeField] [Range(0f, 1f)] private float cropXPercent = 0.21f;
    [SerializeField] [Range(0f, 1f)] private float cropYPercent = 0.2f;
    [SerializeField] [Range(0f, 1f)] private float cropWidthPercent = 0.58f;
    [SerializeField] [Range(0f, 1f)] private float cropHeightPercent = 0.6f;

    public int totalTime = 10; // seconds
    private int currentTime;
    public bool isTimerRunning = false;
    private Coroutine timerCoroutine;
    private float sandStep; 

    private void Start()
    {
        UpdateTimerText(totalTime);
        currentTime = totalTime;
        text_321.gameObject.SetActive(false);

        ResetSandTimer();

        sandStep = 1.0f / totalTime;
        
        // Create screenshot directory if it doesn't exist
        EnsureScreenshotDirectoryExists();
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

            // Resume background music
            if (backgroundMusicAudioSource != null && !backgroundMusicAudioSource.isPlaying)
            {
                backgroundMusicAudioSource.UnPause();
            }
        }
    }

    private IEnumerator StartCountdownSequence()
    {
        // Trigger game started event immediately when countdown begins
        GameEvents.TriggerGameStarted();

        // Play countdown audio
        if (countdownAudioSource != null && countdownAudioSource.clip != null)
        {
            countdownAudioSource.Play();
        }

        text_321.gameObject.SetActive(true);
        string[] countdownTexts = { "3", "2", "1", "Go!" };

        foreach (string count in countdownTexts)
        {
            text_321.text = count;
            
            // Play whistle when "Go!" appears
            if (count == "Go!" && whistleAudioSource != null && whistleAudioSource.clip != null)
            {
                whistleAudioSource.Play();
            }
            
            yield return StartCoroutine(AnimateCountdownText());
        }

        text_321.gameObject.SetActive(false);

        // start main timer
        buttonPlay.GetComponent<Image>().sprite = pauseSprite;
        timerCoroutine = StartCoroutine(TimerCountdown());
        isTimerRunning = true;

        // Start background music loop
        if (backgroundMusicAudioSource != null && backgroundMusicAudioSource.clip != null)
        {
            backgroundMusicAudioSource.loop = true;
            backgroundMusicAudioSource.Play();
        }
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

        // Pause background music when timer is paused
        if (backgroundMusicAudioSource != null && backgroundMusicAudioSource.isPlaying)
        {
            backgroundMusicAudioSource.Pause();
        }
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
        
        // Stop the timer immediately to prevent sparkles during game over
        isTimerRunning = false;
        buttonPlay.GetComponent<Image>().sprite = playSprite;

        // Trigger game ended event
        GameEvents.TriggerGameEnded();

        // show "Artwork Completed!" for 3 seconds
        yield return StartCoroutine(ShowArtworkCompletedMessage());
    }

    private IEnumerator ShowArtworkCompletedMessage()
    {
        // Stop background music
        if (backgroundMusicAudioSource != null && backgroundMusicAudioSource.isPlaying)
        {
            backgroundMusicAudioSource.Stop();
        }

        // Play whistle sound
        if (whistleAudioSource != null && whistleAudioSource.clip != null)
        {
            whistleAudioSource.Play();
        }

        text_321.gameObject.SetActive(true);
        text_321.text = "Artwork Completed!";
        text_321.transform.localScale = Vector3.one;

        float duration = 0.8f;
        float elapsed = 0f;
        float scaleUp = 0.6f;
        float scaleDown = 0.5f;

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

        yield return new WaitForSeconds(3f);

        ResetSandTimer();
        text_321.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
        text_321.gameObject.SetActive(false);

        TakeScreenshot();

        // Trigger game reset event
        GameEvents.TriggerGameReset();

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

    private void EnsureScreenshotDirectoryExists()
    {
        string savePath = GetScreenshotDirectory();
        if (!Directory.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
        }
    }
    
    private string GetScreenshotDirectory()
    {
        if (useCustomPath && !string.IsNullOrEmpty(customSavePath))
        {
            return customSavePath;
        }
        
        // Use Unity's persistent data path (cross-platform)
        // Mac: ~/Library/Application Support/CompanyName/ProductName/
        // Windows: %userprofile%\AppData\LocalLow\CompanyName\ProductName\
        return Path.Combine(Application.persistentDataPath, screenshotFolderName);
    }

    private void TakeScreenshot()
    {
        StartCoroutine(CaptureAndShowScreenshot());
    }

    private IEnumerator CaptureAndShowScreenshot()
    {
        // Hide all brushes temporarily
        if (imuUdpLogger != null)
        {
            imuUdpLogger.HideAllBrushes();
        }

        // Hide character renderers temporarily (keep animations running)
        Renderer[] characterRenderers = null;
        if (characterGameObject != null)
        {
            characterRenderers = characterGameObject.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in characterRenderers)
            {
                renderer.enabled = false;
            }
        }

        // Wait for end of frame to ensure rendering is complete
        yield return new WaitForEndOfFrame();

        // Take a screenshot of the screen (now without brushes and character visible)
        Texture2D fullTex = ScreenCapture.CaptureScreenshotAsTexture();

        int width = fullTex.width;
        int height = fullTex.height;

        // Parameters to crop to canvas area (configurable in Inspector)
        int cropX = Mathf.RoundToInt(width * cropXPercent);
        int cropY = Mathf.RoundToInt(height * cropYPercent);
        int cropWidth = Mathf.RoundToInt(width * cropWidthPercent);
        int cropHeight = Mathf.RoundToInt(height * cropHeightPercent);

        // Crop the screenshot to canvas area
        Color[] pixels = fullTex.GetPixels(cropX, cropY, cropWidth, cropHeight);
        Texture2D croppedTex = new Texture2D(cropWidth, cropHeight, TextureFormat.RGB24, false);
        croppedTex.SetPixels(pixels);
        croppedTex.Apply();

        // Save to disk
        SaveScreenshotToDisk(croppedTex);

        // Cleanup memory
        Destroy(fullTex);
        Destroy(croppedTex);

        // Show character renderers again
        if (characterRenderers != null)
        {
            foreach (Renderer renderer in characterRenderers)
            {
                renderer.enabled = true;
            }
        }

        // Show brushes again
        if (imuUdpLogger != null)
        {
            imuUdpLogger.ShowAllBrushes();
        }

        canvasPainter.ClearCanvas();
    }

    private void SaveScreenshotToDisk(Texture2D texture)
    {
        // Generate filename with timestamp
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string fileName = $"Drawing_{timestamp}.png";
        
        string savePath = GetScreenshotDirectory();
        string fullPath = Path.Combine(savePath, fileName);
        
        // Encode texture to PNG
        byte[] bytes = texture.EncodeToPNG();
        
        // Save to disk
        File.WriteAllBytes(fullPath, bytes);
    }

    /// <summary>
    /// Call this method when a flare is triggered to play the surprised utterance with a 0.5s delay
    /// </summary>
    public void PlaySurprisedUtteranceOnFlare()
    {
        StartCoroutine(PlaySurprisedUtteranceWithDelay());
    }

    private IEnumerator PlaySurprisedUtteranceWithDelay()
    {
        yield return new WaitForSeconds(0.5f);

        if (surprisedUtterance != null && surprisedUtterance.clip != null && !surprisedUtterance.isPlaying)
        {
            surprisedUtterance.loop = false;
            surprisedUtterance.Play();
        }
    }

 }
