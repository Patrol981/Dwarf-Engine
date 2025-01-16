#version 460

#include directional_light
#include point_light

layout(location = 0) in vec2 uv;

layout(location = 0) out vec4 outColor;

layout(set = 0, binding = 0) uniform sampler2D _colorSampler;
layout(set = 0, binding = 1) uniform sampler2D _depthSampler;

layout (push_constant) uniform Push {
  vec2 windowSize;
  float depthMin;
  float depthMax;
  float edgeLow;
  float edgeHigh;
  float contrast;
  float stipple;
} push;

layout(set = 1, binding = 0) #include global_ubo

float getProjectedDepthFromTexture(vec2 pTexCoords, mat4 pInverseProjection) {
  float depth = texture(_depthSampler, pTexCoords).r;
  vec3 ndc = vec3(pTexCoords * 2.0 - 1.0, depth);
  vec4 view = pInverseProjection * vec4(ndc, 1.0);
  view.xyz /= view.w;
  return -view.z;
}

vec4 pixelEffect() {
  float depth = getProjectedDepthFromTexture(uv, inverse(ubo.projection));
  vec2 texel_size = 1.0 / push.windowSize.xy;

  vec2 uvs[4];
  uvs[0] = vec2(uv.x, uv.y + texel_size.y);
  uvs[1] = vec2(uv.x, uv.y - texel_size.y);
  uvs[2] = vec2(uv.x + texel_size.x, uv.y);
  uvs[3] = vec2(uv.x - texel_size.x, uv.y);

  float depth_diff = 0.0;
  for(int i = 0; i < 4; i++) {
    float d = getProjectedDepthFromTexture(uvs[i], inverse(ubo.projection));
    depth_diff += abs(depth - d);
  }

  float depth_edge = step(push.depthMax, depth_diff);
  vec3 edge_mix = mix(texture(_colorSampler, uv).rgb, vec3(0), depth_edge);

  vec4 pixelResult = vec4(edge_mix, 1.0);

  return pixelResult;
}

vec3 quantizeColor(vec3 color, float levels) {
    return floor(color * levels) / levels;
}

float edgeDetection(vec2 pTexCoords) {
    float dx = 1.0 / push.windowSize.x;
    float dy = 1.0 / push.windowSize.y;

    float kernel[9] = float[](
        -1, -1, -1,
        -1,  8, -1,
        -1, -1, -1
    );

    float edge = 0.0;
    int index = 0;
    for (int y = -1; y <= 1; ++y) {
        for (int x = -1; x <= 1; ++x) {
            vec2 offset = vec2(float(x) * dx, float(y) * dy);
            vec3 sampleValue = texture(_colorSampler, pTexCoords + offset).rgb;
            edge += kernel[index] * length(sampleValue);
            index++;
        }
    }
    return clamp(edge, 0.0, 1.0);
}

void main() {
  // outColor = vec4(mix(texture(_colorSampler, uv).rgb, texture(_depthSampler, uv).rgb, 1.0), 1.0);
  // outColor = vec4(texture(_colorSampler, uv).rgb, 1.0);
  // outColor = pixelEffect();

  vec3 color = texture(_colorSampler, uv).rgb;
  float colorLevels = 9.0;
  // color = quantizeColor(color, colorLevels);
  float edge = edgeDetection(uv);
  vec3 finalColor = mix(color, vec3(0.0), edge);

  outColor = vec4(finalColor, 1.0);

}