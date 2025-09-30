using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//
//jobbar med euler eftersom att vinklarna som används aldrig kommer skapa gimbal locks/quats kan ha lite akward grejer vid övergångar
//jobbar i lokalt eftersom att vi bryr bara om relationen mellan brushen och eulers
public class BrushWiggle : MonoBehaviour
{

    public ImuUdpLogger ImuUdpLogger;
    public Transform[] bones;
    [SerializeField] public Transform brushroot;

    [Range(0f, 20f)] public float wiggleSpeed = 5f;
    private List<WiggleBone> bonestojiggle;
    [SerializeField]
    public float MaxAngle = 15f;
    //minskar mängden desto mer  
    public float AngleReduction = 5f;

    public struct WiggleBone
    {
        public Transform T;
        public Vector3 originalLocalEuler;
        public Vector3 eulerOffset;
    }

    void Start()
    {
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

        for (int i = 0; i < bonestojiggle.Count; i++)
        {
            WiggleBone wb = bonestojiggle[i];

            // tippen ska röra sig i motsatt riktning
            Vector3 targetOffset = -brushEuler;

            // clampa så att man inte får galna värden, ska jsutera lite bättre sen
            float positiv_limit = Mathf.Clamp(MaxAngle - AngleReduction * i, 0, MaxAngle);
            float negativ_limit = Mathf.Clamp(-MaxAngle + AngleReduction * i, -MaxAngle, 0);

            targetOffset.x = Mathf.Clamp(targetOffset.x, negativ_limit, positiv_limit);
            targetOffset.y = Mathf.Clamp(targetOffset.y, 0, 0);
            targetOffset.z = Mathf.Clamp(targetOffset.z, negativ_limit, positiv_limit);

            // lerpa mellan offset och targetOffset
            wb.eulerOffset = Vector3.Lerp(
                wb.eulerOffset,
                targetOffset,
                Time.deltaTime * wiggleSpeed
            );


            wb.T.localEulerAngles = wb.originalLocalEuler + wb.eulerOffset;
            bonestojiggle[i] = wb;
        }  
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
