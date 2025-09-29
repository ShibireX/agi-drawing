using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Creates a simple fullscreen canvas in front of a specified camera.
/// </summary>
public class FullscreenCanvas : MonoBehaviour
{
    // The camera to look at
    [Tooltip("Camera that looks at the canvas. Leave Target Camera empty as it will auto-grab MainCamera upon playing.")]
    public Camera targetCamera;
    // Distance from the camera to place the canvas
    [Tooltip("Distance from the camera to place the canvas.")]
    public float distanceFromCamera = 25f;
    // The quad representing the canvas
    private GameObject canvasQuad;

    /// <summary>
    /// Initializes the canvas in front of the camera.
    /// </summary>
    void Start()
    {
        // If no camera is assigned, try to get the main camera
        if (targetCamera == null) targetCamera = Camera.main; // DO NOT use compound assignment on this
        if (targetCamera == null)
        {
            // No camera found, disable script and throw error
            Debug.LogError("[FullscreenCanvas] No camera assigned and no MainCamera found.");
            enabled = false;
            return;
        }

        CreateCanvas();
    }

    /// <summary>
    /// Creates the quad canvas in front of the camera.
    /// </summary>
    void CreateCanvas()
    {
        // Create the quad
        canvasQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        canvasQuad.name = "CanvasQuad";

        // Place in front of the camera
        float d = Mathf.Max(distanceFromCamera, targetCamera.nearClipPlane + 0.01f);
        Vector3 pos = targetCamera.transform.position + targetCamera.transform.forward * d;
        canvasQuad.transform.position = pos;
        canvasQuad.transform.rotation = Quaternion.LookRotation(targetCamera.transform.forward, targetCamera.transform.up);

        // Scale to cover the camera view
        float height = targetCamera.orthographic
            ? (targetCamera.orthographicSize * 2f)
            : (2f * d * Mathf.Tan(targetCamera.fieldOfView * Mathf.Deg2Rad * 0.5f));
        float width = height * targetCamera.aspect;
        canvasQuad.transform.localScale = new Vector3(width, height, 1f);

        // Make it white and unlit
        var mr = canvasQuad.GetComponent<MeshRenderer>();
        Shader unlitShader = Shader.Find("Unlit/Color");
        Material mat = new Material(unlitShader);
        mat.color = Color.white;
        mr.material = mat;
    }
}
