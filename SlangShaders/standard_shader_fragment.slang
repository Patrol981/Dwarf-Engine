#include "./structs/global_ubo.slang"
#include "./structs/model_ubo.slang"
#include "./structs/object_data.slang"
#include "./structs/point_light.slang"

struct VertexOutput
{
    float4 position : SV_Position;
    float3 fragColor : TEXCOORD0;
    float3 fragPositionWorld : TEXCOORD1;
    float3 fragNormalWorld : TEXCOORD2;
    float2 texCoord : TEXCOORD3;
    int filterFlag : FLAT_TEXCOORD4;
};

// Outputs
struct FragmentOutput
{
    float4 color : SV_Target;
};

cbuffer GlobalUBO : register(b1)
{
    GlobalUBO ubo;
};

cbuffer ModelUBO : register(b3)
{
    GlobalUBO model;
};

StructuredBuffer<PointLight> pointLightBuffer : register(t4);

Texture2D textureSampler : register(t0);
SamplerState samplerState : register(s0);
// Texture2DArray arraySampler : register(t1);

// Push constants
cbuffer Push : register(b5)
{
    float4x4 transform;
    float4x4 normalMatrix;
};

float2 applyDistortion(float3 pos, float2 uv, float str, float radius)
{
    float3 direction = normalize(pos - ubo.importantEntityPosition);
    float distance = length(pos - ubo.importantEntityPosition);

    float effectStrength = smoothstep(radius, 0.0, distance) * str;

    return uv + direction.xy * effectStrength;
}

FragmentOutput main(VertexOutput input)
{
    FragmentOutput output;

    float3 surfaceNormal = normalize(input.fragNormalWorld);
    float3 viewDir = normalize(ubo.cameraPosition - input.fragPositionWorld);
    float3 result = float3(0, 0, 0);

    float alpha = 1.0;
    float4 colorMod;

    if (ubo.hasImportantEntity == 1 && input.filterFlag == 1)
    {
        float radiusHorizontal = 1.0;
        float distortionStrength = 1.0;

        float dist = distance(input.fragPositionWorld.xz, ubo.importantEntityPosition.xz);

        float fragToCamera = distance(input.fragPositionWorld, ubo.cameraPosition);
        float entityToCamera = distance(ubo.importantEntityPosition, ubo.cameraPosition);

        if (fragToCamera <= entityToCamera && dist < radiusHorizontal)
        {
            alpha = 0.5;
            float2 distortion = applyDistortion(input.fragPositionWorld, input.texCoord, distortionStrength, radiusHorizontal);
            colorMod = textureSampler.Sample(samplerState, distortion);
        }
        else
        {
            colorMod = textureSampler.Sample(samplerState, input.texCoord);
        }
    }
    else
    {
        colorMod = textureSampler.Sample(samplerState, input.texCoord);
    }

    result += calc_dir_light(ubo.directionalLight, surfaceNormal, viewDir);
    for (int i = 0; i < ubo.pointLightLength; i++)
    {
        result += calc_point_light(pointLightBuffer[i], surfaceNormal, viewDir);
    }

    if (ubo.layer == 0)
    {
        output.color = float4(result, alpha);
    }
    else if (ubo.layer == 1)
    {
        output.color = colorMod * float4(result, alpha);
    }

    return output;
}
