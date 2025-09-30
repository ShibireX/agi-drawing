Shader "Unlit/InstancedParticles"
{
    Properties
    { 
        _BaseColor("Base Color (multiplier)", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

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
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f {
                float4 posCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float4 col : TEXCOORD1;
            };

            v2f vert(appdata v, uint instanceID : SV_InstanceID)
            {
                v2f o;

                Particle p = _Particles[instanceID];
                float3 pos = p.position.xyz;
                float radius = p.position.w;

                // scale sphere by radius in object space
                float3 obj = v.vertex.xyz * radius;

                // object->world then translate by per-instance pos
                float4 world = mul(unity_ObjectToWorld, float4(obj, 1));
                world.xyz += pos;

                // to clip
                o.posCS = mul(UNITY_MATRIX_VP, world);

                // normal to world (approx; ignores non-uniform scale on the GameObject)
                float3 nWS = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                o.normalWS = nWS;

                o.col = p.color * _BaseColor;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // simple view-lighting for shape
                float3 L = normalize(_WorldSpaceCameraPos - float3(0,0,0));
                float ndl = saturate(dot(normalize(i.normalWS), L)) * 0.7 + 0.3;
                return float4(i.col.rgb * ndl, i.col.a);
            }
            ENDHLSL
        }
    }
}
