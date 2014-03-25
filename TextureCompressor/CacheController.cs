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

        public static TexInfo LoadCacheTextureInfo(String name)
        {
            String cacheConfig = KSPUtil.ApplicationRootPath + "GameData/BoulderCo/textureCompressorCache/" + name + ".tcache";
            if (File.Exists(cacheConfig))
            {
                    ConfigNode config = ConfigNode.Load(cacheConfig);
                    string swidth = config.GetValue("orig_width");
                    string sheight = config.GetValue("orig_height");
                    if (swidth != null && sheight != null)
                    {
                        int width, height;
                        int.TryParse(swidth, out width);
                        int.TryParse(sheight, out height);
                        TexInfo t = new TexInfo(name, width, height);
                        return t;
                    }
             }
            return null;
        }

        public static GameDatabase.TextureInfo FetchCacheTexture(TexInfo Texture, bool compress, bool mipmaps, bool makeNotReadable)
        {
            String textureName = Texture.name;
            String originalTextureFile = KSPUtil.ApplicationRootPath + "GameData/" + textureName;
            String cacheFile = KSPUtil.ApplicationRootPath + "GameData/BoulderCo/textureCompressorCache/" + textureName;
            String cacheConfigFile = cacheFile + ".tcache";
            cacheFile += ".pngcache";
            if (File.Exists(cacheConfigFile))
            {
                ConfigNode config = ConfigNode.Load(cacheConfigFile);
                string format = config.GetValue("orig_format");
                String cacheHash = config.GetValue("md5");
                originalTextureFile += format;
                String hashString = GetMD5String(originalTextureFile);

                if (format != null && File.Exists(originalTextureFile + format) && File.Exists(cacheFile))
                {
                    
                    String cacheIsNormString = config.GetValue("is_normal");
                    String cacheWidthString = config.GetValue("width");
                    String cacheHeihtString = config.GetValue("height");
                    bool cacheIsNorm = false;
                    int cacheWidth = 0;
                    int cacheHeight = 0;
                    bool.TryParse(cacheIsNormString, out cacheIsNorm);
                    int.TryParse(cacheWidthString, out cacheWidth);
                    int.TryParse(cacheHeihtString, out cacheHeight);

                    if (cacheHash != hashString || cacheIsNorm != Texture.isNormalMap || Texture.resizeWidth != cacheWidth || Texture.resizeHeight != cacheHeight)
                    {
                        if (cacheHash != hashString)
                        {
                            TextureCompressor.DBGLog(cacheHash + " != " + hashString);
                        }
                        if (cacheIsNorm != Texture.isNormalMap)
                        {
                            TextureCompressor.DBGLog(cacheIsNorm + " != " + Texture.isNormalMap);
                        }
                        if (Texture.resizeWidth != cacheWidth)
                        {
                            TextureCompressor.DBGLog(Texture.resizeWidth + " != " + cacheWidth);
                        }
                        if (Texture.resizeHeight != cacheHeight)
                        {
                            TextureCompressor.DBGLog(Texture.resizeHeight + " != " + cacheHeight);
                        }
                        return RebuildCache(Texture, compress, mipmaps, makeNotReadable);
                    }
                    else if (cacheHash == hashString && !Texture.needsResize)
                    {
                        return LoadDefaultTexture(Texture, mipmaps, makeNotReadable, compress);
                    }
                    else
                    {
                        TextureCompressor.DBGLog("Loading from cache...");
                        Texture2D newTex = new Texture2D(4, 4);
                        GameDatabase.TextureInfo cacheTexture = new GameDatabase.TextureInfo(newTex, Texture.isNormalMap, !makeNotReadable, compress);
                        TextureConverter.IMGToTexture(cacheFile, cacheTexture, mipmaps, cacheIsNorm, Texture.resizeWidth, Texture.resizeHeight);
                        cacheTexture.name = textureName;
                        newTex.name = textureName;
                        if (compress)
                        {
                            newTex.Compress(true);
                        }
                        newTex.Apply(mipmaps, makeNotReadable);
                        return cacheTexture;
                    }
                }
                else if (!Texture.needsResize)
                {
                    return LoadDefaultTexture(Texture, mipmaps, makeNotReadable, compress);
                }
                else
                {
                    TextureCompressor.DBGLog("Texture " + originalTextureFile+ " does not exist!");
                    return RebuildCache(Texture, compress, mipmaps, makeNotReadable);
                }
            }
            else if (!Texture.needsResize)
            {
                return LoadDefaultTexture(Texture, mipmaps, makeNotReadable, compress);
            }
            else
            {
                return RebuildCache(Texture, compress, mipmaps, makeNotReadable);
            }
        }

        private static GameDatabase.TextureInfo LoadDefaultTexture(TexInfo Texture, bool mipmaps, bool makeNotReadable, bool compress)
        {
            String cacheConfigFile = KSPUtil.ApplicationRootPath + "GameData/BoulderCo/textureCompressorCache/" + Texture.name + ".tcache";
            GameDatabase.TextureInfo texInfo = TextureConverter.GetReadable(Texture, mipmaps);
            String originalTextureFile = texInfo.texture.name;
            texInfo.name = Texture.name;
            texInfo.texture.name = Texture.name;
            if (compress)
            {
                texInfo.texture.Compress(true);
            }
            texInfo.texture.Apply(mipmaps, makeNotReadable);
            texInfo.isReadable = !makeNotReadable;
            texInfo.isNormalMap = Texture.isNormalMap;
            texInfo.isCompressed = compress;

            String hashString = GetMD5String(originalTextureFile);

            ConfigNode config = new ConfigNode();
            config.AddValue("md5", hashString); TextureCompressor.DBGLog("md5: " + hashString);
            config.AddValue("orig_format", Path.GetExtension(originalTextureFile)); TextureCompressor.DBGLog("orig_format: " + Path.GetExtension(originalTextureFile));
            config.AddValue("orig_width", Texture.width.ToString()); TextureCompressor.DBGLog("orig_width: " + Texture.width.ToString());
            config.AddValue("orig_height", Texture.height.ToString()); TextureCompressor.DBGLog("orig_height: " + Texture.height.ToString());
            config.AddValue("is_normal", texInfo.isNormalMap.ToString()); TextureCompressor.DBGLog("is_normal: " + texInfo.isNormalMap.ToString());
            config.AddValue("width", Texture.width.ToString()); TextureCompressor.DBGLog("width: " + Texture.width.ToString());
            config.AddValue("height", Texture.height.ToString()); TextureCompressor.DBGLog("height: " + Texture.height.ToString());

            config.Save(cacheConfigFile);
            TextureCompressor.DBGLog("Saved Config.");

            return texInfo;
        }

        private static GameDatabase.TextureInfo RebuildCache(TexInfo Texture, bool compress, bool mipmaps, bool makeNotReadable)
        {
            TextureCompressor.DBGLog("Rebuilding Cache... " + Texture.name);
            GameDatabase.TextureInfo cacheTexture;
            
            TextureCompressor.DBGLog("Loading texture...");
            cacheTexture = TextureConverter.GetReadable(Texture, mipmaps);
            TextureCompressor.DBGLog("Texture loaded.");

            Texture2D tex = cacheTexture.texture;

            String textureName = cacheTexture.name;
            String cacheFile = KSPUtil.ApplicationRootPath + "GameData/BoulderCo/textureCompressorCache/" + textureName;
            TextureConverter.WriteTo(cacheTexture.texture, cacheFile+".pngcache");

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
            config.AddValue("orig_width", Texture.width.ToString()); TextureCompressor.DBGLog("orig_width: " + Texture.width.ToString());
            config.AddValue("orig_height", Texture.height.ToString()); TextureCompressor.DBGLog("orig_height: " + Texture.height.ToString());
            config.AddValue("is_normal", cacheTexture.isNormalMap.ToString()); TextureCompressor.DBGLog("is_normal: " + cacheTexture.isNormalMap.ToString());
            config.AddValue("width", Texture.resizeWidth.ToString()); TextureCompressor.DBGLog("width: " + Texture.resizeWidth.ToString());
            config.AddValue("height", Texture.resizeHeight.ToString()); TextureCompressor.DBGLog("height: " + Texture.resizeHeight.ToString());
            
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
            if (File.Exists(file))
            {
                FileStream stream = File.OpenRead(file);
                MD5 md5 = MD5.Create();
                byte[] hash = md5.ComputeHash(stream);
                stream.Close();
                MD5String = BitConverter.ToString(hash);
                LastFile = file;
                return MD5String;
            }
            else
            {
                return null;
            }
        }

        public static int MemorySaved(int originalWidth, int originalHeight, TextureFormat originalFormat, bool originalMipmaps, GameDatabase.TextureInfo Texture)
        {
            TextureCompressor.DBGLog("Texture replaced!");
            int width = Texture.texture.width;
            int height = Texture.texture.height;
            TextureFormat format = Texture.texture.format;
            bool mipmaps = Texture.texture.mipmapCount == 1 ? false : true;
            TextureCompressor.DBGLog("Texture: " + Texture.name);
            TextureCompressor.DBGLog("is normalmap: " + Texture.isNormalMap);
            Texture2D tex = Texture.texture;
            TextureCompressor.DBGLog("originalWidth: " + originalWidth);
            TextureCompressor.DBGLog("originalHeight: " + originalHeight);
            TextureCompressor.DBGLog("originalFormat: " + originalFormat);
            TextureCompressor.DBGLog("originalMipmaps: " + originalMipmaps);
            TextureCompressor.DBGLog("width: " + width);
            TextureCompressor.DBGLog("height: " + height);
            TextureCompressor.DBGLog("format: " + format);
            TextureCompressor.DBGLog("mipmaps: " + mipmaps);
            bool readable = true;
            try { tex.GetPixel(0, 0); }
            catch { readable = false; };
            TextureCompressor.DBGLog("readable: " + readable);
            if (readable != Texture.isReadable)
            { TextureCompressor.DBGLog("Readbility does not match!"); }
            int oldSize = 0;
            int newSize = 0;
            switch (originalFormat)
            {
                case TextureFormat.ARGB32:
                case TextureFormat.RGBA32:
                case TextureFormat.BGRA32:
                    oldSize = 4 * (originalWidth * originalHeight);
                    break;
                case TextureFormat.RGB24:
                    oldSize = 3 * (originalWidth * originalHeight);
                    break;
                case TextureFormat.Alpha8:
                    oldSize = originalWidth * originalHeight;
                    break;
                case TextureFormat.DXT1:
                    oldSize = (originalWidth * originalHeight) / 2;
                    break;
                case TextureFormat.DXT5:
                    oldSize = originalWidth * originalHeight;
                    break;
            }
            switch (format)
            {
                case TextureFormat.ARGB32:
                case TextureFormat.RGBA32:
                case TextureFormat.BGRA32:
                    newSize = 4 * (width * height);
                    break;
                case TextureFormat.RGB24:
                    newSize = 3 * (width * height);
                    break;
                case TextureFormat.Alpha8:
                    newSize = width * height;
                    break;
                case TextureFormat.DXT1:
                    newSize = (width * height) / 2;
                    break;
                case TextureFormat.DXT5:
                    newSize = width * height;
                    break;
            }
            if (originalMipmaps)
            {
                oldSize += (int)(oldSize * .33f);
            }
            if (mipmaps)
            {
                newSize += (int)(newSize * .33f);
            }
            return (oldSize - newSize);
        }
        
    }
}
