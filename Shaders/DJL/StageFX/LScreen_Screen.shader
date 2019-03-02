// Render loop/processing shader for dot matrix stage FX
// Copyright (c) 2019 Dj Lukis.LT
// MIT license (see LICENSE in https://github.com/lukis101/VRCUnityStuffs)

Shader "DJL/StageFX/Screen"
{
Properties
{
	[NoScaleOffset]
	_Buffer ("Buffer", 2D) = "black" {}
	[Enum(UnityEngine.Rendering.CullMode)] _CullMode("Cull Mode", Float) = 0 // Off
	[Toggle(_)] _Shape("Circle shape", Float) = 0
	_Radius ("Segment radius",  Range(0.0,  1.0)) = 0.4
	_Glow ("Glow",  Range( 1.0,  5.0)) = 1.0
	_Mult ("Multiplier (fading)", Range (0.0, 1.0)) = 1.0

	[Header(Modifiers)]
	_HueOffset ("Hue shift",  Range( 0.0,  1.0)) = 0.0
	_Desaturate ("Desaturate",  Range( 0.0,  1.0)) = 0.0
	
	[Header(Colors)]
	[HDR]_FrontColor("Front color", Color) = (1,1,1,1)
	[HDR]_TrailColor1("Trail color 1(start)", Color) = (1,0,0,1)
	[HDR]_TrailColor2("Trail color 2(middle)", Color) = (0,1,0,1)
	[HDR]_TrailColor3("Trail color 3(end)", Color) = (0,0,1,1)

	// [Header(Stencil)]
	// [IntRange] _Stencil ("ID", Range(0,255)) = 0
	// [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Comparison", Int) = 3
	// [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Int) = 0
}
SubShader
{
	Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }

	/*Stencil
	{
		Ref [_Stencil]
		Comp [_StencilComp]
		Pass keep
		Fail keep
		ZFail keep
	}*/
	Pass
	{
		Name "FORWARD"
		Tags { "LightMode"="ForwardBase" }
		Blend One One
		Cull [_CullMode]
		ZWrite Off
		//ZTest [_ZTest]
		//AlphaToMask On
		//ColorMask RGBA

		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag
		#pragma multi_compile_instancing
		#pragma target 3.0

		#include "UnityCG.cginc"
		#include "ColorFuncs.cginc"
		#define DIM_EDGES
		#define ANTIALIAS
		//#define DEBUG_ANTIALIAS

		Texture2D<half> _Buffer;
		float4 _Buffer_TexelSize;

		uniform float _Shape;
		uniform float _Glow;
		uniform float _Mult;
		//uniform float _HueOffset;
		//uniform float _Desaturate;
		UNITY_INSTANCING_BUFFER_START(Props)
			UNITY_DEFINE_INSTANCED_PROP(float4, _FrontColor)
			UNITY_DEFINE_INSTANCED_PROP(float4, _TrailColor1)
			UNITY_DEFINE_INSTANCED_PROP(float4, _TrailColor2)
			UNITY_DEFINE_INSTANCED_PROP(float4, _TrailColor3)
			UNITY_DEFINE_INSTANCED_PROP(float, _Radius)
			UNITY_DEFINE_INSTANCED_PROP(float, _HueOffset)
			UNITY_DEFINE_INSTANCED_PROP(float, _Desaturate)
		UNITY_INSTANCING_BUFFER_END(Props)

		struct vs_in
		{
			float4 vertex : POSITION;
			float2 uv : TEXCOORD0;
			UNITY_VERTEX_INPUT_INSTANCE_ID
		};
		struct fs_in
		{
			float4 pos : SV_POSITION;
			float4 uv  : TEXCOORD0;
			UNITY_VERTEX_INPUT_INSTANCE_ID
			UNITY_VERTEX_OUTPUT_STEREO
		};

		float3 LerpHSV_(in float3 c1, in float3 c2, in float value)
		{
			static const float DESAT_DIM = 0.9; // brightness when desaturated
			float3 hsv1 = RGBtoHSV(c1);
			float3 hsv2 = RGBtoHSV(c2);
			float3 hsvout = float3(HueLerp(hsv1.x,hsv2.x,value),lerp(hsv1.y,hsv2.y,value),lerp(hsv1.z,hsv2.z,value));
			float desat = UNITY_ACCESS_INSTANCED_PROP(Props, _Desaturate);
			hsvout.y *= (1-desat);
			hsvout.z *= DESAT_DIM+(1-desat)*(1-DESAT_DIM);
			hsvout.x = frac(hsvout.x + UNITY_ACCESS_INSTANCED_PROP(Props, _HueOffset)); // shift and wrap to unit range
			return HSVtoRGB(hsvout);
		}

		//--- Vertex shader ---//
		fs_in vert(vs_in v)
		{
			fs_in o;
			UNITY_SETUP_INSTANCE_ID(v);
			UNITY_TRANSFER_INSTANCE_ID(v, o);
			UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
			o.pos = UnityObjectToClipPos(v.vertex);
			o.uv  = float4(v.uv.xy, v.uv.xy * _Buffer_TexelSize.zw);
			return o;
		}

		//--- Fragment shader ---//
		half4 frag (fs_in i) : SV_Target
		{
			UNITY_SETUP_INSTANCE_ID(i);
			static const fixed VALUE_STEP = 1.0/256.0;
			static const uint  FRONT_STEPS = 4;
			static const float SIDE_FALLOFF = 0.3;
			
			const static float MAIN_RANGE = 1.0 - VALUE_STEP*(FRONT_STEPS+1);
			float2 fragpos = i.uv.zw; // in pixels
			int2 addr = fragpos; // discard fractional part
			float2 segmcenter = addr + float2(0.5, 0.5);
			float segmdist_sq = max(abs(segmcenter.x-fragpos.x),abs(segmcenter.y-fragpos.y));
			float segmdist_c = distance(fragpos, segmcenter);
			float segmdist = lerp(segmdist_sq, segmdist_c, _Shape);

			bool reflprobe = _ScreenParams.x < 257; // Prevent shaping in low resolution

			float radius = UNITY_ACCESS_INSTANCED_PROP(Props, _Radius);
#ifdef ANTIALIAS
			float2 fw = fwidth(i.uv.xy)*_ScreenParams.xy*0.125;
			float duv = saturate((fw.x+fw.y)*0.5-0.5 + reflprobe);
			radius = lerp(radius, 1, duv);
			//return float4( duv,0,0,1);
	#ifdef DEBUG_ANTIALIAS // Bypass to compare anti-alias logic
			if (i.uv.x < 0.5)
				radius = UNITY_ACCESS_INSTANCED_PROP(Props, _Radius);
	#endif
#endif

			half input = _Buffer.Load(int3(addr.xy, 0));
			half3 color = 0;
			
			float alpha = segmdist < radius;
			alpha *= input > 0;
#ifdef ANTIALIAS
			alpha *= 1.0 + reflprobe*0.9; // Compensate brightness in reflection probes
#endif

			half input_adj = input / MAIN_RANGE; // compensate for range reduction by 'front' color

			half3 col1 = 0;
			half3 col2 = UNITY_ACCESS_INSTANCED_PROP(Props, _TrailColor2).rgb;
			half colorratio = 0;
			if (input_adj > 0.5) // Color 1 to 2
			{
				half3 cfront  = UNITY_ACCESS_INSTANCED_PROP(Props, _FrontColor ).rgb;
				half3 ctrail1 = UNITY_ACCESS_INSTANCED_PROP(Props, _TrailColor1).rgb;
				col1 = lerp(cfront, ctrail1, input_adj < MAIN_RANGE);
				colorratio = 1-(input_adj-0.5)*2;
				// workaround for color conversions not preserving full black
				if ((col1.r+col1.g+col1.b) < VALUE_STEP)
					alpha = 0;
			}
			else // Color 2 to 3
			{
				colorratio = input_adj*2;
				col1 = UNITY_ACCESS_INSTANCED_PROP(Props, _TrailColor3).rgb;
			}
			color = LerpHSV_(col1, col2, colorratio);

#ifdef DIM_EDGES
			// Dim at screen horizontal borders
			alpha *= 2 - abs(i.uv.x-0.5)*4;
#endif
			// Smooth out dot edges
			//float radius2 = radius*0.15;
			//alpha *= (1-saturate((segmdist-radius+radius2)/radius2));

#ifdef ANTIALIAS
	#ifdef DEBUG_ANTIALIAS // Bypass to compare anti-alias logic
			if (i.uv.x > 0.5)
	#endif
				alpha *= clamp(1-duv*5, 0.1, 1);
#endif

			half3 adjustedcol = pow(color,2)*_Glow;
			return half4(adjustedcol*alpha*_Mult, 1);
		}
		ENDCG
	}
}
//FallBack "Diffuse"
}