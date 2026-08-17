Shader "Visage/Spine/Skeleton Lit Advance" {
	Properties {
		_Cutoff ("Shadow alpha cutoff", Range(0, 1)) = 0.1
		_Color ("Main Color", Vector) = (1,1,1,1)
		[HDR] _AddColor ("Add Color", Vector) = (1,1,1,0)
		[NoScaleOffset] _MainTex ("Main Texture", 2D) = "black" {}
		_DissolveTex ("Dissolve Texture", 2D) = "black" {}
		_DissolveThreshold ("Dissolve Threshold", Range(0, 1)) = 0.1
		_DissolveLineWidth ("Dissolve Width", Float) = 1
		[HDR] _DissolveColor ("Dissolve Color", Vector) = (1,1,1,1)
		[Toggle(_SCAN_HIGHLIGHT)] _Scan_HighLight ("Scan_HighLight", Float) = 0
		_HighLightColor ("HighLight Color", Vector) = (1,1,1,1)
		[Toggle] _HsvShiftValue ("UseHsvShift", Float) = 0
		[Toggle(_STRAIGHT_ALPHA_INPUT)] _StraightAlphaInput ("Straight Alpha Texture", Float) = 0
		[Toggle(_ZWRITE)] _ZWrite ("Depth Write", Float) = 0
		[Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows ("Receive Shadows", Float) = 0
		[Toggle(_DOUBLE_SIDED_LIGHTING)] _DoubleSidedLighting ("Double-Sided Lighting", Float) = 0
		[MaterialToggle(_LIGHT_AFFECTS_ADDITIVE)] _LightAffectsAdditive ("Light Affects Additive", Float) = 0
		[MaterialToggle(_TINT_BLACK_ON)] _TintBlack ("Tint Black", Float) = 0
		_Black ("    Dark Color", Vector) = (0,0,0,0)
		[HideInInspector] _StencilRef ("Stencil Reference", Float) = 1
		[Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Compare", Float) = 8
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType"="Opaque" }
		LOD 200

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			float4 _MainTex_ST;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Vertex_Stage_Output
			{
				float2 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.uv = (input.uv.xy * _MainTex_ST.xy) + _MainTex_ST.zw;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			Texture2D<float4> _MainTex;
			SamplerState sampler_MainTex;
			float4 _Color;

			struct Fragment_Stage_Input
			{
				float2 uv : TEXCOORD0;
			};

			float4 frag(Fragment_Stage_Input input) : SV_TARGET
			{
				return _MainTex.Sample(sampler_MainTex, input.uv.xy) * _Color;
			}

			ENDHLSL
		}
	}
	Fallback "Hidden/InternalErrorShader"
}