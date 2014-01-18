using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace TextureCompressor
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    public class TextureCompressor : MonoBehaviour
    {
        static bool Compressed = false;
        static int LastTextureIndex = -1;
        static int gcCount = 0;
        static long memorySaved = 0;
        static bool DBL_LOG = false;
        
        const int GC_COUNT_TRIGGER = 20;
        
        static ConfigNode config;
        static ConfigNode overrides;
        static List<String> overridesList = new List<string>();
        static List<String> foldersList = new List<string>();
        static List<String> readableList = new List<string>();
        static List<String> normalList = new List<string>();

        static bool config_mipmaps = false;
        static bool config_compress = true;
        static int config_scale = 1;
        static int config_max_size = 1;
        static bool config_mipmaps_normals = false;
        static bool config_compress_normals = true;
        static int config_scale_normals = 1;
        static int config_max_size_normals = 1;
        static FilterMode config_filter_mode = FilterMode.Bilinear;
        static bool config_make_not_readable = false;

        protected void Awake()
        {
            PopulateConfig();
            if (HighLogic.LoadedScene == GameScenes.MAINMENU && !Compressed)
            {
                Update();
                Compressed = true;
                
                foreach(GameDatabase.TextureInfo Texture in GameDatabase.Instance.databaseTexture)
                {
                    Texture2D texture = Texture.texture;
                    Log("--------------------------------------------------------");
                    Log("Name: " + texture.name);
                    Log("Format: " + texture.format.ToString());
                    Log("MipMaps: " + texture.mipmapCount.ToString());
                    Log("Size: " + texture.width.ToString() + "x" + texture.height);
                    Log("Readable: " + Texture.isReadable);
                }
                int kbSaved = (int)(memorySaved / 1024f);
                int mbSaved = (int)((memorySaved / 1024f) / 1024f);
                Log("Memory Saved : " + memorySaved.ToString() + "B");
                Log("Memory Saved : " + kbSaved.ToString() + "kB");
                Log("Memory Saved : " + mbSaved.ToString() + "MB");

                TextureConverter.DestroyImageBuffer();
            }
        }

        protected void Update()
        {
            PopulateConfig();

            if (!Compressed && GameDatabase.Instance.databaseTexture.Count > 0)
            {
                int LocalLastTextureIndex = GameDatabase.Instance.databaseTexture.Count-1;
                if (LastTextureIndex != LocalLastTextureIndex)
                {
                    for (int i = LastTextureIndex + 1; i < GameDatabase.Instance.databaseTexture.Count; i++)
                    {
                        GameDatabase.TextureInfo Texture = GameDatabase.Instance.databaseTexture[i];
                        LastTextureIndex = i;
                        GameDatabase.TextureInfo replacementTex;
                        int originalWidth = Texture.texture.width;
                        int originalHeight = Texture.texture.height;
                        TextureFormat originalFormat = Texture.texture.format;
                        bool originalMipmaps = Texture.texture.mipmapCount == 1 ? false : true;
                        if (Texture.name.Length > 0 && foldersList.Exists(n => Texture.name.StartsWith(n)))
                        {
                            SetNormalMap(Texture);

                            string overrideName = overridesList.Find(n => Texture.name.Length == Regex.Match(Texture.name, n).Length);
                            if (overrideName != null)
                            {
                                ConfigNode overrideNode = overrides.GetNode(overrideName);
                                replacementTex = ApplyNodeSettings(Texture, overrideNode);
                            }
                            else
                            {
                                replacementTex = ApplySettings(Texture);
                            }
                            GameDatabase.Instance.databaseTexture[LastTextureIndex] = replacementTex;
                            Texture2D.DestroyImmediate(Texture.texture);
                            Texture = replacementTex;
                        }
                        else if(config_compress)
                        {
                            tryCompress(Texture);
                        }
                        updateMemoryCount(originalWidth, originalHeight, originalFormat, originalMipmaps, Texture);
                        gcCount++;
                    }
                    if (gcCount > GC_COUNT_TRIGGER)
                    {
                        System.GC.Collect();
                        gcCount = 0;
                    }
                }
            }
        }

        private static void SetNormalMap(GameDatabase.TextureInfo Texture)
        {
            Texture.isNormalMap = Texture.name.EndsWith("_NRM") || normalList.Contains(Texture.name);
            String originalTextureFile = KSPUtil.ApplicationRootPath + "GameData/" + Texture.name + ".mbm";
            if (!Texture.isNormalMap && File.Exists(originalTextureFile))
            {
                FileStream stream = File.OpenRead(originalTextureFile);

                //while stream is open, if it is an MBM, flag normal maps.
                stream.Position = 12;
                if (stream.ReadByte() == 1)
                {
                    Texture.isNormalMap = true;
                }
                
                stream.Close();
            }
        }

        private void tryCompress(GameDatabase.TextureInfo Texture)
        {
            Texture2D tex = Texture.texture;
            if (tex.format != TextureFormat.DXT1 && tex.format != TextureFormat.DXT5)
            {
                try { 
                    tex.GetPixel(0, 0);
                    tex.Compress(true);
                    Texture.isCompressed = true;
                    Texture.isReadable = true;
                }
                catch {
                    Texture.isReadable = false;
                }
            }
        }

        private void PopulateConfig()
        {
            if (config == null)
            {
                String configString = KSPUtil.ApplicationRootPath + "GameData/BoulderCo/textureCompressorConfigs/textureCompressor.tcfg";
                ConfigNode settings = ConfigNode.Load(configString);
                String dbg = settings.GetValue("DBG");
                if(dbg != null)
                {
                    DBL_LOG = true;
                }
                config = settings.GetNode("COMPRESSOR");
                
                List<String> configfiles = new List<string>();
                
                if (System.IO.Directory.Exists(KSPUtil.ApplicationRootPath + "GameData/BoulderCo/textureCompressorConfigs"))
                {
                    configfiles.AddRange(System.IO.Directory.GetFiles(KSPUtil.ApplicationRootPath + "GameData/BoulderCo/textureCompressorConfigs", "*.tcfg", System.IO.SearchOption.AllDirectories));
                }

                overrides = settings.GetNode("OVERRIDES");
                ConfigNode folders = settings.GetNode("FOLDERS");
                ConfigNode normals = settings.GetNode("NORMAL_LIST");

                if(overrides == null)
                {
                    overrides = new ConfigNode("OVERRIDES");
                }
                if(folders == null)
                {
                    folders = new ConfigNode("FOLDERS");
                }
                if (normals == null)
                {
                    normals = new ConfigNode("NORMAL_LIST");
                }
                String pathStart = (KSPUtil.ApplicationRootPath + "GameData/BoulderCo/textureCompressorConfigs/").Replace('\\', '/');
                foreach(String configFile in configfiles)
                {
                    
                    String unixConfigFile = configFile.Replace('\\', '/');
                    String folder = unixConfigFile.Replace(pathStart, "").Replace(".tcfg","");
                    ConfigNode configFolder = ConfigNode.Load(unixConfigFile);
                    String enabledString = configFolder.GetValue("config_enabled");
                    bool isEnabled = false;
                    if ( enabledString != null)
                    {
                        bool.TryParse(enabledString, out isEnabled);
                    }
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
                String filter_modeString = config.GetValue("filter_mode");
                String make_not_readableString = config.GetValue("make_not_readable");

                bool.TryParse(mipmapsString, out config_mipmaps);
                bool.TryParse(compressString, out config_compress);
                int.TryParse(scaleString, out config_scale);
                int.TryParse(max_sizeString, out config_max_size);
                config_filter_mode = (FilterMode)Enum.Parse(typeof(FilterMode), filter_modeString);
                bool.TryParse(make_not_readableString, out config_make_not_readable);

                String mipmapsString_normals = config.GetValue("mipmaps_normals");
                String compressString_normals = config.GetValue("compress_normals");
                String scaleString_normals = config.GetValue("scale_normals");
                String max_sizeString_normals = config.GetValue("max_size_normals");

                bool.TryParse(mipmapsString_normals, out config_mipmaps_normals);
                bool.TryParse(compressString_normals, out config_compress_normals);
                int.TryParse(scaleString_normals, out config_scale_normals);
                int.TryParse(max_sizeString_normals, out config_max_size_normals);

                DBGLog("Settings:");
                DBGLog("   mipmaps: " + config_mipmaps);
                DBGLog("   compress: " + config_compress);
                DBGLog("   scale: " + config_scale);
                DBGLog("   max_size: " + config_max_size);
                DBGLog("   mipmaps_normals: " + config_mipmaps_normals);
                DBGLog("   compress_normals: " + config_compress_normals);
                DBGLog("   scale_normals: " + config_scale_normals);
                DBGLog("   max_size_normals: " + config_max_size_normals);
                DBGLog("   filter_mode: " + config_filter_mode);
                DBGLog("   make_not_readable: " + config_make_not_readable);
                DBGLog("   normal List: ");
                foreach(String normal in normalList)
                {
                    DBGLog("      "+normal);
                }
            }
        }

        private void CopyConfigNode(ConfigNode original, ConfigNode copy)
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

        private GameDatabase.TextureInfo ApplyNodeSettings(GameDatabase.TextureInfo Texture, ConfigNode overrideNode)
        {
            String normalString = Texture.isNormalMap ? "_normals" : "";
            String mipmapsString = overrideNode.GetValue("mipmaps" + normalString);
            String compressString = overrideNode.GetValue("compress" + normalString);
            String scaleString = overrideNode.GetValue("scale" + normalString);
            String max_sizeString = overrideNode.GetValue("max_size" + normalString);
            String filter_modeString = overrideNode.GetValue("filter_mode");
            String make_not_readableString = overrideNode.GetValue("make_not_readable");

            bool local_mipmaps = Texture.isNormalMap ? config_mipmaps_normals : config_mipmaps;
            bool local_compress = Texture.isNormalMap ? config_compress_normals : config_compress;
            int local_scale = Texture.isNormalMap ? config_scale_normals : config_scale;
            int local_max_size = Texture.isNormalMap ? config_max_size_normals : config_max_size;
            FilterMode filter_mode = config_filter_mode;
            bool local_not_readable = config_make_not_readable;

            if (mipmapsString != null)
            {
                bool.TryParse(mipmapsString, out local_mipmaps);
            }
            if (compressString != null)
            {
                bool.TryParse(compressString, out local_compress);
            }
            if (scaleString != null)
            {
                int.TryParse(scaleString, out local_scale);
            }
            if (filter_modeString != null)
            {
                try
                {
                    filter_mode = (FilterMode)Enum.Parse(typeof(FilterMode), filter_modeString);
                }
                catch
                {
                    filter_mode = config_filter_mode;
                }
            }
            if (make_not_readableString != null)
            {
                bool.TryParse(make_not_readableString, out local_not_readable);
            }
            if (max_sizeString != null)
            {
                int.TryParse(max_sizeString, out local_max_size);
            }
            Texture2D tex = Texture.texture;
            TextureFormat format = tex.format;

            return UpdateTex(Texture, local_compress, local_mipmaps, local_scale, filter_mode, local_not_readable, local_max_size);

        }

        private GameDatabase.TextureInfo ApplySettings(GameDatabase.TextureInfo Texture)
        {
            bool mipmaps = TextureCompressor.config_mipmaps;
            bool compress = TextureCompressor.config_compress;
            int scale = TextureCompressor.config_scale;
            int max_size = TextureCompressor.config_max_size;
            if(Texture.isNormalMap)
            {
                mipmaps = TextureCompressor.config_mipmaps_normals;
                compress = TextureCompressor.config_compress_normals;
                scale = TextureCompressor.config_scale_normals;
                max_size = TextureCompressor.config_max_size_normals;
            }

            return UpdateTex( Texture, compress, mipmaps, scale, config_filter_mode, config_make_not_readable, max_size);

        }

        private GameDatabase.TextureInfo UpdateTex(GameDatabase.TextureInfo Texture, bool compress, bool mipmaps, int scale, FilterMode filterMode, bool makeNotReadable, int max_size)
        {
            Texture2D tex = Texture.texture;
                        
            int width = tex.width / scale;
            int height = tex.height / scale;
            
            int tmpScale = scale-1;
            while (width < 1 && tmpScale > 0)
            {
                width = tex.width / tmpScale--;
            }
            tmpScale = scale-1;
            while (height < 1 && tmpScale > 0)
            {
                height = tex.height / tmpScale--;
            }
            
            if (max_size != 0)
            {
                if (width > max_size)
                {
                    width = max_size;
                }
                if (height > max_size)
                {
                    height = max_size;
                }
            }

            GameDatabase.TextureInfo ret = CacheController.FetchCacheTexture(Texture, width, height, compress, mipmaps, makeNotReadable && !readableList.Contains(Texture.name));
            ret.texture.filterMode = filterMode;
            return ret;

        }

        private void updateMemoryCount(int originalWidth, int originalHeight, TextureFormat originalFormat, bool originalMipmaps, GameDatabase.TextureInfo Texture)
        {
            DBGLog("Texture replaced!");
            int width = Texture.texture.width;
            int height = Texture.texture.height;
            TextureFormat format = Texture.texture.format;
            bool mipmaps = Texture.texture.mipmapCount == 1 ? false : true;
            DBGLog("Texture: " + Texture.name);
            DBGLog("is normalmap: " + Texture.isNormalMap);
            Texture2D tex = Texture.texture;
            DBGLog("originalWidth: " + originalWidth);
            DBGLog("originalHeight: " + originalHeight);
            DBGLog("originalFormat: " + originalFormat);
            DBGLog("originalMipmaps: " + originalMipmaps);
            DBGLog("width: " + width);
            DBGLog("height: " + height);
            DBGLog("format: " + format);
            DBGLog("mipmaps: " + mipmaps);
            bool readable = true;
            try{tex.GetPixel(0,0);}catch{readable = false;};
            DBGLog("readable: " + readable);
            if(readable != Texture.isReadable)
            { DBGLog("Readbility does not match!"); }
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
            int saved = (oldSize - newSize);

            memorySaved += saved;
            
            DBGLog("Saved " + saved + "B");
            DBGLog("Accumulated Saved " + memorySaved + "B");
        }

        public static void DBGLog(String message)
        {
            if (DBL_LOG)
            {
                UnityEngine.Debug.Log("TextureCompressor: " + message);
            }
        }
        public static void Log(String message)
        {
            UnityEngine.Debug.Log("TextureCompressor: " + message);
        }

    }
}
