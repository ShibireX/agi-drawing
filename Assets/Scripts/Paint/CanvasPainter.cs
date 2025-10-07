using UnityEngine;

namespace Paint
{
    /// <summary>
    /// Manages dynamic texture painting on the canvas when particles collide with it.
    /// Receives collision data from the particle simulation and paints to a RenderTexture.
    /// </summary>
    public class CanvasPainter : MonoBehaviour
    {
        [Header("Canvas Setup")]
        [Tooltip("The canvas GameObject with a MeshRenderer")]
        public GameObject canvasObject;
        
        [Tooltip("Resolution of the paint texture (higher = more detail)")]
        public Vector2Int textureResolution = new Vector2Int(2048, 2048);
        
        [Tooltip("Base color of the canvas (unpainted)")]
        public Color canvasBaseColor = Color.white;
        
        [Header("Paint Settings")]
        [Tooltip("Radius of each paint splat in UV space (0-1)")]
        [Range(0.001f, 0.1f)]
        public float paintRadius = 0.01f;
        
        [Tooltip("How much the paint color blends with existing paint (0=no blend, 1=full blend)")]
        [Range(0f, 1f)]
        public float blendAmount = 0.3f;
        
        [Header("Orientation")]
        [Tooltip("Flip U (horizontal) if paint appears mirrored horizontally")]
        public bool flipU = false;
        [Tooltip("Flip V (vertical) if paint appears upside down")]
        public bool flipV = false;
        
        [Header("Compute Shader")]
        [Tooltip("Compute shader for painting to the canvas")]
        public ComputeShader canvasPaintCS;
        
        // Canvas dimensions (not used in current implementation, kept for shader compatibility)
        [HideInInspector]
        public Vector3 canvasDimensions = new Vector3(1.0f, 1.0f, 1.0f);
        
        // Runtime textures
        private RenderTexture paintTexture;
        private Material canvasMaterial;
        
        // Compute shader kernel
        private int paintKernel;
        
        // Collision buffer shared with particle system
        private ComputeBuffer collisionBuffer;
        private int maxCollisionsPerFrame = 1000;
        
        // Collision data structure (must match compute shader and PaintSystem)
        public struct CollisionData
        {
            public Vector3 position;  // world space position
            public Vector3 color;     // RGB color
        }
        
        void Start()
        {
            // If canvasObject is not assigned, try to find it
            if (canvasObject == null)
            {
                // Try to find a child named "Canvas"
                Transform canvasChild = transform.Find("Canvas");
                if (canvasChild != null)
                {
                    canvasObject = canvasChild.gameObject;
                }
                else
                {
                    // Use this GameObject if no child found
                    canvasObject = gameObject;
                }
            }
            
            InitializeCanvas();
            InitializeComputeShader();
        }
        
        void InitializeCanvas()
        {
            // Create the paint texture with Metal-compatible settings
            paintTexture = new RenderTexture(textureResolution.x, textureResolution.y, 0, RenderTextureFormat.ARGB32);
            paintTexture.enableRandomWrite = true;
            paintTexture.useMipMap = false;  // Disable mipmaps for compute shader writes
            paintTexture.autoGenerateMips = false;
            paintTexture.filterMode = FilterMode.Bilinear;
            paintTexture.wrapMode = TextureWrapMode.Clamp;
            
            // Explicitly create the texture
            paintTexture.Create();
            
            // Clear to base color
            RenderTexture.active = paintTexture;
            GL.Clear(true, true, canvasBaseColor);
            RenderTexture.active = null;
            
            // Get or create material for the canvas
            if (canvasObject != null)
            {
                var renderer = canvasObject.GetComponent<MeshRenderer>();
                
                // If no renderer on this object, try to find in children
                if (renderer == null)
                {
                    renderer = canvasObject.GetComponentInChildren<MeshRenderer>();
                }
                
                if (renderer != null)
                {
                    // Create a new material instance
                    canvasMaterial = new Material(renderer.sharedMaterial);
                    canvasMaterial.mainTexture = paintTexture;
                    renderer.material = canvasMaterial;
                }
            }
        }
        
        void InitializeComputeShader()
        {
            if (canvasPaintCS == null) return;
            
            paintKernel = canvasPaintCS.FindKernel("PaintToCanvas");
            
            // Allocate collision buffer
            collisionBuffer = new ComputeBuffer(maxCollisionsPerFrame, sizeof(float) * 6);
            
            // Set static parameters
            canvasPaintCS.SetTexture(paintKernel, "_CanvasTexture", paintTexture);
            canvasPaintCS.SetBuffer(paintKernel, "_Collisions", collisionBuffer);
            canvasPaintCS.SetInts("_TextureResolution", textureResolution.x, textureResolution.y);
        }
        
        /// <summary>
        /// Process collision data and paint to the canvas.
        /// Called by PaintSystem after particle simulation.
        /// </summary>
        public void ProcessCollisions(CollisionData[] collisions, int count)
        {
            if (canvasPaintCS == null || collisionBuffer == null || count <= 0) return;
            
            int collisionCount = Mathf.Min(count, maxCollisionsPerFrame);
            
            // SIMPLIFIED APPROACH: Map world coordinates directly to UV
            // The collision zone in world space is: X=[-1.75, 1.75], Y=[-1, 1], Z=[-0.05, 0.05]
            // We'll map X to U and Y to V directly
            CollisionData[] localCollisions = new CollisionData[collisionCount];
            for (int i = 0; i < collisionCount; i++)
            {
                // Store world position - we'll do the UV mapping in the C# code
                Vector3 worldPos = collisions[i].position;
                
                // Map world coordinates to full UV range [0, 1]
                // Canvas collision zone: X [-1.75, 1.75], Y [-1, 1], Z [-0.05, 0.05]
                // X = horizontal, Y = vertical, Z = thickness
                // U = vertical on canvas (maps world Y)
                // V = horizontal on canvas (maps world X) - inverted
                
                // Remap world Y [-1, 1] to U [0, 1] (VERTICAL)
                float u = (worldPos.y + 1.0f) / 2.0f; // [-1,1] → [0,1]
                
                // Remap world X [-1.75, 1.75] to V [0, 1] (HORIZONTAL) - INVERTED!
                float normalizedX = (worldPos.x + 1.75f) / 3.5f; // [-1.75,1.75] → [0,1]
                float v = 1.0f - normalizedX; // [0,1] → [1,0] (flipped)
                
                // Store UV coordinates in the position field (shader will use these directly as UV)
                localCollisions[i].position = new Vector3(u, v, 0);
                localCollisions[i].color = collisions[i].color;
            }
            
            // Upload collision data to GPU (now in canvas local space)
            collisionBuffer.SetData(localCollisions, 0, 0, collisionCount);
            
            // RE-BIND texture every frame to ensure it's connected (some Unity versions need this)
            canvasPaintCS.SetTexture(paintKernel, "_CanvasTexture", paintTexture);
            
            // Update dynamic parameters
            canvasPaintCS.SetFloat("_PaintRadius", paintRadius);
            canvasPaintCS.SetFloat("_BlendAmount", blendAmount);
            canvasPaintCS.SetVector("_CanvasDimensions", canvasDimensions);
            canvasPaintCS.SetInt("_CollisionCount", collisionCount);
            canvasPaintCS.SetInt("_FlipU", flipU ? 1 : 0);
            canvasPaintCS.SetInt("_FlipV", flipV ? 1 : 0);
            
            // Dispatch compute shader (one thread per collision)
            int threadGroups = Mathf.CeilToInt(collisionCount / 8.0f);
            canvasPaintCS.Dispatch(paintKernel, threadGroups, 1, 1);
        }
        
        /// <summary>
        /// Get the collision buffer for the particle system to write to.
        /// </summary>
        public ComputeBuffer GetCollisionBuffer()
        {
            return collisionBuffer;
        }
        
        /// <summary>
        /// Get the maximum number of collisions that can be processed per frame.
        /// </summary>
        public int GetMaxCollisionsPerFrame()
        {
            return maxCollisionsPerFrame;
        }
        
        /// <summary>
        /// Clear the canvas back to the base color.
        /// </summary>
        public void ClearCanvas()
        {
            if (paintTexture != null)
            {
                RenderTexture.active = paintTexture;
                GL.Clear(true, true, canvasBaseColor);
                RenderTexture.active = null;
            }
        }
        
        void OnDestroy()
        {
            if (paintTexture != null)
            {
                paintTexture.Release();
                Destroy(paintTexture);
            }
            
            if (collisionBuffer != null)
            {
                collisionBuffer.Release();
                collisionBuffer = null;
            }
            
            if (canvasMaterial != null)
            {
                Destroy(canvasMaterial);
            }
        }
        
        void OnValidate()
        {
            // Update compute shader parameters in editor
            if (Application.isPlaying && canvasPaintCS != null && collisionBuffer != null)
            {
                canvasPaintCS.SetFloat("_PaintRadius", paintRadius);
                canvasPaintCS.SetFloat("_BlendAmount", blendAmount);
            }
        }
    }
}

