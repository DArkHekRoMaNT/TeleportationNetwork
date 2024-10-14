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

#include noise2d.ash
#include vertexflagbits.ash
#include fogandlight.vsh
#include vertexwarp.vsh

vec4 addWavinessEffect(vec4 worldPos, float waviness)
{
	if (waviness > 0.1) {
		float str = max(0, waviness - 0.1);
		str *= clamp(1.5 * length(worldPos) * waviness - 1, 0, 250);

		float xf = (worldPos.x + gl_Position.x) / 10;
		float zf = (worldPos.z + gl_Position.z) / 10;
		worldPos.x += str * gnoise(vec3(xf, zf, windWaveCounter/6)) / 5;
		worldPos.y += str * gnoise(vec3(xf, zf, windWaveCounter/10)) / 5;
		worldPos.z += str * gnoise(vec3(xf, zf, windWaveCounter/3.5)) / 5;
	}
	return worldPos;
}

vec4 addEffect(vec4 worldPos)
{
	vec3 noisepos = vec3((worldPos.x + playerpos.x) / 3, (worldPos.z + playerpos.z) / 3, waterWaveCounter / 8);
	worldPos.x += waterWaveIntensity * gnoise(noisepos) / 0.2;
	return worldPos;
}

void main () {
	uv = uvIn;

	worldPos = modelMatrix * vec4(vertexPosition, 1.0);
	//float strength = windWaveIntensity * (1 + windSpeed) / 30.0;
	//worldPos = addWavinessEffect(worldPos, strength);


	//worldPos = addEffect(worldPos);


	if (dontWarpVertices == 0) {
		worldPos = applyVertexWarping(flags | addRenderFlags, worldPos);
		worldPos = applyGlobalWarping(worldPos);
	}
	gl_Position = projectionMatrix * viewMatrix *  worldPos;
	fogAmount = getFogLevel(worldPos, fogMinIn, fogDensityIn);
	
	rgbaFog = rgbaFogIn;
	
	dist = length(worldPos);
}
