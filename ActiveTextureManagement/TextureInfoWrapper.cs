using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ActiveTextureManagement
{
    public class TextureInfoWrapper : GameDatabase.TextureInfo
    {
        public TextureInfoWrapper(UrlDir.UrlFile file, UnityEngine.Texture2D newTex, bool nrmMap, bool readable, bool compress)
            : base(file, newTex, nrmMap, readable, compress)
        {

        }

    }
}
