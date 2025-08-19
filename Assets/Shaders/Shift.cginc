uint HashU32(uint x) {
    x ^= x >> 17; x *= 0xed5ad4bbu;
    x ^= x >> 11; x *= 0xac4c1b51u;
    x ^= x >> 15; x *= 0x31848babu;
    x ^= x >> 14;
    return x;
}

float Hash01(uint x) {
    return (HashU32(x) >> 8) * (1.0 / 16777216.0); // 24 bits -> 2^24
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
    out float4 Out
)
{
    float4 accum = 0;

    if (Steps <= 1)
    {
        accum += SAMPLE_TEXTURE2D(Texture, Sampler, UV) * Strength;
        Out = accum;
        return;
    }

    float invN = 1.0 / (Steps - 1); // 每段的比例長度 (t 空間)

    for (int i = 0; i < Steps; i++)
    {
        // 原本的線性比例 t
        float t = i * invN;

        // 在 invN 單位上加隨機擾動
        uint seed = (uint)i;
        seed = seed * 747796405u ^ 2891336453u;
        float rnd = Hash01(seed);          // [0,1)
        float r   = (rnd - 0.5) * 2.0;     // [-1,1]

        // jitter 依 RandomStrength 漸進，1 = 最大 (±invN)
        float jitter = r * invN * RandomStrength;

        float tJittered = saturate(t + jitter); // 保護範圍 0..1

        // 對應到 UV 偏移範圍
        float p = lerp(-MaxSpanUV, MaxSpanUV, tJittered);

        // 波動方向
        float2 dir = float2(1, sin(t * WaveFreq + Time) * WaveStrength);

        float2 offset = dir * p;

        // Gaussian 權重
        float w = exp(-(p * p) / (2.0 * Sigma * Sigma));

        accum += SAMPLE_TEXTURE2D(Texture, Sampler, UV + offset) * (Strength * w);
    }

    Out = accum;
}
