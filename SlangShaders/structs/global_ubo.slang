#pragma once

#include "./directional_light.slang"

struct GlobalUBO
{
    float4x4 view;
    float4x4 projection;
    float3 cameraPosition;
    int layer;
    DirectionalLight directionalLight;
    int pointLightLength;
    int hasImportantEntity;
    float3 importantEntityPosition;
}
