// Copyright NVIDIA Corporation 2007 -- Ignacio Castano <icastano@nvidia.com>
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

namespace NvidiaTextureTools
{

    /// DXT1 block.
    public class BlockDXT1
    {
        public Color16 col0 = new Color16();
        public Color16 col1 = new Color16();
        
    //union
        byte[] row = new byte[4];
        public uint indices
        {
            get{ return BitConverter.ToUInt32(row,0);}
            set{ row = BitConverter.GetBytes(value);}
        }
    //  
        public void WriteBytes(byte[] dst, int index)
        {
            BitConverter.GetBytes(col0.u).CopyTo(dst, index);
            BitConverter.GetBytes(col1.u).CopyTo(dst, index+2);
            row.CopyTo(dst, index + 4);
        }

        public uint evaluatePalette(Color32[] color_array, bool d3d9)
        {
            // Does bit expansion before interpolation.
            color_array[0].b = (byte)((col0.b << 3) | (col0.b >> 2));
            color_array[0].g = (byte)((col0.g << 2) | (col0.g >> 4));
            color_array[0].r = (byte)((col0.r << 3) | (col0.r >> 2));
            color_array[0].a = 0xFF;

            // @@ Same as above, but faster?
            //  Color32 c;
            //  c.u = ((col0.u << 3) & 0xf8) | ((col0.u << 5) & 0xfc00) | ((col0.u << 8) & 0xf80000);
            //  c.u |= (c.u >> 5) & 0x070007;
            //  c.u |= (c.u >> 6) & 0x000300;
            //  color_array[0].u = c.u;

            color_array[1].r = (byte)((col1.r << 3) | (col1.r >> 2));
            color_array[1].g = (byte)((col1.g << 2) | (col1.g >> 4));
            color_array[1].b = (byte)((col1.b << 3) | (col1.b >> 2));
            color_array[1].a = 0xFF;

            // @@ Same as above, but faster?
            //  c.u = ((col1.u << 3) & 0xf8) | ((col1.u << 5) & 0xfc00) | ((col1.u << 8) & 0xf80000);
            //  c.u |= (c.u >> 5) & 0x070007;
            //  c.u |= (c.u >> 6) & 0x000300;
            //  color_array[1].u = c.u;

            if (col0.u > col1.u)
            {
                int bias = 0;
                if (d3d9) bias = 1;

                // Four-color block: derive the other two colors.
                color_array[2].r = (byte)((2 * color_array[0].r + color_array[1].r + bias) / 3);
                color_array[2].g = (byte)((2 * color_array[0].g + color_array[1].g + bias) / 3);
                color_array[2].b = (byte)((2 * color_array[0].b + color_array[1].b + bias) / 3);
                color_array[2].a = 0xFF;

                color_array[3].r = (byte)((2 * color_array[1].r + color_array[0].r + bias) / 3);
                color_array[3].g = (byte)((2 * color_array[1].g + color_array[0].g + bias) / 3);
                color_array[3].b = (byte)((2 * color_array[1].b + color_array[0].b + bias) / 3);
                color_array[3].a = 0xFF;

                return 4;
            }
            else
            {
                // Three-color block: derive the other color.
                color_array[2].r = (byte)((color_array[0].r + color_array[1].r) / 2);
                color_array[2].g = (byte)((color_array[0].g + color_array[1].g) / 2);
                color_array[2].b = (byte)((color_array[0].b + color_array[1].b) / 2);
                color_array[2].a = 0xFF;

                // Set all components to 0 to match DXT specs.
                color_array[3].r = 0x00; // color_array[2].r;
                color_array[3].g = 0x00; // color_array[2].g;
                color_array[3].b = 0x00; // color_array[2].b;
                color_array[3].a = 0x00;

                return 3;
            }
        }
        //uint evaluatePaletteNV5x(Color32[] color_array);

        //void evaluatePalette3(Color32[] color_array, bool d3d9);
        //void evaluatePalette4(Color32[] color_array, bool d3d9);

        //void decodeBlock(ColorBlock block, bool d3d9 = false);
        //void decodeBlockNV5x(ColorBlock block);

        //void setIndices(int * idx);

        //void flip4();
        //void flip2();

        /// Return true if the block uses four color mode, false otherwise.
        public bool isFourColorMode()
        {
            return col0.u > col1.u;
        }
    }

    /// DXT5 alpha block.
    public class AlphaBlockDXT5
    {
        //union
         
        byte alpha0b;      // 8
        byte alpha1b;      // 16
        public byte alpha0
        {
            get 
            {
                return alpha0b;
            }
            set
            {
                alpha0b = value;
                uvalue &= 0xFFFFFFFFFFFFFF00;
                uvalue |= value;
            }
        }
        public byte alpha1
        {
            get
            {
                return alpha1b;
            }
            set
            {
                alpha1b = value;
                uvalue &= 0xFFFFFFFFFFFF00FF;
                uvalue |= (byte)(value<<8);
            }
        }
        /*
        byte bits0;       // 3 - 19
        byte bits1;       // 6 - 22
        byte bits2;       // 9 - 25
        byte bits3;       // 12 - 28
        byte bits4;       // 15 - 31
        byte bits5;       // 18 - 34
        byte bits6;       // 21 - 37
        byte bits7;       // 24 - 40
        byte bits8;       // 27 - 43
        byte bits9;       // 30 - 46
        byte bitsA;       // 33 - 49
        byte bitsB;       // 36 - 52
        byte bitsC;       // 39 - 55
        byte bitsD;       // 42 - 58
        byte bitsE;       // 45 - 61
        byte bitsF;       // 48 - 64
            */
        ulong uvalue;
        public ulong u
        {
            set
            {
                uvalue = value;
                alpha0b = (byte)((value)&0xFF);
                alpha1b = (byte)((value>>8)&0xFF);
                /*
                bits0 = (byte)((value>>16)&0x07);
                bits1 = (byte)((value>>19)&0x07);
                bits2 = (byte)((value>>22)&0x07);
                bits3 = (byte)((value>>25)&0x07);
                bits4 = (byte)((value>>28)&0x07);
                bits5 = (byte)((value>>31)&0x07);
                bits6 = (byte)((value>>34)&0x07);
                bits7 = (byte)((value>>37)&0x07);
                bits8 = (byte)((value>>40)&0x07);
                bits9 = (byte)((value>>43)&0x07);
                bitsA = (byte)((value>>46)&0x07);
                bitsB = (byte)((value>>49)&0x07);
                bitsC = (byte)((value>>52)&0x07);
                bitsD = (byte)((value>>55)&0x07);
                bitsE = (byte)((value>>58)&0x07);
                bitsF = (byte)((value>>61)&0x07);*/
            }
            get{return uvalue;}
        }

        public void evaluatePalette(byte[] alpha, bool d3d9)
        {
            if (alpha0 > alpha1) {
                evaluatePalette8(alpha, d3d9);
            }
            else {
                evaluatePalette6(alpha, d3d9);
            }
        }
        
        void evaluatePalette8(byte[] alpha, bool d3d9)
{
    int bias = 0;
    if (d3d9) bias = 3;

    // 8-alpha block:  derive the other six alphas.
    // Bit code 000 = alpha0, 001 = alpha1, others are interpolated.
    alpha[0] = alpha0;
    alpha[1] = alpha1;
    alpha[2] = (byte)((6 * alpha[0] + 1 * alpha[1] + bias) / 7);    // bit code 010
    alpha[3] = (byte)((5 * alpha[0] + 2 * alpha[1] + bias) / 7);    // bit code 011
    alpha[4] = (byte)((4 * alpha[0] + 3 * alpha[1] + bias) / 7);    // bit code 100
    alpha[5] = (byte)((3 * alpha[0] + 4 * alpha[1] + bias) / 7);    // bit code 101
    alpha[6] = (byte)((2 * alpha[0] + 5 * alpha[1] + bias) / 7);    // bit code 110
    alpha[7] = (byte)((1 * alpha[0] + 6 * alpha[1] + bias) / 7);    // bit code 111
}

void evaluatePalette6(byte[] alpha, bool d3d9)
{
    int bias = 0;
    if (d3d9) bias = 2;

    // 6-alpha block.
    // Bit code 000 = alpha0, 001 = alpha1, others are interpolated.
    alpha[0] = alpha0;
    alpha[1] = alpha1;
    alpha[2] = (byte)((4 * alpha[0] + 1 * alpha[1] + bias) / 5);    // Bit code 010
    alpha[3] = (byte)((3 * alpha[0] + 2 * alpha[1] + bias) / 5);    // Bit code 011
    alpha[4] = (byte)((2 * alpha[0] + 3 * alpha[1] + bias) / 5);    // Bit code 100
    alpha[5] = (byte)((1 * alpha[0] + 4 * alpha[1] + bias) / 5);    // Bit code 101
    alpha[6] = 0x00;                                        // Bit code 110
    alpha[7] = 0xFF;                                        // Bit code 111
}
        //void indices(byte[] index_array);

        public uint index(uint index)
        {
            uint offset = (3 * index + 16);
            return (uint)((this.u >> (int)offset) & 0x7);
        }
        public void setIndex(uint index, uint value)
        {
            uint offset = (3 * index + 16);
            ulong mask = (ulong)((0x7) << (int) offset);
            this.u = (this.u & ~mask) | ((ulong)((value) << (int)offset));
        }
        //void decodeBlock(ColorBlock block, bool d3d9 = false);
        //void decodeBlock(AlphaBlock4x4 block, bool d3d9 = false);

        //void flip4();
        //void flip2();

        internal void WriteBytes(byte[] dst, int index)
        {
            BitConverter.GetBytes(uvalue).CopyTo(dst, index);
        }
    }


    /// DXT5 block.
    public class BlockDXT5
    {
        public AlphaBlockDXT5 alpha = new AlphaBlockDXT5();
        public BlockDXT1 color = new BlockDXT1();

        //void decodeBlock(ColorBlock block, bool d3d9 = false);
        //void decodeBlockNV5x(ColorBlock block);

        //void flip4();
        //void flip2();
        public void WriteBytes(byte[] dst, int index)
        {
            alpha.WriteBytes(dst, index);
            color.WriteBytes(dst, index+8);
        }
    }


}

