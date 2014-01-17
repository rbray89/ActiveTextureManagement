using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace TextureCompressor
{
    class CacheController
    {
        static String MD5String = "";
        static String LastFile = "";

        public static GameDatabase.TextureInfo FetchCacheTexture(GameDatabase.TextureInfo Texture, int width, int height, bool compress, bool mipmaps, FilterMode filterMode, bool makeNotReadable)
        {
            String textureName = Texture.name;
            bool isNormal = Texture.isNormalMap;
            String originalTextureFile = KSPUtil.ApplicationRootPath + "GameData/" + textureName;
            String cacheFile = KSPUtil.ApplicationRootPath + "GameData/BoulderCo/textureCompressorCache/" + textureName;
            String cacheConfigFile = cacheFile + ".tcache";
            if (File.Exists(cacheFile) && File.Exists(cacheConfigFile))
            {
               
                ConfigNode config = ConfigNode.Load(cacheConfigFile);
                string format = config.GetValue("orig_format");
                if (format != null && File.Exists(originalTextureFile + format))
                {
                    originalTextureFile += format;
                    String cacheHash = config.GetValue("md5");
                    String cacheIsNormString = config.GetValue("is_normal");
                    String cacheWidthString = config.GetValue("width");
                    String cacheHeihtString = config.GetValue("height");
                    bool cacheIsNorm = false;
                    int cacheWidth = 0;
                    int cacheHeight = 0;
                    bool.TryParse(cacheIsNormString, out cacheIsNorm);
                    int.TryParse(cacheWidthString, out cacheWidth);
                    int.TryParse(cacheHeihtString, out cacheHeight);

                    String hashString = GetMD5String(originalTextureFile);
                    if (cacheHash != hashString || cacheIsNorm != isNormal || width != cacheWidth || height != cacheHeight)
                    {
                        return RebuildCache(Texture, width, height, compress, mipmaps, filterMode, makeNotReadable);
                    }
                    else
                    {
                        Texture2D newTex = new Texture2D(4, 4);
                        GameDatabase.TextureInfo cacheTexture = new GameDatabase.TextureInfo(newTex, isNormal, true, false);
                        bool hasMipmaps = newTex.mipmapCount == 1 ? false : true;
                        TextureConverter.IMGToTexture(cacheFile, cacheTexture, mipmaps, cacheIsNorm, width, height);
                        cacheTexture.name = textureName;
                        newTex.name = textureName;
                        if (!hasMipmaps)
                        {

                        }
                        if (compress)
                        {
                            newTex.Compress(true);
                        }
                        newTex.Apply(mipmaps, makeNotReadable);
                        return cacheTexture;
                    }
                }
                else
                {
                    return RebuildCache(Texture, width, height, compress, mipmaps, filterMode, makeNotReadable);
                }
            }
            else
            {
                return RebuildCache(Texture, width, height, compress, mipmaps, filterMode, makeNotReadable);
            }
        }

        private static GameDatabase.TextureInfo RebuildCache(GameDatabase.TextureInfo Texture, int width, int height, bool compress, bool mipmaps, FilterMode filterMode, bool makeNotReadable)
        {
            TextureCompressor.Log("Rebuilding Cache... " + Texture.name);
            GameDatabase.TextureInfo cacheTexture;
            bool isNormalFormat = Texture.name.EndsWith("_NRM");
            bool hasMipmaps = Texture.texture.mipmapCount == 1 ? false : true;

            Texture.isReadable = true;
            try { Texture.texture.GetPixel(0, 0); }
            catch
            {
                Texture.isReadable = false;
            }

            TextureCompressor.Log("Loading texture...");
            cacheTexture = TextureConverter.GetReadable(Texture, mipmaps, isNormalFormat, width, height);
            TextureCompressor.Log("Texture loaded.");

            Texture2D tex = cacheTexture.texture;

            String textureName = cacheTexture.name;
            String cacheFile = KSPUtil.ApplicationRootPath + "GameData/BoulderCo/textureCompressorCache/" + textureName;
            TextureConverter.WriteTo(cacheTexture.texture, cacheFile);

            tex.filterMode = filterMode;
            if (compress)
            {
                tex.Compress(true);
                cacheTexture.isCompressed = true;
            }
            if (!makeNotReadable)
            {
                tex.Apply(mipmaps);
                cacheTexture.isReadable = true;
            }
            else
            {
                tex.Apply(mipmaps, true);
                cacheTexture.isReadable = false;
            }

            String originalTextureFile = cacheTexture.texture.name;
            cacheTexture.texture.name = cacheTexture.name;
            String cacheConfigFile = cacheFile + ".tcache";
            ConfigNode config = ConfigNode.Load(cacheConfigFile) ?? new ConfigNode();
            TextureCompressor.Log("Created Config for" + originalTextureFile);

            String hashString = GetMD5String(originalTextureFile);
            config.values.RemoveValues("");
            config.AddValue("md5", hashString); TextureCompressor.Log("md5: " + hashString);
            config.AddValue("orig_format", Path.GetExtension(originalTextureFile)); TextureCompressor.Log("orig_format: " + Path.GetExtension(originalTextureFile));
            config.AddValue("is_normal", cacheTexture.isNormalMap.ToString()); TextureCompressor.Log("is_normal: " + cacheTexture.isNormalMap.ToString());
            config.AddValue("width", width.ToString()); TextureCompressor.Log("width: " + width.ToString());
            config.AddValue("height", height.ToString()); TextureCompressor.Log("height: " + height.ToString());
            
            

            config.Save(cacheConfigFile);
            TextureCompressor.Log("Saved Config.");

            return cacheTexture;
        }

        static String GetMD5String(String file)
        {
            if(file == LastFile)
            {
                return MD5String;
            }
            Stream stream = File.OpenRead(file);
            MD5 md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(stream);
            MD5String = BitConverter.ToString(hash);
            LastFile = file;
            return MD5String;
        }

    }
}
