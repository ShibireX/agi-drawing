using UnityEngine;

public class ClearButton : MonoBehaviour
{
    public void ClearPaint()
    {

        // Placeholder until we implement a system so that the CLEAR button will call a method
        // that could for example reset the canvas’ RenderTexture
        //  or wipes whatever paint data structure we'll end up using
        
        // Current implementation: Find all paint spheres and destroy them
        GameObject[] paintSpheres = GameObject.FindGameObjectsWithTag("Paint");

        // Destroy each paint sphere
        foreach (GameObject sphere in paintSpheres)
        {
            Destroy(sphere);
        }

        // Confirmation log
        Debug.Log("[ClearButton] Cleared all paint spheres.");
    }
}
