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
                int origWidth, origHeight;
                string origWidthString = config.GetValue("orig_width");
                string origHeightString = config.GetValue("orig_height");
                int.TryParse(origWidthString, out origWidth);
                int.TryParse(origHeightString, out origHeight);

                if (origWidthString == null || origHeightString == null ||
                    cacheHash == null || format == null)
                {
                    return RebuildCache(Texture, compress, mipmaps, makeNotReadable);
                }

                originalTextureFile += format;
                String hashString = GetMD5String(originalTextureFile);

                Texture.Resize(origWidth, origHeight);

                if (format != null && File.Exists(originalTextureFile) && File.Exists(cacheFile))
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
                        return RebuildCache(Texture, compress, mipmaps, makeNotReadable);
                    }
                    else
                    {
                        TextureCompressor.DBGLog("Loading from cache... " + textureName);
                        Texture.needsResize = false;
                        Texture2D newTex = new Texture2D(4, 4);
                        GameDatabase.TextureInfo cacheTexture = new GameDatabase.TextureInfo(newTex, Texture.isNormalMap, !makeNotReadable, compress);
                        Texture.texture = cacheTexture;
                        Texture.filename = cacheFile;
                        TextureConverter.IMGToTexture(Texture, mipmaps, cacheIsNorm);
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
                else
                {
                    return RebuildCache(Texture, compress, mipmaps, makeNotReadable);
                }
            }
            else
            {
                return RebuildCache(Texture, compress, mipmaps, makeNotReadable);
            }

        }

        private static GameDatabase.TextureInfo RebuildCache(TexInfo Texture, bool compress, bool mipmaps, bool makeNotReadable)
        {
            Texture.loadOriginalFirst = true;
            TextureCompressor.DBGLog("Loading texture...");
            TextureConverter.GetReadable(Texture, mipmaps);
            TextureCompressor.DBGLog("Texture loaded.");

            GameDatabase.TextureInfo cacheTexture = Texture.texture;
            Texture2D tex = cacheTexture.texture;

            String textureName = cacheTexture.name;
            String cacheFile = KSPUtil.ApplicationRootPath + "GameData/BoulderCo/textureCompressorCache/" + textureName;
            if (Texture.needsResize)
            {
                TextureCompressor.DBGLog("Rebuilding Cache... " + Texture.name);

                TextureCompressor.DBGLog("Saving cache file " + cacheFile + ".pngcache");
                TextureConverter.WriteTo(cacheTexture.texture, cacheFile + ".pngcache");

                String originalTextureFile = Texture.filename;
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
            }
            else
            {
                String directory = Path.GetDirectoryName(cacheFile + ".none");
                if (File.Exists(directory))
                {
                    File.Delete(directory);
                }
                Directory.CreateDirectory(directory);
            }

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
