#version 460

#include directional_light
#include point_light

layout(location = 0) in vec2 uv;
layout(location = 1) in vec2 position;

layout(location = 0) out vec4 outColor;

layout(set = 0, binding = 0) uniform sampler2D _colorSampler;
layout(set = 0, binding = 1) uniform sampler2D _depthSampler;

layout(set = 1, binding = 0) #include global_ubo

const mat3 kx = mat3(
	vec3(-1, 0, 1),
	vec3(-2, 0, 2),
	vec3(-1, 0, 1)
);
// y direction kernel
const mat3 ky = mat3(
	vec3(-1, -2, -1),
	vec3(0, 0, 0),
	vec3(1, 2, 1)
);

// Controls kernel diameter
const int _KernelSize = 4;
// Controls kernel segments
const int _N = 8;

// Controls high frequency detail
const float _Hardness = 8.0;
// Controls sharpness of "paint splotches"
const float _Sharpness = 16.0;
// Controls alpha value
const float _Alpha = 1.0;
// Controls kernel threshold
const float _ZeroCrossing = 0.58;
// Controls polynomial weights distribution, high numbers equivalent to blurring image
const float _Zeta = 0.1;

float detectEdgeSobel(sampler2D pTexture, vec2 pUv, vec2 pTexelSize) {
  float Gx[9] = float[](
    -1.0,  0.0,  1.0,
    -2.0,  0.0,  2.0,
    -1.0,  0.0,  1.0
  );

  float Gy[9] = float[](
    -1.0, -2.0, -1.0,
    0.0,  0.0,  0.0,
    1.0,  2.0,  1.0
  );

  vec3 smp[9];

  for(int i = 0; i < 3; i++) {
    for(int j = 0; j < 3; j++) {
      vec2 offset = vec2(float(i - 1), float(j - 1)) * pTexelSize;
      smp[i * 3 + j] = texture(pTexture, uv + offset).rgb;
    }
  }

  float edgeX = 0.0;
  float edgeY = 0.0;

  for(int i = 0; i < 9; i++) {
    // float intensity = dot(smp[i], vec3(0.299, 0.587, 0.114)); // Convert to grayscale
    float intensity = dot(smp[i], vec3(1, 1, 1));
    edgeX += intensity * Gx[i];
    edgeY += intensity * Gy[i];
  }

  return length(vec2(edgeX, edgeY));
}

vec4 sobel(vec2 screen_size, vec2 uv) {
	// Calculate Sobel to approximate structure tensor
	vec2 d = screen_size.xy;

	vec3 sobel_x = (
		1.0f * texture(_colorSampler, uv + vec2(-d.x, -d.y)).rgb +
		2.0f * texture(_colorSampler, uv + vec2(-d.x,  0.0)).rgb +
		1.0f * texture(_colorSampler, uv + vec2(-d.x,  d.y)).rgb +
		-1.0f * texture(_colorSampler, uv + vec2(d.x, -d.y)).rgb +
		-2.0f * texture(_colorSampler, uv + vec2(d.x,  0.0)).rgb +
		-1.0f * texture(_colorSampler, uv + vec2(d.x,  d.y)).rgb
		) / 4.0f;
	vec3 sobel_y = (
		1.0f * texture(_colorSampler, uv + vec2(-d.x, -d.y)).rgb +
		2.0f * texture(_colorSampler, uv + vec2( 0.0, -d.y)).rgb +
		1.0f * texture(_colorSampler, uv + vec2( d.x, -d.y)).rgb +
		-1.0f * texture(_colorSampler, uv + vec2(-d.x, d.y)).rgb +
		-2.0f * texture(_colorSampler, uv + vec2( 0.0, d.y)).rgb +
		-1.0f * texture(_colorSampler, uv + vec2( d.x, d.y)).rgb
		) / 4.0f;

	// Structure Tensor (4x4 matrix)
	return vec4(dot(sobel_x, sobel_x), dot(sobel_y, sobel_y), dot(sobel_x, sobel_y), 1.0);
}

float gaussian(float sigma, float pos) {
	return (1.0f / sqrt(2.0f * 3.14 * sigma * sigma)) * exp(-(pos * pos) / (2.0f * sigma * sigma));
}

vec4 blur(vec4 tensor, vec2 uv, vec2 d) {
	// Gaussian Blur
	int kernelRadius = 5;

	vec4 col = vec4(0.0);
	float kernelSum = 0.0;

	// Blur x pass
	for (int x = -kernelRadius; x <= kernelRadius; x++) {
		// Apply gaussian weights to current pixel color multiplied by tensor
		vec4 c = tensor * texture(_colorSampler, uv + vec2(float(x), 0.0) * d.xy);
		float gauss = gaussian(2.0, float(x));

		// Return current pixel color multiplied by weight
		col += c * gauss;
		kernelSum += gauss;
	}
	// Normalize color
	col = col / kernelSum;

	// Blur y pass
	for (int y = -kernelRadius; y <= kernelRadius; y++)
	{
		// Apply gaussian weights to current pixel color multiplied by tensor
		vec4 c = tensor * texture(_colorSampler, uv + vec2(0.0, float(y)) * d.xy);
		float gauss = gaussian(2.0, float(y));

		// Return current pixel color multiplied by weight
		col += c * gauss;
		kernelSum += gauss;
	}

	// Normalize color
	return vec4(col / kernelSum);
}

vec4 scaling_factor(vec4 t) {
	// Calculate Eigenvalues
	float lambda1 = 0.5 * (t.x + t.y + sqrt(t.x * t.x - 2.0 * t.x * t.y + t.y * t.y + 4.0 * t.z * t.z));
	float lambda2 = 0.5 * (t.x + t.y - sqrt(t.x * t.x - 2.0 * t.x * t.y + t.y * t.y + 4.0 * t.z * t.z));

	// Calculate and Normalize Eigenvector
	vec2 v = vec2(lambda1 - t.x, -t.z);
	vec2 n = vec2(0.0, 0.0);

	if (length(v) > 0.0)
	{
		n = normalize(v);
	}
	else
	{
		n = vec2(0.0, 1.0);
	}

	// Angle relative to x-axis of Eigenvector
	float phi = -atan(n.y / n.x);

	// Scaling Factor
	float A = 0.0;

	if (lambda1 + lambda2 > 0.0)
	{
		A = (lambda1 - lambda2) / (lambda1 + lambda2);
	}

	// Kernel deform factors
	return vec4(n, phi, A);
}

vec4 kuwahara(vec2 n, float phi, float A, vec2 d, vec2 uv) {
	// Kuwahara Filter
	int radius = _KernelSize / 2;
	float a = float((radius)) * clamp((_Alpha + A) / _Alpha, 0.1, 2.0);
	float b = float((radius)) * clamp(_Alpha / (_Alpha + A), 0.1, 2.0);

	// Displace kernel
	float cos_phi = cos(phi);
	float sin_phi = sin(phi);

	mat2 R = mat2(vec2(cos_phi, -sin_phi), vec2(sin_phi, cos_phi));
	mat2 S = mat2(vec2(0.5 / a, 0.0), vec2(0.0, 0.5 / b));

	mat2 SR = matrixCompMult(S, R);


	// Find kernel radius
	int max_x = int(sqrt(a * a * cos_phi * cos_phi + b * b * sin_phi * sin_phi));
	int max_y = int(sqrt(a * a * sin_phi * sin_phi + b * b * cos_phi * cos_phi));

	// Contrast threshold
	float sinZeroCross = sin(_ZeroCrossing);
	float eta = (_Zeta + cos(_ZeroCrossing)) / (sinZeroCross * sinZeroCross);

	// Initialize weighting matrices
	vec4 m[8];
	vec3 s[8];

	for (int k = 0; k < _N; k++) {
		m[k] = vec4(0.0);
		s[k] = vec3(0.0);
	}

	// Calculate Kuwahara filter weights
	for (int y = -max_y; y <= max_y; y++) {
		for(int x = -max_x; x <= max_x; x++) {
			vec2 vec = SR * vec2(float(x), float(y));
			// Calculates weight if within shifted radius
			if (dot(vec, vec) <= 0.25) {
				vec3 c = texture(_colorSampler, uv + vec2(float(x), float(y)) * d).rgb;
				c = clamp(c, 0.0, 1.0);
				float sum = 0.0;
				float w[8];
				float z, vxx, vyy;

				// Polynomial Weights
				vxx = _Zeta - eta * vec.x * vec.x;
				vyy = _Zeta - eta * vec.y * vec.y;
				z = max(0, vec.y + vxx);
				w[0] = z * z;
				sum += w[0];
				z = max(0, -vec.x + vyy);
				w[2] = z * z;
				sum += w[2];
				z = max(0, -vec.y + vxx);
				w[4] = z * z;
				sum += w[4];
				z = max(0, vec.x + vyy);
				w[6] = z * z;
				sum += w[6];
				vec = sqrt(2.0) / 2.0 * vec2(vec.x - vec.y, vec.x + vec.y);
				vxx = _Zeta - eta * vec.x * vec.x;
				vyy = _Zeta - eta * vec.y * vec.y;
				z = max(0, vec.y + vxx);
				w[1] = z * z;
				sum += w[1];
				z = max(0, -vec.x + vyy);
				w[3] = z * z;
				sum += w[3];
				z = max(0, -vec.y + vxx);
				w[5] = z * z;
				sum += w[5];
				z = max(0, vec.x + vyy);
				w[7] = z * z;
				sum += w[7];

				float g = exp(-3.125 * dot(vec, vec)) / sum;

				// Calculates polynomial weight
				for (int k = 0; k < 8; k++) {
					float wk = w[k] * g;
					m[k] += vec4(c * wk, wk);
					s[k] += c * c * wk;
				}
			}
		}
	}

	// Calculates output color
	vec4 output_color = vec4(0.0);
  for (int k = 0; k < _N; ++k) {
    m[k].rgb /= m[k].w;
		s[k] = abs(s[k] / m[k].w - m[k].rgb * m[k].rgb);

		float sigma2 = s[k].r + s[k].g + s[k].b;
		float w = 1.0 / (1.0 + pow(_Hardness * 1000.0 * sigma2, 0.5 * _Sharpness));

		output_color += vec4(m[k].rgb * w, w);
	}
	// Normalize color output
	return clamp(output_color / output_color.w, 0.0, 1.0);
}

void main() {
  vec3 screen_color = texture(_colorSampler, uv).rgb;
  vec3 screen_normal = texture(_colorSampler, uv).rgb * 2.0 - 1.0;
  screen_normal = normalize(screen_normal);

  vec2 texelSize = vec2(1.0 / ubo.screenSize.x, 1.0 / ubo.screenSize.y);
  float edge = detectEdgeSobel(_depthSampler, uv, texelSize);
  edge = pow(edge, 0.6);
  vec3 final_color = mix(screen_color, vec3(0.0), edge);

  vec4 tensor = sobel(ubo.screenSize, uv);
	vec4 fac = scaling_factor(tensor);
	vec4 blur = blur(fac, uv, ubo.screenSize);
  outColor = kuwahara(blur.xy, blur.z, blur.w, ubo.screenSize, uv);

  // outColor = vec4(final_color, 1.0);
}