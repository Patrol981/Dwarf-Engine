#version 460

layout(location = 0) in vec3 position;
layout(location = 1) in vec3 color;
layout(location = 2) in vec3 normal;
layout(location = 3) in vec2 uv;
layout(location = 4) in ivec4 jointIndices;
layout(location = 5) in vec4 jointWeights;

layout(location = 0) out vec3 fragColor;
layout(location = 1) out vec3 fragPositionWorld;
layout(location = 2) out vec3 fragNormalWorld;
layout(location = 3) out vec2 texCoord;

#include material

#include directional_light
#include point_light
#include object_data

layout(set = 1, binding = 0) #include global_ubo

// 500 FPS on avg
// TODO: optimize set, so its reusable across all models?
layout(set = 3, binding = 0) #include skinned_model_ubo

#define MAX_NUM_JOINTS 128
layout(std140, set = 5, binding = 0) uniform JointBuffer {
    mat4 jointMatrices[MAX_NUM_JOINTS];
} jointBuffer;

layout(std140, set = 2, binding = 0) readonly buffer ObjectBuffer {
    ObjectData objectData[];
} objectBuffer;

int MAX_JOINT_INFLUENCE = 4;

vec3 applyBoneTransform(vec4 p) {
    vec4 result = vec4(0.0);
    for (int i = 0; i < 4; ++i) {
        mat4 boneTransform = jointBuffer.jointMatrices[jointIndices[i]];
        result += jointWeights[i] * (boneTransform * vec4(position, 1.0));
        result /= dot(jointWeights, vec4(1.0));
    }
    return result.xyz;
}

void main() {
    mat4 skinMat =
        jointWeights.x * jointBuffer.jointMatrices[jointIndices.x] +
            jointWeights.y * jointBuffer.jointMatrices[jointIndices.y] +
            jointWeights.z * jointBuffer.jointMatrices[jointIndices.z] +
            jointWeights.w * jointBuffer.jointMatrices[jointIndices.w];

    vec4 positionWorld =
        objectBuffer.objectData[gl_BaseInstance].transformMatrix *
            objectBuffer.objectData[gl_BaseInstance].nodeMatrix *
            skinMat *
            vec4(position, 1.0);

    vec3 worldPos = positionWorld.xyz / positionWorld.w;
    gl_Position = ubo.projection * ubo.view * vec4(worldPos, 1.0);

    fragNormalWorld = normalize(mat3(objectBuffer.objectData[gl_BaseInstance].normalMatrix) * normal);

    fragPositionWorld = positionWorld.xyz;
    fragColor = color;
    texCoord = uv;
}
