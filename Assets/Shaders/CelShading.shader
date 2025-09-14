Shader "Custom/CelShading"
{
    Properties{
        [Header(Color)]
        _Color("Base color", Color) = (1, 0, 1, 1)
        _MainTex ("Main tex", 2D) = "white" {}
        [HDR]_AmbientColor("Ambient color", Color) = (0.1, 0, .01, 1)
        [HDR]_SpecularColor("Specular Color", Color) = (0.9, 0.9, 0.9, 1)
        [HDR]_RimColor("Rim color", Color) = (1, 0, 1, 1)
        _Glossiness("Glossiness", Range(0.0, 1.0)) = 0.5
        [Header(Rim settings)]
        _RimAmount("Rim amount", Range(0.0, 1.0)) = 0.5
        [Header(Band cutoffs)]
        _bandCutOff("Band cutoff", Range(0.0, 1.0)) = 0.5
        _bandAmount("Band amount", int) = 4
        [HDR]_Emission("Emission", Color) = (0, 0, 0, 1)
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineThickness("Outline Thickness", Range(0.0,0.05)) = 0.02
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        // Cel Shading Pass, some things changed for visual preference
        Pass
        {
            Name "Celshading"
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
                float3 viewDir : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _AmbientColor;
            fixed4 _SpecularColor;
            fixed4 _RimColor;
            half _Glossiness;
            half _RimAmount;
            half _bandCutOff;
            int _bandAmount;
            fixed4 _Emission;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldPos = worldPos;
                o.viewDir = normalize(_WorldSpaceCameraPos - worldPos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Main directional light
                fixed3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                fixed3 lightColor = _LightColor0.rgb;

                // Albedo texture
                fixed3 albedoTex = tex2D(_MainTex, i.uv).rgb;

                // Base calculations
                fixed3 normal = normalize(i.normalDir);
                float n_dot_L = saturate(dot(lightDir, normal));

                // Bands
                float diffuse = round(saturate(n_dot_L / max(_bandCutOff, 0.0001)) * _bandAmount) / _bandAmount;

                // Specular
                float3 viewDir = normalize(i.viewDir);
                float3 halfVector = normalize(lightDir + viewDir);
                float h_dot_v = dot(viewDir, reflect(-lightDir, normal));
                float specular = dot(normal, halfVector);
                specular = _SpecularColor.rgb * step(1 - _Glossiness, h_dot_v);

                // Rim light / outline
                float rimDot = 1 - saturate(dot(viewDir, normal));
                float rimAmount = step(1 - _RimAmount, rimDot);
                float3 rimLight = rimAmount * _RimColor.rgb * n_dot_L;

                // Final color
                float3 finalColor = (_Color.rgb + albedoTex + specular) * lightColor * _AmbientColor.rgb * _Emission.rgb * diffuse + rimLight;
                return fixed4(finalColor, 1);
            }
            ENDCG
        }

        // Outline Pass, had to do it here since my original was done in urp
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
                // invert extrude normal style, not perfect but works
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
