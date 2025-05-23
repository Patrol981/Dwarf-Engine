#version 460

layout (location = 0) in vec3 position;
layout (location = 1) in vec3 color;
layout (location = 2) in vec3 normal;
layout (location = 3) in vec2 uv;

layout (location = 0) out vec2 texCoord;

layout (push_constant) uniform Push {
  mat4 transform;
  vec3 spriteSheetData;
  bool flipX;
  bool flipY;
} push;


layout (set = 0, binding = 0) uniform GlobalUbo {
  mat4 view;
  mat4 projection;
  vec3 lightPosition;
  vec4 lightColor;
  vec4 ambientLightColor;
  vec3 cameraPosition;
  int layer;
} globalUBO;


void main() {
  texCoord = uv;
  // vec4 positionWorld = spriteUBO.spriteMatrix * vec4(position, 1.0);
  vec4 positionWorld = push.transform * vec4(position, 1.0);
  gl_Position = globalUBO.projection * globalUBO.view * positionWorld;
  // gl_Position = globalUBO.projection * vec4(position, 1.0);
}