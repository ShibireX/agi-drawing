using UnityEngine;

namespace Paint
{
    [DisallowMultipleComponent]
    public sealed class Particles : MonoBehaviour
    {
        [Header("Capacity")]
        [Min(1)] public int particleCount = 100_000;   // capacity (fixed buffer size)
        [Min(0)] public int activeCount = 100_000;     // how many are "alive" right now

        /// <summary>Structured buffer: float4 position, float4 velocity, float4 color.</summary>
        public ComputeBuffer ParticleBuffer { get; private set; }

        // 3 * float4 = 12 floats * 4 bytes
        public const int ParticleStride = 12 * 4;

        struct ParticleCPU
        {
            public Vector4 position; // xyz, w = radius
            public Vector4 velocity; // xyz, w = spare
            public Vector4 color;    // rgb, w = alpha
        }

        [Header("Defaults")]
        public float defaultRadius = 0.1f;
        public Vector3 initialVelocityRange = new Vector3(1, 1, 1);
        public Color initialColor = Color.white;

        void OnEnable()
        {
            Allocate();
            UploadInitialData();
            ClampActiveCount();
        }

        void OnDisable() => Release();

        void OnValidate()
        {
            // Keep values sane when edited in Inspector
            particleCount = Mathf.Max(1, particleCount);
            activeCount = Mathf.Clamp(activeCount, 0, particleCount);
        }

        void Allocate()
        {
            Release(); // handle domain reload/hot swapping
            ParticleBuffer = new ComputeBuffer(particleCount, ParticleStride, ComputeBufferType.Structured);
        }

        void UploadInitialData()
        {
            var data = new ParticleCPU[particleCount];
            for (int i = 0; i < data.Length; i++)
            {
                data[i].position = new Vector4(0, 0, 0, defaultRadius);

                // Start dead: age = -1; velocity can be 0
                data[i].velocity = new Vector4(0, 0, 0, -1f);

                // visible color as you like
                data[i].color = new Vector4(initialColor.r, initialColor.g, initialColor.b, 1f);
            }
            ParticleBuffer.SetData(data);
        }

        void ClampActiveCount() => activeCount = Mathf.Clamp(activeCount, 0, particleCount);

        public void ResizeIfNeeded(int desiredCount)
        {
            if (desiredCount == particleCount && ParticleBuffer != null) return;
            particleCount = Mathf.Max(1, desiredCount);
            Allocate();
            UploadInitialData();
            ClampActiveCount();
        }

        void Awake()
        {
            if (ParticleBuffer == null)
            {
                Allocate();
                UploadInitialData();
                ClampActiveCount();
            }
        }

        public void EnsureAllocated()
        {
            if (ParticleBuffer == null)
            {
                Allocate();
                UploadInitialData();
                ClampActiveCount();
            }
        }

        public void Bind(ComputeShader cs, int kernel, string bufferName = "_Particles")
        {
            EnsureAllocated();
            cs.SetBuffer(kernel, bufferName, ParticleBuffer);
            cs.SetInt("_ActiveCount", activeCount);
        }

        public void Bind(Material mat, string bufferName = "_Particles")
        {
            EnsureAllocated();
            mat.SetBuffer(bufferName, ParticleBuffer);
            mat.SetInt("_ActiveCount", activeCount);
        }

        void Release()
        {
            if (ParticleBuffer != null)
            {
                ParticleBuffer.Dispose();
                ParticleBuffer = null;
            }
        }
    }
}
