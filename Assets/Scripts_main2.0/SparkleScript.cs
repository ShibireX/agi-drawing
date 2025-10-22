using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SparkleScript : MonoBehaviour
{
    [Header("Sparkle Settings")]
    public float velocityThreshold = 1.0f;    // Minimum speed to trigger a sparkle
    private float sparkleInterval = 2.0f;     // Seconds between sparkles (for sequence logic)
    public int maxConsecutiveSparkles = 5;   // How many sparkles allowed in one sequence
    public float sparkleCooldown = 0.5f;      // Time (in seconds) where no sparkles can play after one triggers

    private int currentSparkle = 0;           // Which child particle system index to use
    private float lastSparkleTime;            // Time of last sparkle (for sequence)
    private float lastCooldownTime;           // Time of last sparkle (for cooldown lock)
    private Vector3 lastPosition;             // For velocity calculation
    
    private int playerID = 0;
    private ImuUdpLogger imu_receiver;

    void Start()
    {
        lastPosition = transform.position;
        lastSparkleTime = Time.time;
        lastCooldownTime = -sparkleCooldown;  // so first sparkle isn't blocked
        
        // count how many objects with tag "Brush"
        GameObject[] brushes = GameObject.FindGameObjectsWithTag("Brush");
        playerID = brushes.Length - 1;
        
        // Find camera tagged as "MainCamera" and get the component from there
        imu_receiver = Camera.main != null ? Camera.main.GetComponent<ImuUdpLogger>() : null;
        
        if (imu_receiver == null)
        {
            Debug.LogWarning("SparkleScript: IMUReceiver not found on MainCamera!");
        }
    }

    void Update()
    {
        // Calculate velocity magnitude
        Vector3 velocity = (transform.position - lastPosition) / Time.deltaTime;
        float currentVel = velocity.magnitude;

        // Check if speed is above threshold
        if (currentVel > velocityThreshold)
        {
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

            // Play particle system at child index = currentSparkle
            //Debug.Log("current sparkle: " + currentSparkle);
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
                    
                    ps.Play();
                }
            }

            // Update times
            lastSparkleTime = Time.time;
            lastCooldownTime = Time.time;  // start cooldown
        }

        lastPosition = transform.position;
    }

    void ApplyColorToParticleSystem(ParticleSystem ps, Color brushColor, bool includeChildren = true)
    {
        if (ps == null) return;

        Debug.Log($"SparkleScript: Applying color {brushColor} to {ps.name}");
        
        // Stop and clear any existing particles
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        
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
                Debug.Log($"  └─ Updated Color over Lifetime gradient");
            }
            else
            {
                colorOverLifetime.color = new ParticleSystem.MinMaxGradient(brushColor);
            }
        }
        
        // Try to apply color to the material as well
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null && renderer.sharedMaterial != null)
        {
            Material mat = renderer.material; // Creates an instance
            
            if (mat.HasProperty("_TintColor"))
            {
                mat.SetColor("_TintColor", brushColor);
                Debug.Log($"  └─ Set _TintColor");
            }
            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", brushColor);
                Debug.Log($"  └─ Set _Color");
            }
        }

        // Recursively apply to all child particle systems
        if (includeChildren)
        {
            ParticleSystem[] childSystems = ps.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem childPs in childSystems)
            {
                if (childPs != ps) // Don't re-apply to self
                {
                    Debug.Log($"  └─ Also applying to child: {childPs.name}");
                    ApplyColorToParticleSystem(childPs, brushColor, false); // Don't recurse infinitely
                }
            }
        }
    }
}
