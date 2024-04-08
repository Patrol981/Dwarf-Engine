#version 450

layout (location = 0) in vec2 fragOffset;

layout (location = 0) out vec4 outColor;

layout (push_constant) uniform Push {
  mat4 transform;
  int guizmoType;
} push;

layout (set = 0, binding = 0) uniform GlobalUbo {
  mat4 view;
  mat4 projection;
  vec3 lightPosition;
  vec4 lightColor;
  vec4 ambientLightColor;
  vec3 cameraPosition;
  int layer;
} ubo;

void main() {
  if(push.guizmoType == 0) {
    float dis = sqrt(dot(fragOffset, fragOffset));
    if(dis > 1.0) discard;
  }
  outColor = vec4(ubo.lightColor.xyz, 1.0);
}