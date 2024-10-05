#version 330 core
#extension GL_ARB_explicit_attrib_location: enable

layout(location = 0) in vec3 vertexPosition;
layout(location = 1) in vec2 uvIn;

uniform mat4 projectionMatrix;
uniform mat4 modelViewMatrix;
uniform vec4 worldPos;

uniform vec4 rgbaTint;
uniform vec3 rgbaAmbientIn;
uniform vec4 rgbaLightIn;
uniform vec4 rgbaGlowIn;
uniform vec4 rgbaFogIn;
uniform float fogMinIn;
uniform float fogDensityIn;


out vec2 uv;
out float dist;

out vec4 rgbaFog;
out float fogAmount;

#include vertexflagbits.ash
#include fogandlight.vsh

void main () {
	uv = uvIn;
	vec4 camPos = modelViewMatrix * vec4(vertexPosition, 1.0);
	gl_Position = projectionMatrix * camPos;
	
	fogAmount = getFogLevel(worldPos, fogMinIn, fogDensityIn);
	
	rgbaFog = rgbaFogIn;
	
	dist = length(worldPos);
}
