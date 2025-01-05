#version 460

layout (input_attachment_index = 0, binding = 0) uniform subpassInput inputColor;
layout (input_attachment_index = 1, binding = 1) uniform subpassInput inputDepth;

layout (location = 0) out vec4 outColor;

void main() {
  vec3 color = subpassLoad(inputColor).rgb;
  vec3 depth = subpassLoad(inputDepth).rgb;
  outColor.rgb = color;
}