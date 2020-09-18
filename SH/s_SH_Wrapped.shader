Shader "DJL/Debug/SH Wrapped"
{
	Properties
	{
		[Gamma] _Exposure("Exposure", Range(0.0, 10.0)) = 1.0
		[Toggle(_)] _Gamma("Gamma output", Float) = 0
		[Toggle(_)] _Equirectangular("Equirectangular projection", Float) = 0
		[Header(Spherical Harmonics)]
		_Wrap("Wrap", Range(0, 2)) = 0
		[IntRange]_Method("Method", Range(0, 3)) = 0
		_Generalised("Valve Generalised", Range(0, 1)) = 0
		_Bands("Band strengths", Vector) = (1,1,1,0)
		[Header(Cubemap)]
		[Toggle(_)] _CubemapMode("Cubemap mode", Float) = 0
		_CubemapLod("LOD", Range(0.0, 10.0)) = 0.0
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" "IgnoreProjector" = "True" }
		LOD 100

		Pass
		{
			Name "FORWARD"
			Tags { "LightMode" = "ForwardBase" }
			Cull Off

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			#include "AutoLight.cginc"
			#include "Lighting.cginc"

			#pragma multi_compile_fwdbase

			uniform float _Exposure;
			uniform float _Gamma;
			uniform float _Equirectangular;

			uniform float _Wrap;
			uniform float _Method;
			uniform float _Generalised;
			uniform float3 _Bands;

			uniform float _CubemapMode;
			uniform float _CubemapLod;

			struct appdata
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float3 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float3 normal : TEXCOORD0;
				float2 uv : TEXCOORD1;
			};

			// Implementation used by Unity
			// https://docs.unity3d.com/Manual/LightProbes-TechnicalInformation.html
			// https://www.ppsloan.org/publications/StupidSH36.pdf
			float3 ShadeSH9_stock(float3 normal)
			{
				float3 x0, x1, x2;
				float3 conv = _Bands.xyz; // debugging

				// Constant (L0)
				x0 = float3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w);

				// Linear (L1) polynomial terms
				x1.r = (dot(unity_SHAr.xyz, normal));
				x1.g = (dot(unity_SHAg.xyz, normal));
				x1.b = (dot(unity_SHAb.xyz, normal));

				// 4 of the quadratic (L2) polynomials
				float4 vB = normal.xyzz * normal.yzzx;
				x2.r = dot(unity_SHBr, vB);
				x2.g = dot(unity_SHBg, vB);
				x2.b = dot(unity_SHBb, vB);

				// Final (5th) quadratic (L2) polynomial
				float vC = normal.x * normal.x - normal.y * normal.y;
				x2 += unity_SHC.rgb * vC;

				return x0*conv.x + x1*conv.y + x2*conv.z;
			}

			float3 ShadeSH9_wrapped(float3 normal, float3 conv)
			{
				float3 x0, x1, x2;
				conv *= float3(1, 1.5, 4); // Undo pre-applied cosine convolution
				conv *= _Bands.xyz; // debugging

				// Constant (L0)
				// Band 0 has constant part from 6th kernel (band 1) pre-applied, but ignore for performance
				x0 = float3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w);

				// Linear (L1) polynomial terms
				x1.r = (dot(unity_SHAr.xyz, normal));
				x1.g = (dot(unity_SHAg.xyz, normal));
				x1.b = (dot(unity_SHAb.xyz, normal));

				// 4 of the quadratic (L2) polynomials
				float4 vB = normal.xyzz * normal.yzzx;
				x2.r = dot(unity_SHBr, vB);
				x2.g = dot(unity_SHBg, vB);
				x2.b = dot(unity_SHBb, vB);

				// Final (5th) quadratic (L2) polynomial
				float vC = normal.x * normal.x - normal.y * normal.y;
				x2 += unity_SHC.rgb * vC;

				return x0 * conv.x + x1 * conv.y + x2 * conv.z;
			}
			float3 ShadeSH9_wrappedCorrect(float3 normal, float3 conv)
			{
				const float3 cosconv_inv = float3(1, 1.5, 4); // Inverse of the pre-applied cosine convolution
				float3 x0, x1, x2;
				conv *= cosconv_inv; // Undo pre-applied cosine convolution
				conv *= _Bands.xyz; // debugging

				// Constant (L0)
				x0 = float3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w);
				// Remove the constant part from L2 and add it back with correct convolution
				float3 otherband = float3(unity_SHBr.z, unity_SHBg.z, unity_SHBb.z) / 3.0;
				x0 = (x0 + otherband) * conv.x - otherband * conv.z;

				// Linear (L1) polynomial terms
				x1.r = (dot(unity_SHAr.xyz, normal));
				x1.g = (dot(unity_SHAg.xyz, normal));
				x1.b = (dot(unity_SHAb.xyz, normal));

				// 4 of the quadratic (L2) polynomials
				float4 vB = normal.xyzz * normal.yzzx;
				x2.r = dot(unity_SHBr, vB);
				x2.g = dot(unity_SHBg, vB);
				x2.b = dot(unity_SHBb, vB);

				// Final (5th) quadratic (L2) polynomial
				float vC = normal.x * normal.x - normal.y * normal.y;
				x2 += unity_SHC.rgb * vC;

				return x0 + x1 * conv.y + x2 * conv.z;
			}

			// SH Convolution Functions
			// Code adapted from https://blog.selfshadow.com/2012/01/07/righting-wrap-part-2/
			///////////////////////////

			float3 GeneralWrapSH(float fA) // original unoptimized
			{
				// Normalization factor for our model.
				float norm = 0.5 * (2 + fA) / (1 + fA);
				float4 t = float4(2 * (fA + 1), fA + 2, fA + 3, fA + 4);
				return norm * float3(t.x / t.y, 2 * t.x / (t.y * t.z),
					t.x * (fA * fA - t.x + 5) / (t.y * t.z * t.w));
			}
			float3 GeneralWrapSHOpt(float fA)
			{
				const float4 t0 = float4(-0.047771, -0.129310, 0.214438, 0.279310);
				const float4 t1 = float4( 1.000000,  0.666667, 0.250000, 0.000000);

				float3 r;
				r.xyz = saturate(t0.xxy * fA + t0.yzw);
				r.xyz = -r * fA + t1.xyz;
				return r;
			}

			float3 GreenWrapSHOpt(float fW)
			{
				const float4 t0 = float4(0.0, 1.0 / 4.0, -1.0 / 3.0, -1.0 / 2.0);
				const float4 t1 = float4(1.0, 2.0 / 3.0,  1.0 / 4.0,  0.0);

				float3 r;
				r.xyz = t0.xxy * fW + t0.xzw;
				r.xyz = r.xyz * fW + t1.xyz;
				return r;
			}

			float3 SHConvolution(float wrap)
			{
				float3 a = GeneralWrapSH(wrap);
				float3 b = GreenWrapSHOpt(wrap);
				return lerp(b, a, _Generalised);
			}

			// http://www.geomerics.com/wp-content/uploads/2015/08/CEDEC_Geomerics_ReconstructingDiffuseLighting1.pdf
			float shEvaluateDiffuseL1Geomerics_local(float L0, float3 L1, float3 n)
			{
				// average energy
				float R0 = L0;

				// avg direction of incoming light
				float3 R1 = 0.5f * L1;

				// directional brightness
				float lenR1 = length(R1);

				// linear angle between normal and direction 0-1
				//float q = 0.5f * (1.0f + dot(R1 / lenR1, n));
				//float q = dot(R1 / lenR1, n) * 0.5 + 0.5;
				float q = dot(normalize(R1), n) * 0.5 + 0.5;
				//float q = ((dot(normalize(R1), n) + _Wrap) / (1 + _Wrap)) * 0.5 + 0.5; // Blind attemp to add wrapping
				q = saturate(q); // Silent: Thanks to ScruffyRuffles for the bug identity.

				// power for q
				// lerps from 1 (linear) to 3 (cubic) based on directionality
				float p = 1.0f + 2.0f * lenR1 / R0;

				// dynamic range constant
				// should vary between 4 (highly directional) and 0 (ambient)
				float a = (1.0f - lenR1 / R0) / (1.0f + lenR1 / R0);

				return R0 * (a + (1.0f - a) * (p + 1.0f) * pow(q, p));
			}
			float3 BetterSH9(float3 normal)
			{
				float3 L0 = float3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w);
				float3 nonLinearSH = float3(0, 0, 0);
				nonLinearSH.r = shEvaluateDiffuseL1Geomerics_local(L0.r, unity_SHAr.xyz, normal);
				nonLinearSH.g = shEvaluateDiffuseL1Geomerics_local(L0.g, unity_SHAg.xyz, normal);
				nonLinearSH.b = shEvaluateDiffuseL1Geomerics_local(L0.b, unity_SHAb.xyz, normal);
				nonLinearSH = max(nonLinearSH, 0);
				return nonLinearSH;
			}

			float3 uvToSphere(float2 uv)
			{
				float3 dir;
				dir.x = -sin(uv.x * UNITY_TWO_PI) * sin(uv.y * UNITY_PI);
				dir.y = -cos(uv.y * UNITY_PI);
				dir.z = -cos(uv.x * UNITY_TWO_PI) * sin(uv.y * UNITY_PI);
				return dir;
			}

			v2f vert (appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.normal = UnityObjectToWorldNormal(v.normal);
				o.uv = v.uv;
				return o;
			}
			
			float4 frag (v2f i) : SV_Target
			{
				float3 normal = normalize(lerp(i.normal, uvToSphere(i.uv.xy), _Equirectangular));
				float3 sh_conv =  SHConvolution(_Wrap);
				float3 color = 0;

				// SH
				if (_Method < 1)
					color = ShadeSH9_stock(normal);
				if (_Method >= 1)
					color = ShadeSH9_wrapped(normal, sh_conv);
				if (_Method >= 2)
					color = ShadeSH9_wrappedCorrect(normal, sh_conv);
				if (_Method >= 2.99)
					color = BetterSH9(normal);

				// Reflection Cubemap
				if (_CubemapMode)
					color = DecodeHDR(UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, normal, _CubemapLod) , unity_SpecCube0_HDR);

				if (_Gamma)
					color = pow(color, 2.2);
				return float4(color*_Exposure, 1);
			}
			ENDCG
		}

		UsePass "VertexLit/SHADOWCASTER"
	}

	FallBack Off
}
