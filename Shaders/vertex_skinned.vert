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

layout(push_constant) uniform Push {
    mat4 transform;
    mat4 normalMatrix;
} push;

layout(set = 1, binding = 0) #include global_ubo

// 500 FPS on avg
// TODO: optimize set, so its reusable across all models?
layout(set = 3, binding = 0) #include skinned_model_ubo

layout(std140, set = 5, binding = 0) readonly buffer JointBuffer {
    mat4 jointMatrices[];
} jointBuffer;

layout(std140, set = 2, binding = 0) readonly buffer ObjectBuffer {
    ObjectData objectData[];
} objectBuffer;

int MAX_JOINT_INFLUENCE = 4;

void main() {
    mat4 skinMat =
        jointWeights.x * jointBuffer.jointMatrices[int(jointIndices.x)] +
            jointWeights.y * jointBuffer.jointMatrices[int(jointIndices.y)] +
            jointWeights.z * jointBuffer.jointMatrices[int(jointIndices.z)] +
            jointWeights.w * jointBuffer.jointMatrices[int(jointIndices.w)];

    vec4 animatedPosition = vec4(0.0f);
    mat4 jointTransform = mat4(0.0f);
    for (int i = 0; i < MAX_JOINT_INFLUENCE; ++i) {
        if (jointWeights[i] == 0) {
            continue;
        }
        if (jointIndices[i] >= 100) {
            animatedPosition = vec4(position, 1.0f);
            jointTransform = mat4(1.0f);
            break;
        }

        vec4 localPosition = jointBuffer.jointMatrices[jointIndices[i]] * vec4(position, 1.0f);
        animatedPosition += localPosition * jointWeights[i];
        jointTransform += jointBuffer.jointMatrices[jointIndices[i]] * jointWeights[i];
    }

    // vec4 positionWorld = push.transform * skinMat * vec4(position, 1.0);
    // vec4 positionWorld = objectBuffer.objectData[gl_BaseInstance].transformMatrix * skinMat * vec4(position, 1.0);
    mat4 modelMatrix = objectBuffer.objectData[gl_BaseInstance].transformMatrix;
    vec4 positionWorld = animatedPosition * modelMatrix;

    // vec4 positionWorld = skinMat * vec4(position, 1.0);

    // vec4 positionWorld =  totalPosition;
    gl_Position = ubo.projection * ubo.view * positionWorld;

    // fragNormalWorld = normalize(mat3(objectBuffer.objectData[gl_BaseInstance].normalMatrix) * normal);
    mat3 normalMatrix = mat3(objectBuffer.objectData[gl_BaseInstance].normalMatrix) * mat3(jointTransform);
    fragNormalWorld = normalize(normalMatrix * normal);
    fragPositionWorld = positionWorld.xyz;
    fragColor = color;
    texCoord = uv;
}
