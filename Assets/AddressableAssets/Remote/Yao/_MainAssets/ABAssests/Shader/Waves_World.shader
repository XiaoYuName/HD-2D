// 原作 Visage/Water/Waves_World 的还原实现。
//
// AssetRipper 导出的是 //DummyShaderTextExporter 占位版（frag 只有一行 return _MainTex * _Color）。
// 这份实现是照 AssetStudio 反汇编出的 d3d11 subprogram 逐条还原的，常量缓冲对应关系：
//   cb1[0..2] = _MainTex_ST / _NoiseTex_ST / _FoamTex_ST
//   cb1[3]    = (_FoamFactor, _FoamWaveInt, _FoamWavePow, _NoisePower)
//   cb1[4]    = (_NoiseSpeed, _scrollXSpeed, _scrollYSpeed, _Gloss)
//   cb1[5].x  = _FresnelPow      cb1[6..9] = _Color / _FoamColor / _SpecularColor / _FresnelColor
//   t0..t4    = MainTex, NoiseTex, FoamTex, _CameraDepthTexture, _CameraOpaqueTexture
//
// 依赖：URP Renderer 必须打开 Opaque Texture（折射）和 Depth Texture（岸边泡沫 + 边缘透明），
// 场景里必须有方向光（着色包含主光漫反射与高光，无光会发黑）。
Shader "Visage/Water/Waves_World"
{
    Properties
    {
        [Header(Main)] _MainTex ("MainTex", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _scrollXSpeed ("X Scroll Speed", Range(-10, 10)) = 0
        _scrollYSpeed ("Y Scroll Speed", Range(-10, 10)) = 0
        [Header(Bisturb)] _NoiseTex ("NoiseTex", 2D) = "white" {}
        _NoisePower ("NoisePower", Range(0, 1)) = 0.1
        _NoiseSpeed ("NoiseSpeed", Range(-10, 10)) = 1
        [Header(Foam)] _FoamTex ("FoamTex", 2D) = "white" {}
        [HDR] _FoamColor ("FoamColor", Color) = (3,3,3,3)
        _FoamFactor ("FoamFactor", Range(0, 1)) = 0.1
        _FoamWaveInt ("FoamWaveInt", Range(0, 1)) = 0.2
        _FoamWavePow ("FoamWavePow", Range(1, 10)) = 2
        [Header(Specular)] _Gloss ("SpecularPow", Range(1, 10)) = 0
        _SpecularColor ("SpecularColor", Color) = (1,1,1,1)
        [Header(Fresnel)] _FresnelPow ("FresnelPow", Range(1, 10)) = 0
        _FresnelColor ("FresnelColor", Color) = (1,1,1,1)
        [Header(Wave)] _WaveA ("Wave A (dir, steepness, wavelength)", Vector) = (1,0,0.5,10)
        _WaveB ("Wave B", Vector) = (0,1,0.25,20)
        _WaveC ("Wave C", Vector) = (1,1,0.15,10)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            // 反汇编里的混合状态就是预乘 alpha
            Blend One OneMinusSrcAlpha
            // 原作用一整块大水面，这里是 4x4 拼块，开 ZWrite 会在拼缝处互相剔除，故关闭
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float2 worldUV    : TEXCOORD1;
                float3 positionWS : TEXCOORD2;   // 未位移的世界坐标，视线方向用它
                float4 screenPos  : TEXCOORD3;
            };

            TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_FoamTex);  SAMPLER(sampler_FoamTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _NoiseTex_ST;
                float4 _FoamTex_ST;
                half4  _Color;
                half4  _FoamColor;
                half4  _SpecularColor;
                half4  _FresnelColor;
                float4 _WaveA;
                float4 _WaveB;
                float4 _WaveC;
                float  _scrollXSpeed;
                float  _scrollYSpeed;
                float  _NoisePower;
                float  _NoiseSpeed;
                float  _FoamFactor;
                float  _FoamWaveInt;
                float  _FoamWavePow;
                float  _Gloss;
                float  _FresnelPow;
            CBUFFER_END

            // Gerstner 波：wave = (dirX, dirZ, steepness, wavelength)
            // 反汇编里 k = 2π/波长、相速 c = sqrt(9.8/k)、振幅 a = steepness/k，
            // 相位取未位移的世界 XZ，切线/副切线同步累加用于算法线。
            float3 GerstnerWave(float4 wave, float3 posWS, inout float3 tangent, inout float3 binormal)
            {
                float  k = 6.28318548 / wave.w;
                float  c = sqrt(9.8 / k);
                float2 d = normalize(wave.xy);
                float  f = k * (dot(d, posWS.xz) - c * _Time.y);
                float  a = wave.z / k;

                float s = sin(f);
                float co = cos(f);

                tangent  += float3(-d.x * d.x * (wave.z * s),
                                    d.x       * (wave.z * co),
                                   -d.x * d.y * (wave.z * s));
                binormal += float3(-d.x * d.y * (wave.z * s),
                                    d.y       * (wave.z * co),
                                   -d.y * d.y * (wave.z * s));

                return float3(d.x * (a * co), a * s, d.y * (a * co));
            }

            Varyings vert(Attributes input)
            {
                Varyings o;

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);

                float3 tangent  = float3(1, 0, 0);
                float3 binormal = float3(0, 0, 1);
                float3 offset   = 0;
                offset += GerstnerWave(_WaveA, posWS, tangent, binormal);
                offset += GerstnerWave(_WaveB, posWS, tangent, binormal);
                offset += GerstnerWave(_WaveC, posWS, tangent, binormal);

                // 原作把位移加在物体空间顶点上（水面只有平移，等价）
                float3 displacedOS = input.positionOS.xyz + offset;
                float3 displacedWS = TransformObjectToWorld(displacedOS);

                o.positionCS = TransformWorldToHClip(displacedWS);
                o.normalWS   = TransformObjectToWorldNormal(normalize(cross(binormal, tangent)));
                o.uv         = input.uv;
                o.worldUV    = posWS.xz * 0.0625;   // 反汇编里的 1/16
                o.positionWS = posWS;
                o.screenPos  = ComputeScreenPos(o.positionCS);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 N = normalize(input.normalWS);
                float3 L = _MainLightPosition.xyz;
                float3 V = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                float3 R = normalize(reflect(-L, N));

                float  NdotL   = max(0, dot(N, L));
                float  NdotV   = saturate(dot(N, V));
                float  fresnel = pow(1 - NdotV, _FresnelPow);
                float  spec    = pow(saturate(dot(R, V)), _Gloss);

                // 主光漫反射 + 高光 + 环境光
                float3 lighting = SampleSH(N)
                                + _MainLightColor.rgb * NdotL
                                + _SpecularColor.rgb * _MainLightColor.rgb * spec;

                float2 screenUV = input.screenPos.xy / input.screenPos.w;

                // 浪尖泡沫：法线越偏离竖直越白（反汇编里的常数 34）
                float crest = pow(34.0 * (1.0 - saturate(input.normalWS.y)), _FoamWavePow);

                float2 scroll  = float2(_scrollXSpeed, _scrollYSpeed);
                float2 foamUV  = input.uv * _FoamTex_ST.xy + _FoamTex_ST.zw - 2.0 * scroll * _Time.y;
                half   foamMask = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, foamUV).g;
                float  crestFoam = saturate(foamMask * crest - _FoamFactor);

                // 岸边泡沫 + 边缘透明：靠深度差判断浅水
                float  sceneEye  = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float  prox      = 1.0 - abs(sceneEye - input.screenPos.w);
                float  shoreFoam = sqrt(saturate(foamMask * prox - _FoamFactor));
                float  alpha     = min(1.0, 0.4 * (1.0 - prox));

                float  foam = crestFoam * _FoamWaveInt + shoreFoam;

                // 主纹理：世界 UV + 滚动 + 噪声扰动
                float2 baseUV  = input.worldUV + _Time.y * scroll;
                float2 noiseUV = (baseUV - _Time.y * _NoiseSpeed) * _NoiseTex_ST.xy + _NoiseTex_ST.zw;
                float2 n       = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).rg;

                float2 distort = n * _NoisePower + float2(-0.05, 0.0);
                float2 mainUV  = (n * _NoisePower + baseUV) * _MainTex_ST.xy + _MainTex_ST.zw;
                float3 water   = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUV).rgb;

                // 折射：用扰动过的屏幕 UV 采不透明图
                float3 refraction = SampleSceneColor(distort * 0.15 + screenUV);

                float3 col = water * _Color.rgb + refraction;
                col = lerp(col, _FoamColor.rgb, foam);
                col += _FresnelColor.rgb * fresnel;
                col *= lighting;

                return half4(col * alpha, alpha);   // 预乘 alpha，配合 Blend One OneMinusSrcAlpha
            }
            ENDHLSL
        }
    }

    Fallback Off
}
