#ifndef COLOR_FUNCS
#define COLOR_FUNCS

// Color conversion functions by Ian Taylor,
// source: http://www.chilliant.com/rgb2hsv.html

const static float Epsilon = 1e-10;

float3 LerpColor_RGB(in float3 rgb1, in float3 rgb2, in float interp)
{
	return lerp(rgb1, rgb2, interp);
}

// ----- HSV operations ----- //

float3 HUEtoRGB(in float H)
{
	float R = abs(H * 6 - 3) - 1;
	float G = 2 - abs(H * 6 - 2);
	float B = 2 - abs(H * 6 - 4);
	return saturate(float3(R,G,B));
}
float3 RGBtoHCV(in float3 RGB)
{
	// Based on work by Sam Hocevar and Emil Persson
	float4 P = (RGB.g < RGB.b) ? float4(RGB.bg, -1.0, 2.0/3.0) : float4(RGB.gb, 0.0, -1.0/3.0);
	float4 Q = (RGB.r < P.x) ? float4(P.xyw, RGB.r) : float4(RGB.r, P.yzx);
	float C = Q.x - min(Q.w, Q.y);
	float H = abs((Q.w - Q.y) / (6 * C + Epsilon) + Q.z);
	return float3(H, C, Q.x);
}
float3 RGBtoHSV(in float3 RGB)
{
	float3 HCV = RGBtoHCV(RGB);
	float S = HCV.y / (HCV.z + Epsilon);
	return float3(HCV.x, S, HCV.z);
}
float3 HSVtoRGB(in float3 HSV)
{
	float3 RGB = HUEtoRGB(HSV.x);
	return ((RGB - 1) * HSV.y + 1) * HSV.z;
}

// Code taken from blog post of Alan Zucconi
// https://www.alanzucconi.com/2016/01/06/colour-interpolation/2/
float HueLerp(in float h1, in float h2, in float interp)
{
	float h;
	float d = h2 - h1;
	if (h1 > h2)
	{
		// Swap hues
		float temp = h2;
		h2 = h1;
		h1 = temp;

		d = -d;
		interp = 1 - interp;
	}

	if (d > 0.5) // 180deg
	{
		h1 = h1 + 1; // 360deg
		h = frac( h1 + interp * (h2 - h1) ); // 360deg
	}
	if (d <= 0.5) // 180deg
	{
		h = h1 + interp * d;
	}
	return h;
}

float3 LerpHSV(in float3 hsv1, in float3 hsv2, in float interp)
{
	float hue = HueLerp(hsv1.x, hsv2.x, interp);
	return float3(hue, lerp(hsv1.yz, hsv2.yz, interp));
}
float3 LerpColor_HSV_Simple(in float3 rgb1, in float3 rgb2, in float interp)
{
	float3 hsv1 = RGBtoHSV(rgb1);
	float3 hsv2 = RGBtoHSV(rgb2);
	float3 hsvout = lerp(hsv1, hsv2, interp);
	return HSVtoRGB(hsvout);
}
float3 LerpColor_HSV(in float3 rgb1, in float3 rgb2, in float interp)
{
	float3 hsv1 = RGBtoHSV(rgb1);
	float3 hsv2 = RGBtoHSV(rgb2);	
	float3 hsvout = LerpHSV(hsv1, hsv2, interp);
	return HSVtoRGB(hsvout);
}

// ----- HCY operations ----- //

// The weights of RGB contributions to luminance.
// Should sum to unity.
float3 HCYwts = float3(0.299, 0.587, 0.114);

float3 HCYtoRGB(in float3 HCY)
{
	float3 RGB = HUEtoRGB(HCY.x);
	float Z = dot(RGB, HCYwts);
	if (HCY.z < Z)
	{
		HCY.y *= HCY.z / Z;
	}
	else if (Z < 1)
	{
		HCY.y *= (1 - HCY.z) / (1 - Z);
	}
	return (RGB - Z) * HCY.y + HCY.z;
}
float3 RGBtoHCY(in float3 RGB)
{
	// Corrected by David Schaeffer
	float3 HCV = RGBtoHCV(RGB);
	float Y = dot(RGB, HCYwts);
	float Z = dot(HUEtoRGB(HCV.x), HCYwts);
	if (Y < Z)
	{
		HCV.y *= Z / (Epsilon + Y);
	}
	else
	{
		HCV.y *= (1 - Z) / (Epsilon + 1 - Y);
	}
	return float3(HCV.x, HCV.y, Y);
}

float3 LerpColor_HCY(in float3 rgb1, in float3 rgb2, in float interp)
{
	float3 hcy1 = RGBtoHCY(rgb1);
	float3 hcy2 = RGBtoHCY(rgb2);	
	float3 hsvout = LerpHSV(hcy1, hcy2, interp);
	return HCYtoRGB(hsvout);
}
float3 LerpColor_HCY_Simple(in float3 rgb1, in float3 rgb2, in float interp)
{
	float3 hcy1 = RGBtoHCY(rgb1);
	float3 hcy2 = RGBtoHCY(rgb2);	
	float3 hsvout = lerp(hcy1, hcy2, interp);
	return HCYtoRGB(hsvout);
}

// SOURCE: https://gist.github.com/mattatz/44f081cac87e2f7c8980
/*
 * Conversion between RGB and LAB colorspace.
 * Import from flowabs glsl program : https://code.google.com/p/flowabs/source/browse/glsl/?r=f36cbdcf7790a28d90f09e2cf89ec9a64911f138
 */
float3 rgb2xyz( float3 c ) {
	float3 tmp;
	tmp.x = ( c.r > 0.04045 ) ? pow( ( c.r + 0.055 ) / 1.055, 2.4 ) : c.r / 12.92;
	tmp.y = ( c.g > 0.04045 ) ? pow( ( c.g + 0.055 ) / 1.055, 2.4 ) : c.g / 12.92,
	tmp.z = ( c.b > 0.04045 ) ? pow( ( c.b + 0.055 ) / 1.055, 2.4 ) : c.b / 12.92;
	const float3x3 mat = float3x3(
		0.4124, 0.3576, 0.1805,
		0.2126, 0.7152, 0.0722,
		0.0193, 0.1192, 0.9505 
	);
	return 100.0 * mul(tmp, mat);
}

float3 xyz2lab( float3 c ) {
	float3 n = c / float3(95.047, 100, 108.883);
	float3 v;
	v.x = ( n.x > 0.008856 ) ? pow( n.x, 1.0 / 3.0 ) : ( 7.787 * n.x ) + ( 16.0 / 116.0 );
	v.y = ( n.y > 0.008856 ) ? pow( n.y, 1.0 / 3.0 ) : ( 7.787 * n.y ) + ( 16.0 / 116.0 );
	v.z = ( n.z > 0.008856 ) ? pow( n.z, 1.0 / 3.0 ) : ( 7.787 * n.z ) + ( 16.0 / 116.0 );
	return float3(( 116.0 * v.y ) - 16.0, 500.0 * ( v.x - v.y ), 200.0 * ( v.y - v.z ));
}

float3 rgb2lab( float3 c ) {
	float3 lab = xyz2lab( rgb2xyz( c ) );
	return float3( lab.x / 100.0, 0.5 + 0.5 * ( lab.y / 127.0 ), 0.5 + 0.5 * ( lab.z / 127.0 ));
}

float3 lab2xyz( float3 c ) {
	float fy = ( c.x + 16.0 ) / 116.0;
	float fx = c.y / 500.0 + fy;
	float fz = fy - c.z / 200.0;
	return float3(
		 95.047 * (( fx > 0.206897 ) ? fx * fx * fx : ( fx - 16.0 / 116.0 ) / 7.787),
		100.000 * (( fy > 0.206897 ) ? fy * fy * fy : ( fy - 16.0 / 116.0 ) / 7.787),
		108.883 * (( fz > 0.206897 ) ? fz * fz * fz : ( fz - 16.0 / 116.0 ) / 7.787)
	);
}

float3 xyz2rgb( float3 c ) {
	const float3x3 mat = float3x3(
		3.2406, -1.5372, -0.4986,
		-0.9689, 1.8758, 0.0415,
		0.0557, -0.2040, 1.0570
	);
	float3 v = mul(c / 100.0, mat);
	float3 r;
	r.x = ( v.r > 0.0031308 ) ? (( 1.055 * pow( v.r, ( 1.0 / 2.4 ))) - 0.055 ) : 12.92 * v.r;
	r.y = ( v.g > 0.0031308 ) ? (( 1.055 * pow( v.g, ( 1.0 / 2.4 ))) - 0.055 ) : 12.92 * v.g;
	r.z = ( v.b > 0.0031308 ) ? (( 1.055 * pow( v.b, ( 1.0 / 2.4 ))) - 0.055 ) : 12.92 * v.b;
	return r;
}

float3 lab2rgb( float3 c ) {
	return xyz2rgb( lab2xyz( float3(100.0 * c.x, 2.0 * 127.0 * (c.y - 0.5), 2.0 * 127.0 * (c.z - 0.5)) ) );
}

#endif
