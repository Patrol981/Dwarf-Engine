#version 460

#extension GL_EXT_samplerless_texture_functions : require

layout(location = 0) in vec3 fragColor;
layout(location = 1) in vec3 fragPositionWorld;
layout(location = 2) in vec3 fragNormalWorld;
layout(location = 3) in vec2 texCoord;
layout(location = 4) flat in int filterFlag;
layout(location = 5) in float entityToFragDistance;
layout(location = 6) in float fogVisiblity;
layout(location = 7) in vec2 screenTexCoord;

layout(location = 0) out vec4 outColor;

#include material

#include skin_data
#include fog
#include directional_light
#include point_light

#include sobel

layout (push_constant) uniform Push {
  mat4 transform;
  mat4 normalMatrix;
} push;

layout(set = 0, binding = 0) uniform texture2D _texture;
layout(set = 0, binding = 1) uniform sampler _sampler;

layout(set = 1, binding = 0) uniform texture2D _hatchTexture;
layout(set = 1, binding = 1) uniform sampler _hatchSampler;

layout(set = 2, binding = 0) #include global_ubo
// set 3 = ssbo set
layout(set = 4, binding = 0) #include skinned_model_ubo

layout(std140, set = 5, binding = 0) readonly buffer PointLightBuffer {
  PointLight pointLights[];
} pointLightBuffer;

// set 6 = joints

layout(set = 7, binding = 0) uniform sampler2D _prevColor;
layout(set = 7, binding = 1) uniform sampler2D _prevDepth;

#include light_calc

vec3 computeViewNormal(vec2 inTexCoords) {
  // Sample depth texture and convert to NDC
  float depth = texture(_prevDepth, inTexCoords).r;
  depth = depth * 2.0 - 1.0; // Remap from [0, 1] to [-1, 1]

  // Convert screen UV to NDC coordinates
  vec2 ndcPos = inTexCoords * 2.0 - 1.0;

  // Reconstruct clip-space position
  vec4 clipPos = vec4(ndcPos, depth, 1.0);

  // Transform to view space
  vec4 viewPos = inverse(ubo.projection) * clipPos;
  viewPos.xyz /= viewPos.w; // Perspective divide

  // Compute partial derivatives for view-space position
  vec3 viewPosDx = dFdx(viewPos.xyz);
  vec3 viewPosDy = dFdy(viewPos.xyz);

  // Compute view-space normal using the right-hand rule
  vec3 normal = normalize(cross(viewPosDx, viewPosDy));

  // Ensure normals point outward by flipping if necessary
  if (dot(normal, viewPos.xyz) > 0.0) {
      normal = -normal;
  }

  return normal;
}

void main() {
  vec3 surfaceNormal = normalize(fragNormalWorld);
  vec3 viewDir = normalize(ubo.cameraPosition - fragPositionWorld);

  vec3 result = vec3(0,0,0);

  result += calc_dir_light(ubo.directionalLight, surfaceNormal, viewDir);
  for(int i = 0; i < ubo.pointLightLength; i++) {
    PointLight light = pointLightBuffer.pointLights[i];
    result += calc_point_light(light, surfaceNormal, viewDir);
  }

  float alpha = 1.0;

  if (ubo.hasImportantEntity == 1 && filterFlag == 1) {
      float radiusHorizontal = 1.0;

      float fragToCamera = distance(fragPositionWorld, ubo.cameraPosition);
      float entityToCamera = distance(ubo.importantEntityPosition, ubo.cameraPosition);

      if(fragToCamera <= entityToCamera && entityToFragDistance < radiusHorizontal) {
        alpha = 0.5;
      }
  }

  // float hatchScale = 5.0;
  vec3 worldCoords = fragPositionWorld * 0.1;
  vec2 hatchCoords = vec2(worldCoords.x, worldCoords.z);
  vec4 hatch = texture(
    sampler2D(_hatchTexture, _hatchSampler),
    vec2(1.001f, sin(0.5)) * gl_FragCoord.xy * ubo.hatchScale
  );
  hatch *= hatch * hatch * hatch;

  vec3 xnm = fragNormalWorld;
  vec3 col = vec3(xnm.x + xnm.y + xnm.z) / 3.0;

  col.rgb *= 2.1;
  col.rgb *= (floor(6.0 * col.rgb) * 4.0) / 9.0;

  vec4 texColor = texture(sampler2D(_texture, _sampler), texCoord).rgba;

  // vec3 prevColor = texture(_prevColor, screenTexCoord).rgb;
  float intensity = 0.2126 * texColor.r + 0.7152 * texColor.g + 0.0722 * texColor.b;
  vec3 canvasTone = mix(vec3(0.9, 0.9, 0.9), vec3(1.0, 0.9, 0.9), intensity);
  // texColor.rgb = canvasTone; // Replace texColor's RGB with grayscale intensity
  // vec3 whitescaleColor = vec3(intensity);

  // outColor = texColor * hatch * vec4(col, 1.0) * vec4(result, alpha) * vec4(whitescaleColor, 1.0);
  outColor = texColor * vec4(result, alpha);
  outColor = mix(vec4(1.0), outColor, fogVisiblity);
  // outColor = vec4(mix(hatch.rgb, texColor.rgb, 0.7), 1.0) * vec4(result, alpha);
}
