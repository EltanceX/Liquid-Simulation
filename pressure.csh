#version 450 core
#define PI 3.1415926535
layout(local_size_x = 64) in;


layout(std430, binding = 0) buffer InputBufferA {
    vec2 PredictedPositions[];
};

layout(std430, binding = 1) buffer InputBufferB {
    float Densities[];
};

layout(std430, binding = 2) buffer InputBufferC {
    vec2 Velocities[];
};

layout(std430, binding = 3) buffer OutputBuffer {
    vec2 VelocitiesOutput[];
};

uniform float mass;
uniform float smoothingRadius;
uniform float targetDensity;
uniform float pressureMultiplier;



float ConvertDensityToPressure(float density)
{
    float densityError = density - targetDensity;
    float pressure = densityError * pressureMultiplier;
    return pressure;
}
float CalculateSharedPressure(float densityA, float densityB)
{
    float pressureA = ConvertDensityToPressure(densityA);
    float pressureB = ConvertDensityToPressure(densityB);
    return (pressureA + pressureB) / 2;
}
float SmoothingKernalDerivate(float dst, float radius)
{
    if (dst >= radius) return 0;
    float scale = 12 / (pow(radius, 4) * PI);
    return (dst - radius) * scale;
}
vec2 CalculatePressureForce(uint idx)
{
    vec2 samplePoint = PredictedPositions[idx];
    vec2 pressureForce = vec2(0.0);

    for (int i = 0; i < PredictedPositions.length(); i++)
    {
        if(i == idx) continue;
        float dst = length(PredictedPositions[i] - samplePoint);
        if (dst == 0) continue;
        vec2 dir = (PredictedPositions[i] - samplePoint) / dst;
        float slope = SmoothingKernalDerivate(dst, smoothingRadius);
        // float density = Densities[i];
        float otherDensity = Densities[i];
        float sharedPressure = CalculateSharedPressure(otherDensity, Densities[idx]);
        pressureForce += sharedPressure * dir * slope * mass / otherDensity;
    }
    return pressureForce;
}
void main() {
    uint idx = gl_GlobalInvocationID.x;

    if (idx >= PredictedPositions.length() || idx < 0) return;
    vec2 pos = PredictedPositions[idx];

    // VelocitiesOutput[idx] = pos;
    // VelocitiesOutput[idx] = vec2(idx);
    VelocitiesOutput[idx] = CalculatePressureForce(idx);
}