Shader "Custom/HairShader"
{
    Properties{
        [Header(Color)]
        _Color("Base color", Color) = (1, 0, 1, 1)
        _MainTex ("Main tex", 2D) = "white" {}
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
        Tags { "RenderType"="Transparent" "Queue"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

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
    fixed4 mainTex = tex2D(_MainTex, i.uv);
    fixed3 albedo = mainTex.rgb * _Color.rgb;
    fixed alpha = mainTex.a;

    // === Lighting ===
    fixed3 N = normalize(i.normalDir);
    fixed3 L = normalize(_WorldSpaceLightPos0.xyz);
    fixed3 V = normalize(i.viewDir);
    fixed3 H = normalize(L + V);

    // Basic Lambert diffuse
    float NdotL = saturate(dot(N, L));
    
    // Cel banding for stylization
    float bandedDiffuse = floor(NdotL * _bandAmount) / _bandAmount;
    bandedDiffuse = smoothstep(_bandCutOff - 0.05, _bandCutOff + 0.05, bandedDiffuse);

    // === Specular ===
    float specularStrength = pow(saturate(dot(N, H)), _Glossiness * 64.0); 
    specularStrength = step(0.5, specularStrength);
    fixed3 specular = specularStrength * _SpecularColor.rgb;

    // === Rim lighting ===
    float rimDot = 1 - saturate(dot(V, N));
    float rimAmount = step(1 - _RimAmount, rimDot);
    fixed3 rimLight = rimAmount * _RimColor.rgb * NdotL;

    // === Combine ===
    fixed3 lightColor = _LightColor0.rgb;
    fixed3 ambient = _AmbientColor.rgb * albedo;
    fixed3 diffuse = albedo * lightColor * bandedDiffuse;
    fixed3 finalColor = ambient + diffuse + specular + rimLight;

    return fixed4(finalColor, alpha);
}
            ENDCG
        }

           


        
    }
    FallBack "Diffuse"
}
