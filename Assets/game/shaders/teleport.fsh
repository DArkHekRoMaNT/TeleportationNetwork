#version 330 core

#define TWO_PI 6.28318530718
#define PI 3.14159265359

in vec2 uv;
in float dist;
in vec4 rgbaFog;
in float fogAmount;


out vec4 outColor;
// ? out vev4 outGlow;

uniform int riftIndex;
uniform sampler2D primaryFb;
uniform sampler2D depthTex;
uniform vec2 invFrameSize;
uniform float counter;
uniform float counterSmooth;
uniform float glich;

#include noise2d.ash
#include fogandlight.fsh

float fbm(vec2 p) {
    float a = 15.0;
    float f = 15.0;
    return a * cnoise2(p * f) 
		 + a * 0.5 * cnoise2(p*f*2.0) 
		 + a * 0.25 * cnoise2(p*f*4.0)
		 + a * 0.1 * cnoise2(p*f*8.0);
}

float circle(vec2 p) {
    float r = length(p);
    float radius = 0.4;
    float height = 1.0; 
    float width = 150.0;
    return height - pow(r - radius, 2.0) * width;
}

float hash(vec2 p) { return fract(1e4 * sin(17.0 * p.x + p.y * 0.1) * (0.1 + abs(sin(p.y * 13.0 + p.x)))); }

float cnoise(vec2 x) {
    vec2 i = floor(x);
    vec2 f = fract(x);

    // Four corners in 2D of a tile
    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));

    // Simple 2D lerp using smoothstep envelope between the values.
    // return vec3(mix(mix(a, b, smoothstep(0.0, 1.0, f.x)),
    //            mix(c, d, smoothstep(0.0, 1.0, f.x)),
    //            smoothstep(0.0, 1.0, f.y)));

    // Same code, with the clamps in smoothstep and common subexpressions
    // optimized away.
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}

void main()
{
	float x = gl_FragCoord.x * invFrameSize.x;
	float y = gl_FragCoord.y * invFrameSize.y;
	
	float z1 = linearDepth(texture(depthTex, vec2(x,y)).r); //depth from gbuffer depth, a float32 texture
	float z2 = linearDepth(gl_FragCoord.z);
	
	float aDiff = max(0, z2 - z1) * 1000;
	//if (aDiff > 2) discard;
	if (aDiff > 0) discard; // No visible behind other blocks (at all)
	
	//float f = length(uv - 0.5) * (1.5 - sin(counter/5) * 0.25) * 1.75;
    vec2 st = uv - 0.5;
	float f = length(st) * 2.4;
	
	float noise = 
		  cnoise(vec2(gl_FragCoord.x / 300.0, gl_FragCoord.y / 300.0 - counter / 3))  / 50.0
		+ cnoise(vec2(gl_FragCoord.x / 200.0, gl_FragCoord.y / 200.0 - counter / 4))  / 75.0
		+ cnoise(vec2(gl_FragCoord.x / 2.0, gl_FragCoord.y / 2.0 - counter)) / 200.0;
	
	vec4 col = texture(primaryFb, vec2(x,y) + noise);

	//float angle = mod(1, 2 * PI);
	//float k = cnoise2(vec2(angle * 20, 1))/4 + cnoise2(vec2(angle * 5, 1))/4 + cnoise2(vec2(1, angle));

	col.a = clamp(pow(1 - f, 0.01), 0, 1); // To circle
	//col.a = clamp(col.a - aDiff, 0, 1); // No visible behind other blocks (smooth)
	// col.a -= clamp((dist * (1 + fogAmount/2.0) - 50) / 40, 0, 0.7 + fogAmount * 0.3); // Do nothing? 


	vec3 tp1 = vec3(0.15, 0.84, 0.64);
	vec3 tp2 = vec3(0.00, 0.65, 0.45);

    //float pulse = 0.5 + 0.5 * sin(counter);
    //float edgeNoise = cnoise2((uv - 0.5) * 13.0 + counter) * 0.3;
    //float ring = smoothstep(0.2 + edgeNoise, 0.25 + edgeNoise, dist) * 
    //            (1.0 - smoothstep(0.4 + edgeNoise, 0.45 + edgeNoise, dist));
    //vec3 portalColor = mix(tp1, tp2, pulse);
    //float glow = 0.7 - smoothstep(0.0, 0.2, dist);
    //glow *= pulse;
    //vec3 color = portalColor * ring + glow * vec3(0.5, 0.8, 1.0);

	//col.r += 0.09;
	//col.g += 0.78;
	//col.b += 0.59;

	//col.r += portalColor.r;
	//col.g += portalColor.g;
	//col.b += portalColor.b;

	float time = counter * 1.1;
	vec2 uv2 = uv - 0.5;
	st  = vec2(
            atan(uv2.y, uv2.x) ,
            length(uv2) * 1. + time * 0.1
        );
    st.x += st.y * 1.1;// - time * 0.3;
    st.x = mod(st.x , TWO_PI);
    
    float n = fbm(st) * 1.5 - 1.0;
    n = max(n, 0.1);
    float circle = max(1.0 - circle(uv2), 0.0);
    
    float a = n/circle;
    float mask = smoothstep(0.41, 0.4, length(uv2));
    a *= 0.4 - mask * 0.2;

	vec3 portalColor = mix(tp1 * 0.5, tp2 * 1.1, noise);
	col.r += portalColor.r * a;
	col.g += portalColor.g * a;
	col.b += portalColor.b * a;

	vec3 rust = vec3(
		(col.r * 0.393) + (col.g * 0.769) + (col.b * 0.189),
		(col.r * 0.349) + (col.g * 0.686) + (col.b * 0.168),
		(col.r * 0.272) + (col.g * 0.534) + (col.b * 0.131));

	col.r = rust.r * glich + col.r * (1-glich);
	col.g = rust.g * glich + col.g * (1-glich);
	col.b = rust.b * glich + col.b * (1-glich);
	

	outColor = applyFog(col, fogAmount);
}
