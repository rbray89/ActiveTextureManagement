using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LibSquishPort
{
    class alpha
    {
        static int FloatToInt(float a, int limit)
        {
            // use ANSI round-to-zero behaviour to get round-to-nearest
            int i = (int)(a + 0.5f);

            // clamp to the limit
            if (i < 0)
                i = 0;
            else if (i > limit)
                i = limit;

            // done
            return i;
        }

        public static unsafe void CompressAlphaDxt3(byte[] rgba, int mask, byte* block)
        {
            byte* bytes = block;

            // quantise and pack the alpha values pairwise
            for (int i = 0; i < 8; ++i)
            {
                // quantise down to 4 bits
                float alpha1 = (float)rgba[8 * i + 3] * (15.0f / 255.0f);
                float alpha2 = (float)rgba[8 * i + 7] * (15.0f / 255.0f);
                int quant1 = FloatToInt(alpha1, 15);
                int quant2 = FloatToInt(alpha2, 15);

                // set alpha to zero where masked
                int bit1 = 1 << (2 * i);
                int bit2 = 1 << (2 * i + 1);
                if ((mask & bit1) == 0)
                    quant1 = 0;
                if ((mask & bit2) == 0)
                    quant2 = 0;

                // pack into the byte
                bytes[i] = (byte)(quant1 | (quant2 << 4));
            }
        }

        public unsafe void DecompressAlphaDxt3(byte[] rgba, byte* block)
        {
            byte* bytes = block;

            // unpack the alpha values pairwise
            for (int i = 0; i < 8; ++i)
            {
                // quantise down to 4 bits
                byte quant = bytes[i];

                // unpack the values
                byte lo = (byte)(quant & 0x0f);
                byte hi = (byte)(quant & 0xf0);

                // convert back up to bytes
                rgba[8 * i + 3] = (byte)(lo | (lo << 4));
                rgba[8 * i + 7] = (byte)(hi | (hi >> 4));
            }
        }

        static void FixRange(ref int min, ref int max, int steps)
        {
            if (max - min < steps)
                max = Math.Min(min + steps, 255);
            if (max - min < steps)
                min = Math.Max(0, max - steps);
        }

        static int FitCodes(byte[] rgba, int mask, byte[] codes, byte[] indices)
        {
            // fit each alpha value to the codebook
            int err = 0;
            for (int i = 0; i < 16; ++i)
            {
                // check this pixel is valid
                int bit = 1 << i;
                if ((mask & bit) == 0)
                {
                    // use the first code
                    indices[i] = 0;
                    continue;
                }

                // find the least error and corresponding index
                int value = rgba[4 * i + 3];
                int least = int.MaxValue;
                int index = 0;
                for (int j = 0; j < 8; ++j)
                {
                    // get the squared error from this code
                    int dist = (int)value - (int)codes[j];
                    dist *= dist;

                    // compare with the best so far
                    if (dist < least)
                    {
                        least = dist;
                        index = j;
                    }
                }

                // save this index and accumulate the error
                indices[i] = (byte)index;
                err += least;
            }

            // return the total error
            return err;
        }

        public unsafe static void WriteAlphaBlock(int alpha0, int alpha1, byte[] indices, byte* block)
        {
            byte* bytes = block;

            // write the first two bytes
            bytes[0] = (byte)alpha0;
            bytes[1] = (byte)alpha1;

            // pack the indices with 3 bits each
            byte* dest = bytes + 2;
            int src = 0;
            for (int i = 0; i < 2; ++i)
            {
                // pack 8 3-bit values
                int value = 0;
                for (int j = 0; j < 8; ++j)
                {
                    int index = indices[src++];
                    value |= (index << 3 * j);
                }

                // store in 3 bytes
                for (int j = 0; j < 3; ++j)
                {
                    int u8 = (value >> 8 * j) & 0xff;
                    *dest++ = (byte)u8;
                }
            }
        }

        public unsafe static void WriteAlphaBlock5(int alpha0, int alpha1, byte[] indices, byte* block)
        {
            // check the relative values of the endpoints
            if (alpha0 > alpha1)
            {
                // swap the indices
                byte[] swapped = new byte[16];
                for (int i = 0; i < 16; ++i)
                {
                    byte index = indices[i];
                    if (index == 0)
                        swapped[i] = 1;
                    else if (index == 1)
                        swapped[i] = 0;
                    else if (index <= 5)
                        swapped[i] = (byte)(7 - index);
                    else
                        swapped[i] = index;
                }

                // write the block
                WriteAlphaBlock(alpha1, alpha0, swapped, block);
            }
            else
            {
                // write the block
                WriteAlphaBlock(alpha0, alpha1, indices, block);
            }
        }

        public unsafe static void WriteAlphaBlock7(int alpha0, int alpha1, byte[] indices, byte* block)
        {
            // check the relative values of the endpoints
            if (alpha0 < alpha1)
            {
                // swap the indices
                byte[] swapped = new byte[16];
                for (int i = 0; i < 16; ++i)
                {
                    byte index = indices[i];
                    if (index == 0)
                        swapped[i] = 1;
                    else if (index == 1)
                        swapped[i] = 0;
                    else
                        swapped[i] = (byte)(9 - index);
                }

                // write the block
                WriteAlphaBlock(alpha1, alpha0, swapped, block);
            }
            else
            {
                // write the block
                WriteAlphaBlock(alpha0, alpha1, indices, block);
            }
        }

        public static unsafe void CompressAlphaDxt5(byte[] rgba, int mask, byte* block)
        {
            // get the range for 5-alpha and 7-alpha interpolation
            int min5 = 255;
            int max5 = 0;
            int min7 = 255;
            int max7 = 0;
            for (int i = 0; i < 16; ++i)
            {
                // check this pixel is valid
                int bit = 1 << i;
                if ((mask & bit) == 0)
                    continue;

                // incorporate into the min/max
                int value = rgba[4 * i + 3];
                if (value < min7)
                    min7 = value;
                if (value > max7)
                    max7 = value;
                if (value != 0 && value < min5)
                    min5 = value;
                if (value != 255 && value > max5)
                    max5 = value;
            }

            // handle the case that no valid range was found
            if (min5 > max5)
                min5 = max5;
            if (min7 > max7)
                min7 = max7;

            // fix the range to be the minimum in each case
            FixRange(ref min5, ref max5, 5);
            FixRange(ref min7, ref max7, 7);

            // set up the 5-alpha code book
            byte[] codes5 = new byte[8];
            codes5[0] = (byte)min5;
            codes5[1] = (byte)max5;
            for (int i = 1; i < 5; ++i)
                codes5[1 + i] = (byte)(((5 - i) * min5 + i * max5) / 5);
            codes5[6] = 0;
            codes5[7] = 255;

            // set up the 7-alpha code book
            byte[] codes7 = new byte[8];
            codes7[0] = (byte)min7;
            codes7[1] = (byte)max7;
            for (int i = 1; i < 7; ++i)
                codes7[1 + i] = (byte)(((7 - i) * min7 + i * max7) / 7);

            // fit the data to both code books
            byte[] indices5 = new byte[16];
            byte[] indices7 = new byte[16];
            int err5 = FitCodes(rgba, mask, codes5, indices5);
            int err7 = FitCodes(rgba, mask, codes7, indices7);

            // save the block with least error
            if (err5 <= err7)
                WriteAlphaBlock5(min5, max5, indices5, block);
            else
                WriteAlphaBlock7(min7, max7, indices7, block);
        }

        public unsafe void DecompressAlphaDxt5(byte[] rgba, byte* block)
        {
            // get the two alpha values
            byte* bytes = block;
            int alpha0 = bytes[0];
            int alpha1 = bytes[1];

            // compare the values to build the codebook
            byte[] codes = new byte[8];
            codes[0] = (byte)alpha0;
            codes[1] = (byte)alpha1;
            if (alpha0 <= alpha1)
            {
                // use 5-alpha codebook
                for (int i = 1; i < 5; ++i)
                    codes[1 + i] = (byte)(((5 - i) * alpha0 + i * alpha1) / 5);
                codes[6] = 0;
                codes[7] = 255;
            }
            else
            {
                // use 7-alpha codebook
                for (int i = 1; i < 7; ++i)
                    codes[1 + i] = (byte)(((7 - i) * alpha0 + i * alpha1) / 7);
            }

            // decode the indices
            byte[] indices = new byte[16];
            byte* src = bytes + 2;
            int dest = 0;
            for (int i = 0; i < 2; ++i)
            {
                // grab 3 bytes
                int value = 0;
                for (int j = 0; j < 3; ++j)
                {
                    int u8 = *src++;
                    value |= (u8 << 8 * j);
                }

                // unpack 8 3-bit values from it
                for (int j = 0; j < 8; ++j)
                {
                    int index = (value >> 3 * j) & 0x7;
                    indices[dest++] = (byte)index;
                }
            }

            // write out the indexed codebook values
            for (int i = 0; i < 16; ++i)
                rgba[4 * i + 3] = codes[indices[i]];
        }
    }
}
