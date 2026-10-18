// Cuaderno en mano: luz propia (vela simulada) para que no dependa de las luces del recinto.
Shader "Gamejam/Cuaderno"
{
    Properties
    {
        _BaseMap ("Base", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        _Tint ("Tint", Color) = (1,1,1,1)
        _Gloss ("Gloss", Range(0,1)) = 0.25
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "Cuaderno"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Tint;
                float _BumpScale;
                float _Gloss;
                float _Cull;
                float4 _LampPos;
                float4 _LampColor;
                float4 _Ambient;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                float sign = v.tangentOS.w * GetOddNegativeScale();
                o.tangentWS = float4(TransformObjectToWorldDir(v.tangentOS.xyz), sign);
                return o;
            }

            half4 frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _Tint;
                float3 n = normalize(i.normalWS);
                if (!front) n = -n;
                float3 t = normalize(i.tangentWS.xyz - n * dot(i.tangentWS.xyz, n));
                float3 b = cross(n, t) * i.tangentWS.w;
                half3 tn = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                n = normalize(tn.x * t + tn.y * b + tn.z * n);
                float3 toLamp = _LampPos.xyz - i.positionWS;
                float dist = length(toLamp);
                float3 l = toLamp / max(dist, 1e-4);
                float atten = 1.0 / (1.0 + dist * dist * _LampPos.w);
                float diffuse = saturate(dot(n, l) * 0.8 + 0.2);
                float3 v = normalize(_WorldSpaceCameraPos - i.positionWS);
                float3 h = normalize(l + v);
                float spec = pow(saturate(dot(n, h)), lerp(8.0, 64.0, _Gloss)) * _Gloss;
                half3 color = albedo.rgb * (_Ambient.rgb + _LampColor.rgb * diffuse * atten) + _LampColor.rgb * spec * atten * 0.35;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
