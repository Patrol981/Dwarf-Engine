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

void main() {
  vec4 positionWorld = modelUBO.modelMatrix * vec4(position, 1.0);
  // gl_Position = ubo.projection * ubo.view * push.modelMatrix * vec4(position, 1.0);
  gl_Position = ubo.projection * ubo.view * positionWorld;
  // gl_Position = modelUBO.modelMatrix * vec4(position, 1.0);

  fragNormalWorld = normalize(mat3(push.normalMatrix) * normal);
  fragPositionWorld = positionWorld.xyz;
  fragColor = color;

  texCoord = uv;

  /*
  vec3 directionToLight = ubo.lightPosition - positionWorld.xyz;
  float attenuation = 1.0 / dot(directionToLight, directionToLight);
  vec3 lightColor = ubo.lightColor.xyz * ubo.lightColor.w * attenuation;
  vec3 ambientLight = ubo.ambientLightColor.xyz * ubo.ambientLightColor.w;
  // ubo.lightDirection
  vec3 diffuse = lightColor * max(dot(normalWorldSpace, normalize(directionToLight)), 0);
  // float lightIntesitivity = AMBIENT + 

  // fragColor = lightIntesitivity * color;
  fragColor = (diffuse + ambientLight) * color;
  // fragColor *= modelUBO.material;
  // fragColor = modelUBO.material;
  */
}