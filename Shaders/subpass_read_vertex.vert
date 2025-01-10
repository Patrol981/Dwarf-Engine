#version 460

#include directional_light
#include point_light

layout(set = 1, binding = 0) #include global_ubo

void main() {
	gl_Position = vec4(vec2((gl_VertexIndex << 1) & 2, gl_VertexIndex & 2) * 2.0f - 1.0f, 0.0f, 1.0f);
}