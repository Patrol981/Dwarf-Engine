#version 450

layout (location = 0) in vec2 texCoord;

layout (location = 0) out vec4 outColor;

layout (set = 0, binding = 0) uniform GlobalUbo {
  mat4 view;
  mat4 projection;
  vec3 lightPosition;
  vec4 lightColor;
  vec4 ambientLightColor;
} globalUBO;

layout (set = 1, binding = 0) uniform SpriteUBO {
  mat4 spriteMatrix;
  vec3 spriteColor;
  bool useTexture;
} spriteUBO;

layout (set = 2, binding = 0) uniform sampler2D textureSampler;

void main() {
  if(spriteUBO.useTexture) {
    outColor = vec4(spriteUBO.spriteColor, 1.0) * texture(textureSampler, texCoord);
  } else {
    outColor = vec4(spriteUBO.spriteColor, 1.0);
  }
}