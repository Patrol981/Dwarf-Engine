#version 450

layout (location = 0) in vec3 fragColor;
layout (location = 1) in vec3 fragPositionWorld;
layout (location = 2) in vec3 fragNormalWorld;
layout (location = 3) in vec2 texCoord;

layout (location = 0) out vec4 outColor;

layout (push_constant) uniform Push {
  mat4 transform;
  mat4 normalMatrix;
} push;

layout (set = 0, binding = 0) uniform sampler2D textureSampler;

layout (set = 1, binding = 0) uniform GlobalUbo {
  mat4 view;
  mat4 projection;
  vec3 lightPosition;
  vec4 lightColor;
  vec4 ambientLightColor;
  vec3 cameraPosition;
} ubo;

layout (set = 2, binding = 0) uniform ModelUBO {
  vec3 material;
} modelUBO;

void main() {
  // ambient
  float ambientStrength = 0.1;
  vec3 lightColor = ubo.lightColor.xyz;
  vec3 ambient = ambientStrength * lightColor;

  // diffuse
  vec3 lightDirection = normalize(ubo.lightPosition - fragPositionWorld);
  float diff = max(dot(fragNormalWorld, lightDirection), 0.0);
  vec3 diffuse = diff * lightColor;

  // specular
  float specularStrength = 0.5;
  vec3 viewDir = normalize(ubo.cameraPosition - fragPositionWorld);
  vec3 reflectDir = reflect(-lightDirection, fragNormalWorld);
  float spec = pow(max(dot(viewDir, reflectDir), 0.0), 32);
  vec3 specular = specularStrength * spec * lightColor;

  // output
  vec3 result = (ambient + diffuse + specular) * fragColor * modelUBO.material;
  outColor = texture(textureSampler, texCoord) * vec4(result, 1.0);
}