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

layout (set = 1, binding = 0) uniform GlobalUbo {
  mat4 view;
  mat4 projection;
  vec3 lightPosition;
  vec4 lightColor;
  vec4 ambientLightColor;
  vec3 cameraPosition;
  int layer;
} ubo;

// 500 FPS on avg
// TODO: optimize set, so its reusable across all models?
layout (set = 2, binding = 0) uniform ModelUBO {
  // vec3 material_color;
  // mat4 bonesMatrix;
  // Material material;

  vec3 color;
  vec3 ambient;
  vec3 diffuse;
  vec3 specular;
  float shininess;

  // int isSkinned;

} modelUBO;

layout (std430, set = 3, binding = 0) readonly buffer JointBuffer {
  mat4 jointMatrices[];
};

void main() {
  mat4 skinMat =
    jointWeights.x * jointMatrices[int(jointIndices.x)] +
    jointWeights.y * jointMatrices[int(jointIndices.y)] +
    jointWeights.z * jointMatrices[int(jointIndices.z)] +
    jointWeights.w * jointMatrices[int(jointIndices.w)];

  vec4 positionWorld = push.transform * skinMat * vec4(position, 1.0);

  // vec4 positionWorld = push.transform * skinMat * vec4(position, 1.0);

  // vec4 positionWorld =  totalPosition;
  gl_Position = ubo.projection * ubo.view * positionWorld;

  fragNormalWorld = normalize(mat3(push.normalMatrix) * normal);
  fragPositionWorld = positionWorld.xyz;
  fragColor = color;
  texCoord = uv;
}