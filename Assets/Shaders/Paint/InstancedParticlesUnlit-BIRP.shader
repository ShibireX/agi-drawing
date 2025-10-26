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
                float3 vertex    : POSITION;
                float2 uv        : TEXCOORD0;
                uint   instanceID: SV_InstanceID;
            };

            struct v2f {
                float4 posCS : SV_POSITION;
                float4 col   : COLOR;
                float2 uv    : TEXCOORD0;
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

                // pass uv
                o.uv = v.uv;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Distance from quad center in UV space.
                float2 centered = i.uv - float2(0.5, 0.5);
                float dist = length(centered) * 2.0;

                float alphaMask = saturate(1.0 - dist);

                clip(alphaMask - 0.01);

                float4 rawColor = i.col;
                float4 blendColor = float4(0.85, 0.85, 0.85, 1.0);
                float mixFactor = 0.3;
                float4 baseCol = rawColor * (1.0f - mixFactor) + blendColor * mixFactor;

                // Apply spherical alpha
                baseCol.a *= alphaMask;

                return baseCol;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
