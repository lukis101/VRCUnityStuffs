
// Custom Alpha-to-coverage by Dj Lukis.LT (Unlicense)

// Dithering code logic yoinked from Amplify and XSToon (MIT)
// https://github.com/Xiexe/Xiexes-Unity-Shaders

// Edit fragment shader to use texture alpha instead of UV.x

Shader "DJL/A2C-Custom"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Alpha("Alpha Value", Range(0, 1)) = 1
        _DitherGradient("Dither Strength", Range(0, 1)) = 1
        [ToggleUI]_Gamma("Gamma Adjust", Float) = 0
        [Toggle(_NATIVE_A2C)]_AlphaToMask("Native A2C", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType"="TransparentCutout" }

        Cull Off
        //Blend Off
        AlphaToMask [_AlphaToMask]
        //LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // 4.5+ Required for GetRenderTargetSampleCount()
            #pragma target 5.0
            #pragma shader_feature _NATIVE_A2C

            #include "UnityCG.cginc"

            uniform float _Alpha;
            uniform float _Gamma;
            uniform float _DitherGradient;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.screenPos = ComputeGrabScreenPos(o.vertex);
                return o;
            }

            /// Dither matrix from "Amplify Shader Editor"
            inline half Dither8x8Bayer(int x, int y)
            {
                const half dither[64] = {
                    1, 49, 13, 61, 4, 52, 16, 64,
                    33, 17, 45, 29, 36, 20, 48, 32,
                    9, 57, 5, 53, 12, 60, 8, 56,
                    41, 25, 37, 21, 44, 28, 40, 24,
                    3, 51, 15, 63, 2, 50, 14, 62,
                    35, 19, 47, 31, 34, 18, 46, 30,
                    11, 59, 7, 55, 10, 58, 6, 54,
                    43, 27, 39, 23, 42, 26, 38, 22
                };
                int r = y * 8 + x;
                return dither[r] / 65; // Use 65 instead of 64 to get better centering
            }
			// https://github.com/Xiexe/Xiexes-Unity-Shaders/blob/2bade4beb87e96d73811ac2509588f27ae2e989f/Main/CGIncludes/XSHelperFunctions.cginc#L120
            half2 calcScreenUVs(float4 screenPos)
            {
                half2 uv = screenPos / (screenPos.w + 0.0000000001);
                #if UNITY_SINGLE_PASS_STEREO
                    uv.xy *= half2(_ScreenParams.x * 2, _ScreenParams.y);
                #else
                    uv.xy *= _ScreenParams.xy;
                #endif
    
                return uv;
            }
            
            half applyDithering(half alpha, float4 screenPos, half spacing)
            {
                half2 screenuv = calcScreenUVs(screenPos).xy;
                half dither = Dither8x8Bayer(fmod(screenuv.x, 8), fmod(screenuv.y, 8));
                return alpha + (0.5 - dither)/spacing;
            }


            half4 frag(v2f i
#ifndef _NATIVE_A2C	
			, out uint cov : SV_Coverage
#endif
			) : SV_Target
            {
                half a = _Alpha;

                // Demo with just UV
                a *= i.uv.x;

                // Or to use texture instead:
                //half4 tex = tex2D(_MainTex, i.uv);
                //a *= tex.r;

                if (_Gamma)
                    a = pow(a, 2.2);

                // Get the amount of MSAA samples enabled
                uint samplecount = GetRenderTargetSampleCount();

                a = applyDithering(a, i.screenPos, samplecount / _DitherGradient);
				
#ifndef _NATIVE_A2C	
                // center out the steps
                a = a * samplecount + 0.5;

                // Shift and subtract to get the needed amount of positive bits
                cov = (1u << (uint)(a)) - 1u;

                // Output 1 as alpha, otherwise result would be a^2
				a = 1;
#endif
                return half4(1,1,1, a);
            }
            ENDCG
        }
    }
}
