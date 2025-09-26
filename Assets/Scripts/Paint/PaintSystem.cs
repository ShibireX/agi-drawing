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
        [Min(1)] public uint threadGroupSizeX = 256;

        public float maxInitialSpeed = 3f;

        [Header("Spawning")]
        public float spawnRate = 5000f;   // particles per second while Space is held
        public float lifeTime = 3f;       // seconds
        public float spawnRadius = 0.1f;  // matches your Particles.defaultRadius

        ComputeBuffer spawnCountBuffer;    // 1 uint
        float spawnCarry;

        [Header("Rendering")]
        public Mesh mesh;            // assign a low-poly sphere
        public Material renderMat;   // material using Unlit/InstancedParticles
        public Color baseColor = Color.white;

        // refs
        Particles particlesOwner;
        ComputeBuffer argsBuffer;
        int kernelIntegrate;
        Bounds drawBounds;

        public Vector3 spawnPosition;
        public Vector3 spawnDirection;
        public Vector3 spawnColor;
        public bool emit;

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
            // keep uniforms fresh
            simulationCS.SetFloats("_BoundsExtents", boundsExtents.x, boundsExtents.y, boundsExtents.z);
            simulationCS.SetFloat("_Gravity", gravity);
            simulationCS.SetFloat("_Damping", damping);
            simulationCS.SetFloat("_DeltaTime", Time.deltaTime);
            simulationCS.SetFloat("_MaxInitialSpeed", maxInitialSpeed);
            simulationCS.SetFloat("_LifeTime", lifeTime);
            simulationCS.SetFloat("_SpawnRadius", spawnRadius);

            // emit control
            //bool emit = Input.GetKey(KeyCode.Space);
            simulationCS.SetInt("_EmitEnabled", emit ? 1 : 0);

            // per-frame spawn budget (particles this frame)
            int budget = 0;
            if (emit)
            {
                float want = spawnRate * Time.deltaTime + spawnCarry;
                budget = Mathf.FloorToInt(want);
                spawnCarry = want - budget;
            }
            else
            {
                // optional: keep carry so tapping space still exacts average rate
                // spawnCarry = 0f;
            }
            simulationCS.SetInt("_SpawnBudget", budget);

            simulationCS.SetVector("_SpawnPosition", spawnPosition);
            simulationCS.SetVector("_SpawnDirection", spawnDirection);
            simulationCS.SetVector("_SpawnColor", spawnColor);

            // reset GPU counter to 0 each frame
            uint[] zero = { 0u };
            spawnCountBuffer.SetData(zero);

            // ensure bindings (cheap)
            particlesOwner.Bind(simulationCS, kernelIntegrate, "_Particles");
            particlesOwner.Bind(renderMat, "_Particles");

            // compute & draw over full buffer so spawns can fill any dead slot
            uint groups = (uint)((particlesOwner.particleCount + threadGroupSizeX - 1) / threadGroupSizeX);
            if (groups > 0) simulationCS.Dispatch(kernelIntegrate, (int)groups, 1, 1);

            // Draw all instances; dead ones have radius=0 so they vanish
            UpdateArgsBuffer(); // set instance count = particleCount (or activeCount if you keep them equal)
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
