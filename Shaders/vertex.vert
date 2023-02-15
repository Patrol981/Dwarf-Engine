#version 450

layout (location = 0) in vec3 position;
layout (location = 1) in vec3 color;
layout (location = 2) in vec3 normal;
layout (location = 3) in vec2 uv;

layout (location = 0) out vec3 fragColor;
layout (location = 1) out vec2 texCoord;

layout (set = 0, binding = 0) uniform GlobalUbo {
  mat4 view;
  mat4 projection;
  vec3 lightDirection;
} ubo;


layout (set = 1, binding = 0) uniform ModelUBO {
  mat4 modelMatrix;
  mat4 normalMatrix;
  vec3 material;
} modelUBO;


layout (push_constant) uniform Push {
  mat4 modelMatrix;
  mat4 normalMatrix;
} push;

const float AMBIENT = 0.02;

void main() {
  // gl_Position = ubo.projection * ubo.view * push.modelMatrix * vec4(position, 1.0);
  gl_Position = ubo.projection * ubo.view * modelUBO.modelMatrix * vec4(position, 1.0);
  // gl_Position = modelUBO.modelMatrix * vec4(position, 1.0);

  vec3 normalWorldSpace = normalize(mat3(push.normalMatrix) * normal);
  float lightIntesitivity = AMBIENT + max(dot(normalWorldSpace, ubo.lightDirection), 0);

  fragColor = lightIntesitivity * color;
  // fragColor *= modelUBO.material;
  // fragColor = modelUBO.material;
  texCoord = uv;
}