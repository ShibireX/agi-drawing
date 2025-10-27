Shader "Custom/BRP/Glass"
{
    Properties
    {
        _MainTex ("Albedo (RGB) and Alpha", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0,2)) = 1.0
        _RoughnessMap ("Roughness Map", 2D) = "gray" {}
        _RoughnessStrength ("Roughness Strength", Range(0,2)) = 1.0
        _DisplacementMap ("Displacement Map", 2D) = "gray" {}
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows alpha vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _NormalMap;
        sampler2D _RoughnessMap;
        sampler2D _DisplacementMap;
        fixed4 _Color;
        half _Metallic;
        half _Glossiness;
        half _NormalStrength;
        half _RoughnessStrength;
        half _DisplacementStrength;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_NormalMap;
            float2 uv_RoughnessMap;
            float2 uv_DisplacementMap;
        };

        void vert(inout appdata_full v)
        {
            float d = tex2Dlod(_DisplacementMap, float4(v.texcoord.xy, 0, 0)).r;
            v.vertex.xyz += v.normal * d;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) + _Color;
            fixed4 roughSample = tex2D(_RoughnessMap, IN.uv_RoughnessMap);
            fixed3 normalTex = UnpackNormal(tex2D(_NormalMap, IN.uv_NormalMap));
            normalTex = normalize(lerp(float3(0,0,1), normalTex, _NormalStrength));

            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = saturate(_Glossiness * (1.0 - roughSample.r * _RoughnessStrength));
            o.Normal = normalTex;
            o.Alpha = c.a;
        }
        ENDCG
    }

    FallBack "Transparent/Diffuse"
}
