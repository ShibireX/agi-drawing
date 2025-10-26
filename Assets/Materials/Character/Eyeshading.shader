Shader "Custom/EyeShading"
{
    Properties{
        [Header(Color)]
        _Color("Base color", Color) = (1, 0, 1, 1)
        _MainTex("Main Diffuse Texture", 2D) = "white" {}
        _AmbientColor("Ambient color", Color) = (0.1, 0, 0.1, 1)
        [Header(Transparency)]
        _Alpha("Alpha", Range(0.0, 1.0)) = 1.0
        [Header(Outline)]
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineThickness("Outline Thickness", Range(0.0,0.05)) = 0.02
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

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
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalDir : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _AmbientColor;
            half _Alpha;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Sample main diffuse texture
                fixed3 texColor = tex2D(_MainTex, i.uv).rgb;
                fixed3 albedo = texColor * _Color.rgb;
                
                // Matte lighting calculation (no specular)
                fixed3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                fixed3 lightColor = _LightColor0.rgb;
                fixed3 normal = normalize(i.normalDir);
                float n_dot_L = saturate(dot(lightDir, normal));
                
                fixed3 diffuse = albedo * lightColor * n_dot_L;
                fixed3 ambient = _AmbientColor.rgb * albedo;
                fixed3 finalColor = diffuse + ambient;
                
                return fixed4(finalColor, _Alpha);
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
