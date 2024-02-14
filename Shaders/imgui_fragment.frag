#version 450

layout(binding = 0) uniform sampler2D FontTexture;

layout (location = 0) in vec4 color;
layout (location = 1) in vec2 texCoord;
layout (location = 0) out vec4 outputColor;

void main() {
  // outputColor = color * texture(sampler2D(FontTexture, FontSampler), texCoord);
  // inColor * texture(fontSampler, inUV);
  outputColor = color * texture(FontTexture, texCoord);
}