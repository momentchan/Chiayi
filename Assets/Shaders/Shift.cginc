#ifndef PI
    #define PI 3.14159265359
#endif

// ---------- Hash utils ----------
uint HashU32(uint x) {
    x ^= x >> 17; x *= 0xED5AD4BBu;
    x ^= x >> 11; x *= 0xAC4C1B51u;
    x ^= x >> 15; x *= 0x31848BABu;
    x ^= x >> 14;
    return x;
}

float Hash01(uint x) {
    return (HashU32(x) >> 8) * (1.0 / 16777216.0); // 24-bit to [0,1)
}

// ---------- Math helpers ----------
float WrapDist01(float a, float b) {
    // wrap distance in [0, 0.5]
    float d = abs(a - b);
    return min(d, 1.0 - d);
}

float RaisedCosineBump(float x01) {
    // x01: 0..1 (0=center, 1=edge)
    // return 0..1, center=1, edge=0, and continuous first derivative
    return 0.5 * (1.0 + cos(PI * x01));
}

// pulse envelope: smooth, adjustable width and strength, and time-varying intensity
float ComputePulse(
    float t,                 // 0..1 (current step phase)
    float time,
    float pulseFreq,         // PulseParams.x
    float pulseStrength,     // PulseParams.y
    float pulseIntensityChg, // PulseParams.z
    float pulseWidth,        // PulseParams.w (半寬，建議 0<p≤0.5)
    float pulseSpeed         // PulseSpeed
) {
    float phaseT   = frac(t * pulseFreq);
    float phaseCtr = frac(pulseSpeed * time);

    // wrap distance → normalize to 0..1, for window function
    float d = WrapDist01(phaseT, phaseCtr);
    float invWidth = 1.0 / max(pulseWidth, 1e-4);
    float a = saturate(1.0 - d * invWidth);      // 0..1 (0=outer, 1=center)

    float bump = (a > 0.0) ? RaisedCosineBump(1.0 - a) : 0.0; // center is strongest
    float intensityMod = (sin(pulseIntensityChg * time) * 0.3 + 0.5); // 0.2..0.8
    return 1.0 + bump * pulseStrength * intensityMod;
}

float JitterT(int i, float invN, float randomStrength, float seedFloat) {
    // use user seed for random number (together with i)
    uint seed = (uint)i ^ HashU32(asuint(seedFloat * 1e6));
    seed = seed * 747796405u ^ 2891336453u;
    float r = (Hash01(seed) - 0.5) * 2.0; // [-1,1]
    return r * invN * randomStrength;
}

void Shifting_float(
    UnityTexture2D Texture, UnitySamplerState Sampler,
    float2 UV,
    float Steps,
    float MaxSpanUV,
    float WaveFreq,
    float WaveStrength,
    float Sigma,
    float Strength,
    float RandomStrength,
    float Seed,
    float Time,
    float PulseSpeed,
    float4 PulseParams,     // x=freq, y=strength, z=intensityChange, w=width
    out float4 Out
) {
    // step count processing and early return
    int steps = max(1, (int)round(Steps));
    if (steps <= 1) {
        Out = SAMPLE_TEXTURE2D(Texture, Sampler, UV) * Strength;
        return;
    }

    float invN = 1.0 / max(1.0, (Steps - 1.0));
    float invTwoSigma2 = 1.0 / max(2.0 * Sigma * Sigma, 1e-6);

    // pre-fetch parameters to avoid multiple reads in loop
    float pulseFreq          = PulseParams.x;
    float pulseStrength      = PulseParams.y;
    float pulseIntensityChg  = PulseParams.z;
    float pulseWidth         = PulseParams.w;

    float4 accum = 0.0;

    [loop]
    for (int i = 0; i < steps; i++) {
        // basic phase and jitter
        float t  = i * invN;
        float tj = saturate(t + JitterT(i, invN, RandomStrength, Seed));

        // UV offset (-MaxSpanUV..+MaxSpanUV)
        float p = lerp(-MaxSpanUV, MaxSpanUV, tj);

        // wave direction: X stable, Y time-varying with step sine wave
        float2 dir = float2(1.0, sin(t * WaveFreq + Time * 0.1) * WaveStrength);
        float2 offset = dir * p;

        // Gaussian weight
        float w = exp(-(p * p) * invTwoSigma2);

        // smooth pulse (time-varying)
        float pulse = ComputePulse(t, Time, pulseFreq, pulseStrength, pulseIntensityChg, pulseWidth, PulseSpeed);

        // accumulate
        accum += SAMPLE_TEXTURE2D(Texture, Sampler, UV + offset) * (Strength * w * pulse);
        // for energy conservation, can change to: *(Strength / steps) or normalize w
    }

    Out = accum;
}
