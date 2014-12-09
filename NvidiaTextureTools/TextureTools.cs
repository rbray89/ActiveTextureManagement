using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace NvidiaTextureTools
{
    public class TextureToolsDXT
    {

        public static void GetDXT(Texture2D texture, int i, byte[] bytes, TextureFormat format)
        {
            Color32[] colors = texture.GetPixels32(i);
            uint w = (uint) texture.width>>i;
	        uint h = (uint) texture.height>>i;
	
	        ColorBlock rgba = new ColorBlock();
            BlockDXT1 block1 = new BlockDXT1();
	        BlockDXT5 block5 = new BlockDXT5();

            int blocksize = format == TextureFormat.DXT1 ? 8 : 16;
            int index = 0;
	        for (uint y = 0; y < h; y += 4) {
		        for (uint x = 0; x < w; x += 4) {
			        rgba.init(w, h, colors, x, y);

                    if (format == TextureFormat.DXT1)
                    {
                        QuickCompress.compressDXT1(rgba, block1);
                        block1.WriteBytes(bytes, index);
                    }
                    else
                    {
                        QuickCompress.compressDXT5(rgba, block5, 0);
                        block5.WriteBytes(bytes, index);
                    }

                    index += blocksize;
		        }
	        }
        }

    }
}
