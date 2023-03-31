#version 450

layout(set = 0, binding = 1) uniform texture2D FontTexture; // 1 0
layout(set = 0, binding = 2) uniform sampler FontSampler; // 0 1

layout (location = 0) in vec4 color;
layout (location = 1) in vec2 texCoord;
layout (location = 0) out vec4 outputColor;

void main()
{
    outputColor = color * texture(sampler2D(FontTexture, FontSampler), texCoord);
}