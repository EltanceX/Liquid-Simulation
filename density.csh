#version 450 core
#define PI 3.1415926535
// #define mass 2.0
// #define smoothingRadius 0.25
layout(local_size_x = 64) in;
// layout(binding = 0) buffer ParticleBuffer {
//     vec2 position[];
//     vec2 velocity[];
//     float density[];
//     float pressure[];
// } particles;
layout(std430, binding = 0) buffer InputBuffer {
    vec2 PredictedPositions[];
};
layout(std430, binding = 1) buffer OutputBuffer {
    float result[];
};
// uniform float h;
// uniform float mass;
// uniform float restDensity;
uniform float mass;
uniform float smoothingRadius;


float SmoothingKernel(float dst, float radius)
{
    if (dst >= radius) return 0.0;

    float volume = (PI * pow(radius, 4.0)) / 6.0;
    return (radius - dst) * (radius - dst) / volume;
}
float CalculateDensity(vec2 samplePoint)
{
    float density = 1.0;
    for (int i = 0; i < PredictedPositions.length(); i++)
    {
        //float dst = (particle.Position - samplePoint).Length();
        float dst = length(PredictedPositions[i] - samplePoint);
        // return 66.0+dst;
        //Vector2.Normalize()
        // return dst;
        float influence = SmoothingKernel(dst, smoothingRadius);
        density += mass * influence;
    }
    return density;
}
void main() {
    uint idx = gl_GlobalInvocationID.x;
    // result[0] = 0.7;

    if (idx >= PredictedPositions.length() || idx < 0) return;
    vec2 pos = PredictedPositions[idx];
    // vec2 pos = particles.position[idx];
    // float density = 0.0;

    // for (uint i = 0; i < particles.position.length(); i++) {

    // }
    result[idx] = CalculateDensity(pos);
    // result[idx] = mass;
    // result[idx] = pos.x;

    // particles.density[idx] = density;
}