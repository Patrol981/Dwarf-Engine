#version 450

layout (location = 0) in vec3 position;
layout (location = 1) in vec3 color;
layout (location = 2) in vec3 normal;
layout (location = 3) in vec2 uv;

layout (location = 0) out vec3 fragColor;

layout (set = 0, binding = 0) uniform GlobalUbo {
  mat4 view;
  mat4 projection;
  vec3 lightDirection;
} ubo;

layout (push_constant) uniform Push {
  mat4 modelMatrix;
  mat4 normalMatrix;
} push;

// const vec3 DIRECTION_TO_LIGHT = normalize(vec3(1.0, -3.0, -1.0));
const float AMBIENT = 0.02;

void main() {
  // mat4 mvp = push.modelMatrix * ubo.view * ubo.projection;
  gl_Position = ubo.projection * ubo.view * push.modelMatrix * vec4(position, 1.0);
  // gl_Position = push.modelMatrix *  ubo.view * ubo.projection * vec4(position, 1.0);

  //mat3 normalMatrix = transpose(inverse(mat3(push.modelMatrix)));
  //vec3 normalWorldSpace = normalize(normalMatrix * normal);
  vec3 normalWorldSpace = normalize(mat3(push.normalMatrix) * normal);

  float lightIntesitivity = AMBIENT + max(dot(normalWorldSpace, ubo.lightDirection), 0);

  // fragColor = color;
  fragColor = lightIntesitivity * color;
  // fragColor = vec3(1.0, 1.0, 1.0);
}