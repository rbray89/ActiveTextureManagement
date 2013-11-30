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
        static int LastTextureIndex = -1;
        static int memorySaved = 0;

        protected void Awake()
        {
            if (HighLogic.LoadedScene == GameScenes.MAINMENU && !Compressed)
            {
                Update();
                Compressed = true;
                int kbSaved = (int) (memorySaved / 1024f);
                int mbSaved = (int)((memorySaved / 1024f) / 1024f);
                Log("Memory Saved : "+ memorySaved.ToString() + "B");
                Log("Memory Saved : " + kbSaved.ToString() + "kB");
                Log("Memory Saved : " + mbSaved.ToString() + "MB");
            }
        }

        protected void Update()
        {
            if (!Compressed && GameDatabase.Instance.databaseTexture.Count > 0)
            {
                int LocalLastTextureIndex = GameDatabase.Instance.databaseTexture.Count-1;
                if (LastTextureIndex != LocalLastTextureIndex)
                {
                    for (int i = LastTextureIndex + 1; i < GameDatabase.Instance.databaseTexture.Count; i++)
                    {
                        GameDatabase.TextureInfo Texture = GameDatabase.Instance.databaseTexture[i];
                        LastTextureIndex = i;
                        CompressTexture(Texture.texture);
                    }
                }
            }
        }

        private void CompressTexture(Texture2D texture)
        {
            try
            {
                TextureFormat format = texture.format;

                if (format != TextureFormat.DXT1 && format != TextureFormat.DXT5)
                {
                    texture.GetPixel(0, 0);
                    Log("--------------------------------------------------------");
                    Log("Name: " + texture.name);
                    Log("Format: " + texture.format.ToString());
                    Log("Size: " + texture.width.ToString() + "x" + texture.height);
                    texture.Compress(true);
                    TextureFormat newFormat = texture.format;
                    int oldSize = 0;
                    int newSize = 0;
                    switch (format)
                    {
                        case TextureFormat.ARGB32:
                        case TextureFormat.RGBA32:
                        case TextureFormat.BGRA32:
                            oldSize = 4 * (texture.width * texture.height);
                            break;
                        case TextureFormat.RGB24:
                            oldSize = 3 * (texture.width * texture.height);
                            break;
                        case TextureFormat.Alpha8:
                            oldSize = texture.width * texture.height;
                            break;
                    }
                    if (newFormat == TextureFormat.DXT1)
                    {
                        newSize = (texture.width * texture.height) / 2;
                    }
                    else
                    {
                        newSize = (texture.width * texture.height);
                    }
                    int saved = (oldSize - newSize);
                    Log("Saved: " + saved.ToString() + "B");
                    memorySaved += saved;
                }
                else
                {
                    //Log("Already Compressed.");
                }
            }
            catch (UnityException e)
            {

            }

        }

        public static void Log(String message)
        {
            UnityEngine.Debug.Log("TextureCompressor: " + message);
        }

    }
}
