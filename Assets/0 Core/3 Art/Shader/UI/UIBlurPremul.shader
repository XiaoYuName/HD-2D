// UI 图片高斯/散景模糊着色器（预乘 Alpha 版）
// 与 UI/UIBlur 算法一致，但采样按 alpha 加权（预乘）后再还原，
// 因此模糊带透明边的精灵（如角色立绘）时不会把周围透明像素的黑色带进来，
// 避免出现“黑色方块/黑边”。适合 背景+角色 等多张图片各自模糊后再叠加合成。
// 通过 _BlurSize 控制模糊强度（0 = 完全清晰）。兼容 Built-in 与 URP（UGUI 渲染与管线无关）。
Shader "UI/UIBlurPremul"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BlurSize ("Blur Size (px)", Range(0, 16)) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float _BlurSize;

            // 圆盘采样点数：越多越平滑、开销越大
            #define BLUR_SAMPLES 48
            #define GOLDEN_ANGLE 2.39996323
            #define TWO_PI 6.28318530718

            // 每像素伪随机值，用于打散采样图案，消除环状/网格条带
            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            // 圆盘散景模糊（预乘 Alpha）：黄金角螺旋在圆盘内均匀采样模拟相机失焦圆斑。
            // 关键区别：每个采样先把 rgb 乘以自身 alpha 再累加（预乘），最后除以累加的 alpha 还原 rgb。
            // 这样完全透明的像素（alpha=0）不会把它们的黑色 rgb 带入结果，避免透明精灵边缘出现黑框/黑边。
            // _BlurSize 为圆盘半径（像素），0 时退化为单次采样。
            fixed4 BlurredSample(float2 uv)
            {
                if (_BlurSize <= 0.001)
                    return tex2D(_MainTex, uv) + _TextureSampleAdd;

                float2 radiusUV = _MainTex_TexelSize.xy * _BlurSize;

                // 以像素坐标做哈希，得到每像素不同的起始旋转角，避免所有像素图案对齐产生条带
                float2 pixel = uv * _MainTex_TexelSize.zw;
                float angleOffset = Hash(pixel) * TWO_PI;

                float4 sumPremul = float4(0, 0, 0, 0);  // 预乘后的 rgb 与 alpha 的加权累加
                float total = 0.0;

                [loop]
                for (int i = 0; i < BLUR_SAMPLES; i++)
                {
                    // sqrt 保证圆盘内面积均匀分布；黄金角保证角向均匀
                    float t = (i + 0.5) / BLUR_SAMPLES;
                    float r = sqrt(t);
                    float a = i * GOLDEN_ANGLE + angleOffset;
                    float2 off = float2(cos(a), sin(a)) * r * radiusUV;

                    // 越靠近圆心权重略高，使虚化中心更稳、边缘更柔和
                    float w = 1.0 - 0.5 * t;

                    fixed4 c = tex2D(_MainTex, uv + off) + _TextureSampleAdd;
                    c.rgb *= c.a;               // 预乘：透明像素不贡献颜色
                    sumPremul += c * w;
                    total += w;
                }

                sumPremul /= total;             // 得到平均的（预乘 rgb，alpha）
                // 还原直通 alpha 的颜色，供 SrcAlpha/OneMinusSrcAlpha 正常混合
                if (sumPremul.a > 0.0001)
                    sumPremul.rgb /= sumPremul.a;
                return sumPremul;
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = BlurredSample(IN.texcoord) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}
