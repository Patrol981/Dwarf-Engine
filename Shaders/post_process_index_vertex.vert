#version 460

layout(location = 0) out vec2 uv;

void main() {
  vec2 position = vec2((gl_VertexIndex << 1) & 2, gl_VertexIndex & 2) * 2.0f - 1.0f;
	gl_Position = vec4(position, 0.0f, 1.0f);
  uv = position * 0.5f + 0.5f;
}