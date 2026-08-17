// 原作同名 shader 的重写实现。
// AssetRipper 只能导出编译后字节码的「属性声明 + 直接返回 _MainTex」占位版本，
// 这里按属性表和材质参数还原其行为：
//   _MainTex   —— Tilemap/Sprite 运行时提供的形状遮罩（纯白 + alpha 轮廓）
//   _SecondTex —— 真正的地表贴图，按世界坐标 XZ 平铺，所以纹理跨格子连续、不逐格重复
//   输出       —— SecondTex 的颜色，用遮罩 alpha 做裁切
// 原名叫 Lit，但工程里的 2.5D 场景是平涂无光照风格（场景中也没有灯），故按 unlit 实现。
Shader "Visage/Sprite/Lit_WorldTile_Cut"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _SecondTex ("Second Texture", 2D) = "white" {}
        _SecondTexScale ("SecondTexScale", Range(2, 16)) = 8
        [Toggle(_OPEN_CUTOFF)] _openCutoff ("Open Cutoff", Float) = 0
        _Cutoff ("AlphaCutOff", Range(0, 1)) = 0.5
        // Tilemap 是水平面，直接按朝上着色；Halfpace 斜台要用自身法线，侧面才有明暗
        [Toggle] _UseMeshNormal ("Use Mesh Normal (Halfpace)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
        }
        LOD 200

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            // 必须留在 AlphaTest 队列并写深度：水面 shader 要靠 _CameraDepthTexture 做岸边泡沫、
            // 靠 _CameraOpaqueTexture 做折射，地面进不了这两张图的话两个效果都会失效。
            Cull Off
            ZWrite On
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local_fragment _OPEN_CUTOFF

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
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                half4  color      : COLOR;
            };

            TEXTURE2D(_MainTex);   SAMPLER(sampler_MainTex);
            TEXTURE2D(_SecondTex); SAMPLER(sampler_SecondTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _SecondTex_ST;
                float  _SecondTexScale;
                float  _Cutoff;
                float  _openCutoff;
                float  _UseMeshNormal;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                // Tilemap 的面朝上，法线就是 (0,1,0)；Halfpace 斜台的侧面靠它才有明暗
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _MainTex);
                o.color = input.color;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 形状遮罩：只用 alpha，RGB 在原始图集里本来就是纯白
                half4 mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                #if defined(_OPEN_CUTOFF)
                    clip(mask.a - _Cutoff);
                #else
                    clip(mask.a - 0.01);
                #endif

                // 世界空间平铺：格子在 XZ 平面上，所以取 xz
                float2 worldUV = input.positionWS.xz / max(_SecondTexScale, 0.0001);
                half4 ground = SAMPLE_TEXTURE2D(_SecondTex, sampler_SecondTex, worldUV);

                // 和水面用同一套光照（环境光 + 主光漫反射），两者才协调
                float3 N = (_UseMeshNormal > 0.5) ? normalize(input.normalWS) : float3(0, 1, 0);
                Light mainLight = GetMainLight();
                half3 lighting = SampleSH(N) + mainLight.color * saturate(dot(N, mainLight.direction));

                return half4(ground.rgb * input.color.rgb * lighting, 1.0);
            }
            ENDHLSL
        }

        // URP 走深度预通道时也要把地面写进 _CameraDepthTexture，否则水面的岸边泡沫检测不到岛
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
            #pragma shader_feature_local_fragment _OPEN_CUTOFF

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct DepthVaryings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _SecondTex_ST;
                float  _SecondTexScale;
                float  _Cutoff;
                float  _openCutoff;
                float  _UseMeshNormal;
            CBUFFER_END

            DepthVaryings depthVert(DepthAttributes input)
            {
                DepthVaryings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return o;
            }

            half4 depthFrag(DepthVaryings input) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                #if defined(_OPEN_CUTOFF)
                    clip(a - _Cutoff);
                #else
                    clip(a - 0.01);
                #endif
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
