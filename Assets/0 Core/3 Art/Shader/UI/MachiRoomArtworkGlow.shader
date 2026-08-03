Shader "UI/MachiRoomArtworkGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _InnerSize ("Image Size", Vector) = (100, 100, 0, 0)
        _OuterGlowWidth ("Outer Glow Width", Float) = 42
        _OuterGlowIntensity ("Outer Glow Intensity", Range(0, 2)) = 0.8
        _CornerRadius ("Corner Radius", Float) = 3
        _GlowCornerRadius ("Glow Corner Radius", Float) = 64
        _InnerRimWidth ("Inner Rim Width", Float) = 18
        _InnerRimSpread ("Inner Rim Spread", Range(0.05, 1)) = 0.35
        _EdgeSoftness ("Surface Edge Softness", Float) = 12
        _Falloff ("Outer Glow Falloff", Float) = 1.7
        _SweepWidth ("Sweep Width", Range(0.01, 1)) = 0.12
        _SweepSoftness ("Sweep Softness", Range(0.01, 1)) = 0.18
        _SurfaceAngle ("Surface Angle", Range(-80, 80)) = -25
        _PulseDuration ("Pulse Duration", Float) = 2.8
        _PulseScale ("Pulse Width Scale", Range(0, 0.5)) = 0.18
        _PulseIntensity ("Pulse Intensity", Range(0, 1)) = 0.22
        _SweepSpeed ("Sweep Speed", Float) = 0.12
        _SweepIntensity ("Sweep Intensity", Range(0, 2)) = 0.75
        _AnimTime ("Animation Time", Float) = 0
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
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
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct AppData { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 worldPosition : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            fixed4 _Color;
            float4 _ClipRect;
            float2 _InnerSize;
            float _OuterGlowWidth;
            float _OuterGlowIntensity;
            float _CornerRadius;
            float _GlowCornerRadius;
            float _InnerRimWidth;
            float _InnerRimSpread;
            float _EdgeSoftness;
            float _Falloff;

            // 圆角矩形有向距离场：外正内负
            float RoundedRectDistance(float2 p, float2 halfSize, float radius)
            {
                radius = min(radius, min(halfSize.x, halfSize.y));
                float2 d = abs(p) - (halfSize - radius);
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - radius;
            }
            float _SweepWidth;
            float _SweepSoftness;
            float _SurfaceAngle;
            float _PulseDuration;
            float _PulseScale;
            float _PulseIntensity;
            float _SweepSpeed;
            float _SweepIntensity;
            float _AnimTime;

            Varyings Vert(AppData input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                float2 localPixels = (input.uv - 0.5) * _InnerSize;
                float2 halfSize = _InnerSize * 0.5;
                float signedDistance = RoundedRectDistance(localPixels, halfSize, _CornerRadius);
                // 光晕用大圆角，避免直角在四角勾出方框轮廓
                float glowDistance = RoundedRectDistance(localPixels, halfSize, max(_GlowCornerRadius, _CornerRadius));

                float glowWidth = max(_OuterGlowWidth, 1.0);
                float falloff = max(_Falloff, 1.05);
                // 向外用幂函数而非 exp：在 glowWidth 处精确归零，网格边界不会留下硬直边
                float outward = pow(saturate(1.0 - max(glowDistance, 0.0) / glowWidth), falloff);
                // 向内只保留一圈窄边光，内部不再被均匀白雾填满；边界处两侧都等于 1，不会出现亮边缝
                float inwardRamp = saturate(1.0 - max(-glowDistance, 0.0) / max(_InnerRimWidth, 1.0));
                float inward = pow(inwardRamp, falloff / max(_InnerRimSpread, 0.05));
                float outerGlow = glowDistance >= 0.0 ? outward : inward;

                // 表面流光沿边缘再软收一段，避免贴着矩形边界收成一条直线
                float edgeSoftness = max(_EdgeSoftness, 0.5);
                float inside = 1.0 - smoothstep(-edgeSoftness, 0.0, signedDistance);

                float2 centered = input.uv - 0.5;
                float aspect = max(_InnerSize.x / max(_InnerSize.y, 1.0), 0.01);
                centered.x *= aspect;
                float angle = radians(_SurfaceAngle);
                float2 direction = float2(cos(angle), sin(angle));
                float projected = dot(centered, direction);
                float extent = length(float2(aspect, 1.0)) * 0.5;
                float pulse = sin(_AnimTime * 6.2831853 / max(_PulseDuration, 0.01));
                float width = max(_SweepWidth * (1.0 + pulse * _PulseScale), 0.01);
                float center = lerp(-extent - width, extent + width, frac(_AnimTime * max(_SweepSpeed, 0.001)));
                float distanceToBand = abs(projected - center);
                float band = 1.0 - smoothstep(width, width + max(_SweepSoftness, 0.01), distanceToBand);
                float core = exp(-pow(distanceToBand / max(width * 0.3, 0.01), 2.0));
                float shimmer = saturate(sin((input.uv.x * 21.0 + input.uv.y * 27.0) + _AnimTime * 5.0) * 0.5 + 0.5);
                float pulseIntensity = 1.0 + pulse * _PulseIntensity;
                float surface = saturate((band * 0.65 + core * 0.9 + band * shimmer * 0.12) * pulseIntensity * _SweepIntensity) * inside;

                fixed4 color = input.color;
                color.rgb = lerp(color.rgb, 1.0, saturate(core * 0.9 + band * 0.12));
                color.a *= saturate(outerGlow * _OuterGlowIntensity * pulseIntensity + surface);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
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
