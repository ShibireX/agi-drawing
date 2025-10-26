using UnityEngine;

public class CharacterManager : MonoBehaviour
{
    [SerializeField] private Animator characterAnimator;

    private bool isGameStarted = false;
    private bool isGameEnded = false;

    public float handwaveCooldown = 5f;
    public float thumbsUpCooldown = 5f;

    private float handwaveTimer = 0f;
    private float thumbsUpTimer = 0f;

    public float countdownDuration = 4f;
    private float countdownTimer = 0f;

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

        if (!isGameStarted && !isGameEnded)
        {
            HandleIdleHandwave();
        }
        else if (isGameStarted && !isGameEnded)
        {
            HandleThumbsUp();
        }

        if (characterAnimator.GetBool("is_character_counting"))
        {
            countdownTimer += Time.deltaTime;
            if (countdownTimer >= countdownDuration)
            {
                characterAnimator.SetBool("is_character_counting", false);
                StartThumbsUpLoop();
            }
        }

        handwaveTimer += Time.deltaTime;
        thumbsUpTimer += Time.deltaTime;
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
        characterAnimator.SetBool("is_character_counting", true);
    }

    private void OnGameEnded()
    {
        isGameEnded = true;
        characterAnimator.SetBool("is_game_ended", true);
        characterAnimator.SetBool("is_character_gameover", true);

        StopThumbsUp();
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
        if (thumbsUpTimer >= thumbsUpCooldown)
        {
            PlayRandomThumbsUp();
            thumbsUpTimer = 0f;
        }
    }

    private void PlayRandomThumbsUp()
    {
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
    }
}
