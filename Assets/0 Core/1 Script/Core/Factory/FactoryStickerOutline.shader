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

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // 当前像素接近透明、但其邻域(按描边宽度偏移)有不透明像素 → 画描边色（沿精灵 alpha 边缘）
                if (_OutlineWidth > 0 && color.a < _AlphaThreshold)
                {
                    float2 o = _MainTex_TexelSize.xy * _OutlineWidth;
                    half a =
                        tex2D(_MainTex, IN.texcoord + float2( o.x, 0)).a +
                        tex2D(_MainTex, IN.texcoord + float2(-o.x, 0)).a +
                        tex2D(_MainTex, IN.texcoord + float2(0,  o.y)).a +
                        tex2D(_MainTex, IN.texcoord + float2(0, -o.y)).a +
                        tex2D(_MainTex, IN.texcoord + float2( o.x,  o.y)).a +
                        tex2D(_MainTex, IN.texcoord + float2(-o.x,  o.y)).a +
                        tex2D(_MainTex, IN.texcoord + float2( o.x, -o.y)).a +
                        tex2D(_MainTex, IN.texcoord + float2(-o.x, -o.y)).a;
                    if (a > 0)
                        color = _OutlineColor;
                }

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
