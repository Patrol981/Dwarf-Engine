#version 460

layout (location = 0) in vec2 in_position;
layout (location = 1) in vec2 in_texCoord;
layout (location = 2) in vec4 in_color;

layout (push_constant) uniform PushConstants {
	mat4 projection_matrix;
} push;

layout (location = 0) out vec4 color;
layout (location = 1) out vec2 texCoord;

/*
out gl_PerVertex
{
  vec4 gl_Position;
};
*/

void main() {
  gl_Position = push.projection_matrix * vec4(in_position, 0, 1);
  color = in_color;
  texCoord = in_texCoord;
}