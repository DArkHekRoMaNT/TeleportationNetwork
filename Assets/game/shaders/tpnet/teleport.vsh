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
uniform float stage;
uniform int broken;
uniform int direction;

out vec2 uv;
out float dist;

out vec4 rgbaFog;
out float fogAmount;
out vec4 worldPos;

#include noise2d.ash
#include vertexflagbits.ash
#include fogandlight.vsh
#include vertexwarp.vsh

vec4 addEffect(vec4 worldPos)
{
	const float dx[] = float[4](0, 1, 0, -1); 
	const float dz[] = float[4](1, 0, -1, 0); 

	float maxLen = sqrt(0.2);
	float len = pow(length(vertexPosition), 1);
	len = maxLen - len;
	if (len <= 0)
		return worldPos;

	float frequency = 500000;
	float speed = 3;
    float wave = sin(frequency * len - speed * timeCounter) / 4;
	//if (wave < 0) wave = 0;
	
	if (bool(broken)) 
	{	
		float mod = 0.5;
		int dir = (direction + 2) % 4;
		worldPos.x += dx[dir] * wave * len * mod;
		worldPos.z += dz[dir] * wave * len * mod;
		worldPos.y += wave * len * mod;
	}
	else
	{
		worldPos.x += dx[direction] * wave * len;
		worldPos.z += dz[direction] * wave * len;
	}

	return worldPos;
}

void main () {
	uv = uvIn;

	worldPos = modelMatrix * vec4(vertexPosition, 1.0);
	worldPos = addEffect(worldPos);

	if (dontWarpVertices == 0) {
		worldPos = applyVertexWarping(flags | addRenderFlags, worldPos);
		worldPos = applyGlobalWarping(worldPos);
	}
	gl_Position = projectionMatrix * viewMatrix *  worldPos;
	fogAmount = getFogLevel(worldPos, fogMinIn, fogDensityIn);
	
	rgbaFog = rgbaFogIn;
	
	dist = length(worldPos);
}
