#version 450

layout (location = 0) in vec3 position;
layout (location = 1) in vec3 color;
layout (location = 2) in vec3 normal;
layout (location = 3) in vec2 uv;
layout (location = 4) in vec4 jointIndices;
layout (location = 5) in vec4 jointWeights;

layout (location = 0) out vec3 fragColor;
layout (location = 1) out vec3 fragPositionWorld;
layout (location = 2) out vec3 fragNormalWorld;
layout (location = 3) out vec2 texCoord;

struct Material {
  vec3 color;
  vec3 ambient;
  vec3 diffuse;
  vec3 specular;
  float shininess;
};

layout (push_constant) uniform Push {
  mat4 transform;
  mat4 normalMatrix;
} push;

layout (set = 1, binding = 0) #include global_ubo

// 500 FPS on avg
// TODO: optimize set, so its reusable across all models?
layout (set = 2, binding = 0) #include model_ubo

void main() {
  vec4 positionWorld = push.transform * vec4(position, 1.0);
  gl_Position = ubo.projection * ubo.view * positionWorld;

  fragNormalWorld = normalize(mat3(push.normalMatrix) * normal);
  fragPositionWorld = positionWorld.xyz;
  fragColor = color;
  texCoord = uv;
}