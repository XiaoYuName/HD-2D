// 原作 Visage/Sprite/Lit 的还原实现（建筑、mess_ 等静态地图元件用的基础 shader）。
// AssetRipper 导出的是 //DummyShaderTextExporter 占位版（frag 只有一行 return tex），
// 这份照 AssetStudio 反汇编还原：
//   AssetStudio.CLI shader_assets_all_*.bundle out --game Normal --types Shader
//   → Shader/Visage_Sprite_Lit.shader
//
// 反汇编要点（UniversalForward 的 fp，hash 62d5e7d7f81e0900）：
//   r1   = cb3[0] * tex                       ← cb3[0] = _RendererColor
//   clip(tex.a * _RendererColor.a - _Cutoff)   ← cb4[1].x = _Cutoff
//   r0.xyz = cb0[4].xyz + cb0[0].xyz           ← 环境光 + 主光颜色，**没有 N·L**
//   循环 additional lights：只乘距离/聚光衰减，同样不带 N·L、不采样阴影
//   o0 = 光照 * r1
// 也就是说地图元件是"平光"：法线完全不参与，主光只贡献一个整体色偏，
// 立体感全靠美术在贴图里画。这一点和普通 Lit 差别很大，别自行加 dot(N,L)。
//
// 这个 fragment 和 Visage/Sprite/Lit_Swing 是**同一个 hash**，两者只差顶点摆动，
// 所以 [[Sprites_Lit_Swing]] 里的 frag 必须和这里保持一致。
Shader "Visage/Sprite/Lit"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Cutoff ("AlphaCutOff", Range(0, 1)) = 0.5
        [Enum(Cull Off,0, Cull Front,1, Cull Back,2)] _CullMode ("Culling", Float) = 2
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

            // 反汇编里两个 Pass 都是 Cull Off；_CullMode 属性虽然存在但没接进渲染状态。
            Cull Off
            ZWrite On
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
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
                float  _Cutoff;
                float  _CullMode;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs vpi = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = vpi.positionCS;
                o.positionWS = vpi.positionWS;
                o.uv = input.uv;          // 原作没有 TRANSFORM_TEX，直接透传
                o.color = input.color;    // SpriteRenderer.color（反汇编里是 _RendererColor）
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                clip(c.a - _Cutoff);

                // 平光：环境光取 SH 的常数项。工程用的是 Flat 环境光，
                // 所以传哪个法线都一样，这里传 up 只是为了有个确定值。
                half3 lighting = SampleSH(half3(0, 1, 0)) + _MainLightColor.rgb;

                uint count = GetAdditionalLightsCount();
                for (uint i = 0u; i < count; ++i)
                {
                    Light l = GetAdditionalLight(i, input.positionWS);
                    lighting += l.color * l.distanceAttenuation;   // 同样不乘 N·L
                }

                return half4(c.rgb * lighting, c.a);
            }
            ENDHLSL
        }

        // 元件自己投影但不接收阴影（forward 的 frag 里没有阴影采样）。
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
                float  _Cutoff;
                float  _CullMode;
            CBUFFER_END

            float3 _LightDirection;

            ShadowVaryings shadowVert(ShadowAttributes input)
            {
                ShadowVaryings o;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 lightDir = _MainLightPosition.xyz;
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
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
                // 反汇编的 shadow fp（hash 8b119f26a479805b）只用贴图 alpha 和 _Cutoff，
                // 不乘 _RendererColor。
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                clip(a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
