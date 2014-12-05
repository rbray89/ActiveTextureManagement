using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace ActiveTextureManagement
{

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
