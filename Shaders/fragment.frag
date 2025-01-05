#version 460

layout(location = 0) in vec3 fragColor;
layout(location = 1) in vec3 fragPositionWorld;
layout(location = 2) in vec3 fragNormalWorld;
layout(location = 3) in vec2 texCoord;
layout(location = 4) flat in int filterFlag;
layout(location = 5) in float entityToFragDistance;
layout(location = 6) in float fogVisiblity;

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

// layout(set = 0, binding = 0) uniform sampler2D textureSampler;
layout(set = 0, binding = 0) uniform texture2D _texture;
layout(set = 0, binding = 1) uniform sampler _sampler;
// layout (set = 0, binding = 1) uniform sampler2DArray arraySampler;

layout(set = 1, binding = 0) #include global_ubo

layout(set = 3, binding = 0) #include model_ubo

layout(std140, set = 4, binding = 0) readonly buffer PointLightBuffer {
    PointLight pointLights[];
} pointLightBuffer;

#include light_calc

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

    outColor = colorMod * vec4(result, alpha);
    outColor = mix(vec4(0.0, 0.0, 0.0, 1.0), outColor, fogVisiblity);
}
