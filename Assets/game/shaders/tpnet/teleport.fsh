#version 330 core

#define TWO_PI 6.28318530718
#define PI 3.14159265359

in vec2 uv;
in float dist;
in vec4 rgbaFog;
in float fogAmount;
in vec4 worldPos;

out vec4 outColor;

uniform sampler2D primaryFb;
uniform sampler2D depthTex;
uniform vec2 invFrameSize;
uniform float time;
uniform float glich;
uniform float stage;
uniform int broken;

const vec3 temporalColor = vec3(0.15, 0.84, 0.64);
const vec3 temporalColor2 = vec3(0.00, 0.65, 0.45);

#include noise2d.ash
#include fogandlight.fsh
#include underwatereffects.fsh

float fbm(vec2 p) {
    float a = 15.0;
    float f = 15.0;
    return a * cnoise2(p * f) 
		 + a * cnoise2(p * f * 2.0) * 0.5
		 + a * cnoise2(p * f * 4.0) * 0.25
		 + a * cnoise2(p * f * 8.0) * 0.1;
}

float circle(vec2 p) {
    float r = length(p); 
    float radius = 0.435 * stage * (1. + sin(time) * 0.01); // White ring size
    float height = 1.0; 
    float width = 150.0;
    return height - pow(r - radius, 2.0) * width;
}

vec4 rusty(vec4 col) {
	vec3 rust = vec3(
		(col.r * 0.393) + (col.g * 0.769) + (col.b * 0.189),
		(col.r * 0.349) + (col.g * 0.686) + (col.b * 0.168),
		(col.r * 0.272) + (col.g * 0.534) + (col.b * 0.131));

    if (bool(broken))
        return vec4(rust, col.a);

	col.r = rust.r * glich + col.r * (1-glich);
	col.g = rust.g * glich + col.g * (1-glich);
	col.b = rust.b * glich + col.b * (1-glich);

    return col;
}

vec4 outerBlur()
{
    vec3 color = vec3(1, 3, 5) * temporalColor;
    vec2 wv = (uv - 0.5) * 0.075;
    float d = length(wv) / stage;
    float w = 0.022 / d;
    w = w * w * w;
    color *= w;
    return vec4(color, (color.r + color.g + color.b)/3 - 0.4);
}

vec4 portal()
{
    vec2 texturePos = uv - 0.5;

    vec4 color = vec4(temporalColor / 1.6, 1); // Background
    float f = length(texturePos) * 2.2; // Inner clip size
	color.a = clamp(pow(1 * stage - f, 0.01), 0, 1) * stage * 1.5; // Clip to circle
    vec2 st  = vec2(
        atan(texturePos.y, texturePos.x) ,
        length(texturePos) * 1. + time * 0.03
    );
    st.x += st.y * 1.1;// - time * 0.3;
    st.x = mod(st.x , TWO_PI);
    
    float n = fbm(st) * 1.5 - 1.0;
    n = max(n, 0.1);
    float circle = max(1.0 - circle(texturePos), 0.0);
    
    float a = n/circle;
    float mask = smoothstep(0.41, 0.4, length(texturePos));
    a *= 0.4 - mask * 0.2;

	float noise = cnoise2(vec2(gl_FragCoord.x / 300, gl_FragCoord.y / 300 - time / 3)) / 50.0
				+ cnoise2(vec2(gl_FragCoord.x / 200, gl_FragCoord.y / 200 - time / 4)) / 75.0
				+ cnoise2(vec2(gl_FragCoord.x / 2  , gl_FragCoord.y / 2   - time    )) / 200.0;

	vec3 portalColor = mix(temporalColor * 0.5, temporalColor2 * 1.1, noise);
	color.r += portalColor.r * a;
	color.g += portalColor.g * a;
	color.b += portalColor.b * a;

    return color;
}

void main()
{
	float x = gl_FragCoord.x * invFrameSize.x;
	float y = gl_FragCoord.y * invFrameSize.y;
	
	float z1 = linearDepth(texture(depthTex, vec2(x,y)).r); // Depth from gbuffer depth, a float32 texture
	float z2 = linearDepth(gl_FragCoord.z);
	float aDiff = max(0, z2 - z1) * 1000;
	if (aDiff > 0) discard; // No visible behind other blocks

    float radius = length(uv - 0.5);
    if (radius > 0.44 * stage)
	{
        outColor = outerBlur();
    }
    else
    {
	    outColor = portal();
    }

    outColor = rusty(outColor);

    vec3 xTangent = dFdx( worldPos.xyz );
    vec3 yTangent = dFdy( worldPos.xyz );
    vec3 norm = normalize( cross( xTangent, yTangent ) );

	float normalShadeIntensity = 0.5;
    float minNormalShade = 0.6;
	float murkiness = getUnderwaterMurkiness();
	if (murkiness > 0)
    {
		outColor = applyFogAndShadowWithNormal(outColor, 0, norm, normalShadeIntensity, minNormalShade, worldPos.xyz);
		outColor.rgb = applyUnderwaterEffects(outColor.rgb, murkiness);	
	}
    else
	{
		outColor = applyFogAndShadowWithNormal(outColor, fogAmount, norm, normalShadeIntensity, minNormalShade, worldPos.xyz);
	}
}
