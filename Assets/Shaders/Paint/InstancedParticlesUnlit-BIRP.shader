Shader "Unlit/InstancedParticlesUnlit-BIRP"
{
    Properties
    {
        _BaseColor("Base Color (multiplier)", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct Particle {
                float4 position; // xyz, w=radius
                float4 velocity;
                float4 color;    // rgb, w=alpha
            };

            StructuredBuffer<Particle> _Particles;
            float4 _BaseColor;

            struct appdata {
                float3 vertex : POSITION;
                uint   instanceID : SV_InstanceID;
            };

            struct v2f {
                float4 posCS : SV_POSITION;
                float4 col   : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;

                Particle p = _Particles[v.instanceID];
                float  r   = p.position.w;

                // scale mesh in object space
                float3 objPosOS = v.vertex * r;

                // object -> world, then add per-instance translation
                float3 worldPos = mul(unity_ObjectToWorld, float4(objPosOS, 1)).xyz + p.position.xyz;

                // world -> clip
                o.posCS = mul(UNITY_MATRIX_VP, float4(worldPos, 1));
                o.col   = p.color * _BaseColor;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return i.col; // raw color, no lighting
            }
            ENDHLSL
        }
    }
    Fallback Off
}
