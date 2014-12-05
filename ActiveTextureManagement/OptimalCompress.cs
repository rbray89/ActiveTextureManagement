using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ActiveTextureManagement
{
    public class OptimalCompress
    {
        static uint greenDistance(int g0, int g1)
        {
            //return abs(g0 - g1);
            int d = g0 - g1;
            return (uint)(d * d);
        }

        static uint alphaDistance(int a0, int a1)
        {
            //return abs(a0 - a1);
            int d = a0 - a1;
            return (uint)(d * d);
        }

        /*static uint nearestGreen4(uint green, uint maxGreen, uint minGreen)
        {
                uint bias = maxGreen + (maxGreen - minGreen) / 6;

                uint index = 0;
                if (maxGreen - minGreen != 0) index = clamp(3 * (bias - green) / (maxGreen - minGreen), 0U, 3U);

                return (index * minGreen + (3 - index) * maxGreen) / 3;
        }*/

        static uint computeGreenError(ColorBlock rgba, BlockDXT1 block, uint bestError = uint.MaxValue)
        {

            //      uint g0 = (block.col0.g << 2) | (block.col0.g >> 4);
            //      uint g1 = (block.col1.g << 2) | (block.col1.g >> 4);

            int[] palette = new int[4];
            palette[0] = (block.col0.g << 2) | (block.col0.g >> 4);
            palette[1] = (block.col1.g << 2) | (block.col1.g >> 4);
            palette[2] = (2 * palette[0] + palette[1]) / 3;
            palette[3] = (2 * palette[1] + palette[0]) / 3;

            uint totalError = 0;
            for (uint i = 0; i < 16; i++)
            {
                int green = rgba.color[i].g;

                uint error = greenDistance(green, palette[0]);
                error = Math.Min(error, greenDistance(green, palette[1]));
                error = Math.Min(error, greenDistance(green, palette[2]));
                error = Math.Min(error, greenDistance(green, palette[3]));

                totalError += error;

                //      totalError += nearestGreen4(green, g0, g1);

                if (totalError > bestError)
                {
                    // early out
                    return totalError;
                }
            }

            return totalError;
        }

        static uint computeGreenIndices(ColorBlock rgba, Color32[] palette)
        {
            int color0 = palette[0].g;
            int color1 = palette[1].g;
            int color2 = palette[2].g;
            int color3 = palette[3].g;

            uint indices = 0;
            for (uint i = 0; i < 16; i++)
            {
                int color = rgba.color[i].g;

                uint d0 = greenDistance(color0, color);
                uint d1 = greenDistance(color1, color);
                uint d2 = greenDistance(color2, color);
                uint d3 = greenDistance(color3, color);

                uint b0 = d0 > d3 ? (uint)1 : (uint)0;
                uint b1 = d1 > d2 ? (uint)1 : (uint)0;
                uint b2 = d0 > d2 ? (uint)1 : (uint)0;
                uint b3 = d1 > d3 ? (uint)1 : (uint)0;
                uint b4 = d2 > d3 ? (uint)1 : (uint)0;

                uint x0 = b1 & b2;
                uint x1 = b0 & b3;
                uint x2 = b0 & b4;

                indices |= (x2 | ((x0 | x1) << 1)) << (int)(2 * i);
            }

            return indices;
        }

        // Choose quantized color that produces less error. Used by DXT3 compressor.
        static uint quantize4(byte a)
        {
            int q0 = Math.Max((int)(a >> 4) - 1, 0);
            int q1 = (a >> 4);
            int q2 = Math.Max((int)(a >> 4) + 1, 0xF);

            q0 = (q0 << 4) | q0;
            q1 = (q1 << 4) | q1;
            q2 = (q2 << 4) | q2;

            uint d0 = alphaDistance(q0, a);
            uint d1 = alphaDistance(q1, a);
            uint d2 = alphaDistance(q2, a);

            if (d0 < d1 && d0 < d2) return (uint)(q0 >> 4);
            if (d1 < d2) return (uint)(q1 >> 4);
            return (uint)(q2 >> 4);
        }

        static uint nearestAlpha8(uint alpha, uint maxAlpha, uint minAlpha)
        {
            float bias = maxAlpha + (float)(maxAlpha - minAlpha) / (2.0f * 7.0f);
            float scale = 7.0f / (float)(maxAlpha - minAlpha);

            uint index = (uint)Mathf.Clamp((bias - (float)(alpha)) * scale, 0.0f, 7.0f);

            return (index * minAlpha + (7 - index) * maxAlpha) / 7;
        }

        /*static uint computeAlphaError8( ColorBlock & rgba,  AlphaBlockDXT5 * block, int bestError = INT_MAX)
        {
                int totalError = 0;

                for (uint i = 0; i < 16; i++)
                {
                        byte alpha = rgba.color[i].a;

                        totalError += alphaDistance(alpha, nearestAlpha8(alpha, block.alpha0, block.alpha1));

                        if (totalError > bestError)
                        {
                                // early out
                                return totalError;
                        }
                }

                return totalError;
        }*/

        static float computeAlphaError(AlphaBlock4x4 src, AlphaBlockDXT5 dst, float bestError = float.MaxValue)
        {
            byte[] alphas = new byte[8];
            dst.evaluatePalette(alphas, false); // @@ Use target decoder.

            float totalError = 0;

            for (uint i = 0; i < 16; i++)
            {
                byte alpha = src.alpha[i];

                uint minDist = int.MaxValue;
                for (uint p = 0; p < 8; p++)
                {
                    uint dist = alphaDistance(alpha, alphas[p]);
                    minDist = Math.Min(dist, minDist);
                }

                totalError += minDist * src.weights[i];

                if (totalError > bestError)
                {
                    // early out
                    return totalError;
                }
            }

            return totalError;
        }

        static void computeAlphaIndices(AlphaBlock4x4 src, AlphaBlockDXT5 dst)
        {
            byte[] alphas = new byte[8];
            dst.evaluatePalette(alphas, /*d3d9=*/false); // @@ Use target decoder.

            for (uint i = 0; i < 16; i++)
            {
                byte alpha = src.alpha[i];

                uint minDist = int.MaxValue;
                uint bestIndex = 8;
                for (uint p = 0; p < 8; p++)
                {
                    uint dist = alphaDistance(alpha, alphas[p]);

                    if (dist < minDist)
                    {
                        minDist = dist;
                        bestIndex = p;
                    }
                }
                dst.setIndex(i, bestIndex);
            }
        }

        static byte[,] OMatch5 = new byte[256, 2];
        static byte[,] OMatch6 = new byte[256, 2];
        static byte[,] OMatchAlpha5 = new byte[256, 2];
        static byte[,] OMatchAlpha6 = new byte[256, 2];
        static bool init = false;


        static int Mul8Bit(int a, int b)
        {
            int t = a * b + 128;
            return (t + (t >> 8)) >> 8;
        }

        static int Lerp13(int a, int b)
        {
#if DXT_USE_ROUNDING_BIAS
    // with rounding bias
    return a + Mul8Bit(b-a, 0x55);
#else
            // without rounding bias
            // replace "/ 3" by "* 0xaaab) >> 17" if your compiler sucks or you really need every ounce of speed.
            return (a * 2 + b) / 3;
#endif
        }

        static void PrepareOptTable(byte[,] table, byte[] expand, int size, bool alpha_mode)
        {
            for (int i = 0; i < 256; i++)
            {
                int bestErr = 256 * 100;

                for (int min = 0; min < size; min++)
                {
                    for (int max = 0; max < size; max++)
                    {
                        int mine = expand[min];
                        int maxe = expand[max];

                        int err;
                        if (alpha_mode) err = Math.Abs((maxe + mine) / 2 - i);
                        else err = Math.Abs(Lerp13(maxe, mine) - i);
                        err *= 100;

                        // DX10 spec says that interpolation must be within 3% of "correct" result,
                        // add this as error term. (normally we'd expect a random distribution of
                        // +-1.5% error, but nowhere in the spec does it say that the error has to be
                        // unbiased - better safe than sorry).
                        err += Math.Abs(max - min) * 3;

                        if (err < bestErr)
                        {
                            table[i * 2, 0] = (byte)max;
                            table[i * 2, 1] = (byte)min;
                            bestErr = err;
                        }
                    }
                }
            }
        }


        static void initSingleColorLookup()
        {
            if (!init)
            {
                init = true;
                byte[] expand5 = new byte[32];
                byte[] expand6 = new byte[64];

                for (int i = 0; i < 32; i++)
                {
                    expand5[i] = (byte)((i << 3) | (i >> 2));
                }

                for (int i = 0; i < 64; i++)
                {
                    expand6[i] = (byte)((i << 2) | (i >> 4));
                }

                PrepareOptTable(OMatch5, expand5, 32, false);
                PrepareOptTable(OMatch6, expand6, 64, false);
                PrepareOptTable(OMatchAlpha5, expand5, 32, true);
                PrepareOptTable(OMatchAlpha6, expand6, 64, true);
            }
        }


        // Single color compressor, based on:
        // https://mollyrocket.com/forums/viewtopic.php?t=392
        public static void compressDXT1(Color32 c, BlockDXT1 dxtBlock)
        {
            initSingleColorLookup();
            dxtBlock.col0.r = OMatch5[c.r, 0];
            dxtBlock.col0.g = OMatch6[c.g, 0];
            dxtBlock.col0.b = OMatch5[c.b, 0];
            dxtBlock.col1.r = OMatch5[c.r, 1];
            dxtBlock.col1.g = OMatch6[c.g, 1];
            dxtBlock.col1.b = OMatch5[c.b, 1];
            dxtBlock.indices = 0xaaaaaaaa;

            if (dxtBlock.col0.u < dxtBlock.col1.u)
            {
                ushort u0tmp = dxtBlock.col0.u;
                dxtBlock.col0.u = dxtBlock.col1.u;
                dxtBlock.col1.u = dxtBlock.col0.u;
                dxtBlock.indices ^= 0x55555555;
            }
        }

        void compressDXT1a(Color32 c, uint alphaMask, BlockDXT1 dxtBlock)
        {
            if (alphaMask == 0)
            {
                compressDXT1(c, dxtBlock);
            }
            else
            {
                dxtBlock.col0.r = OMatchAlpha5[c.r, 0];
                dxtBlock.col0.g = OMatchAlpha6[c.g, 0];
                dxtBlock.col0.b = OMatchAlpha5[c.b, 0];
                dxtBlock.col1.r = OMatchAlpha5[c.r, 1];
                dxtBlock.col1.g = OMatchAlpha6[c.g, 1];
                dxtBlock.col1.b = OMatchAlpha5[c.b, 1];
                dxtBlock.indices = 0xaaaaaaaa; // 0b1010..1010

                if (dxtBlock.col0.u > dxtBlock.col1.u)
                {
                    ushort u0tmp = dxtBlock.col0.u;
                    dxtBlock.col0.u = dxtBlock.col1.u;
                    dxtBlock.col1.u = dxtBlock.col0.u;
                }

                dxtBlock.indices |= alphaMask;
            }
        }

        void compressDXT1G(byte g, BlockDXT1 dxtBlock)
        {
            dxtBlock.col0.r = 31;
            dxtBlock.col0.g = OMatch6[g, 0];
            dxtBlock.col0.b = 0;
            dxtBlock.col1.r = 31;
            dxtBlock.col1.g = OMatch6[g, 1];
            dxtBlock.col1.b = 0;
            dxtBlock.indices = 0xaaaaaaaa;

            if (dxtBlock.col0.u < dxtBlock.col1.u)
            {
                ushort u0tmp = dxtBlock.col0.u;
                dxtBlock.col0.u = dxtBlock.col1.u;
                dxtBlock.col1.u = dxtBlock.col0.u;
                dxtBlock.indices ^= 0x55555555;
            }
        }


        // Brute force green channel compressor
        void compressDXT1G(ColorBlock rgba, BlockDXT1 block)
        {
            byte ming = 63;
            byte maxg = 0;

            bool isSingleColor = true;
            byte singleColor = rgba.color[0].g;

            // Get min/max green.
            for (uint i = 0; i < 16; i++)
            {
                byte green = (byte)((rgba.color[i].g + 1) >> 2);
                ming = Math.Min(ming, green);
                maxg = Math.Max(maxg, green);

                if (rgba.color[i].g != singleColor) isSingleColor = false;
            }

            if (isSingleColor)
            {
                compressDXT1G(singleColor, block);
                return;
            }

            block.col0.r = 31;
            block.col1.r = 31;
            block.col0.g = maxg;
            block.col1.g = ming;
            block.col0.b = 0;
            block.col1.b = 0;

            uint bestError = computeGreenError(rgba, block);
            byte bestg0 = maxg;
            byte bestg1 = ming;

            // Expand search space a bit.
            int greenExpand = 4;
            ming = (byte)((ming <= greenExpand) ? 0 : ming - greenExpand);
            maxg = (byte)((maxg >= 63 - greenExpand) ? 63 : maxg + greenExpand);

            for (byte g0 = (byte)(ming + 1); g0 <= maxg; g0++)
            {
                for (byte g1 = ming; g1 < g0; g1++)
                {
                    block.col0.g = g0;
                    block.col1.g = g1;
                    uint error = computeGreenError(rgba, block, bestError);

                    if (error < bestError)
                    {
                        bestError = error;
                        bestg0 = g0;
                        bestg1 = g1;
                    }
                }
            }

            block.col0.g = bestg0;
            block.col1.g = bestg1;

            Color32[] palette = new Color32[4];
            block.evaluatePalette(palette, false); // @@ Use target decoder.
            block.indices = computeGreenIndices(rgba, palette);
        }


        /*void OptimalCompress::initLumaTables() {

            // For all possible color pairs:
            for (int c0 = 0; c0 < 65536; c0++) {
                for (int c1 = 0; c1 < 65536; c1++) {
            
                    // Compute 

                }
            }


            for (int r = 0; r < 1<<5; r++) {
                for (int g = 0; g < 1<<6; g++) {
                    for (int b = 0; b < 1<<5; b++) {


                    }
                }
            }
        }*/


        // Brute force Luma compressor
        void compressDXT1_Luma(ColorBlock rgba, BlockDXT1 block)
        {

            // F_YR = 19595/65536.0f, F_YG = 38470/65536.0f, F_YB = 7471/65536.0f;
            // 195841
            //if (


            /*
                byte ming = 63;
                byte maxg = 0;
        
                bool isSingleColor = true;
                byte singleColor = rgba.color(0).g;

                // Get min/max green.
                for (uint i = 0; i < 16; i++)
                {
                        byte green = (rgba.color[i].g + 1) >> 2;
                        ming = min(ming, green);
                        maxg = max(maxg, green);

                        if (rgba.color[i].g != singleColor) isSingleColor = false;
                }

                if (isSingleColor)
                {
                        compressDXT1G(singleColor, block);
                        return;
                }

                block.col0.r = 31;
                block.col1.r = 31;
                block.col0.g = maxg;
                block.col1.g = ming;
                block.col0.b = 0;
                block.col1.b = 0;

                int bestError = computeGreenError(rgba, block);
                int bestg0 = maxg;
                int bestg1 = ming;

                // Expand search space a bit.
                 int greenExpand = 4;
                ming = (ming <= greenExpand) ? 0 : ming - greenExpand;
                maxg = (maxg >= 63-greenExpand) ? 63 : maxg + greenExpand;

                for (int g0 = ming+1; g0 <= maxg; g0++)
                {
                        for (int g1 = ming; g1 < g0; g1++)
                        {
                                block.col0.g = g0;
                                block.col1.g = g1;
                                int error = computeGreenError(rgba, block, bestError);
                        
                                if (error < bestError)
                                {
                                        bestError = error;
                                        bestg0 = g0;
                                        bestg1 = g1;
                                }
                        }
                }
        
                block.col0.g = bestg0;
                block.col1.g = bestg1;

                nvDebugCheck(bestg0 == bestg1 || block.isFourColorMode());
            */

            Color32[] palette = new Color32[4];
            block.evaluatePalette(palette, false); // @@ Use target decoder.
            block.indices = computeGreenIndices(rgba, palette);
        }


        void compressDXT5A(AlphaBlock4x4 src, AlphaBlockDXT5 dst)
        {
            byte mina = 255;
            byte maxa = 0;

            byte mina_no01 = 255;
            byte maxa_no01 = 0;

            // Get min/max alpha.
            for (uint i = 0; i < 16; i++)
            {
                byte alpha = src.alpha[i];
                mina = Math.Min(mina, alpha);
                maxa = Math.Max(maxa, alpha);

                if (alpha != 0 && alpha != 255)
                {
                    mina_no01 = Math.Min(mina_no01, alpha);
                    maxa_no01 = Math.Max(maxa_no01, alpha);
                }
            }

            if (maxa - mina < 8)
            {
                dst.alpha0 = maxa;
                dst.alpha1 = mina;

            }
            else if (maxa_no01 - mina_no01 < 6)
            {
                dst.alpha0 = mina_no01;
                dst.alpha1 = maxa_no01;

            }
            else
            {
                float besterror = computeAlphaError(src, dst);
                byte besta0 = maxa;
                byte besta1 = mina;

                // Expand search space a bit.
                int alphaExpand = 8;
                mina = (byte)((mina <= alphaExpand) ? 0 : mina - alphaExpand);
                maxa = (byte)((maxa >= 255 - alphaExpand) ? 255 : maxa + alphaExpand);

                for (byte a0 = (byte)(mina + 9); a0 < maxa; a0++)
                {
                    for (byte a1 = mina; a1 < a0 - 8; a1++)
                    {
                        dst.alpha0 = a0;
                        dst.alpha1 = a1;
                        float error = computeAlphaError(src, dst, besterror);

                        if (error < besterror)
                        {
                            besterror = error;
                            besta0 = a0;
                            besta1 = a1;
                        }
                    }
                }

                // Try using the 6 step encoding.
                /*if (mina == 0 || maxa == 255)*/
                {

                    // Expand search space a bit.
                    alphaExpand = 6;
                    mina_no01 = (byte)((mina_no01 <= alphaExpand) ? 0 : mina_no01 - alphaExpand);
                    maxa_no01 = (byte)((maxa_no01 >= 255 - alphaExpand) ? 255 : maxa_no01 + alphaExpand);

                    for (byte a0 = (byte)(mina_no01 + 9); a0 < maxa_no01; a0++)
                    {
                        for (byte a1 = mina_no01; a1 < a0 - 8; a1++)
                        {
                            dst.alpha0 = a1;
                            dst.alpha1 = a0;
                            float error = computeAlphaError(src, dst, besterror);

                            if (error < besterror)
                            {
                                besterror = error;
                                besta0 = a1;
                                besta1 = a0;
                            }
                        }
                    }
                }

                dst.alpha0 = besta0;
                dst.alpha1 = besta1;
            }

            computeAlphaIndices(src, dst);
        }


        void compressDXT5A(ColorBlock src, AlphaBlockDXT5 dst)
        {
            AlphaBlock4x4 tmp = new AlphaBlock4x4();
            tmp.init(src, 3);
            compressDXT5A(tmp, dst);
        }


        static float threshold = 0.15f;

        static float computeAlphaError_RGBM(ColorSet src, ColorBlock RGB, AlphaBlockDXT5 dst, float bestError = float.MaxValue)
        {
            byte[] alphas = new byte[8];
            dst.evaluatePalette(alphas, /*d3d9=*/false); // @@ Use target decoder.

            float totalError = 0;

            for (uint i = 0; i < 16; i++)
            {
                float R = src.color[i].x;
                float G = src.color[i].y;
                float B = src.color[i].z;

                float r = (float)(RGB.color[i].r) / 255.0f;
                float g = (float)(RGB.color[i].g) / 255.0f;
                float b = (float)(RGB.color[i].b) / 255.0f;

                float minDist = float.MaxValue;
                for (uint p = 0; p < 8; p++)
                {
                    // Compute M.
                    float M = (float)(alphas[p]) / 255.0f * (1 - threshold) + threshold;

                    // Decode color.
                    float fr = r * M;
                    float fg = g * M;
                    float fb = b * M;

                    // Measure error.
                    float error = Mathf.Pow(R - fr, 2) + Mathf.Pow(G - fg, 2) + Mathf.Pow(B - fb, 2);

                    minDist = Mathf.Min(error, minDist);
                }

                totalError += minDist * src.weights[i];

                if (totalError > bestError)
                {
                    // early out
                    return totalError;
                }
            }

            return totalError;
        }

        static void computeAlphaIndices_RGBM(ColorSet src, ColorBlock RGB, AlphaBlockDXT5 dst)
        {
            byte[] alphas = new byte[8];
            dst.evaluatePalette(alphas, /*d3d9=*/false); // @@ Use target decoder.

            for (uint i = 0; i < 16; i++)
            {
                float R = src.color[i].x;
                float G = src.color[i].y;
                float B = src.color[i].z;

                float r = (float)(RGB.color[i].r) / 255.0f;
                float g = (float)(RGB.color[i].g) / 255.0f;
                float b = (float)(RGB.color[i].b) / 255.0f;

                float minDist = float.MaxValue;
                uint bestIndex = 8;
                for (uint p = 0; p < 8; p++)
                {
                    // Compute M.
                    float M = (float)(alphas[p]) / 255.0f * (1 - threshold) + threshold;

                    // Decode color.
                    float fr = r * M;
                    float fg = g * M;
                    float fb = b * M;

                    // Measure error.
                    float error = Mathf.Pow(R - fr, 2) + Mathf.Pow(G - fg, 2) + Mathf.Pow(B - fb, 2);

                    if (error < minDist)
                    {
                        minDist = error;
                        bestIndex = p;
                    }
                }

                dst.setIndex(i, bestIndex);
            }
        }


        void compressDXT5A_RGBM(ColorSet src, ColorBlock RGB, AlphaBlockDXT5 dst)
        {
            byte mina = 255;
            byte maxa = 0;

            byte mina_no01 = 255;
            byte maxa_no01 = 0;

            // Get min/max alpha.
            /*for (uint i = 0; i < 16; i++)
            {
                byte alpha = src.alpha[i];
                mina = min(mina, alpha);
                maxa = max(maxa, alpha);

                if (alpha != 0 && alpha != 255) {
                    mina_no01 = min(mina_no01, alpha);
                    maxa_no01 = max(maxa_no01, alpha);
                }
            }*/
            mina = 0;
            maxa = 255;
            mina_no01 = 0;
            maxa_no01 = 255;

            /*if (maxa - mina < 8) {
                dst.alpha0 = maxa;
                dst.alpha1 = mina;

                nvDebugCheck(computeAlphaError(src, dst) == 0);
            }
            else if (maxa_no01 - mina_no01 < 6) {
                dst.alpha0 = mina_no01;
                dst.alpha1 = maxa_no01;

                nvDebugCheck(computeAlphaError(src, dst) == 0);
            }
            else*/
            {
                float besterror = computeAlphaError_RGBM(src, RGB, dst);
                byte besta0 = maxa;
                byte besta1 = mina;

                // Expand search space a bit.
                int alphaExpand = 8;
                mina = (byte)((mina <= alphaExpand) ? 0 : mina - alphaExpand);
                maxa = (byte)((maxa >= 255 - alphaExpand) ? 255 : maxa + alphaExpand);

                for (byte a0 = (byte)(mina + 9); a0 < maxa; a0++)
                {
                    for (byte a1 = mina; a1 < a0 - 8; a1++)
                    {

                        dst.alpha0 = a0;
                        dst.alpha1 = a1;
                        float error = computeAlphaError_RGBM(src, RGB, dst, besterror);

                        if (error < besterror)
                        {
                            besterror = error;
                            besta0 = a0;
                            besta1 = a1;
                        }
                    }
                }

                // Try using the 6 step encoding.
                /*if (mina == 0 || maxa == 255)*/
                {

                    // Expand search space a bit.
                    alphaExpand = 6;
                    mina_no01 = (byte)((mina_no01 <= alphaExpand) ? 0 : mina_no01 - alphaExpand);
                    maxa_no01 = (byte)((maxa_no01 >= 255 - alphaExpand) ? 255 : maxa_no01 + alphaExpand);

                    for (byte a0 = (byte)(mina_no01 + 9); a0 < maxa_no01; a0++)
                    {
                        for (byte a1 = mina_no01; a1 < a0 - 8; a1++)
                        {
                            dst.alpha0 = a1;
                            dst.alpha1 = a0;
                            float error = computeAlphaError_RGBM(src, RGB, dst, besterror);

                            if (error < besterror)
                            {
                                besterror = error;
                                besta0 = a1;
                                besta1 = a0;
                            }
                        }
                    }
                }

                dst.alpha0 = besta0;
                dst.alpha1 = besta1;
            }

            computeAlphaIndices_RGBM(src, RGB, dst);
        }
    }
}
