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
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _MainTex);
                o.color = input.color;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(tex.a - _Cutoff);

                // 和地表 / 水面同一套光照，画面才协调
                float3 N = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                half3 lighting = SampleSH(N) + mainLight.color * saturate(dot(N, mainLight.direction));

                return half4(tex.rgb * input.color.rgb * lighting, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
