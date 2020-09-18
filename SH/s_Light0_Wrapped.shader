Shader "DJL/Debug/Light0 Wrapped"
{
	Properties
	{
		_Wrap("Wrap", Range(0, 2)) = 0
		[Toggle(_)] _ConserveEnergy("ConserveEnergy", Float) = 0
		[Toggle(_)] _Equirectangular("Equirectangular projection", Float) = 0
		_Generalised("Valve Generalised", Range(0, 1)) = 0
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" "IgnoreProjector"="True" }
		LOD 100

		Pass
		{
			Name "FORWARD"
			Tags { "LightMode"="ForwardBase" }
			Cull Off

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"
			#include "AutoLight.cginc"
			#include "Lighting.cginc"

			#pragma multi_compile_fwdbase

			uniform float _Wrap;
			uniform float _ConserveEnergy;
			uniform float _Generalised;
			uniform float _Equirectangular;
			
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
				float4 worldPos : TEXCOORD1;
				float2 uv : TEXCOORD2;
			};

			v2f vert (appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.normal = UnityObjectToWorldNormal(v.normal);
				o.worldPos = mul(unity_ObjectToWorld, v.vertex);
				o.uv = v.uv;
				return o;
			}

			float3 uvToSphere(float2 uv)
			{
				float3 dir;
				dir.x = -sin(uv.x * UNITY_TWO_PI) * sin(uv.y * UNITY_PI);
				dir.y = -cos(uv.y * UNITY_PI);
				dir.z = -cos(uv.x * UNITY_TWO_PI) * sin(uv.y * UNITY_PI);
				return dir;
			}

			// Green’s model with [now optional] energy conservation
			// http://blog.stevemcauley.com/2011/12/03/energy-conserving-wrapped-diffuse/
			float GreenWrapConserving(float fCosTheta, float wrap)
			{
				return max(0, fCosTheta + wrap) / ((1.0 + wrap) * lerp(1, 1.0 + wrap, _ConserveEnergy));
			}
			// Generalised model for simple point and directional light sources.
			// http://www.cim.mcgill.ca/~derek/files/jgt_wrap.pdf
			float GeneralWrap(float fCosTheta, float wrap)
			{
				return pow(max(0, fCosTheta + wrap) / (1.0 + wrap), 1.0 + wrap);
			}
			// Valve half lambert
			// https://steamcdn-a.akamaihd.net/apps/valve/2006/SIGGRAPH06_Course_ShadingInValvesSourceEngine.pdf
			float ValveWrap(float fCosTheta, float wrap)
			{
				return pow(max(0, fCosTheta + wrap)*0.5 , 2);
			}

			float4 frag (v2f i) : SV_Target
			{
				float3 normal = normalize(lerp(i.normal, uvToSphere(i.uv.xy), _Equirectangular));
				float3 lightDirection = normalize(lerp(_WorldSpaceLightPos0.xyz, _WorldSpaceLightPos0.xyz - i.worldPos.xyz, _WorldSpaceLightPos0.w));
				float NdotL = dot(normal, lightDirection);
				float greenwrap = GreenWrapConserving(NdotL, _Wrap);
				float generalwrap = lerp(ValveWrap(NdotL, _Wrap), GeneralWrap(NdotL, _Wrap), _ConserveEnergy);
				float wrapped = lerp(greenwrap, generalwrap, _Generalised);
				return float4(_LightColor0.rgb * saturate(wrapped), 1);
			}
			ENDCG
		}
		UsePass "VertexLit/SHADOWCASTER"
	}
	FallBack Off
}
