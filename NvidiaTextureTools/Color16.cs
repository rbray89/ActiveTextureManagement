using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace NvidiaTextureTools
{
    public class Color16
    {
        public Color16() { }
        public Color16(Color16 c) { u = c.u; }
        public Color16(ushort U) { u = U; }

        byte rb;//5
        byte gb;//6
        byte bb;//5
        public byte r 
        {
            set
            {
                rb = value;
                uvalue &= 0x07FF;
                uvalue |= (ushort)((value) << 11);
            }
            get { return rb; }
        }
        public byte g
        {
            set
            {
                gb = value;
                uvalue &= 0xF8EF;
                uvalue |= (ushort)((value) << 5);
            }
            get { return gb; }
        }
        public byte b
        {
            set
            {
                bb = value;
                uvalue &= 0xFFE0;
                uvalue |= (ushort)((value));
            }
            get { return bb; }
        }
        ushort uvalue;
        public ushort u 
        {
            set
            {
                uvalue = value;
                rb = (byte)((value >> 11) & 0x1F);
                gb = (byte)((value>>5)&0x3F);
                bb = (byte)(value & 0x1F);
            }
            get { return uvalue; }
        }
      
    }

    public static class ColorExtensions
    {
        public static uint u(this Color32 color)
        {
            return (uint)(color.r | (color.g << 8) | (color.b << 16) | (color.a << 24));
        }

        public static byte[] component(this Color32 color)
        {
            return new byte[4] { color.b, color.g, color.r, color.a };
        }

        public static byte[] bytes(this Texture2D texture)
        {
            byte[] array = new byte[texture.width * texture.height * 4];
            Color32[] colors = texture.GetPixels32();
            for (int i = 0; i < colors.Length; i++ )
            {
                array[i*4] = colors[i].r;
                array[i*4+1] = colors[i].g;
                array[i*4+2] = colors[i].b;
                array[i*4+3] = colors[i].a;
            }
            return array;
        }
    }   



}
