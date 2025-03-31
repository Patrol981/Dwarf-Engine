#include "./structs/global_ubo.slang"
#include "./structs/model_ubo.slang"
#include "./structs/object_data.slang"
#include "./structs/point_light.slang"

struct VertexInput
{
    float3 position : ATTRIB0;
    float3 color : ATTRIB1;
    float3 normal : ATTRIB2;
    float2 uv : ATTRIB3;
    int4 jointIndices : ATTRIB4;
    float4 jointWeights : ATTRIB5;
}

struct VertexOutput
{
    float4 position : SV_Position;
    float3 fragColor : TEXCOORD0;
    float3 fragPositionWorld : TEXCOORD1;
    float3 fragNormalWorld : TEXCOORD2;
    float2 texCoord : TEXCOORD3;
    int filterFlag : FLAT_TEXCOORD4;
}

cbuffer GlobalUBO : register(b1)
{
    GlobalUBO ubo;
};

cbuffer ModelUBO : register(b3)
{
    ModelUBO model;
};

StructuredBuffer<ObjectData> objectBuffer : register(t2);
StructuredBuffer<PointLight> pointLightBuffer : register(t4);

VertexOutput main(VertexInput input, uint instanceID: SV_InstanceID)
{
    VertexOutput output;

    // Transform to world space
    float4 positionWorld = mul(objectBuffer[instanceID].transformMatrix,
                               mul(objectBuffer[instanceID].nodeMatrix, float4(input.position, 1.0)));

    output.position = mul(ubo.projection, mul(ubo.view, positionWorld));
    output.fragNormalWorld = normalize(mul((float3x3)objectBuffer[instanceID].normalMatrix, input.normal));
    output.fragPositionWorld = positionWorld.xyz;
    output.fragColor = input.color;
    output.texCoord = input.uv;
    output.filterFlag = objectBuffer[instanceID].filterFlag;

    return output;
}

