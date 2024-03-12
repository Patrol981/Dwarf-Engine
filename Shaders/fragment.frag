#version 450

layout (location = 0) in vec3 fragColor;
layout (location = 1) in vec3 fragPositionWorld;
layout (location = 2) in vec3 fragNormalWorld;
layout (location = 3) in vec2 texCoord;

layout (location = 0) out vec4 outColor;

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

layout (set = 0, binding = 0) uniform sampler2D textureSampler;
layout (set = 0, binding = 1) uniform sampler2DArray arraySampler;

layout (set = 1, binding = 0) uniform GlobalUbo {
  mat4 view;
  mat4 projection;
  vec3 lightPosition;
  vec4 lightColor;
  vec4 ambientLightColor;
  vec3 cameraPosition;
  int layer;
} ubo;

layout (set = 2, binding = 0) uniform ModelUBO {
  // vec3 material;
  Material material;
} modelUBO;

vec3 sobel_filter(sampler2D tex, vec2 texCoords) {
  vec2 texelSize = 1.0 / textureSize(tex, 0);

  float Gx[3][3];
  Gx[0][0] = -1.0; Gx[0][1] = 0.0; Gx[0][2] = 1.0;
  Gx[1][0] = -2.0; Gx[1][1] = 0.0; Gx[1][2] = 2.0;
  Gx[2][0] = -1.0; Gx[2][1] = 0.0; Gx[2][2] = 1.0;

  float Gy[3][3];
  Gy[0][0] = -1.0; Gy[0][1] = -2.0; Gy[0][2] = -1.0;
  Gy[1][0] = 0.0;  Gy[1][1] = 0.0;  Gy[1][2] = 0.0;
  Gy[2][0] = 1.0;  Gy[2][1] = 2.0;  Gy[2][2] = 1.0;

  vec3 sumGx = vec3(0.0);
  vec3 sumGy = vec3(0.0);

  for(int i = -1; i <= 1; ++i) {
    for(int j = -1; j <= 1; ++j) {
      vec3 sampleColor = texture(tex, texCoords + vec2(i, j) * texelSize).rgb;
      sumGx += sampleColor * Gx[i+1][j+1];
      sumGy += sampleColor * Gy[i+1][j+1];
    }
  }

  float magnitude = length(sumGx) + length(sumGy);

  return vec3(magnitude);
}

vec3 edge_detection(sampler2D tex, vec2 texCoords) {
  vec2 texelSize = 1.0 / textureSize(tex, 0);
  vec3 centerColor = texture(tex, texCoords).rgb;

  vec3 sum = vec3(0.0);
  for(int i = -1; i <= 1; ++i) {
    for(int j = -1; j <= 1; ++j) {
      vec3 sampleColor = texture(tex, texCoords + vec2(i, j) * texelSize).rgb;
      sum += abs(sampleColor - centerColor);
    }
  }

  return sum;
}

bool is_texture_complex_enough(sampler2D tex, vec2 texCoords, float threshold) {
  vec2 texelSize = 1.0 / textureSize(tex, 0);
  vec3 centerColor = texture(tex, texCoords).rgb;

  vec3 sum = vec3(0.0);
  int count = 0;
  for(int i = -1; i <= 1; ++i) {
    for(int j = -1; j <= 1; ++j) {
      vec3 sampleColor = texture(tex, texCoords + vec2(i, j) * texelSize).rgb;
      sum += abs(sampleColor - centerColor);
      count++;
    }
  }

  float averageDifference = length(sum) / float(count);
  return averageDifference <= threshold;
}

void main() {
  // ambient
  // float ambientStrength = 0.1;
  vec3 lightColor = ubo.lightColor.xyz;
  vec3 ambient = modelUBO.material.ambient * lightColor;
  // vec3 ambient = ambientStrength * lightColor;

  // diffuse
  vec3 lightDirection = normalize(ubo.lightPosition - fragPositionWorld);
  float diff = max(dot(fragNormalWorld, lightDirection), 0.0);
  vec3 diffuse = (diff * modelUBO.material.diffuse) * lightColor;
  // vec3 diffuse = diff * lightColor;

  // specular
  // float specularStrength = 0.5;
  vec3 viewDir = normalize(ubo.cameraPosition - fragPositionWorld);
  vec3 reflectDir = reflect(-lightDirection, fragNormalWorld);
  float spec = pow(max(dot(viewDir, reflectDir), 0.0), modelUBO.material.shininess);
  vec3 specular = lightColor * (spec * modelUBO.material.specular);

  vec3 result = (ambient + diffuse + specular) * fragColor * modelUBO.material.color;

  if(ubo.layer == 0) {
    outColor = vec4(result, 1.0);
  } else if(ubo.layer == 5) {
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