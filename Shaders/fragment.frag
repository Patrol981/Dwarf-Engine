#version 450

layout (location = 0) in vec3 fragColor;
layout (location = 1) in vec3 fragPositionWorld;
layout (location = 2) in vec3 fragNormalWorld;
layout (location = 3) in vec2 texCoord;

layout (location = 0) out vec4 outColor;

#include material

#include directional_light
#include point_light

layout (push_constant) uniform Push {
  mat4 transform;
  mat4 normalMatrix;
} push;

layout (set = 0, binding = 0) uniform sampler2D textureSampler;
// layout (set = 0, binding = 1) uniform sampler2DArray arraySampler;

layout (set = 1, binding = 0) #include global_ubo

layout (set = 2, binding = 0) #include model_ubo


void main() {
  vec3 surfaceNormal = normalize(fragNormalWorld);

  // ambient
  // float ambientStrength = 0.1;
  vec3 lightColor = ubo.directionalLight.lightColor.xyz;
  vec3 ambient = modelUBO.ambient * lightColor * ubo.directionalLight.lightIntensity;
  // vec3 ambient = ambientStrength * lightColor;

  // diffuse
  vec3 lightDirection = normalize(ubo.directionalLight.lightPosition - fragPositionWorld);
  float diff = max(dot(fragNormalWorld, lightDirection), 0.0);
  vec3 diffuse = (diff * modelUBO.diffuse) * lightColor;
  vec3 pDiffuse;
  // vec3 diffuse = diff * lightColor;

  for(int i = 0; i < ubo.pointLightsLength; i++) {
    PointLight light = ubo.pointLights[i];
    vec3 pointDir = light.lightPosition.xyz - fragPositionWorld;
    float attenuation = 1.0 / dot(pointDir, pointDir);
    float cosAngIncidence = max(dot(surfaceNormal, normalize(pointDir)), 0);
    vec3 intensity = light.lightColor.xyz * light.lightColor.w * attenuation;

    pDiffuse += intensity * cosAngIncidence;
  }
  // diffuse += pDiffuse;

  // specular
  // float specularStrength = 0.5;
  vec3 viewDir = normalize(ubo.cameraPosition - fragPositionWorld);
  vec3 reflectDir = reflect(-lightDirection, fragNormalWorld);
  float spec = pow(max(dot(viewDir, reflectDir), 0.0), modelUBO.shininess);
  // float spec = pow(max(dot(viewDir, reflectDir), 0.0), 0.5);
  vec3 specular = lightColor * (spec * modelUBO.specular);

  vec3 result = (ambient + diffuse + specular) * fragColor * modelUBO.color;

  if(ubo.layer == 0) {
    outColor = vec4(result, 1.0);
  } else if(ubo.layer == 1) {
    outColor = texture(textureSampler, texCoord) * vec4(result, 1.0);
  }

  // output
  // vec3 result = (ambient + diffuse + specular) * fragColor * modelUBO.material.color;
  /*
  float threshold = 0.1;
  vec3 result = vec3(0.0);
  if(is_texture_complex_enough(textureSampler, texCoord, threshold)) {
    vec3 sobel = sobel_filter(textureSampler, texCoord);
    result = (ambient + diffuse + specular) * fragColor * modelUBO.material.color;
    result += sobel;
    outColor = outColor = texture(textureSampler, texCoord) * vec4(result, 1.0);
  } else {
    result = (ambient + diffuse + specular) * fragColor * modelUBO.material.color;
    outColor = texture(textureSampler, texCoord) * vec4(result, 1.0);
  }
  */

}