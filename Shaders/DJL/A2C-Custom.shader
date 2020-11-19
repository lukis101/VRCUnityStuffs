
// Custom Alpha-to-coverage by Dj Lukis.LT (Unlicense)

// Dithering code logic yoinked from Poiyomi Toon shader (MIT)
// at https://github.com/poiyomi/PoiyomiToonShader

// Edit fragment shader to use texture alpha instead of UV.x

Shader "DJL/A2C-Custom"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Alpha("Alpha Value", Range(0, 1)) = 1
        _DitherGradient("Dither Strength", Range(0, 1)) = 1
        [Toggle(_)]_Gamma("Gamma Adjust", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType"="TransparentCutout" }

        Cull Off
        //Blend Off
        AlphaToMask On
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // 4.5+ Required for direct reading sample count
            #pragma target 4.5

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

            /// Code from Poi Toon:
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
                return dither[r] / 64;
            }
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
                // Edited to be aware of sample count:
                return alpha - dither/ spacing + 1.0/(spacing *2);
            }
            /// --------------- ///

            half4 frag(v2f i, out uint cov : SV_Coverage) : SV_Target
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

                // Center out the steps
                a += 0.5/samplecount;
                // Shift and subtract to get the needed amount of positive bits
                cov = (1u << (uint)(a * samplecount)) - 1;

                return half4(1,1,1, 1);
            }
            ENDCG
        }
    }
}
