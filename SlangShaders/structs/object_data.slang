#pragma once

struct ObjectData
{
    float4x4 transformMatrix;
    float4x4 normalMatrix;
    float4x4 nodeMatrix;
    int filterFlag;
    float4 jointsBufferOffset;
};
