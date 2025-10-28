using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class PaintSparkleManager : MonoBehaviour
{
    [Header("Sparkle Settings")]
    public float sparkleThreshold = 1.0f;    // Minimum speed to trigger a sparkle
    private float sparkleInterval = 2.0f;     // Seconds between sparkles (for sequence logic)
    public int maxConsecutiveSparkles = 5;   // How many sparkles allowed in one sequence
    public float sparkleCooldown = 0.5f;      // Time (in seconds) where no sparkles can play after one triggers

    private int currentSparkle = 0;           // Which child particle system index to use
    private float lastSparkleTime;            // Time of last sparkle (for sequence)
    private float lastCooldownTime;           // Time of last sparkle (for cooldown lock)
    private Vector3 lastPosition;             // For velocity calculation

    [Header("Paint Settings")]
    public float velociyThreshold = 10.0f; 
    public Paint.PaintSystem paintSystem;
    public Transform brushTip;

    [Header("Audio")]
    [SerializeField] private AudioSource colorChangeAudioSource;
    [SerializeField] private AudioSource flareAudioSource;
    [SerializeField] private AudioSource starsAudioSource;

    private int playerID = 0;
    private ImuUdpLogger imu_receiver;
    private Color lastBrushColor;
    private UI_manager uiManager; 
    private bool playerIDSet = false;

    void Start()
    {
        lastPosition = transform.position;
        lastSparkleTime = Time.time;
        lastCooldownTime = -sparkleCooldown;  // so first sparkle isn't blocked

        // playerID is assigned by IMUReceiver via SetPlayerID when the rig is spawned

        // Find camera tagged as "MainCamera" and get the component from there
        imu_receiver = Camera.main != null ? Camera.main.GetComponent<ImuUdpLogger>() : null;

        if (imu_receiver == null)
        {
            Debug.LogWarning("IMUReceiver not found on MainCamera! Please add the IMUReceiver script to your MainCamera.");
        }

        // Find the UI_manager in the scene
        uiManager = FindObjectOfType<UI_manager>();
        if (uiManager == null)
        {
            Debug.LogWarning("UI_manager not found in scene! Sparkles will not check timer state.");
        }

        // Ensure we have a PaintSystem reference (prefab instances won't keep scene refs)
        if (paintSystem == null)
        {
            paintSystem = FindObjectOfType<Paint.PaintSystem>();
            if (paintSystem == null)
            {
                Debug.LogError("PaintSparkleManager: No PaintSystem found in scene.");
            }
        }

        // Initialize last brush color
        if (imu_receiver != null && playerID < imu_receiver.brushes_colour.Length)
        {
            lastBrushColor = imu_receiver.brushes_colour[playerID];
        }

        // Stop all child particle systems on start to prevent auto-play
        StopAllSparkles();
    }

    void OnEnable()
    {
        // Also stop sparkles when the GameObject is re-enabled (e.g., after screenshot)
        // This prevents "Play On Awake" particles from triggering
        StopAllSparkles();
    }

    void StopAllSparkles()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            ParticleSystem ps = transform.GetChild(i).GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    void Update()
    {
        // Warn if playerID was never set (should be set by IMUReceiver)
        if (!playerIDSet && Time.frameCount % 120 == 0)
        {
            Debug.LogWarning($"PaintSparkleManager on {gameObject.name} has no playerID set! Painting may not work correctly.");
        }

        // Check for brush color change (always active, even when timer is stopped)
        if (imu_receiver != null && playerID < imu_receiver.brushes_colour.Length)
        {
            Color currentBrushColor = imu_receiver.brushes_colour[playerID];
            
            // If color changed, play BasicHit effect once (index 1, after Stars at index 0)
            if (currentBrushColor != lastBrushColor)
            {
                Debug.Log($"Brush color changed from {lastBrushColor} to {currentBrushColor}");
                lastBrushColor = currentBrushColor;
                
                // Play color change sound
                if (colorChangeAudioSource != null && colorChangeAudioSource.clip != null)
                {
                    colorChangeAudioSource.Play();
                }
                
                // Play the second child particle system (BasicHit at index 1)
                if (transform.childCount > 1)
                {
                    ParticleSystem ps = transform.GetChild(2).GetComponent<ParticleSystem>();
                    if (ps != null)
                    {
                        ApplyColorToParticleSystem(ps, currentBrushColor, true);
                        ps.Play();
                        Debug.Log($"Playing {ps.name} effect for color change");
                    }
                }
            }
        }

        // Skip velocity-based sparkles and paint if timer is not running
        if (uiManager != null && !uiManager.isTimerRunning)
        {
            lastPosition = transform.position;
            return;
        }

        // Calculate velocity magnitude
        Vector3 velocity = (transform.position - lastPosition) / Time.deltaTime;
        float currentVel = velocity.magnitude;

        // Check if speed is above threshold
        if (currentVel > sparkleThreshold)
        {
            // SPAWN SPARKLES ----------------------------------------------------------------------
            // Prevent sparkles if still inside cooldown
            if (Time.time - lastCooldownTime < sparkleCooldown)
            {
                lastPosition = transform.position;
                return;
            }

            float timeSinceLast = Time.time - lastSparkleTime;

            if (timeSinceLast < sparkleInterval && currentSparkle < maxConsecutiveSparkles - 1)
            {
                currentSparkle++;
            }
            else
            {
                currentSparkle = 0;
            }

            if (transform.childCount > currentSparkle)
            {
                ParticleSystem ps = transform.GetChild(currentSparkle).GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    // Apply brush color to the sparkle effect
                    if (imu_receiver != null && playerID < imu_receiver.brushes_colour.Length)
                    {
                        Color brushColor = imu_receiver.brushes_colour[playerID];
                        ApplyColorToParticleSystem(ps, brushColor, true);
                    }
                    else
                    {
                        Debug.LogWarning($"Cannot apply color - imu_receiver: {imu_receiver != null}, playerID: {playerID}");
                    }
                    
                    // Play appropriate sound based on effect name
                    string effectName = ps.name.ToLower();
                    if (effectName.Contains("star") && starsAudioSource != null && starsAudioSource.clip != null)
                    {
                        starsAudioSource.Play();
                    }
                    else if ((effectName.Contains("glow") || effectName.Contains("impact") || effectName.Contains("cfxr")) 
                             && flareAudioSource != null && flareAudioSource.clip != null)
                    {
                        // Flare/Impact effect
                        flareAudioSource.Play();
                        
                        // Play surprised utterance on flare
                        if (uiManager != null)
                        {
                            uiManager.PlaySurprisedUtteranceOnFlare();
                        }
                    }
                    
                    ps.Play();
                }
            }

            // Update times
            lastSparkleTime = Time.time;
            lastCooldownTime = Time.time;  // start cooldown
        }

        if (currentVel > velociyThreshold)
        {
            // SPAWN PAINT PARTICLES ------------------------------------------------------------------

            Debug.Log(playerID);
            Debug.Log(imu_receiver.brushes_colour[playerID]);
            FireProjectiles(
                playerId: playerID, // managed locally in Unity
                origin: new Vector3(brushTip.position.x, brushTip.position.y, brushTip.position.z),   
                direction: velocity, 
                tipColor: imu_receiver.brushes_colour[playerID] // gets the colour from the IMUReceiver. Assumes that the playerID is alwasy 0, 1, 2 or 3
            );
        }

        lastPosition = transform.position;
    }

    public void SetPlayerID(int id)
    {
        playerID = id;
        playerIDSet = true;
    }

    void FireProjectiles(int playerId, Vector3 origin, Vector3 direction, Color tipColor)
    {
        if (paintSystem == null)
        {
            Debug.LogWarning("PaintSparkleManager: paintSystem is null; skipping paint spawn.");
            return;
        }

        // Use the new multi-player RequestSpawn API
        paintSystem.RequestSpawn(
            playerId: playerId,
            position: origin,
            direction: direction.normalized,
            color: new Vector3(tipColor.r, tipColor.g, tipColor.b)
        );
    }

    void ApplyColorToParticleSystem(ParticleSystem ps, Color brushColor, bool includeChildren = true)
    {
        if (ps == null) return;
        
        // Only stop at the root level (not for each child individually)
        if (includeChildren)
        {
            // Stop and clear the parent particle system (this stops all children too)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        
        // Set the start color
        var main = ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(brushColor);
        
        // Also update Color over Lifetime if it's enabled
        var colorOverLifetime = ps.colorOverLifetime;
        if (colorOverLifetime.enabled)
        {
            // Preserve the existing alpha gradient but apply our color
            var existingGradient = colorOverLifetime.color;
            if (existingGradient.mode == ParticleSystemGradientMode.Gradient)
            {
                Gradient newGradient = new Gradient();
                Gradient oldGradient = existingGradient.gradient;
                
                // Keep the same alpha keys, but replace colors with brush color
                GradientColorKey[] colorKeys = new GradientColorKey[oldGradient.colorKeys.Length];
                for (int i = 0; i < colorKeys.Length; i++)
                {
                    colorKeys[i] = new GradientColorKey(brushColor, oldGradient.colorKeys[i].time);
                }
                
                newGradient.SetKeys(colorKeys, oldGradient.alphaKeys);
                colorOverLifetime.color = new ParticleSystem.MinMaxGradient(newGradient);
            }
            else
            {
                // If it's a simple color, just set it
                colorOverLifetime.color = new ParticleSystem.MinMaxGradient(brushColor);
            }
        }
        
        // Try to apply color to the material as well (for shaders that use material color)
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null && renderer.sharedMaterial != null)
        {
            // Create a material instance to avoid changing the shared material
            Material mat = renderer.material; // This creates an instance
            
            if (mat.HasProperty("_TintColor"))
            {
                mat.SetColor("_TintColor", brushColor);
            }
            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", brushColor);
            }
        }

        // Recursively apply to all child particle systems (without stopping them)
        if (includeChildren)
        {
            ParticleSystem[] childSystems = ps.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem childPs in childSystems)
            {
                if (childPs != ps) // Don't re-apply to self
                {
                    ApplyColorToParticleSystem(childPs, brushColor, false); // false = don't stop children individually
                }
            }
        }
    }
}
