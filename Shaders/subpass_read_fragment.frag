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

float getProjectedDepth(vec2 pTexCoords, float pDepth) {
  vec3 ndc = vec3(pTexCoords * 2.0 - 1.0, pDepth);
  vec4 view = inverse(ubo.projection) * vec4(ndc, 1.0);
  view.xyz /= view.w;

  // return vec3(-view.z / 20);
  return -view.z;
}

float getProjectedDepthFromTexture(vec2 pTexCoords, mat4 pInverseProjection) {
  // Load the depth value using subpass input
    float _depth = subpassLoad(inputDepth).r;

    // Reconstruct normalized device coordinates (NDC)
    vec3 _ndc = vec3(pTexCoords * 2.0 - 1.0, _depth);

    // Transform NDC to view-space coordinates
    vec4 _view = pInverseProjection * vec4(_ndc, 1.0);
    _view.xyz /= _view.w;

    // Return the negative view-space Z value (depth in view space)
    return -_view.z;
}

float calculateLinearDepth(float depth, float nearPlane, float farPlane) {
  // Convert depth from normalized [0, 1] to linear depth
  return (2.0 * nearPlane * farPlane) / (farPlane + nearPlane - depth * (farPlane - nearPlane));
}

vec4 pixelEffect(vec3 pColor, vec2 pTexCoords, float pDepth) {
  // vec4 pixelResult = vec4(getProjectedDepth(pTexCoords, pDepth), 1.0);
  float _depth = getProjectedDepthFromTexture(pTexCoords, inverse(ubo.projection));
  vec2 texel_size = 1.0 / push.windowSize.xy;

  vec2 _uvs[4];
  _uvs[0] = vec2(pTexCoords.x, pTexCoords.y + texel_size.y);
  _uvs[1] = vec2(pTexCoords.x, pTexCoords.y - texel_size.y);
  _uvs[2] = vec2(pTexCoords.x + texel_size.x, pTexCoords.y);
  _uvs[3] = vec2(pTexCoords.x - texel_size.x, pTexCoords.y);

  float depth_diff = 0.0;
  for(int i = 0; i < 4; i++) {
    float _d = getProjectedDepthFromTexture(_uvs[i], inverse(ubo.projection));
    depth_diff += abs(_depth - _d);
  }

  float depth_edge = step(push.depthMax, depth_diff);
  float test = getProjectedDepthFromTexture(_uvs[0], inverse(ubo.projection));
  vec3 edge_mix = mix(pColor, vec3(0), depth_edge);

  // vec4 pixelResult = vec4(edge_mix, 1.0);
  vec4 pixelResult = vec4(vec3(test / 20), 1.0);
  // vec4 pixelResult = vec4(vec3(_depth), 1.0);

  return pixelResult;
}

vec4 sobelEffet() {
  const mat3 sobel_x = mat3(
    -1,  0,  1,
    -2,  0,  2,
    -1,  0,  1
  );

  const mat3 sobel_y = mat3(
    -1, -2, -1,
     0,  0,  0,
     1,  2,  1
  );

  vec3 _color[3][3];
  vec2 texel_size = 1.0 / push.windowSize.xy;

  for (int i = -1; i <= 1; i++) {
    for (int j = -1; j <= 1; j++) {
      vec2 offset = vec2(i, j) * texel_size;
      // _color[i + 1][j + 1] = subpassLoad(inputColor, offset).rgb;
    }
  }

  float gx = 0.0;
  float gy = 0.0;

  for (int i = 0; i < 3; i++) {
    for (int j = 0; j < 3; j++) {
      gx += sobel_x[i][j] * _color[i][j].r; // Use red channel for luminance
      gy += sobel_y[i][j] * _color[i][j].r;
    }
  }

  float edgeIntensity = sqrt(gx * gx + gy * gy);

  return vec4(vec3(edgeIntensity), 1.0);
}

vec4 whatTheFuckAreYouCooking() {
  vec3 centerColor = subpassLoad(inputColor).rgb;

    // Approximate neighboring colors by offsetting
    vec3 leftColor = centerColor;  // Left neighbor approximation
    vec3 rightColor = centerColor; // Right neighbor approximation
    vec3 topColor = centerColor;   // Top neighbor approximation
    vec3 bottomColor = centerColor; // Bottom neighbor approximation

    // Simulate neighboring fragments (offsets hardcoded for simplicity)
    vec2 texel_size = 1.0 / push.windowSize.xy;
    if (gl_FragCoord.x > 0) {
        leftColor = subpassLoad(inputColor).rgb; // Simulate left
    }
    if (gl_FragCoord.x < texel_size.x) {
        rightColor = subpassLoad(inputColor).rgb; // Simulate right
    }
    if (gl_FragCoord.y > 0) {
        topColor = subpassLoad(inputColor).rgb; // Simulate top
    }
    if (gl_FragCoord.y < texel_size.y) {
        bottomColor = subpassLoad(inputColor).rgb; // Simulate bottom
    }

    // Compute gradients (simple difference approximation)
    float gx = rightColor.r - leftColor.r;
    float gy = bottomColor.r - topColor.r;

    // Compute edge intensity
    float edgeIntensity = sqrt(gx * gx + gy * gy);

    return vec4(vec3(edgeIntensity), 1.0);
}

void main() {
  vec3 color = subpassLoad(inputColor).rgb;
  float depth = subpassLoad(inputDepth).r;
  float adjustedDepth = (depth - push.depthMin) * 1.0 / (push.depthMax - push.depthMin);

  vec3 fogColor;

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

    fogColor = mix(vec3(0.0, 0.0, 0.0), color, fogVisibility);
  }

  // float lum = luminance(color);
  // float edge = edgeDetection(uv);
  // edge = smoothstep(push.edgeLow, push.edgeHigh, edge);
  // float stippleEffect = stipple(uv, lum);
  // vec3 finalColor = vec3(1.0 - edge) * stippleEffect * adjustedDepth;

  // outColor = vec4(color, 1.0);
  // outColor = vec4(finalColor, 1.0);
  // outColor = mix(vec4(color, 1.0), vec4(finalColor, 1.0), 1.0);

  // vec4 pixel = pixelEffect(color, uv, depth);
  // vec4 tst = whatTheFuckAreYouCooking();
  // outColor = mix(vec4(color, 1.0), tst, 0.5);
  outColor = vec4(color, 1.0);
}