using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TextureCompressor
{
    class TextureResizer
    {
        public static void Resize(Texture2D texture, int width, int height, TextureFormat format, bool mipmaps)
        {
            Color32[] pixels = texture.GetPixels32();
            int origWidth = texture.width;
            int origHeight = texture.height;
            Color32[] newPixels = new Color32[width * height];
            int index = 0;
            for (int h = 0; h < height; h++)
            {
                for (int w = 0; w < width; w++)
                {
                    newPixels[index] = GetPixel(pixels, texture, ((float)w) / width, ((float)h) / height, width, height);
                    index++;
                }
            }
            texture.Resize(width, height, format, mipmaps);
            texture.SetPixels32(pixels);
            texture.Apply(mipmaps);
        }

        private static Color32 GetPixel(Color32[] pixels, Texture2D tex, float w, float h, float width, float height)
        {
            float widthDist = 4.0f - ((4.0f * (float)width) / tex.width);
            float heightDist = 4.0f - ((4.0f * (float)height) / tex.height);
            int[,] posArray = new int[2, 4];
            posArray[0, 0] = (int)Math.Floor((w * tex.width) - widthDist);
            posArray[0, 1] = (int)Math.Floor(w * tex.width);
            posArray[0, 2] = (int)Math.Ceiling((w * tex.width) + widthDist);
            posArray[0, 3] = (int)Math.Ceiling((w * tex.width) + (2.0 * widthDist));
            posArray[1, 0] = (int)Math.Floor((h * tex.height) - heightDist);
            posArray[1, 1] = (int)Math.Floor(h * tex.height);
            posArray[1, 2] = (int)Math.Ceiling((h * tex.height) + heightDist);
            posArray[1, 3] = (int)Math.Ceiling((h * tex.height) + (2.0 * heightDist));

            Color32 cw1 = new Color32(), cw2 = new Color32(), cw3 = new Color32(), cw4 = new Color32(), ch1 = new Color32(), ch2 = new Color32(), ch3 = new Color32(), ch4 = new Color32();
            int w1 = posArray[0, 0];
            int w2 = posArray[0, 1];
            int w3 = posArray[0, 2];
            int w4 = posArray[0, 3];
            int h1 = posArray[1, 0];
            int h2 = posArray[1, 1];
            int h3 = posArray[1, 2];
            int h4 = posArray[1, 3];

            if (h2 >= 0 && h2 < tex.height)
            {
                if (w2 >= 0 && w2 < tex.width)
                {
                    cw2 = pixels[w2+ (h2*tex.width)];
                }
                if (w1 >= 0 && w1 < tex.width)
                {
                    cw1 = pixels[w1 + (h2 * tex.width)];
                }
                else
                {
                    cw1 = cw2;
                }
                if (w3 >= 0 && w3 < tex.width)
                {
                    cw3 = pixels[w3 + (h2 * tex.width)];
                }
                else
                {
                    cw3 = cw2;
                }
                if (w4 >= 0 && w4 < tex.width)
                {
                    cw4 = pixels[w4 + (h2 * tex.width)];
                }
                else
                {
                    cw4 = cw3;
                }

            }
            if (w2 >= 0 && w2 < tex.width)
            {
                if (h2 >= 0 && h2 < tex.height)
                {
                    ch2 = pixels[w2 + (h2 * tex.width)];
                }
                if (h1 >= 0 && h1 < tex.height)
                {
                    ch1 = pixels[w2 + (h1 * tex.width)];
                }
                else
                {
                    ch1 = ch2;
                }
                if (h3 >= 0 && h3 < tex.height)
                {
                    ch3 = pixels[w2 + (h3 * tex.width)];
                }
                else
                {
                    ch3 = ch2;
                }
                if (h4 >= 0 && h4 < tex.height)
                {
                    ch4 = pixels[w2 + (h4 * tex.width)];
                }
                else
                {
                    ch4 = ch3;
                }
            }
            byte cwr = (byte)(((.25f * cw1.r) + (.75f * cw2.r) + (.75f * cw3.r) + (.25f * cw4.r)) / 2.0f);
            byte cwg = (byte)(((.25f * cw1.g) + (.75f * cw2.g) + (.75f * cw3.g) + (.25f * cw4.g)) / 2.0f);
            byte cwb = (byte)(((.25f * cw1.b) + (.75f * cw2.b) + (.75f * cw3.b) + (.25f * cw4.b)) / 2.0f);
            byte cwa = (byte)(((.25f * cw1.a) + (.75f * cw2.a) + (.75f * cw3.a) + (.25f * cw4.a)) / 2.0f);
            byte chr = (byte)(((.25f * ch1.r) + (.75f * ch2.r) + (.75f * ch3.r) + (.25f * ch4.r)) / 2.0f);
            byte chg = (byte)(((.25f * ch1.g) + (.75f * ch2.g) + (.75f * ch3.g) + (.25f * ch4.g)) / 2.0f);
            byte chb = (byte)(((.25f * ch1.b) + (.75f * ch2.b) + (.75f * ch3.b) + (.25f * ch4.b)) / 2.0f);
            byte cha = (byte)(((.25f * ch1.a) + (.75f * ch2.a) + (.75f * ch3.a) + (.25f * ch4.a)) / 2.0f);
            byte R = (byte)((cwr + chr) / 2.0f);
            byte G = (byte)((cwg + chg) / 2.0f);
            byte B = (byte)((cwb + chb) / 2.0f);
            byte A = (byte)((cwa + cha) / 2.0f);

            Color32 color = new Color32(R, G, B, A);
            return color;
        }
    }
}
