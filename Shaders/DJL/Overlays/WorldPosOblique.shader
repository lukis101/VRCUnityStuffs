// A simple unity shader example/template that visualizes world position to demonstrate
// correct depth sampling with oblique view frustums, target use case being mirrors in VRChat

// Algorithm based on the one provided by Alexander V. Popov 
// in "An Efficient Depth Linearization Method for Oblique View Frustums"
// http://jcgt.org/published/0005/04/03/paper.pdf

// This code is released under "The Unlicense", see UNLICENSE file

Shader "DJL/Overlays/World position(oblique frustum aware)"
{
Properties
{
}
SubShader
{
	Tags { "Queue" = "Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
	//Blend SrcAlpha OneMinusSrcAlpha
	//Blend One One
	Cull Off
	ZWrite Off
	//ZTest Always
    //ColorMask RGBA

	Pass
	{
		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag
		#pragma target 3.0
		
		#include "UnityCG.cginc"
		#define PM UNITY_MATRIX_P

		struct appdata
		{
			float4 vertex : POSITION;
			float2 uv : TEXCOORD0;
		};

		struct v2f
		{
			float4 vertex : SV_POSITION;
			float4 worldPos : TEXCOORD1;
			float4 grabPos : TEXCOORD2;
			float4 worldDirection : TEXCOORD3;
		};

		UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

		inline float4 CalculateFrustumCorrection()
		{
			float x1 = -PM._31/(PM._11*PM._34);
			float x2 = -PM._32/(PM._22*PM._34);
			return float4(x1, x2, 0, PM._33/PM._34 + x1*PM._13 + x2*PM._23);
		}
		inline float CorrectedLinearEyeDepth(float z, float B)
		{
			return 1.0 / (z/PM._34 + B);
		}

		v2f vert(appdata v)
		{
			v2f o;
			o.vertex = UnityObjectToClipPos(v.vertex);
			o.worldPos = mul(unity_ObjectToWorld, v.vertex);
			o.grabPos = ComputeGrabScreenPos(o.vertex);
			o.worldDirection.xyz = o.worldPos.xyz - _WorldSpaceCameraPos;
			// pack correction factor into direction w component to save space
			o.worldDirection.w = dot(o.vertex, CalculateFrustumCorrection());
			return o;
		}

		float4 frag(v2f i) : SV_Target
		{
			float perspectiveDivide = 1.0f / i.vertex.w;
			float4 direction = i.worldDirection * perspectiveDivide;
			float2 screenpos = i.grabPos.xy * perspectiveDivide;

			float z = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenpos);

		// Only tested on setup with reversed Z buffer
		#if UNITY_REVERSED_Z
			if (z == 0)
		#else
			if (z == 1)
		#endif
				return float4(0,0,0,1);

			// Linearize depth and use it to calculate background world position
			float depth = CorrectedLinearEyeDepth(z, direction.w);
			float3 worldpos = direction * depth + (_WorldSpaceCameraPos.xyz);

			return float4(frac(worldpos), 1.0f);
		}
		ENDCG
	}
}
FallBack "Diffuse"
}
