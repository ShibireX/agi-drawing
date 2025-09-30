Shader "Unlit/DisplayDye"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _PigmentMode("Pigment Mode (0=RGB,1=τ->RGB)", Float) = 1
        _Density("Pigment Density", Range(0,4)) = 1.2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Background" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            sampler2D _MainTex;
            float _PigmentMode;
            float _Density;

            struct v2f { float4 pos:SV_Position; float2 uv:TEXCOORD0; };

            v2f vert(uint id:SV_VertexID)
            {
                float2 p = float2((id<<1)&2, id&2);
                v2f o; o.pos = float4(p*2-1, 0, 1); o.uv = p; return o;
            }

            float3 tau_to_rgb(float3 tau, float density)
            {
                // Beer–Lambert: transmission = exp(-density * τ)
                return exp(-density * tau);
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 t = tex2D(_MainTex, i.uv);
                if (_PigmentMode >= 0.5)
                {
                    float3 rgb = tau_to_rgb(t.rgb, max(_Density, 1e-4));
                    return float4(rgb, 1);
                }
                else
                {
                    return t; // plain RGB dye
                }
            }
            ENDHLSL
        }
    }
}
