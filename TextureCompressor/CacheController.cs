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

        public static GameDatabase.TextureInfo FetchCacheTexture(GameDatabase.TextureInfo Texture, int width, int height, bool compress, bool mipmaps, bool makeNotReadable)
        {
            String textureName = Texture.name;
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
                    if (cacheHash != hashString || cacheIsNorm != Texture.isNormalMap || width != cacheWidth || height != cacheHeight)
                    {
                        if (cacheHash != hashString)
                        {
                            TextureCompressor.DBGLog(cacheHash + " != " + hashString);
                        }
                        if (cacheIsNorm != Texture.isNormalMap)
                        {
                            TextureCompressor.DBGLog(cacheIsNorm + " != " + Texture.isNormalMap);
                        }
                        if (width != cacheWidth)
                        {
                            TextureCompressor.DBGLog(width + " != " + cacheWidth);
                        }
                        if (height != cacheHeight)
                        {
                            TextureCompressor.DBGLog(height + " != " + cacheHeight);
                        }
                        return RebuildCache(Texture, width, height, compress, mipmaps, makeNotReadable);
                    }
                    else
                    {
                        TextureCompressor.DBGLog("Loading from cache...");
                        Texture2D newTex = new Texture2D(4, 4);
                        GameDatabase.TextureInfo cacheTexture = new GameDatabase.TextureInfo(newTex, Texture.isNormalMap, !makeNotReadable, compress);
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
                    TextureCompressor.DBGLog("Texture " + originalTextureFile+ " does not exist!");
                    return RebuildCache(Texture, width, height, compress, mipmaps, makeNotReadable);
                }
            }
            else
            {
                return RebuildCache(Texture, width, height, compress, mipmaps, makeNotReadable);
            }
        }

        private static GameDatabase.TextureInfo RebuildCache(GameDatabase.TextureInfo Texture, int width, int height, bool compress, bool mipmaps, bool makeNotReadable)
        {
            TextureCompressor.DBGLog("Rebuilding Cache... " + Texture.name);
            GameDatabase.TextureInfo cacheTexture;
            
            TextureCompressor.DBGLog("Loading texture...");
            cacheTexture = TextureConverter.GetReadable(Texture, mipmaps, width, height);
            TextureCompressor.DBGLog("Texture loaded.");

            Texture2D tex = cacheTexture.texture;

            String textureName = cacheTexture.name;
            String cacheFile = KSPUtil.ApplicationRootPath + "GameData/BoulderCo/textureCompressorCache/" + textureName;
            TextureConverter.WriteTo(cacheTexture.texture, cacheFile);

            if (compress)
            {
                tex.Compress(true);
            }
            cacheTexture.isCompressed = compress;
            if (!makeNotReadable)
            {
                tex.Apply(mipmaps);
            }
            else
            {
                tex.Apply(mipmaps, true);
            }
            cacheTexture.isReadable = !makeNotReadable;

            String originalTextureFile = cacheTexture.texture.name;
            cacheTexture.texture.name = cacheTexture.name;
            String cacheConfigFile = cacheFile + ".tcache";
            
            TextureCompressor.DBGLog("Created Config for" + originalTextureFile);

            String hashString = GetMD5String(originalTextureFile);

            ConfigNode config = new ConfigNode();
            config.AddValue("md5", hashString); TextureCompressor.DBGLog("md5: " + hashString);
            config.AddValue("orig_format", Path.GetExtension(originalTextureFile)); TextureCompressor.DBGLog("orig_format: " + Path.GetExtension(originalTextureFile));
            config.AddValue("is_normal", cacheTexture.isNormalMap.ToString()); TextureCompressor.DBGLog("is_normal: " + cacheTexture.isNormalMap.ToString());
            config.AddValue("width", width.ToString()); TextureCompressor.DBGLog("width: " + width.ToString());
            config.AddValue("height", height.ToString()); TextureCompressor.DBGLog("height: " + height.ToString());
            
            config.Save(cacheConfigFile);
            TextureCompressor.DBGLog("Saved Config.");

            return cacheTexture;
        }

        static String GetMD5String(String file)
        {
            if(file == LastFile)
            {
                return MD5String;
            }
            FileStream stream = File.OpenRead(file);
            MD5 md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(stream);
            stream.Close();
            MD5String = BitConverter.ToString(hash);
            LastFile = file;
            return MD5String;
        }

    }
}
