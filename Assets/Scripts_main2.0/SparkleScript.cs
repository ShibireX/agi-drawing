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

    void Start()
    {
        lastPosition = transform.position;
        lastSparkleTime = Time.time;
        lastCooldownTime = -sparkleCooldown;  // so first sparkle isn't blocked
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
            Debug.Log("current sparkle: " + currentSparkle);
            if (transform.childCount > currentSparkle)
            {
                ParticleSystem ps = transform.GetChild(currentSparkle).GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    ps.Play();
                }
            }

            // Update times
            lastSparkleTime = Time.time;
            lastCooldownTime = Time.time;  // start cooldown
        }

        lastPosition = transform.position;
    }
}
