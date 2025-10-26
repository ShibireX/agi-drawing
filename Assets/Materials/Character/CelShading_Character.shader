Shader "Custom/CharacterShading"
{
    Properties{
        [Header(Color)]
        _Color("Base color", Color) = (1, 0, 1, 1)
        _MainTex ("Main tex", 2D) = "white" {}
        _NormalTex ("Normal Map", 2D) = "bump" {}
        _RoughnessTex ("Roughness Map", 2D) = "white" {}
        _DisplacementTex ("Displacement Map", 2D) = "white" {}
        _MetallicTex ("Metallic Map", 2D) = "white" {}
        _AmbientColor("Ambient color", Color) = (0.1, 0, .01, 1)
        _SpecularColor("Specular Color", Color) = (0.9, 0.9, 0.9, 1)
        _RimColor("Rim color", Color) = (1, 0, 1, 1)
        _Glossiness("Glossiness", Range(0.0, 1.0)) = 0.5
        [Header(Rim settings)]
        _RimAmount("Rim amount", Range(0.0, 1.0)) = 0.5
        [Header(Band cutoffs)]
        _bandCutOff("Band cutoff", Range(0.0, 1.0)) = 0.5
        _bandAmount("Band amount", int) = 4
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineThickness("Outline Thickness", Range(0.0,0.05)) = 0.02
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry"}

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
            sampler2D _NormalTex;
            sampler2D _RoughnessTex;
            sampler2D _DisplacementTex;
            sampler2D _MetallicTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _ShadowColor;
            fixed4 _AmbientColor;
            fixed4 _SpecularColor;
            fixed4 _RimColor;
            half _Glossiness;
            half _RimAmount;
            half _bandCutOff;
            int _bandAmount;
            float _OutlineThickness;

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
    // === Textures ===
    fixed3 albedo = tex2D(_MainTex, i.uv).rgb * _Color.rgb;
    fixed3 normalTex = UnpackNormal(tex2D(_NormalTex, i.uv));
    float roughness = tex2D(_RoughnessTex, i.uv).r; // single channel roughness
    float displacement = tex2D(_DisplacementTex, i.uv).r;
    float metallic = tex2D(_MetallicTex, i.uv).r;

    // === Lighting ===
    fixed3 N = normalize(i.normalDir + normalTex);           // combine mesh + normal map
    fixed3 L = normalize(_WorldSpaceLightPos0.xyz);          // main light
    fixed3 V = normalize(i.viewDir);
    fixed3 H = normalize(L + V);

    // Basic Lambert diffuse
    float NdotL = saturate(dot(N, L));
    
    // Cel banding for stylization
    float bandedDiffuse = floor(NdotL * _bandAmount) / _bandAmount;
    bandedDiffuse = smoothstep(_bandCutOff - 0.05, _bandCutOff + 0.05, bandedDiffuse);

    // === Specular from roughness ===
    float smoothness = 1.0 - roughness;                      // invert roughness to get smoothness
    float specularStrength = pow(saturate(dot(N, H)), smoothness * 64.0); 
    specularStrength = step(0.5, specularStrength);          // hard cel step
    fixed3 specular = specularStrength * _SpecularColor.rgb * metallic;

    // === Combine ===
    fixed3 lightColor = _LightColor0.rgb;
    fixed3 ambient = _AmbientColor.rgb * albedo * (1.0 - metallic);
    fixed3 diffuse = albedo * lightColor * bandedDiffuse * (1.0 - metallic);
    fixed3 metallicReflection = albedo * metallic * lightColor;
    fixed3 finalColor = ambient + diffuse + specular + metallicReflection;

    return fixed4(finalColor, 1.0);
}
            ENDCG
        }

        // Outline Pass, had to do it here since my original was done in urp
               Pass{
            Cull front

            CGPROGRAM

            //include useful shader functions
            #include "UnityCG.cginc"

            //define vertex and fragment shader
            #pragma vertex vert
            #pragma fragment frag

            //color of the outline
            fixed4 _OutlineColor;
            //thickness of the outline
            float _OutlineThickness;

            //the object data that's available to the vertex shader
            struct appdata{
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            //the data that's used to generate fragments and can be read by the fragment shader
            struct v2f{
                float4 position : SV_POSITION;
            };

            //the vertex shader
            v2f vert(appdata v){
                v2f o;
                // Extrude along world space normal for consistent thickness
                float3 worldNormal = normalize(UnityObjectToWorldNormal(v.normal));
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 outlinePos = worldPos + worldNormal * _OutlineThickness;
                o.position = UnityWorldToClipPos(float4(outlinePos, 1.0));
                return o;
            }

            //the fragment shader
            fixed4 frag(v2f i) : SV_TARGET{
                return _OutlineColor;
            }

            ENDCG
        }
        
    }
    FallBack "Diffuse"
}
