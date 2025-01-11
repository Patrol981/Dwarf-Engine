#version 460

#include directional_light
#include point_light

layout (input_attachment_index = 0, binding = 0) uniform subpassInput inputColor;
layout (input_attachment_index = 1, binding = 1) uniform subpassInput inputDepth;

layout (location = 0) out vec4 outColor;

layout(set = 1, binding = 0) #include global_ubo

const int MAX_STEPS = 100;       // Max raymarching steps
const float STEP_SIZE = 0.1;     // Raymarching step size
const float EPSILON = 0.01;      // Intersection threshold

void main() {
  vec3 color = subpassLoad(inputColor).rgb;
  float depth = subpassLoad(inputDepth).r;

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

  outColor = vec4(color, 1.0);
}