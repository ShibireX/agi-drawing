using System.Collections.Generic;
using UnityEngine;

namespace Paint
{
    [RequireComponent(typeof(Particles))]
    public sealed class PaintSystem : MonoBehaviour
    {
        

        [Header("Simulation")]
        public ComputeShader simulationCS;
        public Vector3 boundsExtents = new Vector3(25, 25, 25);
        
        public float gravity = -9.81f;
        public float damping = 0.98f;
        [Range(0f, 1f)] public float airResistance = 0.3f;  // slows particles over time for smoother arcs
        [Min(1)] public uint threadGroupSizeX = 256;

        public float maxInitialSpeed = 3f;
        [Range(0f, 1f)] public float spreadAngle = 0.3f;  // cone spread in radians (~17 degrees default)
        [Range(0f, 1f)] public float forwardBias = 0.7f;  // how much to bias towards forward (0=follow brush exactly, 1=always forward)
        public Vector3 forwardDirection = new Vector3(0, 0, 1);  // direction towards canvas

        [Header("Spawning")]
        public float spawnRate = 5000f;   // particles per second while Space is held
        public float lifeTime = 3f;       // seconds
        [Tooltip("How long (in seconds) before the end of lifetime that particles start fading out")]
        public float fadeOutDuration = 1f;  // seconds
        public float spawnRadius = 0.1f;  // matches your Particles.defaultRadius
        [Tooltip("Minimum brush movement speed (units/sec) required to spawn particles. Prevents painting during small adjustments.")]
        public float movementThreshold = 0.5f;  // units per second
        [Tooltip("Show debug info about movement speed in console")]
        public bool debugMovement = false;

        ComputeBuffer spawnCountBuffer;    // 1 uint
        float spawnCarry;
        public float currentMovementSpeed;

        [Header("Rendering")]
        public Mesh mesh;            // assign a low-poly sphere
        public Material renderMat;   // material using Unlit/InstancedParticles
        public Color baseColor = Color.white;

        // refs
        Particles particlesOwner;
        ComputeBuffer argsBuffer;
        int kernelIntegrate;
        Bounds drawBounds;

        // Multi-player spawn system
        public struct SpawnRequest
        {
            public Vector3 position;
            public Vector3 direction;
            public Vector3 color;
            public Vector3 prevPosition;
            public Vector3 prevDirection;
        }

        private List<SpawnRequest> spawnRequests = new List<SpawnRequest>();
        
        // Legacy single-player support (for compatibility)
        public Vector3 spawnPosition;
        public Vector3 spawnDirection;
        public Vector3 spawnColor;
        public bool emit;

        private Vector3 prevSpawnPosition;
        private Vector3 prevSpawnDirection;

        // Per-player tracking for movement thresholds
        private Dictionary<int, Vector3> playerPrevPositions = new Dictionary<int, Vector3>();
        private Dictionary<int, Vector3> playerPrevDirections = new Dictionary<int, Vector3>();

        /// <summary>
        /// Request to spawn particles from a specific player/source.
        /// Call this each frame a player wants to paint.
        /// </summary>
        public void RequestSpawn(int playerId, Vector3 position, Vector3 direction, Vector3 color)
        {
            Debug.Log("Spawn requested");
            // Get or initialize previous position/direction for this player
            if (!playerPrevPositions.ContainsKey(playerId))
            {
                playerPrevPositions[playerId] = position;
                playerPrevDirections[playerId] = direction;
            }

            // Calculate movement speed for this player
            float currentMovementSpeed = 0f;
            if (Time.deltaTime > 0)
            {
                float distance = Vector3.Distance(position, playerPrevPositions[playerId]);
                currentMovementSpeed = distance / Time.deltaTime;
            }

            // Only add request if movement exceeds threshold
            if (currentMovementSpeed >= movementThreshold)
            {
                spawnRequests.Add(new SpawnRequest
                {
                    position = position,
                    direction = direction,
                    color = color,
                    prevPosition = playerPrevPositions[playerId],
                    prevDirection = playerPrevDirections[playerId]
                });

                if (debugMovement)
                {
                    Debug.Log($"Player {playerId} - Speed: {currentMovementSpeed:F2} u/s | Threshold: {movementThreshold:F2} | Spawning: true");
                }
            }

            // Update tracking
            playerPrevPositions[playerId] = position;
            playerPrevDirections[playerId] = direction;
        }

        void OnEnable()
        {
            // Check that we support everything
            Debug.Log($"Compute:{SystemInfo.supportsComputeShaders}, Instancing:{SystemInfo.supportsInstancing}, API:{SystemInfo.graphicsDeviceType}");

            // Grab the Particles component
            particlesOwner = GetComponent<Particles>();
            if (particlesOwner == null)
            {
                Debug.LogError("PaintSystem needs a Particles component on the same GameObject.");
                enabled = false;
                return;
            }

            // (Optional) require a mesh via Inspector; comment this out if you�ll assign manually.
            if (mesh == null)
            {
                // Safer: assign in Inspector. Built-in sphere resource is not guaranteed across pipelines.
                Debug.LogWarning("Mesh is null. Assign a sphere mesh in the inspector.");
            }

            if (renderMat != null)
            {
                renderMat.enableInstancing = true;                    // double-ensure
                particlesOwner.Bind(renderMat, "_Particles");         // sets buffer + _ActiveCount
            }

            // Compute setup (only if assigned)
            if (simulationCS != null)
            {
                kernelIntegrate = simulationCS.FindKernel("Integrate");
                simulationCS.SetFloats("_BoundsExtents", boundsExtents.x, boundsExtents.y, boundsExtents.z);
                simulationCS.SetFloat("_Gravity", gravity);
                simulationCS.SetFloat("_Damping", damping);
                particlesOwner.Bind(simulationCS, kernelIntegrate, "_Particles");
            }

            // Allocate args buffer if needed
            if (argsBuffer == null)
                argsBuffer = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);

            // Bind render material if present
            if (renderMat != null && particlesOwner.ParticleBuffer != null)
            {
                renderMat.SetBuffer("_Particles", particlesOwner.ParticleBuffer);
                renderMat.SetColor("_BaseColor", baseColor);
            }

            drawBounds = new Bounds(Vector3.zero, boundsExtents * 2f);

            spawnCountBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Structured);
            simulationCS.SetBuffer(kernelIntegrate, "_SpawnedCount", spawnCountBuffer);

            // set static-ish uniforms once
            simulationCS.SetFloat("_LifeTime", lifeTime);
            simulationCS.SetFloat("_SpawnRadius", spawnRadius);

            // Make sure the Particles buffer is bound to material & compute
            particlesOwner.Bind(simulationCS, kernelIntegrate, "_Particles");
            particlesOwner.Bind(renderMat, "_Particles");

            // Optional: we�ll let compute scan all slots
            particlesOwner.activeCount = particlesOwner.particleCount;
            UpdateArgsBuffer();
        }

        void OnDisable()
        {
            spawnCountBuffer?.Dispose();
            spawnCountBuffer = null;

            argsBuffer?.Dispose();
            argsBuffer = null;
        }

        void Update()
        {
            // Set static uniforms
            simulationCS.SetFloats("_BoundsExtents", boundsExtents.x, boundsExtents.y, boundsExtents.z);
            simulationCS.SetFloat("_Gravity", gravity);
            simulationCS.SetFloat("_Damping", damping);
            simulationCS.SetFloat("_AirResistance", airResistance);
            simulationCS.SetFloat("_DeltaTime", Time.deltaTime);
            simulationCS.SetFloat("_MaxInitialSpeed", maxInitialSpeed);
            simulationCS.SetFloat("_LifeTime", lifeTime);
            simulationCS.SetFloat("_FadeOutDuration", fadeOutDuration);
            simulationCS.SetFloat("_SpawnRadius", spawnRadius);
            simulationCS.SetFloat("_SpreadAngle", spreadAngle);
            simulationCS.SetFloat("_ForwardBias", forwardBias);
            simulationCS.SetVector("_ForwardDirection", forwardDirection.normalized);

            // Ensure bindings
            particlesOwner.Bind(simulationCS, kernelIntegrate, "_Particles");
            particlesOwner.Bind(renderMat, "_Particles");

            // Legacy single-player support (for backward compatibility)
            if (emit)
            {
                // Calculate movement speed
                float movementSpeed = 0f;
                if (Time.deltaTime > 0)
                {
                    float distance = Vector3.Distance(spawnPosition, prevSpawnPosition);
                    movementSpeed = distance / Time.deltaTime;
                }
                currentMovementSpeed = movementSpeed;

                // Check if movement exceeds threshold
                if (movementSpeed >= movementThreshold)
                {
                    spawnRequests.Add(new SpawnRequest
                    {
                        position = spawnPosition,
                        direction = spawnDirection,
                        color = spawnColor,
                        prevPosition = prevSpawnPosition,
                        prevDirection = prevSpawnDirection
                    });

                    if (debugMovement)
                    {
                        Debug.Log($"Legacy - Speed: {movementSpeed:F2} u/s | Threshold: {movementThreshold:F2} | Spawning: true");
                    }
                }

                prevSpawnPosition = spawnPosition;
                prevSpawnDirection = spawnDirection;
            }

            // Process all spawn requests (multi-player)
            int totalBudget = 0;
            if (spawnRequests.Count > 0)
            {
                // Divide spawn budget among all active players
                float budgetPerPlayer = (spawnRate * Time.deltaTime + spawnCarry) / spawnRequests.Count;
                
                foreach (var request in spawnRequests)
                {
                    int budget = Mathf.FloorToInt(budgetPerPlayer);
                    totalBudget += budget;

                    // Set per-request spawn data
                    simulationCS.SetVector("_SpawnPosition", request.position);
                    simulationCS.SetVector("_SpawnDirection", request.direction);
                    simulationCS.SetVector("_SpawnColor", request.color);
                    simulationCS.SetVector("_PrevSpawnPosition", request.prevPosition);
                    simulationCS.SetVector("_PrevSpawnDirection", request.prevDirection);
                    simulationCS.SetInt("_SpawnBudget", budget);
                    simulationCS.SetInt("_EmitEnabled", 1);

                    // Reset GPU counter
                    uint[] zero = { 0u };
                    spawnCountBuffer.SetData(zero);

                    // Dispatch compute for this player
                    uint groups = (uint)((particlesOwner.particleCount + threadGroupSizeX - 1) / threadGroupSizeX);
                    if (groups > 0) simulationCS.Dispatch(kernelIntegrate, (int)groups, 1, 1);
                }

                // Update carry with remainder
                float totalWanted = spawnRate * Time.deltaTime + spawnCarry;
                spawnCarry = totalWanted - totalBudget;
            }
            else
            {
                // No spawn requests - still need to update existing particles (aging, physics)
                simulationCS.SetInt("_EmitEnabled", 0);
                simulationCS.SetInt("_SpawnBudget", 0);
                
                uint[] zero = { 0u };
                spawnCountBuffer.SetData(zero);
                
                uint groups = (uint)((particlesOwner.particleCount + threadGroupSizeX - 1) / threadGroupSizeX);
                if (groups > 0) simulationCS.Dispatch(kernelIntegrate, (int)groups, 1, 1);
            }

            // Clear spawn requests for next frame
            spawnRequests.Clear();

            // Draw all instances; dead ones have radius=0 so they vanish
            UpdateArgsBuffer();
            Graphics.DrawMeshInstancedIndirect(mesh, 0, renderMat, drawBounds, argsBuffer);
        }

        void UpdateArgsBuffer()
        {
            if (particlesOwner == null) return;

            if (argsBuffer == null)
                argsBuffer = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);

            uint indexCount = mesh ? (uint)mesh.GetIndexCount(0) : 0u;
            uint indexStart = mesh ? (uint)mesh.GetIndexStart(0) : 0u;
            uint baseVertex = mesh ? (uint)mesh.GetBaseVertex(0) : 0u;
            uint instanceCnt = (uint)Mathf.Max(0, particlesOwner.activeCount);

            uint[] args = { indexCount, instanceCnt, indexStart, baseVertex, 0u };

            if (args[1] == 0) Debug.LogWarning("Draw args instance count is 0 � nothing will render.");

            //Debug.Log("Number of instances: " + args[1]);
            argsBuffer.SetData(args);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(Vector3.zero, boundsExtents * 2f);
        }
    }
}
