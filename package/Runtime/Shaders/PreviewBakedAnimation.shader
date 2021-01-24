﻿Shader "BakedAnimation/PreviewAnimation"
{
	Properties
	{
		_Color ("Color", Color) = (.5,.5,.5,1)
		_Glossiness ("Smoothness", Range(0,1)) = 0.2
		_Metallic ("Metallic", Range(0,1)) = 0.0
		_EmissionFactor ("Emission Factor", float) = .2
		[Header(Skinning)]
		[KeywordEnum(Four, Three, Two, One)] Skin_Quality("Skin Quality", Float) = 0
	}
	SubShader
	{
		Tags
		{
			"RenderType"="Opaque"
		}
		LOD 200 

		CGPROGRAM
		#pragma surface surf Standard fullforwardshadows vertex:vert addshadow
		#pragma target 3.5 
		#pragma multi_compile_instancing
		#pragma instancing_options procedural:setup
		#pragma multi_compile SKIN_QUALITY_FOUR SKIN_QUALITY_THREE SKIN_QUALITY_TWO SKIN_QUALITY_ONE
		#include "Include/Skinning.cginc" 


		half _Glossiness;
		half _Metallic;
		half _EmissionFactor;
		fixed4 _Color;

		struct appdata
		{
			float4 vertex : POSITION;
			float3 normal : NORMAL;
			float2 texcoord : TEXCOORD0;
			float2 texcoord1 : TEXCOORD1;
			float2 texcoord2 : TEXCOORD2;
			float4 tangent : TANGENT;
			uint vertex_id : SV_VertexID;
			uint instance_id : SV_InstanceID;
			UNITY_VERTEX_INPUT_INSTANCE_ID
		};

		struct Input
		{
			float2 uv_MainTex;
			float4 color;
			UNITY_VERTEX_INPUT_INSTANCE_ID
		};


		#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
        StructuredBuffer<float4x4> positions;
		#endif
  
		void setup()
		{
			#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            float4x4 data = positions[unity_InstanceID];
            unity_ObjectToWorld = data;
            unity_WorldToObject = unity_ObjectToWorld;
            unity_WorldToObject._14_24_34 *= -1;
            unity_WorldToObject._11_22_33 = 1.0f / unity_WorldToObject._11_22_33;
			#endif
		}

		// #if defined(SHADER_API_D3D11) || defined(SHADER_API_METAL)
		// StructuredBuffer<BoneWeight> _BoneWeights;
		// StructuredBuffer<Bone> _Animations;
		// #endif

		void vert(inout appdata v, out Input result)
		{
			UNITY_SETUP_INSTANCE_ID(v);
			UNITY_INITIALIZE_OUTPUT(Input, result);

			const TextureClipInfo clip = ToTextureClipInfo(_CurrentAnimation);
			// v.vertex = skin(v.vertex, v.vertex_id, _BoneWeights, _Animations, _CurrentAnimation.x, _CurrentAnimation.y, _CurrentAnimation.z);
			skin(v.vertex, v.normal, v.vertex_id, _Skinning, _Skinning_TexelSize, _Animation, _Animation_TexelSize,
				clip.IndexStart, clip.Frames, (_Time.y * (clip.FramesPerSecond)));
		}

		void surf(Input IN, inout SurfaceOutputStandard o)
		{
			fixed4 col = _Color;
			o.Albedo = col.rgb;
			o.Metallic = _Metallic;
			o.Smoothness = _Glossiness;
			o.Emission = _EmissionFactor;
			o.Alpha = col.a;
		}
		ENDCG

		//UsePass "Standard/SHADOWCASTER"
	}
	FallBack "Diffuse"
}