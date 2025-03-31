#pragma once

#include "./material.slang"
#include "./model_ubo.slang"
#include "./point_light.slang"

struct DirectionalLight
{
    float3 lightPosition;
    float lightIntensity;
    float4 lightColor;
    float4 ambientLightColor;
}

float3 calc_point_light(PointLight light, float3 normal, float3 viewDir, float3 fragPositionWorld)
{
    float3 lightDirection = normalize(light.lightPosition.xyz - fragPositionWorld);

    // Diffuse shading
    float diff = max(dot(normal, lightDirection), 0.0);

    // Specular shading
    float3 reflectDir = reflect(-lightDirection, normal);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), model.shininess);

    // Attenuation
    float distance = length(light.lightPosition.xyz - fragPositionWorld);
    float attenuation = 1.0 / (distance + (distance * distance));

    // Combine results
    float3 ambient = model.ambient;
    float3 diffuse = light.lightColor.xyz * diff;
    float3 specular = model.specular * spec;

    // Apply light intensity (stored in w component)
    diffuse *= light.lightColor.w;

    return diffuse;
}

float3 calc_dir_light(DirectionalLight directionalLight, float3 normal, float3 viewDir)
{
    float3 lightDirection = normalize(-directionalLight.lightPosition);

    // Diffuse shading
    float diff = max(dot(normal, lightDirection), 0.0);

    // Specular shading
    float3 reflectDir = reflect(-lightDirection, normal);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), model.shininess);

    // Combine results
    float3 ambient = directionalLight.ambientLightColor.xyz;
    float3 diffuse = directionalLight.lightColor.xyz * (diff * model.diffuse);
    float3 specular = directionalLight.lightColor.xyz * (spec * model.specular) * directionalLight.lightIntensity;

    return ambient + diffuse + specular;
}
