// 原作 Visage/Sprite/Lit_Swing 的还原实现。
// AssetRipper 导出的是 //DummyShaderTextExporter 占位版，这份照 AssetStudio 反汇编的
// d3d11 subprogram 还原，常量缓冲对应关系：
//   cb3[1] = (_Cutoff, _SwingDir, _SwingPow, _SwingSpeed)
//   cb3[2].x = _SwingRange      cb3[3].xy = _SwingInt.xy
//
// 摆动逻辑：
//   相位 p = sin(物体世界 Z * _SwingRange) + _Time.y * _SwingSpeed   ← 按位置错开，避免整片齐摆
//   包络 env = 10π - 6π * frac(p / 6π)                              ← 反汇编里的常数 18.849556 / 31.415926
//   幅度 amt = (sin(p) * env * 0.04 + _SwingDir) * pow(uv.y, _SwingPow)
//   偏移 = _SwingInt.xy * amt，加在物体空间 XY 上（所以只有顶部摆、根部钉住）
//
// fragment 和 Visage/Sprite/Lit 是**同一个 hash（62d5e7d7f81e0900）**，两者只差顶点摆动。
// 光照是"平光"：环境光 + 主光颜色，**没有 N·L**，另外逐像素叠加 additional lights
// 的距离/聚光衰减（同样不带 N·L、不采样阴影）。改这里的 frag 时要和 Sprites_Lit 同步。
Shader "Visage/Sprite/Lit_Swing"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Cutoff ("Cutoff", Range(0, 1)) = 0.5
        _SwingInt ("SwingInt", Vector) = (1,0,0,0)
        _SwingDir ("SwingDir", Range(-1, 1)) = 0.5
        _SwingPow ("SwingPow", Range(1, 10)) = 1
        _SwingRange ("SwingRange", Range(0.01, 1)) = 1
        _SwingSpeed ("SwingSpeed", Range(0, 4)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }
        LOD 200

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4  color      : COLOR;
            };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _SwingInt;
                float  _Cutoff;
                float  _SwingDir;
                float  _SwingPow;
                float  _SwingRange;
                float  _SwingSpeed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings o;

                // 物体在世界里的 Z，用来给每株植物一个固定的相位偏移
                float objZ = UNITY_MATRIX_M._m23;

                float p   = sin(objZ * _SwingRange) + _Time.y * _SwingSpeed;
                float q   = p * 0.0530516468;                       // p / (6π)
                float f   = (q >= 0.0) ? frac(abs(q)) : -frac(abs(q));
                float env = -f * 18.849556 + 31.415926;             // 10π - 6π·frac
                float amt = sin(p) * env * 0.04 + _SwingDir;
                amt *= pow(abs(input.uv.y), _SwingPow);             // 只有上半部分摆

                float3 posOS = input.positionOS.xyz;
                posOS.xy += _SwingInt.xy * amt;

                VertexPositionInputs vpi = GetVertexPositionInputs(posOS);
                o.positionCS = vpi.positionCS;
                o.positionWS = vpi.positionWS;
                o.uv = input.uv;          // 原作没有 TRANSFORM_TEX，直接透传
                o.color = input.color;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                clip(c.a - _Cutoff);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 lighting = SampleSH(half3(0, 1, 0)) + mainLight.color * mainLight.shadowAttenuation;

                uint count = GetAdditionalLightsCount();
                for (uint i = 0u; i < count; ++i)
                {
                    Light l = GetAdditionalLight(i, input.positionWS);
                    lighting += l.color * l.distanceAttenuation;
                }

                return half4(c.rgb * lighting, c.a);
            }
            ENDHLSL
        }

        // 原作也有 SHADOWCASTER，而且它的顶点程序里**同样带摆动数学**
        // （反汇编能看到 18.849556 / 31.415926 / 0.0530516468），所以影子跟着草一起晃。
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _SwingInt;
                float  _Cutoff;
                float  _SwingDir;
                float  _SwingPow;
                float  _SwingRange;
                float  _SwingSpeed;
            CBUFFER_END

            ShadowVaryings shadowVert(ShadowAttributes input)
            {
                ShadowVaryings o;

                float objZ = UNITY_MATRIX_M._m23;
                float p   = sin(objZ * _SwingRange) + _Time.y * _SwingSpeed;
                float q   = p * 0.0530516468;
                float f   = (q >= 0.0) ? frac(abs(q)) : -frac(abs(q));
                float env = -f * 18.849556 + 31.415926;
                float amt = sin(p) * env * 0.04 + _SwingDir;
                amt *= pow(abs(input.uv.y), _SwingPow);

                float3 posOS = input.positionOS.xyz;
                posOS.xy += _SwingInt.xy * amt;

                float3 positionWS = TransformObjectToWorld(posOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _MainLightPosition.xyz));
                #if UNITY_REVERSED_Z
                    o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.uv = input.uv;
                return o;
            }

            half4 shadowFrag(ShadowVaryings input) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                clip(a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
