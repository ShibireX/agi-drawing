using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterManager : MonoBehaviour
{
    [SerializeField]
    private Animator characterAnimator;

    // Game state tracking
    private bool isGameStarted = false;
    private bool isGameEnded = false;

    // Handwave animation control
    private Coroutine handwaveCoroutine;
    private float lastHandwaveTime = 0f;
    private float handwaveCooldown = 3f; // Minimum 3 seconds between handwaves

    // Thumbs up animation control
    private Coroutine thumbsUpCoroutine;
    private float lastThumbsUpTime = 0f;
    private float thumbsUpCooldown = 2f; // Minimum 2 seconds between thumbs up animations

    // Start is called before the first frame update
    void Start()
    {
        if (characterAnimator == null)
        {
            characterAnimator = GetComponent<Animator>();
        }

        // Initialize game state
        ResetGameState();

        // Subscribe to game events
        GameEvents.OnGameStarted += OnGameStarted;
        GameEvents.OnGameEnded += OnGameEnded;
        GameEvents.OnGameReset += OnGameReset;

        StartRandomHandwaveAnimations();
    }

    void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        GameEvents.OnGameStarted -= OnGameStarted;
        GameEvents.OnGameEnded -= OnGameEnded;
        GameEvents.OnGameReset -= OnGameReset;

        // Stop any running coroutines
        if (handwaveCoroutine != null)
        {
            StopCoroutine(handwaveCoroutine);
        }
        if (thumbsUpCoroutine != null)
        {
            StopCoroutine(thumbsUpCoroutine);
        }
    }

    private void OnGameStarted()
    {
        isGameStarted = true;
        isGameEnded = false;
        
        // Stop handwave animations
        StopRandomHandwaveAnimations();
        
        // Set animator parameters
        characterAnimator.SetBool("is_game_started", true);
        characterAnimator.SetBool("is_game_ended", false);

        // Trigger countdown animation
        characterAnimator.SetBool("is_character_counting", true);
        StartCoroutine(TurnOffCountdownAnimation());
        
        // Start thumbs up animations after countdown
        StartCoroutine(StartThumbsUpAfterCountdown());
    }

    private void OnGameEnded()
    {
        isGameEnded = true;
        
        // Stop thumbs up animations
        StopRandomThumbsUpAnimations();
        
        // Set animator parameters
        Debug.Log("Game ended is palyed");
        characterAnimator.SetBool("is_game_ended", true);
        characterAnimator.SetBool("is_character_gameover", true);

    }

    private void OnGameReset()
    {
        // Reset everything to starting position and play handwave animations
        ResetGameState();
        
        StartRandomHandwaveAnimations();
    }

    private void ResetGameState()
    {
        isGameStarted = false;
        isGameEnded = false;
        
        // Stop thumbs up animations
        StopRandomThumbsUpAnimations();
        
        // Reset animator parameters
        characterAnimator.SetBool("is_game_started", false);
        characterAnimator.SetBool("is_game_ended", false);
        characterAnimator.SetBool("is_handwave_1", false);
        characterAnimator.SetBool("is_handwave_2", false);
        characterAnimator.SetBool("is_character_counting", false);
        characterAnimator.SetBool("is_character_gameover", false);
        characterAnimator.SetBool("is_character_thumbsup_1", false);
        characterAnimator.SetBool("is_character_thumbsup_2", false);
    }

    private void StartRandomHandwaveAnimations()
    {
        if (handwaveCoroutine != null)
        {
            StopCoroutine(handwaveCoroutine);
        }
        handwaveCoroutine = StartCoroutine(RandomHandwaveLoop());
    }

    private void StopRandomHandwaveAnimations()
    {
        if (handwaveCoroutine != null)
        {
            StopCoroutine(handwaveCoroutine);
            handwaveCoroutine = null;
        }
        
        // Ensure handwave animations are stopped
        characterAnimator.SetBool("is_handwave_1", false);
        characterAnimator.SetBool("is_handwave_2", false);
    }

    private IEnumerator RandomHandwaveLoop()
    {
        while (!isGameStarted)
        {
            // Check if enough time has passed since last handwave
            float timeSinceLastHandwave = Time.time - lastHandwaveTime;
            
            if (timeSinceLastHandwave >= handwaveCooldown)
            {
                // Wait for a random interval between handwaves (2-5 seconds)
                float waitTime = Random.Range(2f, 5f);
                yield return new WaitForSeconds(waitTime);

                if (!isGameStarted && (Time.time - lastHandwaveTime) >= handwaveCooldown)
                {
                    PlayRandomHandwave();
                    lastHandwaveTime = Time.time; // Update the last handwave time
                    
                    yield return new WaitForSeconds(Random.Range(1f, 2f));
                }
            }
            else
            {
                // Wait a bit before checking again
                yield return new WaitForSeconds(0.5f);
            }
        }
    }

    private void PlayRandomHandwave()
    {
        // Choose random handwave animation
        bool useHandwave1 = Random.Range(0, 2) == 0;
        
        if (useHandwave1)
        {
            characterAnimator.SetBool("is_handwave_1", true);
            characterAnimator.SetBool("is_handwave_2", false);
        }
        else
        {
            characterAnimator.SetBool("is_handwave_1", false);
            characterAnimator.SetBool("is_handwave_2", true);
        }
    }

    private void PlayRandomThumbsUp()
    {
        // Choose random thumbs up animation
        bool useThumbsUp1 = Random.Range(0, 2) == 0;
        
        if (useThumbsUp1)
        {
            characterAnimator.SetBool("is_character_thumbsup_1", true);
            characterAnimator.SetBool("is_character_thumbsup_2", false);
        }
        else
        {
            characterAnimator.SetBool("is_character_thumbsup_1", false);
            characterAnimator.SetBool("is_character_thumbsup_2", true);
        }
    }

    private void StartRandomThumbsUpAnimations()
    {
        if (thumbsUpCoroutine != null)
        {
            StopCoroutine(thumbsUpCoroutine);
        }
        thumbsUpCoroutine = StartCoroutine(RandomThumbsUpLoop());
    }

    private void StopRandomThumbsUpAnimations()
    {
        if (thumbsUpCoroutine != null)
        {
            StopCoroutine(thumbsUpCoroutine);
            thumbsUpCoroutine = null;
        }
        
        // Ensure thumbs up animations are stopped
        characterAnimator.SetBool("is_character_thumbsup_1", false);
        characterAnimator.SetBool("is_character_thumbsup_2", false);
    }

    private IEnumerator RandomThumbsUpLoop()
    {
        while (isGameStarted && !isGameEnded)
        {
            // Check if enough time has passed since last thumbs up
            float timeSinceLastThumbsUp = Time.time - lastThumbsUpTime;
            
            if (timeSinceLastThumbsUp >= thumbsUpCooldown)
            {
                // Wait for a random interval between thumbs up animations (1-3 seconds)
                float waitTime = Random.Range(8f, 10f);
                yield return new WaitForSeconds(waitTime);

                if (isGameStarted && !isGameEnded && (Time.time - lastThumbsUpTime) >= thumbsUpCooldown)
                {
                    PlayRandomThumbsUp();
                    lastThumbsUpTime = Time.time; // Update the last thumbs up time
                    
                    yield return new WaitForSeconds(Random.Range(7.5f, 11.5f));
                }
            }
            else
            {
                // Wait a bit before checking again
                yield return new WaitForSeconds(0.5f);
            }
        }
    }

    private IEnumerator TurnOffCountdownAnimation()
    {
        // Wait for the countdown animation to complete (4 seconds for "3, 2, 1, Go!")
        yield return new WaitForSeconds(4f);
        
        // Turn off the countdown animation
        characterAnimator.SetBool("is_character_counting", false);
    }

    private IEnumerator StartThumbsUpAfterCountdown()
    {
        // Wait for countdown to finish (4 seconds)
        yield return new WaitForSeconds(4f);
        
        // Start thumbs up animations
        StartRandomThumbsUpAnimations();
    }


}
