using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TextureCompressor
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    public class TextureCompressor : MonoBehaviour
    {
        static bool Compressed = false;

        protected void Awake()
        {

            if (HighLogic.LoadedScene == GameScenes.MAINMENU)
            {
                CompressAllTextures();
            }
        }

        private void CompressAllTextures()
        {
            if(!Compressed)
            {
                foreach (GameDatabase.TextureInfo t in GameDatabase.Instance.databaseTexture)
                {
                    //Log("name: " + t.name);
                    //Log("format: " + t.texture.format.ToString());
                    try
                    {
                        t.texture.GetPixel(0,0);
                        if (t.isReadable && t.texture.format.ToString() != TextureFormat.DXT1.ToString() && t.texture.format.ToString() != TextureFormat.DXT5.ToString())
                        {
                            Log("Compressing... " + t.name);
                            t.texture.Compress(true);
                        }
                    }
                    catch(UnityException e)
                    {

                    }
                    
                }


                Compressed = true;
            }
        }

        public static void Log(String message)
        {
            UnityEngine.Debug.Log("TextureCompressor: " + message);
        }

    }
}
