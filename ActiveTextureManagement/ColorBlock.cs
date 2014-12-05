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
    
    class ColorArray
    {
        private Color32[] m_color = new Color32[4*4];
        public  Color32[] Array
        {
            get {return m_color;}
            set { m_color = value; }
        }

        public Color32 this[uint i]
        {
            get{return m_color[i];}
            set{m_color[i] = value;}
        }
        public Color32 this[int x, int y]
        {
            get{return m_color[y * 4 + x];}
            set{m_color[y * 4 + x] = value;}
        }
    }

    /// Uncompressed 4x4 color block.
    class ColorBlock
    {
        //ColorBlock();
        //ColorBlock(uint * linearImage);
        //ColorBlock(ref ColorBlock block);
        //ColorBlock(Image * img, uint x, uint y);

        //void init(Image * img, uint x, uint y);
        //void init(uint w, uint h, uint[] data, uint x, uint y);
        //void init(uint w, uint h, float[] data, uint x, uint y);

        //void swizzle(uint x, uint y, uint z, uint w); // 0=r, 1=g, 2=b, 3=a, 4=0xFF, 5=0

        public bool isSingleColor()
        {
            uint u = m_color[0].u();

            for (uint i = 1; i < 16; i++)
            {
                if (u != (m_color[i].u()))
                {
                    return false;
                }
            }

            return true;
        }
        //bool hasAlpha();


        // Accessors
        public Color32[] colors()
        {
            return m_color.Array;
        }

        public ColorArray color
        {
            get {return m_color;}
        }
        
    private ColorArray m_color = new ColorArray();

    public ColorBlock(Color32[] sourceRgba)
    {
        m_color.Array = sourceRgba;
    }

    };

    public class VectorArray
    {
        private Vector4[] m_color = new Vector4[4 * 4];
        public Vector4[] Array
        {
            get { return m_color; }
        }

        public Vector4 this[uint i]
        {
            get { return m_color[i]; }
            set { m_color[i] = value; }
        }
        public Vector4 this[int x, int y]
        {
            get { return m_color[y * 4 + x]; }
            set { m_color[y * 4 + x] = value; }
        }
    }

    public class ColorSet
    {
        ColorSet()
        {
            colorCount = 0;
            indexCount = 0;
            w = 0;
            h = 0;
        }
        //~ColorSet() {}

        //void allocate(uint w, uint h);

        //void setColors(float * data, uint img_w, uint img_h, uint img_x, uint img_y);
        //void setColors(Vector3[] colors, float[] weights);
        //void setColors(Vector4[] colors, float[] weights);

        //void setAlphaWeights();
        //void setUniformWeights();

        //void createMinimalSet(bool ignoreTransparent);
        //void wrapIndices();

        //void swizzle(uint x, uint y, uint z, uint w); // 0=r, 1=g, 2=b, 3=a, 4=0xFF, 5=0

        //bool isSingleColor(bool ignoreAlpha);
        //bool hasAlpha();

        float weight(uint i) { return weights[indices[i]]; }

        public bool isValidIndex(uint i) { return i < indexCount && indices[i] >= 0; }

        uint colorCount;
        uint indexCount;    // Fixed to 16
        uint w, h;          // Fixed to 4x4

        public VectorArray color
        {
            get { return colors; }
        }

        private VectorArray colors = new VectorArray();

        public float[] weights = new float[16];  // @@ Add mask to indicate what color components are weighted?
        int[] indices = new int[16];
    }

    /// Uncompressed 4x4 alpha block.
    class AlphaBlock4x4
    {
        public void init(byte a)
        {
            for (int i = 0; i < 16; i++)
            {
                alpha[i] = a;
                weights[i] = 1.0f;
            }
        }
        public void init(ColorBlock src, uint channel)
        {
            // Colors are in BGRA format.
            if (channel == 0) channel = 2;
            else if (channel == 2) channel = 0;

            for (uint i = 0; i < 16; i++)
            {
                alpha[i] = src.color[i].component()[channel];
                weights[i] = 1.0f;
            }
        }
        //void init(ColorSet src, uint channel);

        //void initMaxRGB(ColorSet src, float threshold);
        //void initWeights(ColorSet src);

        public byte[] alpha = new byte[4*4];
        public float[] weights = new float[16];
    };
    
}
