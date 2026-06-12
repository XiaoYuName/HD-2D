// UI 拍照光影着色器
// 用于 UGUI Image：模拟拍照时的光影/调色配置——曝光、对比度、饱和度、色温、整体染色、暗角。
// 兼容 Built-in 与 URP（UGUI Overlay 渲染与渲染管线无关）。
Shader "UI/UIPhotoLighting"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Lighting)]
        _Brightness ("Brightness/Exposure", Range(0, 3)) = 1
        _Contrast ("Contrast", Range(0, 3)) = 1
        _Saturation ("Saturation", Range(0, 3)) = 1
        _Temperature ("Temperature (warm-cool)", Range(-1, 1)) = 0
        _GradeColor ("Grade Color (multiply)", Color) = (1,1,1,1)

        [Header(Vignette)]
        _VignetteIntensity ("Vignette Intensity", Range(0, 1)) = 0
        _VignetteStart ("Vignette Start", Range(0, 1)) = 0.5
        _VignetteColor ("Vignette Color", Color) = (0,0,0,1)

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

            float _Brightness;
            float _Contrast;
            float _Saturation;
            float _Temperature;
            fixed4 _GradeColor;
            float _VignetteIntensity;
            float _VignetteStart;
            fixed4 _VignetteColor;

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
                fixed4 tex = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;
                half4 color = tex * IN.color;
                half3 rgb = color.rgb;

                // 曝光/亮度
                rgb *= _Brightness;

                // 色温：暖色加红减蓝，冷色反之
                rgb.r += _Temperature * 0.12;
                rgb.b -= _Temperature * 0.12;

                // 整体染色（叠加灯光颜色）
                rgb *= _GradeColor.rgb;

                // 对比度（绕中灰 0.5）
                rgb = (rgb - 0.5) * _Contrast + 0.5;

                // 饱和度（基于亮度灰度插值）
                half lum = dot(rgb, half3(0.299, 0.587, 0.114));
                rgb = lerp(lum.xxx, rgb, _Saturation);

                // 暗角：按到中心距离向暗角颜色衰减（用 UV，适合铺满画面的背景图）
                float dist = distance(IN.texcoord, float2(0.5, 0.5)) * 1.41421356;
                float vig = smoothstep(_VignetteStart, 1.0, dist) * _VignetteIntensity;
                rgb = lerp(rgb, _VignetteColor.rgb, vig);

                color.rgb = max(rgb, 0.0);

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
