// 「物料制作」贴纸选中描边：基于精灵 alpha 边缘的 UI 描边 Shader（贴纸大小/形状不定，故用 shader 而非固定 Image 描边）。
// 用法：用本 Shader 建一个 Material，赋给 FactoryMoldStickerView 的 outlineMaterial；选中时把它换到贴纸 Image.material，
// 取消选中换回默认 UI 材质即可（_OutlineWidth>0 才出描边）。
Shader "UI/FactoryStickerOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0.55,1,0.2,1)
        _OutlineWidth ("Outline Width (px)", Range(0,12)) = 4
        _AlphaThreshold ("Alpha Threshold", Range(0,1)) = 0.1

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
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            fixed4 _OutlineColor;
            float _OutlineWidth;
            float _AlphaThreshold;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            // 环形 16 方向采样，取邻域最大 alpha（描边宽度内是否有不透明像素）
            half MaxRingAlpha(float2 uv, float2 o)
            {
                half m = 0;
                // 4 正交
                m = max(m, tex2D(_MainTex, uv + float2( o.x, 0)).a);
                m = max(m, tex2D(_MainTex, uv + float2(-o.x, 0)).a);
                m = max(m, tex2D(_MainTex, uv + float2(0,  o.y)).a);
                m = max(m, tex2D(_MainTex, uv + float2(0, -o.y)).a);
                // 4 对角 (45°)
                const float k = 0.70711;
                m = max(m, tex2D(_MainTex, uv + float2( o.x*k,  o.y*k)).a);
                m = max(m, tex2D(_MainTex, uv + float2(-o.x*k,  o.y*k)).a);
                m = max(m, tex2D(_MainTex, uv + float2( o.x*k, -o.y*k)).a);
                m = max(m, tex2D(_MainTex, uv + float2(-o.x*k, -o.y*k)).a);
                // 8 半角 (22.5° / 67.5°)，填满圆周让描边更平滑
                const float a2 = 0.92388, b2 = 0.38268;
                m = max(m, tex2D(_MainTex, uv + float2( o.x*a2,  o.y*b2)).a);
                m = max(m, tex2D(_MainTex, uv + float2(-o.x*a2,  o.y*b2)).a);
                m = max(m, tex2D(_MainTex, uv + float2( o.x*a2, -o.y*b2)).a);
                m = max(m, tex2D(_MainTex, uv + float2(-o.x*a2, -o.y*b2)).a);
                m = max(m, tex2D(_MainTex, uv + float2( o.x*b2,  o.y*a2)).a);
                m = max(m, tex2D(_MainTex, uv + float2(-o.x*b2,  o.y*a2)).a);
                m = max(m, tex2D(_MainTex, uv + float2( o.x*b2, -o.y*a2)).a);
                m = max(m, tex2D(_MainTex, uv + float2(-o.x*b2, -o.y*a2)).a);
                return m;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 sprite = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                half outlineA = 0;
                if (_OutlineWidth > 0)
                {
                    float2 o = _MainTex_TexelSize.xy * _OutlineWidth;
                    half ringMax = MaxRingAlpha(IN.texcoord, o);

                    // 外缘：邻域是否有不透明像素（在阈值附近做平滑，抗锯齿）
                    half ring = smoothstep(_AlphaThreshold * 0.5, _AlphaThreshold, ringMax);
                    // 内缘：自身越不透明描边越弱（描边只落在精灵 alpha 边缘的透明侧）
                    half inside = smoothstep(_AlphaThreshold, _AlphaThreshold + 0.20, sprite.a);
                    outlineA = saturate(ring * (1 - inside));
                }

                half4 outCol = _OutlineColor;
                half4 color;
                // 精灵盖在描边之上：不透明处显示精灵，边缘透明侧显示平滑描边
                color.rgb = lerp(outCol.rgb, sprite.rgb, sprite.a);
                color.a   = max(sprite.a, outlineA * outCol.a);

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
