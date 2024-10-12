#version 330 core
#extension GL_ARB_explicit_attrib_location: enable

layout(location = 0) in vec3 vertexPosition;
layout(location = 1) in vec2 uvIn;
layout(location = 2) in int flags;

uniform mat4 projectionMatrix;
uniform mat4 viewMatrix;
uniform mat4 modelMatrix;

uniform vec4 rgbaTint;
uniform vec3 rgbaAmbientIn;
uniform vec4 rgbaLightIn;
uniform vec4 rgbaGlowIn;
uniform vec4 rgbaFogIn;
uniform float fogMinIn;
uniform float fogDensityIn;
uniform int dontWarpVertices;
uniform int addRenderFlags;

out vec2 uv;
out float dist;

out vec4 rgbaFog;
out float fogAmount;
out vec4 worldPos;

#include vertexflagbits.ash
#include fogandlight.vsh
#include vertexwarp.vsh

void main () {
	uv = uvIn;

	worldPos = modelMatrix * vec4(vertexPosition, 1.0);
	if (dontWarpVertices == 0) {
		worldPos = applyVertexWarping(flags | addRenderFlags, worldPos);
		worldPos = applyGlobalWarping(worldPos);
	}
	gl_Position = projectionMatrix * viewMatrix *  worldPos;
	fogAmount = getFogLevel(worldPos, fogMinIn, fogDensityIn);
	
	rgbaFog = rgbaFogIn;
	
	dist = length(worldPos);
}
