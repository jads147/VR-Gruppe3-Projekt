#ifndef TERRAIN_BLEND_INCLUDED
#define TERRAIN_BLEND_INCLUDED

// Blends five terrain layers by the mesh's vertex colors (the "Splat" attribute exported from Blender):
//   R = Sand, G = Rock, B = Snow, A = Lakebed, Grass = whatever weight is left (1 - R - G - B - A).
// Used by TerrainBlend.shadergraph through a Custom Function node (File mode, name "TerrainBlend").
//
// Height blending: vertex colors only change every ~1.1 m, so a plain weighted sum gives soft, blurry
// borders. Instead, each layer's weight is raised by its texture brightness (bright grains / stones win
// first) and only layers within `Transition` of the strongest one survive. Small Transition = crisp border.

float TerrainBlend_Luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }

void TerrainBlend_float(
    float4 Weights, float2 UV, float Tiling,
    UnityTexture2D GrassTex, UnityTexture2D SandTex, UnityTexture2D RockTex,
    UnityTexture2D SnowTex, UnityTexture2D LakebedTex,
    float4 GrassTint, float4 SandTint, float4 RockTint, float4 SnowTint, float4 LakebedTint,
    float Transition, float HeightBlend,
    out float3 Color)
{
    float sand  = saturate(Weights.r);
    float rock  = saturate(Weights.g);
    float snow  = saturate(Weights.b);
    float lake  = saturate(Weights.a);
    float grass = saturate(1.0 - (sand + rock + snow + lake));

    float2 uv = UV * Tiling;
    float3 tG = SAMPLE_TEXTURE2D(GrassTex.tex,   GrassTex.samplerstate,   uv).rgb;
    float3 tS = SAMPLE_TEXTURE2D(SandTex.tex,    SandTex.samplerstate,    uv).rgb;
    float3 tR = SAMPLE_TEXTURE2D(RockTex.tex,    RockTex.samplerstate,    uv).rgb;
    float3 tN = SAMPLE_TEXTURE2D(SnowTex.tex,    SnowTex.samplerstate,    uv).rgb;
    float3 tL = SAMPLE_TEXTURE2D(LakebedTex.tex, LakebedTex.samplerstate, uv).rgb;

    // weight + texture "height"
    float aG = grass + TerrainBlend_Luma(tG) * HeightBlend;
    float aS = sand  + TerrainBlend_Luma(tS) * HeightBlend;
    float aR = rock  + TerrainBlend_Luma(tR) * HeightBlend;
    float aN = snow  + TerrainBlend_Luma(tN) * HeightBlend;
    float aL = lake  + TerrainBlend_Luma(tL) * HeightBlend;

    float t = max(Transition, 1e-3);
    float m = max(max(max(aG, aS), max(aR, aN)), aL) - t;

    // keep only layers near the top; layers with (almost) no weight never leak in
    float bG = max(aG - m, 0) * saturate(grass * 20);
    float bS = max(aS - m, 0) * saturate(sand  * 20);
    float bR = max(aR - m, 0) * saturate(rock  * 20);
    float bN = max(aN - m, 0) * saturate(snow  * 20);
    float bL = max(aL - m, 0) * saturate(lake  * 20);
    float total = max(bG + bS + bR + bN + bL, 1e-5);

    Color = (tG * GrassTint.rgb * bG + tS * SandTint.rgb * bS + tR * RockTint.rgb * bR +
             tN * SnowTint.rgb  * bN + tL * LakebedTint.rgb * bL) / total;
}

void TerrainBlend_half(
    half4 Weights, half2 UV, half Tiling,
    UnityTexture2D GrassTex, UnityTexture2D SandTex, UnityTexture2D RockTex,
    UnityTexture2D SnowTex, UnityTexture2D LakebedTex,
    half4 GrassTint, half4 SandTint, half4 RockTint, half4 SnowTint, half4 LakebedTint,
    half Transition, half HeightBlend,
    out half3 Color)
{
    float3 c;
    TerrainBlend_float(Weights, UV, Tiling, GrassTex, SandTex, RockTex, SnowTex, LakebedTex,
                       GrassTint, SandTint, RockTint, SnowTint, LakebedTint, Transition, HeightBlend, c);
    Color = c;
}

#endif
