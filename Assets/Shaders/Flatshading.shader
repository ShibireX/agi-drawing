Shader "Custom/FlatShading"
{
    Properties{
        _Color("Base color", Color) = (1, 0, 1, 1)
        _AmbientColor("Ambient color", Color) = (0.1, 0, 0.1, 1)
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineThickness("Outline Thickness", Range(0.0,0.05)) = 0.02
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            Name "FlatShading"
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normalDir : TEXCOORD0;
            };

            fixed4 _Color;
            fixed4 _AmbientColor;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                fixed3 lightColor = _LightColor0.rgb;
                fixed3 normal = normalize(i.normalDir);
                float n_dot_L = saturate(dot(lightDir, normal));
                
                fixed3 diffuse = _Color.rgb * lightColor * n_dot_L;
                fixed3 ambient = _AmbientColor.rgb * _Color.rgb;
                fixed3 finalColor = diffuse + ambient;
                
                return fixed4(finalColor, 1);
            }
            ENDCG
        }

        Pass
        {
            Name "Outline"
            Cull Front   

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };
            
            float _OutlineThickness;
            fixed4 _OutlineColor;
            
            v2f vert(appdata v)
            {
                v2f o;
                float3 norm = normalize(UnityObjectToWorldNormal(v.normal));
                float3 pos = v.vertex.xyz + norm * _OutlineThickness;
                o.pos = UnityObjectToClipPos(float4(pos, 1.0));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }

        
    }
    FallBack "Diffuse"
}
