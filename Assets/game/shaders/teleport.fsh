#version 330 core

const float PI = 3.1415926535897932384626433832795;

in vec2 uv;
in float dist;
in vec4 rgbaFog;
in float fogAmount;


out vec4 outColor;

uniform int riftIndex;
uniform sampler2D primaryFb;
uniform sampler2D depthTex;
uniform vec2 invFrameSize;
uniform float counter;
uniform float counterSmooth;

#include noise2d.ash
#include fogandlight.fsh


void main()
{
	float x = gl_FragCoord.x * invFrameSize.x;
	float y = gl_FragCoord.y * invFrameSize.y;
	
	float z1 = linearDepth(texture(depthTex, vec2(x,y)).r); //depth from gbuffer depth, a float32 texture
	float z2 = linearDepth(gl_FragCoord.z);
	
	float aDiff = max(0, z2 - z1) * 1000;
	if (aDiff > 2) discard;
	
	float f = length(uv - 0.5) * 2;
	
	float noise = 
		cnoise2(vec2(gl_FragCoord.x / 300, gl_FragCoord.y / 300 - counter / 3))  / 50.0
		+ cnoise2(vec2(gl_FragCoord.x / 200, gl_FragCoord.y / 200 - counter / 4))  / 75.0
		+ cnoise2(vec2(gl_FragCoord.x / 2, gl_FragCoord.y / 2 - counter)) / 200.0
	;
	
	vec4 col = texture(primaryFb, vec2(x,y) + noise);
	
	float seed = riftIndex/10.0 + counterSmooth / 10.0;
	float spikeNoise = cnoise2(vec2(riftIndex, f * counterSmooth/100.0)) + cnoise2(vec2(riftIndex + 4, f * counterSmooth/10.0)) / 15.0;
	float angle = mod(atan(uv.y - 0.5, uv.x - 0.5) + spikeNoise, 2*3.14159);
	
	float k = cnoise2(vec2(angle * 20, seed))/4 + cnoise2(vec2(angle * 5, seed))/4 + cnoise2(vec2(seed, angle));
	k = k / 2 + 1;
	k = (k - 0.2) * 1.3;
	
	float b = pow(1.2 - f * k, 1.2);
	
	b *= pow(1 - f, 0.1);
	
	col.a = clamp(b, 0, 1);
	col.a = pow(col.a, 4);
	col.a = clamp(col.a - aDiff, 0, 1);
	
	col.a -= clamp((dist * (1 + fogAmount/2.0) - 50) / 40, 0, 0.7 + fogAmount * 0.3);
	
	if (col.a < 0.02) discard;
	
	vec3 rust = vec3(
			(col.r * 0.393) + (col.g * 0.769) + (col.b * 0.189),
			(col.r * 0.349) + (col.g * 0.686) + (col.b * 0.168),
			(col.r * 0.272) + (col.g * 0.534) + (col.b * 0.131)
	);
	
	float fg = 1 + clamp(f * 20 - 2, 0.13, 1) * cnoise2(vec2(angle*20, seed));
	
	rust.r *= fg;
	rust.g /= fg;
	
	
	rust = vec3(
			(rust.r * 0.393) + (rust.g * 0.769) + (rust.b * 0.189),
			(rust.r * 0.349) + (rust.g * 0.686) + (rust.b * 0.168),
			(rust.r * 0.272) + (rust.g * 0.534) + (rust.b * 0.131)
	);
	
	float gdiff = min(col.g, 0.1);
	float bdiff = min(col.b, 0.1);
	rust.g -= gdiff;
	rust.b -= bdiff;
	rust.r += gdiff + bdiff;
	col.rgb = mix(col.rgb, rust, 1);
	col.rgb /= 4;
	
	outColor = applyFog(col, fogAmount);
}