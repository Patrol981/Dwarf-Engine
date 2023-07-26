#version 450

layout (location = 0) in vec3 position;
layout (location = 1) in vec3 color;
layout (location = 2) in vec3 normal;
layout (location = 3) in vec2 uv;

layout (location = 0) out vec3 fragColor;
layout (location = 1) out vec3 fragPositionWorld;
layout (location = 2) out vec3 fragNormalWorld;
layout (location = 3) out vec2 texCoord;


layout (set = 0, binding = 0) uniform GlobalUbo {
  mat4 view;
  mat4 projection;
  vec3 lightPosition;
  vec4 lightColor;
  vec4 ambientLightColor;
  vec3 cameraPosition;
} ubo;

layout (push_constant) uniform Push {
  mat4 transform;
  mat4 normalMatrix;
} push;

// 500 FPS on avg
// TODO: optimize set, so its reusable across all models?
layout (set = 1, binding = 0) uniform ModelUBO {
  // mat4 modelMatrix;
  // mat4 normalMatrix;
  vec3 material;
  bool useTexture;
  bool useLight;
} modelUBO;

void main() {
  // vec4 positionWorld = modelUBO.modelMatrix * vec4(position, 1.0);
  vec4 positionWorld =  push.transform * vec4(position, 1.0);
  gl_Position = ubo.projection * ubo.view * positionWorld;

  // fragNormalWorld = normalize(mat3(modelUBO.normalMatrix) * normal);
  fragNormalWorld = normalize(mat3(push.normalMatrix) * normal);
  fragPositionWorld = positionWorld.xyz;
  fragColor = color;
  texCoord = uv;
}