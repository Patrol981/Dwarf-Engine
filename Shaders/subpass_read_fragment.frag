#version 460

#include directional_light
#include point_light

layout(input_attachment_index = 0, binding = 0) uniform subpassInput inputColor;
layout(input_attachment_index = 1, binding = 1) uniform subpassInput inputDepth;

layout(location = 0) in vec2 uv;

layout(location = 0) out vec4 outColor;

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

const int MAX_STEPS = 100;       // Max raymarching steps
const float STEP_SIZE = 0.1;     // Raymarching step size
const float EPSILON = 0.01;      // Intersection threshold

// Function to calculate luminance
float luminance(vec3 color) {
  return dot(color, vec3(0.299, 0.587, 0.114));
}

// Function to apply Sobel edge detection
float edgeDetection(vec2 xy) {
  const int Kx[3][3] = int[3][3](
    int[]( 1,  0, -1),
    int[]( 2,  0, -2),
    int[]( 1,  0, -1)
  );

  const int Ky[3][3] = int[3][3](
    int[]( 1,  2,  1),
    int[]( 0,  0,  0),
    int[](-1, -2, -1)
  );

  float Gx = 0.0;
  float Gy = 0.0;

  for (int x = -1; x <= 1; ++x) {
    for (int y = -1; y <= 1; ++y) {
      vec2 offset = vec2(x, y) * push.windowSize;
      float lum = luminance(subpassLoad(inputColor).rgb);
      // float lum = luminance(subpassLoad(inputColor, xy + offset).rgb);
      Gx += Kx[x + 1][y + 1] * lum;
      Gy += Ky[x + 1][y + 1] * lum;
    }
  }

  return sqrt(Gx * Gx + Gy * Gy);
}

// Function to apply stippling
float stipple(vec2 uv, float luminance) {
  vec2 stippleUV = uv * push.stipple;
  float stippleNoise = fract(sin(dot(stippleUV, vec2(12.9898, 78.233))) * 43758.5453);
  return luminance < stippleNoise ? 1.0 : 0.0;
}

void main() {
  vec3 color = subpassLoad(inputColor).rgb;
  float depth = subpassLoad(inputDepth).r;
  float adjustedDepth = (depth - push.depthMin) * 1.0 / (push.depthMax - push.depthMin);

  if (ubo.useFog == 1 && ubo.hasImportantEntity == 1) {
    float z = depth * 2.0 - 1.0;

    vec4 fragPositionNDC = vec4(
      (gl_FragCoord.x / ubo.fog.y) * 2.0 - 1.0,
      (gl_FragCoord.y / ubo.fog.z) * 2.0 - 1.0,
      z,
      1.0
    );

    vec4 fragPositionView = inverse(ubo.projection) * fragPositionNDC;
    fragPositionView /= fragPositionView.w;

    vec3 fragPositionWorld = (inverse(ubo.view) * fragPositionView).xyz;
    fragPositionWorld.z -= 2.0f;

    float horizontalDistance = distance(fragPositionWorld.xz, ubo.importantEntityPosition.xz);

    float normalizedDistance = horizontalDistance / ubo.fog.x;
    float fogVisibility = exp(-pow(normalizedDistance, 2.0));
    fogVisibility = clamp(fogVisibility, 0.0, 1.0);

    color = mix(vec3(0.0, 0.0, 0.0), color, fogVisibility);
  }

  // float lum = luminance(color);
  // float edge = edgeDetection(uv);
  // edge = smoothstep(push.edgeLow, push.edgeHigh, edge);
  // float stippleEffect = stipple(uv, lum);
  // vec3 finalColor = vec3(1.0 - edge) * stippleEffect * adjustedDepth;

  // outColor = vec4(color, 1.0);
  // outColor = vec4(finalColor, 1.0);
  // outColor = mix(vec4(color, 1.0), vec4(finalColor, 1.0), 1.0);
  outColor = vec4(color, 1.0);
}