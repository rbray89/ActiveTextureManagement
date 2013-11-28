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

        protected void Update()
        {
            if (!Compressed)
            {
                CompressAllTextures();
                if (HighLogic.LoadedScene == GameScenes.MAINMENU)
                {
                    Compressed = true;
                    // empty the cache when we are done
                    processedTexture = null;
                }
            }
        }

        Dictionary<GameDatabase.TextureInfo, bool> processedTexture = new Dictionary<GameDatabase.TextureInfo, bool>();

        private void CompressAllTextures()
        {
            foreach (GameDatabase.TextureInfo t in GameDatabase.Instance.databaseTexture)
            {
                //Log("name: " + t.name);
                //Log("format: " + t.texture.format.ToString());
                if (!processedTexture.ContainsKey(t))
                    try
                    {
                        t.texture.GetPixel(0, 0);
                        if (t.isReadable && t.texture.format != TextureFormat.DXT1 && t.texture.format != TextureFormat.DXT5)
                        {
                            Log("Compressing... " + t.name);
                            t.texture.Compress(true);
                            processedTexture[t] = true;
                        }
                        else
                            processedTexture[t] = false;
                    }
                    catch (UnityException)
                    {
                        processedTexture[t] = false;
                    }
            }
        }

        public static void Log(String message)
        {
            UnityEngine.Debug.Log("TextureCompressor: " + message);
        }

    }
}
