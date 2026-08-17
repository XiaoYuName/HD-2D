// 原作 Visage/Sprite/Lit_Transparent 的重写实现（AssetRipper 导出的是 Dummy 占位版）。
// 用途：RegionCamera 检测到挡住角色的装饰物时，把它换成这个材质并把 _Alpha 从 1 渐变到 0.3，
// 让树木/草丛半透明，露出后面的角色。属性语义没有歧义，按属性表直接实现：
//   _MainTex        sprite 本体（PerRendererData）
//   _Alpha          整体不透明度，RegionCamera 用 DOTween 驱动
//   _HighLightColor HDR 叠色
//   _Cutoff         alpha 裁切阈值
Shader "Visage/Sprite/Lit_Transparent"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Cutoff ("AlphaCutOff", Range(0, 1)) = 0.5
        _Alpha ("Alpha", Range(0, 1)) = 1
        [Header(High Light)] [Space(10)] [HDR] _HighLightColor ("HighLight Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
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
            ZWrite Off                                  // 半透明期间不写深度，否则会挡住后面的角色
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

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
                float3 normalWS   : TEXCOORD1;
                half4  color      : COLOR;
            };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _HighLightColor;
                float  _Cutoff;
                float  _Alpha;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _MainTex);
                o.color = input.color;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(tex.a - _Cutoff);

                // 和其它地表 / 植被同一套光照，半透明前后不会突然变色
                float3 N = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                half3 lighting = SampleSH(N) + mainLight.color * saturate(dot(N, mainLight.direction));

                half3 rgb = tex.rgb * input.color.rgb * _HighLightColor.rgb * lighting;
                return half4(rgb, tex.a * input.color.a * _Alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
