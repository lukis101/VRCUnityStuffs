// Render loop/processing shader for dot matrix stage FX
// This code is released under "The Unlicense", see UNLICENSE file

Shader "DJL/StageFX/Processing(Lite)"
{
Properties
{
	[NoScaleOffset]
	_Buffer ("Main buffer", 2D) = "black" {}
	_Input ("Input (depth texture)", 2D) = "black" {}
	[IntRange] _Decay ("Decay", Range (1, 30)) = 5
	[KeywordEnum(Trail, Fire, Gravity, Glow)] _Mode ("Effect type", Int) = 0
	[Toggle(_)] _Sparkle("Sparkle", Int) = 0
	[Toggle(_HIDING_ON)] _Hide("Hide from regular view", Int) = 0
}
SubShader
{
	Tags { "Queue"="Transparent" "RenderType"="Overlay" "PreviewType"="Plane" "IgnoreProjector"="True" "DisableBatching"="True" }
	Blend Off
	Cull Off
	ZWrite Off
	ZTest Always
	//ColorMask RGB

	Pass
	{
		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag
		#pragma target 3.0

		#pragma shader_feature _HIDING_ON
		#include "UnityCG.cginc"

		Texture2D<half> _Input;
		float4 _Input_ST;
		Texture2D<float> _Buffer;
		float4 _Buffer_TexelSize;

		uniform int _Decay;
		uniform float _Mode;

		struct vs_in
		{
			float4 vertex : POSITION;
			float4 uv : TEXCOORD0;
		};
		struct fs_in
		{
			float4 pos : SV_POSITION;
			float4 uv  : TEXCOORD0;
			float2 iuv : TEXCOORD1;
			float2 adj : TEXCOORD2;
		};

		//--- Vertex shader ---//
		fs_in vert(vs_in v)
		{
			fs_in o;
			if (_ProjectionParams.z == 1) // fill the target camera
			{
				o.pos = float4(v.uv.x*2-1, 1-v.uv.y*2, 0.5, 1);
			}
			else
			{
			#if _HIDING_ON
				o.pos = float4(0,0,-2,1); // out of clip range
			#else
				o.pos = UnityObjectToClipPos(v.vertex); // regular quad for the rest 
			#endif
			}
			float aratio = _Buffer_TexelSize.z / _Buffer_TexelSize.w;
			// xy - aspect ratio adjusted, zw - pixel-scale coords
			o.uv  = float4(v.uv.x*aratio, v.uv.y, v.uv.xy*_Buffer_TexelSize.zw);
			o.iuv = TRANSFORM_TEX(v.uv.xy, _Input)*_Buffer_TexelSize.zw;
			o.adj.x = 90.0 / unity_DeltaTime.w; // fps compensation
			o.adj.y = _Decay * 2.6; // tweaked decay value
			return o;
		}

		//--- Fragment shader ---//
		fixed frag (fs_in i) : SV_Target
		{
			static const fixed STEP = 1.0/255.0;
			static const fixed RANGE = 1.0-STEP;
			static const fixed SCROLLRATIO = 0.5;

			fixed fpscompensate = i.adj.x;
			fixed decayadjusted = i.adj.y;
			fixed last = _Buffer.Load(int3(i.uv.zw, 0)).r;
			fixed inp  = _Input.Load(int3(i.iuv, 0)).r > 0; // discard depth
			fixed noise = 0;
			
			// TODO use value of 0 when uvs out of range
			if (_Mode > 0.5) // Gravity
			{
				last = lerp(last, _Buffer.Load(int3(i.uv.zw + int2(0,1), 0)).r, SCROLLRATIO);
			}

			fixed next = max(last*RANGE - STEP*_Decay*fpscompensate, inp);

			return next;
		}
		ENDCG
	}
}
}