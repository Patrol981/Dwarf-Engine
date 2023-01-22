#version 450

layout (location = 0) in vec2 position;
layout (location = 1) in vec3 color;

// layout (location = 0) out vec3 fragColor;

layout (push_constant) uniform Push {
  mat4 transform;
  vec2 offset;
  vec3 color;
} push;

void main() {
  mat2 actualMat = mat2(push.transform[0][0], push.transform[0][1], push.transform[1][0], push.transform[1][1]);
  gl_Position = vec4(actualMat * position + push.offset, 0.0, 1.0);
  // fragColor = push.color;
}