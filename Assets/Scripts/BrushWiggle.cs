using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//
//jobbar med euler eftersom att vinklarna som används aldrig kommer skapa gimbal locks/quats kan ha lite akward grejer vid övergångar
//jobbar i lokalt eftersom att vi bryr bara om relationen mellan brushen och eulers
public class BrushWiggle : MonoBehaviour
{
    [HideInInspector]
    public float deviceAMag = 0f;

    [HideInInspector]
    public float fireAccelThreshold = 2.0f;
    [SerializeField]
    public float rotationThreshold = 5.0f; // degrees per frame

    public Transform[] bones;
    [SerializeField] public Transform brushroot;
    [Range(0f, 20f)] public float wiggleSpeed = 5f;
    private List<WiggleBone> bonestojiggle;
    [SerializeField]
    public float MaxAngle = 15f;
    //minskar mängden desto mer  
    public float AngleReduction = 5f;
    private Vector3 lastPosition;
    private Vector3 lastBrushRotation;             

    public struct WiggleBone
    {
        public Transform T;
        public Vector3 originalLocalEuler;
        public Vector3 eulerOffset;
    }

    void Start()
    {
        lastPosition = transform.position;
        lastBrushRotation = brushroot.localEulerAngles;

        bonestojiggle = new List<WiggleBone>();
        // statiskt ben så skippar första, antar att den finns i listan. inkludera alla ben 
        for (int i = 1; i < bones.Length; i++)
        {
            WiggleBone WB = new WiggleBone();
            WB.T = bones[i];
            WB.originalLocalEuler = bones[i].localEulerAngles;
            WB.eulerOffset = Vector3.zero;
            bonestojiggle.Add(WB);
        }
    }

    public void ApplyWiggle()
    {
        Vector3 brushEuler = brushroot.localEulerAngles;
        brushEuler = NormalizeEuler(brushEuler);
        
        // Calculate rotation difference from last frame
        Vector3 rotationDelta = brushEuler - lastBrushRotation;
        rotationDelta = NormalizeEuler(rotationDelta);
        float rotationMagnitude = rotationDelta.magnitude;

        for (int i = 0; i < bonestojiggle.Count; i++)
        {
            WiggleBone wb = bonestojiggle[i];
            Vector3 targetOffset = -brushEuler;

            int reverseIndex = (bonestojiggle.Count - 1) - i;
            float positiv_limit = Mathf.Clamp(MaxAngle - AngleReduction * reverseIndex, 0, MaxAngle);
            float negativ_limit = Mathf.Clamp(-MaxAngle + AngleReduction * reverseIndex, -MaxAngle, 0);

            targetOffset.x = Mathf.Clamp(targetOffset.x, negativ_limit, positiv_limit);
            targetOffset.y = 0f; // disable Y wiggle
            targetOffset.z = Mathf.Clamp(targetOffset.z, negativ_limit, positiv_limit);
            
            if (rotationMagnitude > rotationThreshold)
            {
                
                wb.eulerOffset = Vector3.Lerp(
                    wb.eulerOffset,
                    targetOffset,
                    Time.deltaTime * wiggleSpeed
                );

                wb.T.localEulerAngles = wb.originalLocalEuler + wb.eulerOffset;
                bonestojiggle[i] = wb;
            }
            else
            {
                // Smoothly reset to zero offset (no wiggle)
                wb.eulerOffset = Vector3.Lerp(
                    wb.eulerOffset,
                    Vector3.zero,
                    Time.deltaTime * wiggleSpeed
                );
                wb.T.localEulerAngles = wb.originalLocalEuler + wb.eulerOffset;
                bonestojiggle[i] = wb;
            }
        }
        
        // Update last rotation for next frame
        lastBrushRotation = brushEuler;
    }

    void LateUpdate()
    {
        ApplyWiggle();
    }


    //  blir knas med exempelvis -350 så mappar om vinklarna till -180 till 180
    Vector3 NormalizeEuler(Vector3 euler)
    {
        euler.x = (euler.x > 180) ? euler.x - 360 : euler.x;
        euler.y = (euler.y > 180) ? euler.y - 360 : euler.y;
        euler.z = (euler.z > 180) ? euler.z - 360 : euler.z;
        return euler;
    }
}
