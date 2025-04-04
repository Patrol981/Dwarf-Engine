#version 460

layout (location = 0) in vec2 fragOffset;
layout (location = 0) out vec4 outColor;

layout (push_constant) uniform Push {
  vec4 position;
  vec4 color;
  float scale;
  float rotation;
} push;

#include directional_light

layout (set = 0, binding = 0) #include global_ubo

void main() {
  outColor = vec4(push.color.xyz, 1.0);
}