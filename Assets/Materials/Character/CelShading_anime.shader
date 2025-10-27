Shader "Custom/Character_anime"
{
    Properties{
        [Header(Color)]
        _Color("Base color", Color) = (1, 0, 1, 1)
        _AmbientColor("Ambient color", Color) = (0.1, 0, 0.1, 1)
        [Header(Cel Shading)]
        _bandCutOff("Band cutoff", Range(0.0, 0.1)) = 0.05
        _bandAmount("Band amount", int) = 4
        [Header(Outline)]
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
                float4 shadowCoord : TEXCOORD4;
            };

            fixed4 _Color;
            fixed4 _AmbientColor;
            half _bandCutOff;
            int _bandAmount;
            float _OutlineThickness;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldPos = worldPos;
                o.viewDir = normalize(_WorldSpaceCameraPos - worldPos);
                o.shadowCoord = ComputeScreenPos(o.pos);
                return o;
            }

fixed4 frag(v2f i) : SV_Target
{
    // === Simple Matte Anime Style ===
    fixed3 albedo = _Color.rgb;

    // === Basic Lighting ===
    fixed3 N = normalize(i.normalDir);                        // use only mesh normals
    fixed3 L = normalize(_WorldSpaceLightPos0.xyz);           // main light

    // Basic Lambert diffuse
    float NdotL = saturate(dot(N, L));
    
    // Cel banding for anime stylization - use step function for hard bands
    //float celBands = floor(NdotL * _bandAmount) / _bandAmount;
    //celBands = step(_bandCutOff, celBands);  // Hard cutoff instead of smoothstep
    float celBands = floor(NdotL * _bandAmount) / _bandAmount;
    celBands = smoothstep(_bandCutOff - 0.05, _bandCutOff + 0.05, celBands);
    // === Combine (No specular, no metallic, pure matte) ===
    fixed3 lightColor = _LightColor0.rgb;
    fixed3 ambient = _AmbientColor.rgb * albedo;
    fixed3 diffuse = albedo * lightColor * celBands;
    fixed3 finalColor = ambient + diffuse;

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
