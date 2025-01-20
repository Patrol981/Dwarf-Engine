#version 460

layout(location = 0) in vec3 fragColor;
layout(location = 1) in vec3 fragPositionWorld;
layout(location = 2) in vec3 fragNormalWorld;
layout(location = 3) in vec2 texCoord;
layout(location = 4) flat in int filterFlag;
layout(location = 5) in float entityToFragDistance;
layout(location = 6) in float fogVisiblity;
layout(location = 7) in vec2 screenTexCoord;

layout(location = 0) out vec4 outColor;

#include object_data
#include fog
#include material

#include directional_light
#include point_light

layout(push_constant) uniform Push {
    mat4 transform;
    mat4 normalMatrix;
} push;

layout(set = 0, binding = 0) uniform texture2D _texture;
layout(set = 0, binding = 1) uniform sampler _sampler;

layout(set = 1, binding = 0) uniform texture2D _hatchTexture;
layout(set = 1, binding = 1) uniform sampler _hatchSampler;

layout(set = 2, binding = 0) #include global_ubo
// set 3 = object ssbo
layout(set = 4, binding = 0) #include model_ubo // dynamic
layout(std140, set = 5, binding = 0) readonly buffer PointLightBuffer {
    PointLight pointLights[];
} pointLightBuffer;

layout(set = 6, binding = 0) uniform sampler2D _prevColor;
layout(set = 6, binding = 1) uniform sampler2D _prevDepth;

#include light_calc

vec3 computeViewNormal(vec2 inTexCoords) {
  float depth = texture(_prevDepth, inTexCoords).r;

  vec3 viewPos = vec3(inTexCoords * 2.0 - 1.0, depth);

  vec3 dx = vec3(dFdx(viewPos.x), dFdx(viewPos.y), dFdx(viewPos.z));
  vec3 dy = vec3(dFdy(viewPos.x), dFdy(viewPos.y), dFdy(viewPos.z));

  vec3 normal = normalize(cross(dx, dy));
  return normal;
}

void main() {
    vec3 surfaceNormal = normalize(fragNormalWorld);
    vec3 viewDir = normalize(ubo.cameraPosition - fragPositionWorld);

    vec3 lightColor = ubo.directionalLight.lightColor.xyz;
    vec3 result = vec3(0, 0, 0);

    float alpha = 1.0;
    vec4 colorMod;

    if (ubo.hasImportantEntity == 1 && filterFlag == 1) {
        float radiusHorizontal = 1.0;
        float distortionStrength = 1.0;

        // if(ubo.useFog == 1 && entityToFragDistance > ubo.fog.x) discard;

        float fragToCamera = distance(fragPositionWorld, ubo.cameraPosition);
        float entityToCamera = distance(ubo.importantEntityPosition, ubo.cameraPosition);

        if (fragToCamera <= entityToCamera && entityToFragDistance < radiusHorizontal) {
            alpha = 0.5;
            colorMod = texture(sampler2D(_texture, _sampler), texCoord);
        } else {
            colorMod = texture(sampler2D(_texture, _sampler), texCoord);
        }
    } else if(ubo.useFog == 1 && ubo.hasImportantEntity == 1) {
      // if(entityToFragDistance > ubo.fog.x) discard;
      colorMod = texture(sampler2D(_texture, _sampler), texCoord);
    }  else {
      colorMod = texture(sampler2D(_texture, _sampler), texCoord);
    }

    result += calc_dir_light(ubo.directionalLight, surfaceNormal, viewDir);
    for (int i = 0; i < ubo.pointLightLength; i++) {
        PointLight light = pointLightBuffer.pointLights[i];
        result += calc_point_light(light, surfaceNormal, viewDir);
    }

    // vec2 screen_uv = gl_FragCoord.xy / ubo.screenSize;
    // vec3 screen_color = texture(_prevColor, screen_uv).rgb;

    // float luminance = dot(screen_color, vec3(0.299, 0.587, 0.114));

    // float r_channel = screen_color.r;
    // float pow_r = pow(r_channel,0.3);
    // float inverse_pow_r = 1.0 - pow_r;
    // luminance -= inverse_pow_r;
    // luminance = 1.0 - luminance;
    // vec3 final_color = vec3(luminance);

    // // vec3 normal_sample = normalize(texture(normal_texture, SCREEN_UV).rgb * 2.0 - 1.0);
    // vec2 distorted_uv = screen_uv + fragNormalWorld.xy * 2.0;
    // vec3 hatch1_color = 1.0 - texture(sampler2D(_hatchTexture, _hatchSampler), distorted_uv * 15.0).rgb;
    // final_color *= hatch1_color;

    // final_color = mix(vec3(1.0), vec3(0.0), final_color * 20.0);
    // outColor = vec4(final_color, 1.0);
    outColor = colorMod * vec4(result, alpha);

    // outColor = vec4(mix(hatch.rgb, colorMod.rgb, 0.7), 1.0) * vec4(result, alpha);
    // outColor = mix(vec4(0.0, 0.0, 0.0, 1.0), outColor, fogVisiblity);
}
