#version 450

layout (location = 0) in vec3 position;
layout (location = 1) in vec3 color;
layout (location = 2) in vec3 normal;
layout (location = 3) in vec2 uv;

layout (location = 0) out vec3 fragColor;
layout (location = 1) out vec3 fragPositionWorld;

layout (set = 0, binding = 0) uniform GlobalUbo {
  mat4 view;
  mat4 projection;
  vec3 lightPosition;
  vec4 lightColor;
  vec4 ambientLightColor;
  vec3 cameraPosition;
  int layer;
} ubo;

layout (push_constant) uniform Push {
  mat4 transform;
} push;

void main() {
  vec4 positionWorld =  push.transform * vec4(position, 1.0);
  gl_Position = ubo.projection * ubo.view * positionWorld;

  fragPositionWorld = positionWorld.xyz;
  fragColor = color;
}