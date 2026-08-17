// 原作同名 shader 的重写实现（AssetRipper 导出的是 //DummyShaderTextExporter 占位版）。
// 这是 Sprites_Lit_WorldTile_Cut 的无裁切变体，用于水底（Block_*_Ground）：
//   _SecondTex 按世界坐标 XZ 平铺，不透明输出。
// 水底必须是不透明的——水面 shader 的折射项要从 _CameraOpaqueTexture 里采到它，
// 否则折射会采到背景色，整片水会发白。
Shader "Visage/Sprite/Lit_WorldTile"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _SecondTex ("Second Texture", 2D) = "white" {}
        _SecondTexScale ("SecondTexScale", Range(2, 16)) = 8
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
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

            TEXTURE2D(_MainTex);   SAMPLER(sampler_MainTex);
            TEXTURE2D(_SecondTex); SAMPLER(sampler_SecondTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _SecondTex_ST;
                float  _SecondTexScale;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.uv = TRANSFORM_TEX(input.uv, _MainTex);
                o.color = input.color;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // MeshRenderer 上 _MainTex 是 PerRendererData、不会被赋值，默认白图，等于没有遮罩
                half4 mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                float2 worldUV = input.positionWS.xz / max(_SecondTexScale, 0.0001);
                half4 ground = SAMPLE_TEXTURE2D(_SecondTex, sampler_SecondTex, worldUV);

                float3 N = float3(0, 1, 0);
                Light mainLight = GetMainLight();
                half3 lighting = SampleSH(N) + mainLight.color * saturate(dot(N, mainLight.direction));

                return half4(ground.rgb * mask.rgb * input.color.rgb * lighting, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DA { float4 positionOS : POSITION; };
            struct DV { float4 positionCS : SV_POSITION; };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _SecondTex_ST;
                float  _SecondTexScale;
            CBUFFER_END

            DV depthVert(DA input) { DV o; o.positionCS = TransformObjectToHClip(input.positionOS.xyz); return o; }
            half4 depthFrag(DV input) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    Fallback Off
}
