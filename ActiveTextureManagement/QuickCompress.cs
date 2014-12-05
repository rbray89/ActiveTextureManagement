// Copyright (c) 2009-2011 Ignacio Castano <castano@gmail.com>
// Copyright (c) 2007-2009 NVIDIA Corporation -- Ignacio Castano <icastano@nvidia.com>
// 
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;


namespace ActiveTextureManagement
{
        class QuickCompress
        {

static void extractColorBlockRGB(ColorBlock rgba, Vector3[] block)
{
        for (uint i = 0; i < 16; i++)
        {
                Color32 c = rgba.color[i];
                block[i] = new Vector3(c.r, c.g, c.b);
        }
}

static uint extractColorBlockRGBA(ColorBlock rgba, Vector3[] block)
{
        uint num = 0;
        
        for (uint i = 0; i < 16; i++)
        {
                Color32 c = rgba.color[i];
                if (c.a > 127)
                {
                        block[num++] = new Vector3(c.r, c.g, c.b);
                }
        }
        
        return num;
}


// find minimum and maximum colors based on bounding box in color space
static void findMinMaxColorsBox(Vector3[] block, uint num, ref Vector3 maxColor, ref Vector3 minColor)
{
        maxColor = new Vector3(0, 0, 0);
        minColor = new Vector3(255, 255, 255);
        
        for (uint i = 0; i < num; i++)
        {
                maxColor = Vector3.Max(maxColor, block[i]);
                minColor = Vector3.Min(minColor, block[i]);
        }
}


static void selectDiagonal(Vector3[] block, uint num, ref Vector3 maxColor, ref Vector3 minColor)
{
        Vector3 center = (maxColor + minColor) * 0.5f;

        Vector2 covariance = new Vector2(0.0f, 0.0f);
        for (uint i = 0; i < num; i++)
        {
                Vector3 t = block[i] - center;
                covariance.x += t.x * t.z;
                covariance.y += t.y * t.y;
        }

        float x0 = maxColor.x;
        float y0 = maxColor.y;
        float x1 = minColor.x;
        float y1 = minColor.y;
        
        if (covariance.x < 0) {
                swap(ref x0, ref x1);
        }
        if (covariance.y < 0) {
                swap(ref y0, ref y1);
        }
        
        maxColor.Set(x0, y0, maxColor.z);
        minColor.Set(x1, y1, minColor.z);
}

static void insetBBox(ref Vector3 maxColor, ref Vector3 minColor)
{
        Vector3 inset = ((maxColor - minColor) / 16.0f) - Vector3.one*(8.0f / 255.0f) / 16.0f;
        maxColor = Vector3.Min(maxColor - inset, Vector3.one * 255.0f);
        maxColor = Vector3.Max(maxColor - inset, Vector3.zero);
        minColor = Vector3.Min(minColor - inset, Vector3.one * 255.0f);
        minColor = Vector3.Max(minColor - inset, Vector3.zero);
}


// Takes a normalized color in [0, 255] range and returns 
static ushort roundAndExpand(ref Vector3 v)
{
    uint r = (uint)Mathf.Floor(Mathf.Clamp(v.x * (31.0f / 255.0f), 0.0f, 31.0f));
    uint g = (uint)Mathf.Floor(Mathf.Clamp(v.y * (63.0f / 255.0f), 0.0f, 63.0f));
    uint b = (uint)Mathf.Floor(Mathf.Clamp(v.z * (31.0f / 255.0f), 0.0f, 31.0f));

    float r0 = ((r+0) << 3) | ((r+0) >> 2);
    float r1 = ((r+1) << 3) | ((r+1) >> 2);
    if (Mathf.Abs(v.x - r1) < Mathf.Abs(v.x - r0)) r = (uint)Mathf.Min(r+1, 31U);

    float g0 = ((g+0) << 2) | ((g+0) >> 4);
    float g1 = ((g+1) << 2) | ((g+1) >> 4);
    if (Mathf.Abs(v.y - g1) < Mathf.Abs(v.y - g0)) g = (uint)Mathf.Min(g+1, 63U);

    float b0 = ((b+0) << 3) | ((b+0) >> 2);
    float b1 = ((b+1) << 3) | ((b+1) >> 2);
    if (Mathf.Abs(v.z - b1) < Mathf.Abs(v.z - b0)) b = (uint)Mathf.Min(b+1, 31U);


        ushort w = (ushort)((r << 11) | (g << 5) | b);

        r = (r << 3) | (r >> 2);
        g = (g << 2) | (g >> 4);
        b = (b << 3) | (b >> 2);
        v = new Vector3(r, g, b);
        
        return w;
}

// Takes a normalized color in [0, 255] range and returns 
static ushort roundAndExpand01(ref Vector3 v)
{
        uint r = (uint)Mathf.Floor(Mathf.Clamp(v.x * 31.0f, 0.0f, 31.0f));
        uint g = (uint)Mathf.Floor(Mathf.Clamp(v.y * 63.0f, 0.0f, 63.0f));
        uint b = (uint)Mathf.Floor(Mathf.Clamp(v.z * 31.0f, 0.0f, 31.0f));

    float r0 = ((r+0) << 3) | ((r+0) >> 2);
    float r1 = ((r+1) << 3) | ((r+1) >> 2);
    if (Mathf.Abs(v.x - r1) < Mathf.Abs(v.x - r0)) r = (uint)Mathf.Min(r+1, 31U);

    float g0 = ((g+0) << 2) | ((g+0) >> 4);
    float g1 = ((g+1) << 2) | ((g+1) >> 4);
    if (Mathf.Abs(v.y - g1) < Mathf.Abs(v.y - g0)) g = (uint)Mathf.Min(g+1, 63U);

    float b0 = ((b+0) << 3) | ((b+0) >> 2);
    float b1 = ((b+1) << 3) | ((b+1) >> 2);
    if (Mathf.Abs(v.z - b1) < Mathf.Abs(v.z - b0)) b = (uint)Mathf.Min(b+1, 31U);


        ushort w = (ushort)((r << 11) | (g << 5) | b);

        r = (r << 3) | (r >> 2);
        g = (g << 2) | (g >> 4);
        b = (b << 3) | (b >> 2);
        v = new Vector3(r / 255.0f, g / 255.0f, b / 255.0f);
        
        return w;
}



static float colorDistance(Vector3 c0, Vector3 c1)
{
        return Vector3.Dot(c0-c1, c0-c1);
}

Vector3 round255(Vector3 v) {
    //return Vector3(ftoi_round(255 * v.x), ftoi_round(255 * v.y), ftoi_round(255 * v.z)) * (1.0f / 255);
    //return Vector3(floorf(v.x + 0.5f), floorf(v.y + 0.5f), floorf(v.z + 0.5f));
    return v;
}


static uint computeIndices4(Vector3[] block, Vector3 maxColor, Vector3 minColor)
{
        Vector3[] palette = new Vector3[4];
        palette[0] = maxColor;
        palette[1] = minColor;
        //palette[2] = round255((2 * palette[0] + palette[1]) / 3.0f);
        //palette[3] = round255((2 * palette[1] + palette[0]) / 3.0f);
        palette[2] = Vector3.Lerp(palette[0], palette[1], 1.0f / 3.0f);
        palette[3] = Vector3.Lerp(palette[0], palette[1], 2.0f / 3.0f);
        
        uint indices = 0;
        for(int i = 0; i < 16; i++)
        {
                float d0 = colorDistance(palette[0], block[i]);
                float d1 = colorDistance(palette[1], block[i]);
                float d2 = colorDistance(palette[2], block[i]);
                float d3 = colorDistance(palette[3], block[i]);
                
            uint b0 = d0 > d3? (uint)1 : (uint)0;
                uint b1 = d1 > d2? (uint)1 : (uint)0;
                uint b2 = d0 > d2? (uint)1 : (uint)0;
                uint b3 = d1 > d3? (uint)1 : (uint)0;
                uint b4 = d2 > d3? (uint)1 : (uint)0;
                
                uint x0 = b1 & b2;
                uint x1 = b0 & b3;
                uint x2 = b0 & b4;
                
                indices |= (x2 | ((x0 | x1) << 1)) << (2 * i);
        }

        return indices;
}

// maxColor and minColor are expected to be in the same range as the color set.
static uint computeIndices4(ColorSet set, Vector3 maxColor, Vector3 minColor)
{
        Vector3[] palette = new Vector3[4];
        palette[0] = maxColor;
        palette[1] = minColor;
        palette[2] = Vector3.Lerp(palette[0], palette[1], 1.0f / 3.0f);
        palette[3] = Vector3.Lerp(palette[0], palette[1], 2.0f / 3.0f);
       

        Vector3[] row0 = new Vector3[6];
        Vector3[] row1 = new Vector3[6];

        uint indices = 0;
    //for(int i = 0; i < 16; i++)
        for (uint y = 0; y < 4; y++) {
                for (uint x = 0; x < 4; x++) {
            uint i = y*4+x;

            if (!set.isValidIndex(i)) {
                // Skip masked pixels and out of bounds.
                continue;
            }

            Vector3 color = new Vector3(set.color[i].x, set.color[i].y, set.color[i].z);

            // Add error.
            color += row0[1+x];

                    float d0 = colorDistance(palette[0], color);
                    float d1 = colorDistance(palette[1], color);
                    float d2 = colorDistance(palette[2], color);
                    float d3 = colorDistance(palette[3], color);
                
                    uint b0 = d0 > d3?(uint)1:(uint)0;
                    uint b1 = d1 > d2?(uint)1:(uint)0;
                    uint b2 = d0 > d2?(uint)1:(uint)0;
                    uint b3 = d1 > d3?(uint)1:(uint)0;
                    uint b4 = d2 > d3?(uint)1:(uint)0;
                
                    uint x0 = b1 & b2;
                    uint x1 = b0 & b3;
                    uint x2 = b0 & b4;

            uint index = x2 | ((x0 | x1) << 1);
                    indices |= index << (int)(2 * i);

                    // Compute new error.
                    Vector3 diff = color - palette[index];
            
                    // Propagate new error.
                    //row0[1+x+1] += 7.0f / 16.0f * diff;
                    //row1[1+x-1] += 3.0f / 16.0f * diff;
                    //row1[1+x+0] += 5.0f / 16.0f * diff;
                    //row1[1+x+1] += 1.0f / 16.0f * diff;
        }

                swap(ref row0, ref row1);
                row1 = new Vector3[6];
        }

        return indices;
}


private static void swap<T>(ref T a, ref T b)
{
 	T atmp = a;
    a = b;
    b = atmp;
}

static float evaluatePaletteError4(Vector3[] block, Vector3 maxColor, Vector3 minColor)
{
        Vector3[] palette = new Vector3[4];
        palette[0] = maxColor;
        palette[1] = minColor;
        //palette[2] = round255((2 * palette[0] + palette[1]) / 3.0f);
        //palette[3] = round255((2 * palette[1] + palette[0]) / 3.0f);
        palette[2] = Vector3.Lerp(palette[0], palette[1], 1.0f / 3.0f);
        palette[3] = Vector3.Lerp(palette[0], palette[1], 2.0f / 3.0f);
        
        float total = 0.0f;
        for (int i = 0; i < 16; i++)
        {
                float d0 = colorDistance(palette[0], block[i]);
                float d1 = colorDistance(palette[1], block[i]);
                float d2 = colorDistance(palette[2], block[i]);
                float d3 = colorDistance(palette[3], block[i]);

                total += Mathf.Min(Mathf.Min(d0, d1), Mathf.Min(d2, d3));
        }

        return total;
}

static float evaluatePaletteError3(Vector3[] block, Vector3 maxColor, Vector3 minColor)
{
        Vector3[] palette = new Vector3[4];
        palette[0] = minColor;
        palette[1] = maxColor;
        palette[2] = (palette[0] + palette[1]) * 0.5f;
        palette[3] = Vector3.zero;
        
        float total = 0.0f;
        for (int i = 0; i < 16; i++)
        {
                float d0 = colorDistance(palette[0], block[i]);
                float d1 = colorDistance(palette[1], block[i]);
                float d2 = colorDistance(palette[2], block[i]);
                //float d3 = colorDistance(palette[3], block[i]);

                //total += min(min(d0, d1), min(d2, d3));
        total += Mathf.Min(Mathf.Min(d0, d1), d2);
        }

        return total;
}


// maxColor and minColor are expected to be in the same range as the color set.
static uint computeIndices3(ColorSet set, Vector3 maxColor, Vector3 minColor)
{
        Vector3[] palette = new Vector3[4];
        palette[0] = minColor;
        palette[1] = maxColor;
        palette[2] = (palette[0] + palette[1]) * 0.5f;
        
        uint indices = 0;
        for(uint i = 0; i < 16; i++)
        {
        if (!set.isValidIndex(i)) {
            // Skip masked pixels and out of bounds.
            indices |= (uint) 3 << (int)(2 * i);
            continue;
        }

        Vector3 color = new Vector3(set.color[i].x, set.color[i].y, set.color[i].z);
                
                float d0 = colorDistance(palette[0], color);
                float d1 = colorDistance(palette[1], color);
                float d2 = colorDistance(palette[2], color);
                
                uint index;
                if (d0 < d1 && d0 < d2) index = 0;
                else if (d1 < d2) index = 1;
                else index = 2;
                
                indices |= index << (int)(2 * i);
        }

        return indices;
}

static uint computeIndices3(Vector3[] block, Vector3 maxColor, Vector3 minColor)
{
        Vector3[] palette = new Vector3[4];
        palette[0] = minColor;
        palette[1] = maxColor;
        palette[2] = (palette[0] + palette[1]) * 0.5f;
        
        uint indices = 0;
        for(int i = 0; i < 16; i++)
        {
                float d0 = colorDistance(palette[0], block[i]);
                float d1 = colorDistance(palette[1], block[i]);
                float d2 = colorDistance(palette[2], block[i]);
                
                uint index;
                if (d0 < d1 && d0 < d2) index = 0;
                else if (d1 < d2) index = 1;
                else index = 2;
                
                indices |= index << (2 * i);
        }

        return indices;
}




static void optimizeEndPoints4(Vector3[] block, BlockDXT1 dxtBlock)
{
        float alpha2_sum = 0.0f;
        float beta2_sum = 0.0f;
        float alphabeta_sum = 0.0f;
        Vector3 alphax_sum = Vector3.zero;
        Vector3 betax_sum = Vector3.zero;
        
        for( int i = 0; i < 16; ++i )
        {
                uint bits = dxtBlock.indices >> (2 * i);
                
                float beta = (bits & 1);
                if ((bits & 2) != 0) beta = (1 + beta) / 3.0f;
                float alpha = 1.0f - beta;
                
                alpha2_sum += alpha * alpha;
                beta2_sum += beta * beta;
                alphabeta_sum += alpha * beta;
                alphax_sum += alpha * block[i];
                betax_sum += beta * block[i];
        }

        float denom = alpha2_sum * beta2_sum - alphabeta_sum * alphabeta_sum;
        if (Mathf.Approximately(denom, 0.0f)) return;
        
        float factor = 1.0f / denom;
        
        Vector3 a = (alphax_sum * beta2_sum - betax_sum * alphabeta_sum) * factor;
        Vector3 b = (betax_sum * alpha2_sum - alphax_sum * alphabeta_sum) * factor;

        a.x = Mathf.Clamp(a.x, 0, 255);
        a.y = Mathf.Clamp(a.y, 0, 255);
        a.z = Mathf.Clamp(a.z, 0, 255);
        b.x = Mathf.Clamp(b.x, 0, 255);
        b.y = Mathf.Clamp(b.y, 0, 255);
        b.z = Mathf.Clamp(b.z, 0, 255);
        
        ushort color0 = roundAndExpand(ref a);
        ushort color1 = roundAndExpand(ref b);

        if (color0 < color1)
        {
                swap(ref a, ref b);
                swap(ref color0, ref color1);
        }

        dxtBlock.col0 = new Color16(color0);
        dxtBlock.col1 = new Color16(color1);
        dxtBlock.indices = computeIndices4(block, a, b);
}

static void optimizeEndPoints3(Vector3[] block, BlockDXT1 dxtBlock)
{
        float alpha2_sum = 0.0f;
        float beta2_sum = 0.0f;
        float alphabeta_sum = 0.0f;
        Vector3 alphax_sum = Vector3.zero;
        Vector3 betax_sum = Vector3.zero;
        
        for( int i = 0; i < 16; ++i )
        {
                uint bits = dxtBlock.indices >> (2 * i);

                float beta = (float)(bits & 1);
                if ((bits & 2) != 0) beta = 0.5f;
                float alpha = 1.0f - beta;

                alpha2_sum += alpha * alpha;
                beta2_sum += beta * beta;
                alphabeta_sum += alpha * beta;
                alphax_sum += alpha * block[i];
                betax_sum += beta * block[i];
        }

        float denom = alpha2_sum * beta2_sum - alphabeta_sum * alphabeta_sum;
        if (Mathf.Approximately(denom, 0.0f)) return;
        
        float factor = 1.0f / denom;
        
        Vector3 a = (alphax_sum * beta2_sum - betax_sum * alphabeta_sum) * factor;
        Vector3 b = (betax_sum * alpha2_sum - alphax_sum * alphabeta_sum) * factor;

        a.x = Mathf.Clamp(a.x, 0, 255);
        a.y = Mathf.Clamp(a.y, 0, 255);
        a.z = Mathf.Clamp(a.z, 0, 255);
        b.x = Mathf.Clamp(b.x, 0, 255);
        b.y = Mathf.Clamp(b.y, 0, 255);
        b.z = Mathf.Clamp(b.z, 0, 255);
        
        ushort color0 = roundAndExpand(ref a);
        ushort color1 = roundAndExpand(ref b);

        if (color0 < color1)
        {
                swap(ref a, ref b);
                swap(ref color0, ref color1);
        }

        dxtBlock.col0 = new Color16(color1);
        dxtBlock.col1 = new Color16(color0);
        dxtBlock.indices = computeIndices3(block, a, b);
}

        static uint computeAlphaIndices(AlphaBlock4x4 src, AlphaBlockDXT5 block)
        {
                byte[] alphas = new byte[8];
                block.evaluatePalette(alphas, false); // @@ Use target decoder.

                uint totalError = 0;

                for (uint i = 0; i < 16; i++)
                {
                        byte alpha = src.alpha[i];

                        uint besterror = 256*256;
                        uint best = 8;
                        for(uint p = 0; p < 8; p++)
                        {
                                int d = alphas[p] - alpha;
                                uint error = (uint)(d * d);

                                if (error < besterror)
                                {
                                        besterror = error;
                                        best = p;
                                }
                        }
                        //nvDebugCheck(best < 8);

                        totalError += besterror;
                        block.setIndex(i, best);
                }

                return totalError;
        }

        static void optimizeAlpha8(AlphaBlock4x4 src, AlphaBlockDXT5 block)
        {
                float alpha2_sum = 0;
                float beta2_sum = 0;
                float alphabeta_sum = 0;
                float alphax_sum = 0;
                float betax_sum = 0;

                for (uint i = 0; i < 16; i++)
                {
                        uint idx = block.index(i);
                        float alpha;
                        if (idx < 2) alpha = 1.0f - idx;
                        else alpha = (8.0f - idx) / 7.0f;
                        
                        float beta = 1 - alpha;

                        alpha2_sum += alpha * alpha;
                        beta2_sum += beta * beta;
                        alphabeta_sum += alpha * beta;
                        alphax_sum += alpha * src.alpha[i];
                        betax_sum += beta * src.alpha[i];
                }

                float factor = 1.0f / (alpha2_sum * beta2_sum - alphabeta_sum * alphabeta_sum);

                float a = (alphax_sum * beta2_sum - betax_sum * alphabeta_sum) * factor;
                float b = (betax_sum * alpha2_sum - alphax_sum * alphabeta_sum) * factor;

                byte alpha0 = (byte)(Math.Min(Math.Max(a, 0.0f), 255.0f));
                byte alpha1 = (byte)(Math.Min(Math.Max(b, 0.0f), 255.0f));

                if (alpha0 < alpha1)
                {
                        swap(ref alpha0, ref alpha1);

                        // Flip indices:
                        for (uint i = 0; i < 16; i++)
                        {
                                uint idx = block.index(i);
                                if (idx < 2) block.setIndex(i, 1 - idx);
                                else block.setIndex(i, 9 - idx);
                        }
                }
                else if (alpha0 == alpha1)
                {
                        for (uint i = 0; i < 16; i++)
                        {
                                block.setIndex(i, 0);
                        }
                }

                block.alpha0 = alpha0;
                block.alpha1 = alpha1;
        }

        /*
        static void optimizeAlpha6( ColorBlock & rgba, AlphaBlockDXT5 * block)
        {
                float alpha2_sum = 0;
                float beta2_sum = 0;
                float alphabeta_sum = 0;
                float alphax_sum = 0;
                float betax_sum = 0;

                for (int i = 0; i < 16; i++)
                {
                        byte x = rgba.color[i].a;
                        if (x == 0 || x == 255) continue;

                        uint bits = block.index(i);
                        if (bits == 6 || bits == 7) continue;

                        float alpha;
                        if (bits == 0) alpha = 1.0f;
                        else if (bits == 1) alpha = 0.0f;
                        else alpha = (6.0f - block.index(i)) / 5.0f;
                        
                        float beta = 1 - alpha;

                        alpha2_sum += alpha * alpha;
                        beta2_sum += beta * beta;
                        alphabeta_sum += alpha * beta;
                        alphax_sum += alpha * x;
                        betax_sum += beta * x;
                }

                 float factor = 1.0f / (alpha2_sum * beta2_sum - alphabeta_sum * alphabeta_sum);

                float a = (alphax_sum * beta2_sum - betax_sum * alphabeta_sum) * factor;
                float b = (betax_sum * alpha2_sum - alphax_sum * alphabeta_sum) * factor;

                uint alpha0 = uint(min(max(a, 0.0f), 255.0f));
                uint alpha1 = uint(min(max(b, 0.0f), 255.0f));

                if (alpha0 > alpha1)
                {
                        swap(alpha0, alpha1);
                }

                block.alpha0 = alpha0;
                block.alpha1 = alpha1;
        }
        */

        static bool sameIndices( AlphaBlockDXT5 block0, AlphaBlockDXT5 block1)
        {
                ulong mask = ~(ulong)(0xFFFF);
                return (block0.u | mask) == (block1.u | mask);
        }
        
            
public static void compressDXT1(ColorBlock rgba, BlockDXT1 dxtBlock)
{
        if (rgba.isSingleColor())
        {
                OptimalCompress.compressDXT1(rgba.color[0], dxtBlock);
        }
        else
        {
                // read block
                Vector3[] block = new Vector3[16];
                extractColorBlockRGB(rgba, block);
#if true
                // find min and max colors
                Vector3 maxColor = Vector3.zero, minColor = Vector3.zero;
                findMinMaxColorsBox(block, 16, ref maxColor, ref minColor);
                
                selectDiagonal(block, 16, ref maxColor, ref minColor);
                
                insetBBox(ref maxColor, ref minColor);
#else
                float[] weights = new float[16]{1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1};
                Vector3[] cluster = new Vector3[4];
                int count = Fitting.Compute4Means(16, block, weights, Vector3.one, cluster);

                Vector3 maxColor, minColor;
                float bestError = FLT_MAX;

                for (int i = 1; i < 4; i++)
                {
                        for (int j = 0; j < i; j++)
                        {
                        uint16 color0 = roundAndExpand(&cluster[i]);
                        uint16 color1 = roundAndExpand(&cluster[j]);

                                float error = evaluatePaletteError4(block, cluster[i], cluster[j]);
                                if (error < bestError) {
                                        bestError = error;
                                        maxColor = cluster[i];
                                        minColor = cluster[j];
                                }
                        }
                }
#endif

                ushort color0 = roundAndExpand(ref maxColor);
                ushort color1 = roundAndExpand(ref minColor);

                if (color0 < color1)
                {
                        swap(ref maxColor, ref minColor);
                        swap(ref color0, ref color1);
                }

                dxtBlock.col0 = new Color16(color0);
                dxtBlock.col1 = new Color16(color1);
                dxtBlock.indices = computeIndices4(block, maxColor, minColor);

                optimizeEndPoints4(block, dxtBlock);
        }
}


void compressDXT1a( ColorBlock rgba, BlockDXT1 dxtBlock)
{
        bool hasAlpha = false;
        
        for (uint i = 0; i < 16; i++)
        {
                if (rgba.color[i].a == 0) {
                        hasAlpha = true;
                        break;
                }
        }
        
        if (!hasAlpha)
        {
                compressDXT1(rgba, dxtBlock);
        }
        // @@ Handle single RGB, with varying alpha? We need tables for single color compressor in 3 color mode.
        //else if (rgba.isSingleColorNoAlpha()) { ... }
        else 
        {
                // read block
                Vector3[] block = new Vector3[16];
                uint num = extractColorBlockRGBA(rgba, block);
                
                // find min and max colors
                Vector3 maxColor = Vector3.zero, minColor = Vector3.zero;
                findMinMaxColorsBox(block, num, ref maxColor, ref minColor);
                
                selectDiagonal(block, num, ref maxColor, ref minColor);
                
                insetBBox(ref maxColor, ref minColor);
                
                ushort color0 = roundAndExpand(ref maxColor);
                ushort color1 = roundAndExpand(ref minColor);
                
                if (color0 < color1)
                {
                        swap(ref maxColor, ref minColor);
                        swap(ref color0, ref color1);
                }
                
                dxtBlock.col0 = new Color16(color1);
                dxtBlock.col1 = new Color16(color0);
                dxtBlock.indices = computeIndices3(block, maxColor, minColor);
                
                //      optimizeEndPoints(block, dxtBlock);
        }
}

static void compressDXT5A( ColorBlock  src, ref AlphaBlockDXT5 dst, int iterationCount/*=8*/)
{
    AlphaBlock4x4 tmp = new AlphaBlock4x4();
    tmp.init(src, 3);
    compressDXT5A(tmp, ref dst, iterationCount);
}

static void compressDXT5A( AlphaBlock4x4 src, ref AlphaBlockDXT5 dst, int iterationCount/*=8*/)
{
        byte alpha0 = 0;
        byte alpha1 = 255;
        
        // Get min/max alpha.
        for (uint i = 0; i < 16; i++)
        {
                byte alpha = src.alpha[i];
                alpha0 = Math.Max(alpha0, alpha);
                alpha1 = Math.Min(alpha1, alpha);
        }
        
        AlphaBlockDXT5 block = new AlphaBlockDXT5();
        block.alpha0 = (byte)(alpha0 - (alpha0 - alpha1) / 34);
        block.alpha1 = (byte)(alpha1 + (alpha0 - alpha1) / 34);
        uint besterror = computeAlphaIndices(src, block);
        
        AlphaBlockDXT5 bestblock = block;

        for (int i = 0; i < iterationCount; i++)
        {
                optimizeAlpha8(src, block);
                uint error = computeAlphaIndices(src, block);
                
                if (error >= besterror)
                {
                        // No improvement, stop.
                        break;
                }
                if (sameIndices(block, bestblock))
                {
                        bestblock = block;
                        break;
                }
                
                besterror = error;
                bestblock = block;
        };
        
        // Copy best block to result;
        dst = bestblock;
}

public static void compressDXT5( ColorBlock rgba, BlockDXT5 dxtBlock, int iterationCount/*=8*/)
{
        compressDXT1(rgba, dxtBlock.color);
        compressDXT5A(rgba, ref dxtBlock.alpha, iterationCount);
}



void outputBlock4( ColorSet set,  Vector3 start,  Vector3 end, BlockDXT1 block)
{
    Vector3 minColor = start * 255.0f;
    Vector3 maxColor = end * 255.0f;
    ushort color0 = roundAndExpand(ref maxColor);
    ushort color1 = roundAndExpand(ref minColor);

    if (color0 < color1)
    {
        swap(ref maxColor, ref minColor);
        swap(ref color0, ref color1);
    }

    block.col0 = new Color16(color0);
    block.col1 = new Color16(color1);
    block.indices = computeIndices4(set, maxColor / 255.0f, minColor / 255.0f);

    //optimizeEndPoints4(set, block);
}

void outputBlock3( ColorSet set,  Vector3 start,  Vector3 end, BlockDXT1 block)
{
    Vector3 minColor = start * 255.0f;
    Vector3 maxColor = end * 255.0f;
    ushort color0 = roundAndExpand(ref minColor);
    ushort color1 = roundAndExpand(ref maxColor);

    if (color0 > color1)
    {
        swap(ref maxColor, ref minColor);
        swap(ref color0, ref color1);
    }

    block.col0 = new Color16(color0);
    block.col1 = new Color16(color1);
    block.indices = computeIndices3(set, maxColor / 255.0f, minColor / 255.0f);

    //optimizeEndPoints3(set, block);
}

    }

    }

