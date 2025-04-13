#version 460

layout (location = 0) in vec2 texCoord;

layout (location = 0) out vec4 outColor;

layout (push_constant) uniform Push {
  mat4 transform;
  vec3 spriteColor;
  bool useTexture;
} push;


layout (set = 0, binding = 0) uniform GlobalUbo {
  mat4 view;
  mat4 projection;
  vec3 lightPosition;
  vec4 lightColor;
  vec4 ambientLightColor;
  vec3 cameraPosition;
  int layer;
} globalUBO;


layout (set = 2, binding = 0) uniform texture2D _texture;
layout (set = 2, binding = 1) uniform sampler _sampler;

void main() {
  if(push.useTexture) {
    outColor = vec4(push.spriteColor, 1.0) * texture(sampler2D(_texture, _sampler), texCoord);
  } else {
    outColor = vec4(push.spriteColor, 1.0);
  }
}