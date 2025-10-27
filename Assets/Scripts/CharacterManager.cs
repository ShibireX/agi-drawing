using UnityEngine;

public class CharacterManager : MonoBehaviour
{
    [SerializeField] private Animator characterAnimator;

    [Header("Audio")]
    [SerializeField] private AudioSource gameStartUtterance;
    [SerializeField] private AudioSource gameOverUtterance;
    [SerializeField] private AudioSource normalUtterance;

    private bool isGameStarted = false;
    private bool isGameEnded = false;

    public float handwaveCooldown = 5f;
    public float thumbsUpCooldown = 5f;

    private float handwaveTimer = 0f;
    private float thumbsUpTimer = 0f;
    private bool isThumbsUpPlaying = false;

    public float countdownDuration = 4f;
    private float countdownTimer = 0f;

    // Random utterance timer
    public float normalUtteranceInterval = 15f;
    private float normalUtteranceTimer = 0f;

    private void Start()
    {
        if (characterAnimator == null)
            characterAnimator = GetComponent<Animator>();

        ResetGameState();
        GameEvents.OnGameStarted += OnGameStarted;
        GameEvents.OnGameEnded += OnGameEnded;
        GameEvents.OnGameReset += OnGameReset;
    }

    private void OnDestroy()
    {
        GameEvents.OnGameStarted -= OnGameStarted;
        GameEvents.OnGameEnded -= OnGameEnded;
        GameEvents.OnGameReset -= OnGameReset;
    }

    private void Update()
    {
        if (!characterAnimator) return;

        // Update animator timers
        characterAnimator.SetFloat("handwave_1_timer", handwaveTimer);
        characterAnimator.SetFloat("thumbsup_1_timer", thumbsUpTimer);

        bool isCounting = characterAnimator.GetBool("is_character_counting");

        if (!isGameStarted && !isGameEnded)
        {
            HandleIdleHandwave();
        }
        else if (isGameStarted && !isGameEnded && !isCounting)
        {
            // Only handle thumbs up after countdown is complete
            HandleThumbsUp();
        }

        if (isCounting)
        {
            countdownTimer += Time.deltaTime;
            if (countdownTimer >= countdownDuration)
            {
                characterAnimator.SetBool("is_character_counting", false);
                StartThumbsUpLoop();
            }
        }

        handwaveTimer += Time.deltaTime;
        
        // Only increment thumbsUpTimer when not counting
        if (!isCounting)
        {
            thumbsUpTimer += Time.deltaTime;
        }

        // Handle random normal utterance
        HandleNormalUtterance();
    }

    private void HandleNormalUtterance()
    {
        normalUtteranceTimer += Time.deltaTime;
        
        if (normalUtteranceTimer >= normalUtteranceInterval)
        {
            // Only play if no other utterance is currently playing
            if (!IsAnyUtterancePlaying())
            {
                PlayNormalUtterance();
                normalUtteranceTimer = 0f;
            }
        }
    }

    private bool IsAnyUtterancePlaying()
    {
        return (gameStartUtterance != null && gameStartUtterance.isPlaying) ||
               (gameOverUtterance != null && gameOverUtterance.isPlaying) ||
               (normalUtterance != null && normalUtterance.isPlaying);
    }

    private void PlayNormalUtterance()
    {
        if (normalUtterance != null && normalUtterance.clip != null)
        {
            normalUtterance.loop = false;
            normalUtterance.Play();
        }
    }

    private void OnGameStarted()
    {
        isGameStarted = true;
        isGameEnded = false;

        characterAnimator.SetBool("is_game_started", true);
        characterAnimator.SetBool("is_game_ended", false);
        // Stop idle handwave
        StopHandwave();
        // Start countdown
        countdownTimer = 0f;
        thumbsUpTimer = 0f; // Reset thumbs up timer
        characterAnimator.SetBool("is_character_counting", true);

        // Play game start utterance
        if (gameStartUtterance != null && gameStartUtterance.clip != null)
        {
            gameStartUtterance.loop = false;
            gameStartUtterance.Play();
        }
    }

    private void OnGameEnded()
    {
        isGameEnded = true;
        characterAnimator.SetBool("is_game_ended", true);
        characterAnimator.SetBool("is_character_gameover", true);

        StopThumbsUp();

        // Play game over utterance
        if (gameOverUtterance != null && gameOverUtterance.clip != null)
        {
            gameOverUtterance.loop = false;
            gameOverUtterance.Play();
        }
    }

    private void OnGameReset()
    {
        ResetGameState();
    }

    private void ResetGameState()
    {
        isGameStarted = false;
        isGameEnded = false;
        handwaveTimer = 0f;
        thumbsUpTimer = 0f;
        normalUtteranceTimer = 0f;
        isThumbsUpPlaying = false;

        foreach (string param in new string[]
        {
            "is_game_started", "is_game_ended",
            "is_handwave_1", "is_handwave_2",
            "is_character_counting", "is_character_gameover",
            "is_character_thumbsup_1", "is_character_thumbsup_2"
        })
            characterAnimator.SetBool(param, false);
    }

    // Handwave Logic
    private void HandleIdleHandwave()
    {
        if (handwaveTimer >= handwaveCooldown)
        {
            PlayRandomHandwave();
            handwaveTimer = 0f;
        }
    }

    private void PlayRandomHandwave()
    {
        bool useHandwave1 = Random.value > 0.5f;
        characterAnimator.SetBool("is_handwave_1", useHandwave1);
        characterAnimator.SetBool("is_handwave_2", !useHandwave1);

        // Turn off after short delay
        Invoke(nameof(StopHandwave), 1f);
    }

    private void StopHandwave()
    {
        characterAnimator.SetBool("is_handwave_1", false);
        characterAnimator.SetBool("is_handwave_2", false);
    }

    // Thumbs Up Logic
    private void StartThumbsUpLoop()
    {
        thumbsUpTimer = thumbsUpCooldown; // force immediate first thumbs up
    }

    private void HandleThumbsUp()
    {
        if (thumbsUpTimer >= thumbsUpCooldown && !isThumbsUpPlaying)
        {
            PlayRandomThumbsUp();
            thumbsUpTimer = 0f;
        }
    }

    private void PlayRandomThumbsUp()
    {
        isThumbsUpPlaying = true;
        
        bool useThumbsUp1 = Random.value > 0.5f;
        characterAnimator.SetBool("is_character_thumbsup_1", useThumbsUp1);
        characterAnimator.SetBool("is_character_thumbsup_2", !useThumbsUp1);

        // Turn off after short delay
        Invoke(nameof(StopThumbsUp), 1.2f);
    }

    private void StopThumbsUp()
    {
        characterAnimator.SetBool("is_character_thumbsup_1", false);
        characterAnimator.SetBool("is_character_thumbsup_2", false);
        isThumbsUpPlaying = false;
    }
}
