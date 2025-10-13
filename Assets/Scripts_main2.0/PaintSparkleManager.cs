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

    private int playerID = 0;
    private ImuUdpLogger imu_receiver; 

    void Start()
    {
        lastPosition = transform.position;
        lastSparkleTime = Time.time;
        lastCooldownTime = -sparkleCooldown;  // so first sparkle isn't blocked

        // count how many objects with tag "Brush"
        // playerID is equal to the quantity of those objects - 1
        GameObject[] brushes = GameObject.FindGameObjectsWithTag("Brush");
        playerID = brushes.Length - 1;

        // Find camera tagged as "MainCamera" and get the component from there
        imu_receiver = Camera.main != null ? Camera.main.GetComponent<ImuUdpLogger>() : null;

        if (imu_receiver == null)
        {
            Debug.LogWarning("IMUReceiver not found on MainCamera! Please add the IMUReceiver script to your MainCamera.");
        }
    }

    void Update()
    {
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

    void FireProjectiles(int playerId, Vector3 origin, Vector3 direction, Color tipColor)
    {
        if (paintSystem == null) return;

        // Use the new multi-player RequestSpawn API
        paintSystem.RequestSpawn(
            playerId: playerId,
            position: origin,
            direction: direction.normalized,
            color: new Vector3(tipColor.r, tipColor.g, tipColor.b)
        );
    }
}
