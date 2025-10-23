Shader "Custom/CelShading_with_textures"
{
    Properties{
        [Header(Textures)]
        _BaseColorTex("Base Color", 2D) = "white" {}
        _NormalTex("Normal Map", 2D) = "bump" {}
        _RoughnessTex("Roughness Map", 2D) = "white" {}
        
        [Header(Color Tinting)]
        _Color("Base color tint", Color) = (1, 1, 1, 1)
        _NormalStrength("Normal Strength", Range(0.0, 2.0)) = 1.0
        _RoughnessStrength("Roughness Strength", Range(0.0, 1.0)) = 1.0
        
        [Header(Lighting)]
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
                float4 tangent : TANGENT;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalDir : TEXCOORD1;
                float3 tangentDir : TEXCOORD2;
                float3 bitangentDir : TEXCOORD3;
                float3 viewDir : TEXCOORD4;
                float3 worldPos : TEXCOORD5;
            };

            // Texture samplers
            sampler2D _BaseColorTex;
            float4 _BaseColorTex_ST;
            sampler2D _NormalTex;
            float4 _NormalTex_ST;
            sampler2D _RoughnessTex;
            float4 _RoughnessTex_ST;
            
            // Material properties
            fixed4 _Color;
            half _NormalStrength;
            half _RoughnessStrength;
            
            // Lighting properties
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
                o.uv = TRANSFORM_TEX(v.uv, _BaseColorTex);
                
                // Calculate world space vectors for normal mapping
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                o.tangentDir = normalize(mul(unity_ObjectToWorld, float4(v.tangent.xyz, 0.0)).xyz);
                o.bitangentDir = normalize(cross(o.normalDir, o.tangentDir) * v.tangent.w);
                
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldPos = worldPos;
                o.viewDir = normalize(_WorldSpaceCameraPos - worldPos);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Sample textures
                fixed3 baseColor = tex2D(_BaseColorTex, i.uv).rgb * _Color.rgb;
                fixed3 normalMap = UnpackNormal(tex2D(_NormalTex, i.uv));
                normalMap.xy *= _NormalStrength;
                fixed roughness = tex2D(_RoughnessTex, i.uv).r * _RoughnessStrength;
                
                // Calculate normal from normal map
                float3x3 tangentToWorld = float3x3(i.tangentDir, i.bitangentDir, i.normalDir);
                fixed3 normal = normalize(mul(normalMap, tangentToWorld));
                
                // Main directional light
                fixed3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                fixed3 lightColor = _LightColor0.rgb;
                
                // Base lighting calculations
                float n_dot_L = saturate(dot(lightDir, normal));
                
                // Cel shading bands - apply to diffuse
                float diffuse = round(saturate(n_dot_L / max(_bandCutOff, 0.0001)) * _bandAmount) / _bandAmount;
                
                // Specular calculation with roughness
                float3 viewDir = normalize(i.viewDir);
                float3 halfVector = normalize(lightDir + viewDir);
                float h_dot_v = dot(viewDir, reflect(-lightDir, normal));
                
                // Adjust specular based on roughness
                float specularIntensity = 1.0 - roughness;
                float specular = dot(normal, halfVector);
                specular = _SpecularColor.rgb * step(1 - (_Glossiness * specularIntensity), h_dot_v);
                
                // Rim light / outline
                float rimDot = 1 - saturate(dot(viewDir, normal));
                float rimAmount = step(1 - _RimAmount, rimDot);
                float3 rimLight = rimAmount * _RimColor.rgb * n_dot_L;
                
                // Simple diffuse and specular mixing
                float3 diffuseColor = baseColor * diffuse;
                float3 specularColor = _SpecularColor.rgb * specular;
                
                // Final color with cel shading
                float3 finalColor = (diffuseColor + specularColor) * lightColor * _AmbientColor.rgb * _Emission.rgb + rimLight;
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
