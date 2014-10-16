using System.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ActiveTextureManagement
{

    

    [DatabaseLoaderAttrib(new string[] { "png", "tga", "mbm", "jpg", "jpeg", "truecolor" })]
    public class DatabaseLoaderTexture_ATM : DatabaseLoader<GameDatabase.TextureInfo>
    {

        static ConfigNode config;
        static ConfigNode overrides;
        static List<String> overridesList = new List<string>();
        static List<String> foldersList = new List<string>();
        static List<String> readableList = new List<string>();
        static List<String> normalList = new List<string>();
        
        static bool config_mipmaps = false;
        static bool config_compress = true;
        static int config_scale = 1;
        static int config_max_size = 0;
        static int config_min_size = 128;
        static bool config_mipmaps_normals = false;
        static bool config_compress_normals = true;
        static int config_scale_normals = 1;
        static int config_max_size_normals = 0;
        static int config_min_size_normals = 128;
        static FilterMode config_filter_mode = FilterMode.Bilinear;
        static bool config_make_not_readable = false;


        public static void PopulateConfig()
        {
            if (config == null)
            {
                config = GameDatabase.Instance.GetConfigNodes("ACTIVE_TEXTURE_MANAGER")[0];
                String dbg = config.GetValue("DBG");
                if (dbg != null)
                {
                    ActiveTextureManagement.DBL_LOG = true;
                }

                overrides = config.GetNode("OVERRIDES");
                ConfigNode folders = config.GetNode("FOLDERS");
                ConfigNode normals = config.GetNode("NORMAL_LIST");

                if (overrides == null)
                {
                    overrides = new ConfigNode("OVERRIDES");
                }
                if (folders == null)
                {
                    folders = new ConfigNode("FOLDERS");
                }
                if (normals == null)
                {
                    normals = new ConfigNode("NORMAL_LIST");
                }

                foreach (ConfigNode configFolder in GameDatabase.Instance.GetConfigNodes("ACTIVE_TEXTURE_MANAGER_CONFIG"))
                {
                    String enabledString = configFolder.GetValue("enabled");
                    String folder = configFolder.GetValue("folder");
                    bool isEnabled = false;
                    if (enabledString != null)
                    {
                        bool.TryParse(enabledString, out isEnabled);
                    }
                    DBGLog("folder: " + folder);
                    DBGLog("enabled: " + isEnabled);
                    if (isEnabled)
                    {
                        folders.AddValue("folder", folder);
                        ConfigNode modOverrides = configFolder.GetNode("OVERRIDES");
                        ConfigNode modNormals = configFolder.GetNode("NORMAL_LIST");
                        CopyConfigNode(modOverrides, overrides);
                        CopyConfigNode(modNormals, normals);
                    }
                }

                foreach (ConfigNode node in overrides.nodes)
                {
                    overridesList.Add(node.name);
                }
                foreach (ConfigNode.Value folder in folders.values)
                {
                    foldersList.Add(folder.value);
                }
                foreach (ConfigNode.Value texture in normals.values)
                {
                    normalList.Add(texture.value);
                }

                String mipmapsString = config.GetValue("mipmaps");
                String compressString = config.GetValue("compress");
                String scaleString = config.GetValue("scale");
                String max_sizeString = config.GetValue("max_size");
                String min_sizeString = config.GetValue("min_size");
                String filter_modeString = config.GetValue("filter_mode");
                String make_not_readableString = config.GetValue("make_not_readable");

                bool.TryParse(mipmapsString, out config_mipmaps);
                bool.TryParse(compressString, out config_compress);
                int.TryParse(scaleString, out config_scale);
                int.TryParse(max_sizeString, out config_max_size);
                int.TryParse(min_sizeString, out config_min_size);
                config_filter_mode = (FilterMode)Enum.Parse(typeof(FilterMode), filter_modeString);
                bool.TryParse(make_not_readableString, out config_make_not_readable);

                String mipmapsString_normals = config.GetValue("mipmaps_normals");
                String compressString_normals = config.GetValue("compress_normals");
                String scaleString_normals = config.GetValue("scale_normals");
                String max_sizeString_normals = config.GetValue("max_size_normals");
                String min_sizeString_normals = config.GetValue("min_size_normals");

                bool.TryParse(mipmapsString_normals, out config_mipmaps_normals);
                bool.TryParse(compressString_normals, out config_compress_normals);
                int.TryParse(scaleString_normals, out config_scale_normals);
                int.TryParse(max_sizeString_normals, out config_max_size_normals);
                int.TryParse(min_sizeString_normals, out config_min_size_normals);

                Log("Settings:");
                Log("   mipmaps: " + config_mipmaps);
                Log("   compress: " + config_compress);
                Log("   scale: " + config_scale);
                Log("   max_size: " + config_max_size);
                Log("   min_size: " + config_min_size);
                Log("   mipmaps_normals: " + config_mipmaps_normals);
                Log("   compress_normals: " + config_compress_normals);
                Log("   scale_normals: " + config_scale_normals);
                Log("   max_size_normals: " + config_max_size_normals);
                Log("   filter_mode: " + config_filter_mode);
                Log("   make_not_readable: " + config_make_not_readable);
                Log("   normal List: ");
                foreach (String normal in normalList)
                {
                    DBGLog("      " + normal);
                }
            }
        }

        private static void CopyConfigNode(ConfigNode original, ConfigNode copy)
        {
            if (original != null)
            {
                foreach (ConfigNode node in original.nodes)
                {
                    copy.AddNode(node);
                }
                foreach (ConfigNode.Value value in original.values)
                {
                    copy.AddValue(value.name, value.value);
                }
            }
        }

        static public GameDatabase.TextureInfo UpdateTexture(TexInfo texture)
        {
            
            string overrideName = overridesList.Find(n => texture.name.Length == Regex.Match(texture.name, n).Length);
            bool mipmaps = true;
            bool compress = texture.isNormalMap ? false : true;
            int scale = 1;
            int maxSize = 0;
            int minSize = 64;
            FilterMode filterMode = FilterMode.Bilinear;
            bool makeNotReadable = false;

            if (foldersList.Exists(n => texture.name.StartsWith(n)))
            {

                if (texture.isNormalMap)
                {
                    mipmaps = DatabaseLoaderTexture_ATM.config_mipmaps_normals;
                    compress = DatabaseLoaderTexture_ATM.config_compress_normals;
                    scale = DatabaseLoaderTexture_ATM.config_scale_normals;
                    maxSize = DatabaseLoaderTexture_ATM.config_max_size_normals;
                    minSize = DatabaseLoaderTexture_ATM.config_min_size_normals;
                }
                else
                {
                    mipmaps = DatabaseLoaderTexture_ATM.config_mipmaps;
                    compress = DatabaseLoaderTexture_ATM.config_compress;
                    scale = DatabaseLoaderTexture_ATM.config_scale;
                    maxSize = DatabaseLoaderTexture_ATM.config_max_size;
                    minSize = DatabaseLoaderTexture_ATM.config_min_size;
                }
                filterMode = config_filter_mode;
                makeNotReadable = config_make_not_readable;

                if (overrideName != null)
                {
                    ConfigNode overrideNode = overrides.GetNode(overrideName);
                    String normalString = texture.isNormalMap ? "_normals" : "";
                    String mipmapsString = overrideNode.GetValue("mipmaps" + normalString);
                    String compressString = overrideNode.GetValue("compress" + normalString);
                    String scaleString = overrideNode.GetValue("scale" + normalString);
                    String max_sizeString = overrideNode.GetValue("max_size" + normalString);
                    String min_sizeString = overrideNode.GetValue("min_size" + normalString);
                    String filter_modeString = overrideNode.GetValue("filter_mode");
                    String make_not_readableString = overrideNode.GetValue("make_not_readable");

                    if (mipmapsString != null)
                    {
                        bool.TryParse(mipmapsString, out mipmaps);
                    }
                    if (compressString != null)
                    {
                        bool.TryParse(compressString, out compress);
                    }
                    if (scaleString != null)
                    {
                        int.TryParse(scaleString, out scale);
                    }
                    if (filter_modeString != null)
                    {
                        try
                        {
                            filterMode = (FilterMode)Enum.Parse(typeof(FilterMode), filter_modeString);
                        }
                        catch
                        {
                            filterMode = config_filter_mode;
                        }
                    }
                    if (make_not_readableString != null)
                    {
                        bool.TryParse(make_not_readableString, out makeNotReadable);
                    }
                    if (max_sizeString != null)
                    {
                        int.TryParse(max_sizeString, out maxSize);
                    }
                    if (min_sizeString != null)
                    {
                        int.TryParse(min_sizeString, out minSize);
                    }
                }
            }
            texture.SetScalingParams(scale, maxSize, minSize);

            GameDatabase.TextureInfo ret = CacheController.FetchCacheTexture(texture, compress, mipmaps, makeNotReadable && !readableList.Contains(texture.name));
            ret.texture.filterMode = filterMode;
            return ret;
        }


        public static bool IsNormal(String name)
        {
            bool isNormal = name.EndsWith("_NRM") || normalList.Contains(name);
            String originalTextureFile = KSPUtil.ApplicationRootPath + "GameData/" + name + ".mbm";
            if (!isNormal && File.Exists(originalTextureFile))
            {
                FileStream stream = File.OpenRead(originalTextureFile);
                //while stream is open, if it is an MBM, flag normal maps.
                stream.Position = 12;
                if (stream.ReadByte() == 1)
                {
                    isNormal = true;
                }
                stream.Close();
            }
            return isNormal;
        }

        private static void SetNormalMap(GameDatabase.TextureInfo Texture)
        {
            Texture.isNormalMap = IsNormal(Texture.name);
        }

        private void tryCompress(GameDatabase.TextureInfo Texture)
        {
            Texture2D tex = Texture.texture;
            if (tex.format != TextureFormat.DXT1 && tex.format != TextureFormat.DXT5)
            {
                try
                {
                    tex.GetPixel(0, 0);
                    tex.Compress(true);
                    Texture.isCompressed = true;
                    Texture.isReadable = true;
                }
                catch
                {
                    Texture.isReadable = false;
                }
            }
        }


        public DatabaseLoaderTexture_ATM() : base()
        {

        }

        public override IEnumerator Load(UrlDir.UrlFile urlFile, FileInfo file)
        {
            TexInfo t = new TexInfo(urlFile.url);
            GameDatabase.TextureInfo texture = UpdateTexture(t);
            obj = texture;
            successful = true;
            yield return null;
        }

        public static void DBGLog(String message)
        {
            if (ActiveTextureManagement.DBL_LOG)
            {
                UnityEngine.Debug.Log("DatabaseLoaderTexture_ATM: " + message);
            }
        }
        public static void Log(String message)
        {
            UnityEngine.Debug.Log("DatabaseLoaderTexture_ATM: " + message);
        }

    }
}
