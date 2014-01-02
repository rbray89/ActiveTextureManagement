using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TextureCompressor
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    public class TextureCompressor : MonoBehaviour
    {
        static bool Compressed = false;
        static bool Converted = false;
        static int LastTextureIndex = -1;
        static int memorySaved = 0;
        const int MAX_IMAGE_SIZE = 4048*4048*4;
        static byte[] imageBuffer = new byte[MAX_IMAGE_SIZE];
        static ConfigNode config;
        static ConfigNode overrides;
        static ConfigNode overridesFolders;
        static List<String> overridesFolderList = new List<string>();
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
                    if (texture.name.Length > 0 && foldersList.Exists(n => texture.name.StartsWith(n)))
                    {
                        if(!Texture.isReadable)
                        {
                            continue;
                        }
                        bool mipmaps = false;
                        bool makeNotReadable = false;
                        ConfigNode overrideNode = overrides.GetNode(Texture.name);
                        string folder = overridesFolderList.Find(n => Texture.name.StartsWith(n));
                        if (overrideNode != null)
                        {
                            String mipmapsString = overrideNode.GetValue("mipmaps");
                            String make_not_readableString = overrideNode.GetValue("make_not_readable");
                            bool.TryParse(mipmapsString, out mipmaps);
                            bool.TryParse(make_not_readableString, out makeNotReadable);
                        }
                        else if (folder != null)
                        {
                            String mipmapsString = overridesFolders.GetValue("mipmaps");
                            String make_not_readableString = overridesFolders.GetValue("make_not_readable");
                            bool.TryParse(mipmapsString, out mipmaps);
                            bool.TryParse(make_not_readableString, out makeNotReadable);
                        }
                        else
                        {
                            mipmaps = TextureCompressor.config_mipmaps;
                            makeNotReadable = TextureCompressor.config_make_not_readable;
                            if (Texture.isNormalMap)
                            {
                                mipmaps = TextureCompressor.config_mipmaps_normals;
                            }
                        }
                        if (!readableList.Contains(texture.name))
                        {
                            texture.Apply(mipmaps, makeNotReadable);
                        }
                    }
                }
                int kbSaved = (int)(memorySaved / 1024f);
                int mbSaved = (int)((memorySaved / 1024f) / 1024f);
                Log("Memory Saved : " + memorySaved.ToString() + "B");
                Log("Memory Saved : " + kbSaved.ToString() + "kB");
                Log("Memory Saved : " + mbSaved.ToString() + "MB");
                imageBuffer = null;
            }
            if(!Converted)
            {
                List<String> allfiles = new List<string>();
                foreach (String folder in foldersList)
                {
                    if (System.IO.Directory.Exists(KSPUtil.ApplicationRootPath + "GameData/" + folder))
                    {
                        allfiles.AddRange(System.IO.Directory.GetFiles(KSPUtil.ApplicationRootPath + "GameData/" + folder, "*.mbm", System.IO.SearchOption.AllDirectories));
                    }
                }
                foreach (String file in allfiles)
                {
                    FileStream stream = new FileStream(file, FileMode.Open, FileAccess.ReadWrite);
                    stream.Position = 12;
                    if(stream.ReadByte() == 1)
                    {
                        stream.Close();
                        //String unixPath = file.Replace('\\', '/');
                        //MBMToTGA(file);
                        //File.Move(file, file+".origN");
                    }
                    else
                    {
                        stream.Close();
                        //MBMToPNG(file);
                        //File.Move(file, file + ".orig");
                    }
                }
                List<String> mbms = new List<string>();
                mbms.AddRange(System.IO.Directory.GetFiles(KSPUtil.ApplicationRootPath + "GameData/", "*.mbm.origN", System.IO.SearchOption.AllDirectories));
                mbms.AddRange(System.IO.Directory.GetFiles(KSPUtil.ApplicationRootPath + "GameData/", "*.mbm.orig", System.IO.SearchOption.AllDirectories));
                String pathStart = (KSPUtil.ApplicationRootPath + "GameData/").Replace('\\', '/');
                foreach (String file in mbms)
                {
                    String path = file.Replace(pathStart, "");
                    String unixPath = path.Replace('\\', '/');

                    if (foldersList.Exists(n => unixPath.StartsWith(n)))
                    {
                        String replacement = pathStart + unixPath.Replace(".mbm.origN", ".tga");
                        replacement = replacement.Replace(".mbm.orig", ".png");
                        String mbm = pathStart + unixPath.Replace(".origN", "");
                        mbm = mbm.Replace(".orig", "");
                        path = pathStart + unixPath;
                        if (File.Exists(replacement))
                        {
                            File.Delete(replacement);
                        }
                        File.Move(path, mbm);
                    }
                }
                
                Converted = true;
            }
        }

        private void MBMToPNG(string file)
        {
            FileStream mbmStream = new FileStream(file, FileMode.Open, FileAccess.Read);
            mbmStream.Position = 4;

            uint width = 0, height = 0;
            for (int b = 0; b < 4; b++)
            {
                width >>= 8;
                width |= (uint)(mbmStream.ReadByte() << 24);
            }
            for (int b = 0; b < 4; b++)
            {
                height >>= 8;
                height |= (uint)(mbmStream.ReadByte() << 24);
            }
            mbmStream.Position = 16;
            int format = mbmStream.ReadByte();
            mbmStream.Position += 3;
            //Log(file+" width: " + width.ToString() + " height: " + height.ToString() + " format: " + format.ToString());

            int imageSize = (int)(width * height * 3);
            TextureFormat texformat = TextureFormat.RGB24;
            bool alpha = false;
            if (format == 32)
            {
                imageSize += (int)(width * height);
                texformat = TextureFormat.RGBA32;
                alpha = true;
            }

            String pngOut = file.Substring(0, file.Length - 3) + "png";
            mbmStream.Read(imageBuffer, 0, MAX_IMAGE_SIZE);
            mbmStream.Close();

            Texture2D texture = new Texture2D((int)width, (int)height, texformat, false);
            Color32[] colors = new Color32[width * height];
            int n = 0;
            for (int i = 0; i < width * height; i++ )
            {
                colors[i].r = imageBuffer[n++];
                colors[i].g = imageBuffer[n++];
                colors[i].b = imageBuffer[n++];
                if(alpha)
                {
                    colors[i].a = imageBuffer[n++];
                }
            }
            texture.SetPixels32(colors);
            texture.Apply(false);
            byte[] data = texture.EncodeToPNG();
            File.WriteAllBytes(pngOut, data);
        }

        private void MBMToTGA(string file)
        {
            FileStream mbmStream = new FileStream(file, FileMode.Open, FileAccess.Read);
            mbmStream.Position = 4;
            
            uint width  = 0, height = 0;
            for(int b=0; b < 4; b++)
            {
                width >>= 8;
                width |= (uint)(mbmStream.ReadByte() << 24);
            }
            for(int b=0; b < 4; b++)
            {
                height >>= 8;
                height |= (uint)(mbmStream.ReadByte() << 24);
            }
            mbmStream.Position = 16;
            int format = mbmStream.ReadByte();
            mbmStream.Position += 3;
            //Log(file+" width: " + width.ToString() + " height: " + height.ToString() + " format: " + format.ToString());
            bool alpha = false;
            int imageSize = (int)(width * height * 3);
            if(format == 32)
            {
                alpha = true;
                imageSize += (int)(width * height);
            }
            
            String tgaOut = file.Substring(0, file.Length - 3) + "tga";
            FileStream tgaStream = new FileStream(tgaOut, FileMode.OpenOrCreate, FileAccess.Write);
            tgaStream.SetLength(0);
            tgaStream.Write(new byte[] {
                0x00,
                0x00,
                0x02,
                0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,
                0x00,0x00
                }, 0, 12);
            tgaStream.WriteByte((byte)(width & 0xFF));
            tgaStream.WriteByte((byte)(width >> 8));
            tgaStream.WriteByte((byte)(height & 0xFF));
            tgaStream.WriteByte((byte)(height >> 8));
            tgaStream.WriteByte((byte)format);
            if (alpha)
            {
                tgaStream.WriteByte(0x08);
            }
            else
            {
                tgaStream.WriteByte(0x00);
            }
            
            mbmStream.Read(imageBuffer,0,MAX_IMAGE_SIZE);
            mbmStream.Close();
            int inc = alpha ? 4 : 3;
            for (int i = 0; i < imageSize; i += inc)
            {
                byte r = imageBuffer[i];
                imageBuffer[i] = imageBuffer[i+2];
                imageBuffer[i + 2] = r;
            }
            tgaStream.Write(imageBuffer, 0, imageSize);
            /*
            String headerTxt = "TRUEVISION-XFILE.";
            byte[] footer = new byte[9+headerTxt.Length];
            Encoding.ASCII.GetBytes("TRUEVISION-XFILE.").CopyTo(footer,8);
            tgaStream.Write(footer, 0, 8);
            */
            tgaStream.Flush();
            tgaStream.Close();
        }

        private void MBMToTexture(string name, GameDatabase.TextureInfo texture)
        {
            FileStream mbmStream = new FileStream(KSPUtil.ApplicationRootPath + "GameData/" + name + ".mbm", FileMode.Open, FileAccess.Read);
            mbmStream.Position = 4;

            uint width = 0, height = 0;
            for (int b = 0; b < 4; b++)
            {
                width >>= 8;
                width |= (uint)(mbmStream.ReadByte() << 24);
            }
            for (int b = 0; b < 4; b++)
            {
                height >>= 8;
                height |= (uint)(mbmStream.ReadByte() << 24);
            }
            mbmStream.Position = 12;
            if (mbmStream.ReadByte() == 1)
            {
                texture.isNormalMap = true;
            }
            else
            {
                texture.isNormalMap = false;
            }
            mbmStream.Position = 16;
            int format = mbmStream.ReadByte();
            mbmStream.Position += 3;

            int imageSize = (int)(width * height * 3);
            TextureFormat texformat = TextureFormat.RGB24;
            bool alpha = false;
            if (format == 32)
            {
                imageSize += (int)(width * height);
                texformat = TextureFormat.RGBA32;
                alpha = true;
            }

            mbmStream.Read(imageBuffer, 0, MAX_IMAGE_SIZE);
            mbmStream.Close();

            Texture2D tex = texture.texture;
            tex.Resize((int)width, (int)height, texformat, true);
            Color32[] colors = new Color32[width * height];
            int n = 0;
            for (int i = 0; i < width * height; i++)
            {
                colors[i].r = imageBuffer[n++];
                colors[i].g = imageBuffer[n++];
                colors[i].b = imageBuffer[n++];
                if (alpha)
                {
                    colors[i].a = imageBuffer[n++];
                }
            }
            tex.SetPixels32(colors);
            tex.Apply(true, false);
            if (!texture.isNormalMap)
            {
                texture.isCompressed = true;
                tex.Compress(true);
            }
            else
            {
                texture.isCompressed = false;
            }
        }

        private void PNGToTexture(string name, GameDatabase.TextureInfo texture)
        {
            FileStream mbmStream = new FileStream(KSPUtil.ApplicationRootPath + "GameData/" + name + ".png", FileMode.Open, FileAccess.Read);
            mbmStream.Position = 0;
            mbmStream.Read(imageBuffer, 0, MAX_IMAGE_SIZE);
            mbmStream.Close();

            Texture2D tex = texture.texture;
            tex.LoadImage(imageBuffer);
            tex.Apply(true, false);
            if (!texture.isNormalMap)
            {
                texture.isCompressed = true;
                tex.Compress(true);
            }
            else
            {
                texture.isCompressed = false;
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
                        String mbmPath = KSPUtil.ApplicationRootPath + "GameData/" + Texture.name + ".mbm";
                        String pngPath = KSPUtil.ApplicationRootPath + "GameData/" + Texture.name + ".png";

                        Texture.isNormalMap = Texture.name.EndsWith("_NRM") || normalList.Contains(Texture.name);
                        try { Texture.texture.GetPixel(0, 0); }
                        catch
                        {
                            //Log("Converting Unreadable... " + Texture.name + " " + Texture.isNormalMap);
                            if (File.Exists(mbmPath))
                            {
                                Texture2D tex = new Texture2D(2, 2);
                                String name;
                                if (Texture.texture.name.Length > 0)
                                {
                                    name = Texture.texture.name;
                                }
                                else
                                {
                                    name = Texture.name;
                                }
                                Texture2D.DestroyImmediate(Texture.texture);
                                Texture = GameDatabase.Instance.databaseTexture[i] = new GameDatabase.TextureInfo(tex, true, true, true);
                                MBMToTexture(name, Texture);
                                Texture.name = name;
                                tex.name = name;
                            }
                            else if (File.Exists(pngPath))
                            {
                                Texture2D tex = new Texture2D(2, 2);
                                String name;
                                if (Texture.texture.name.Length > 0)
                                {
                                    name = Texture.texture.name;
                                }
                                else
                                {
                                    name = Texture.name;
                                }
                                Texture2D.DestroyImmediate(Texture.texture);
                                Texture = GameDatabase.Instance.databaseTexture[i] = new GameDatabase.TextureInfo(tex, true, true, true);
                                PNGToTexture(name, Texture);
                                Texture.name = name;
                                tex.name = name;
                            }
                            if (Texture.name.EndsWith("_NRM") || normalList.Contains(Texture.name))
                            {
                                //override mistakes in mbm normal setting
                                Texture.isNormalMap = true;
                            }
                        }
                        if (Texture.name.Length > 0 && foldersList.Exists(n => Texture.name.StartsWith(n)))
                        {
                            
                            ConfigNode overrideNode = overrides.GetNode(Texture.name);
                            string folder = overridesFolderList.Find(n => Texture.name.StartsWith(n));
                            if (overrideNode != null)
                            {
                                ApplyNodeSettings(Texture, overrideNode);
                            }
                            else if (folder != null)
                            {
                                ConfigNode overrideFolder = overridesFolders.GetNode(folder);
                            }
                            else
                            {
                                ApplySettings(Texture);
                            }

                            if(Texture.isNormalMap)
                            {
                                if (Texture.texture.name.Length > 0)
                                {
                                    name = Texture.texture.name;
                                }
                                else
                                {
                                    name = Texture.name;
                                }
                                Texture2D orig = Texture.texture;
                                Texture = GameDatabase.Instance.databaseTexture[i] = new GameDatabase.TextureInfo(GameDatabase.BitmapToUnityNormalMap(orig), true, false, Texture.isCompressed);
                                Texture2D.DestroyImmediate(orig);
                                Texture.name = name;
                                Texture.texture.name = name;
                            }
                        }
                        else if(config_compress)
                        {
                            tryCompress(Texture);
                        }
                    }
                }
            }
        }

        private void tryCompress(GameDatabase.TextureInfo Texture)
        {
            if (Texture.texture.format != TextureFormat.DXT1 && Texture.texture.format != TextureFormat.DXT5)
            {
                try { Texture.texture.GetPixel(0, 0); Texture.texture.Compress(true); }
                catch { }
            }
        }

        private void PopulateConfig()
        {
            if (config == null)
            {
                String configString = KSPUtil.ApplicationRootPath + "GameData/BoulderCo/textureCompressor.cfg";
                ConfigNode settings = ConfigNode.Load(configString);
                config = settings.GetNode("COMPRESSOR");
                overrides = settings.GetNode("OVERRIDES");
                overridesFolders = settings.GetNode("OVERRIDES_FOLDERS");
                ConfigNode folders = settings.GetNode("FOLDERS");
                ConfigNode readable = settings.GetNode("LEAVE_READABLE");
                ConfigNode normals = settings.GetNode("NORMAL_LIST");

                foreach (ConfigNode node in overridesFolders.nodes)
                {
                    overridesFolderList.Add(node.name);
                }
                foreach (ConfigNode.Value folder in folders.values)
                {
                    foldersList.Add(folder.value);
                }
                foreach (ConfigNode.Value texture in readable.values)
                {
                    readableList.Add(texture.value);
                }
                foreach (ConfigNode.Value texture in normals.values)
                {
                    normalList.Add(texture.value);
                }

                String mipmapsString = config.GetValue("mipmaps");
                String compressString = config.GetValue("compress");
                String discard_alphaString = config.GetValue("discard_alpha");
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
                String discard_alphaString_normals = config.GetValue("discard_alpha_normals");
                String scaleString_normals = config.GetValue("scale_normals");
                String max_sizeString_normals = config.GetValue("max_size_normals");

                bool.TryParse(mipmapsString_normals, out config_mipmaps_normals);
                bool.TryParse(compressString_normals, out config_compress_normals);
                int.TryParse(scaleString_normals, out config_scale_normals);
                int.TryParse(max_sizeString_normals, out config_max_size_normals);
            }
        }

        private void ApplyNodeSettings(GameDatabase.TextureInfo Texture, ConfigNode overrideNode)
        {
            String mipmapsString = overrideNode.GetValue("mipmaps");
            String compressString = overrideNode.GetValue("compress");
            String scaleString = overrideNode.GetValue("scale");
            String filter_modeString = overrideNode.GetValue("filter_mode");
            String make_not_readableString = overrideNode.GetValue("make_not_readable");

            bool local_mipmaps = false;
            bool local_compress = true;
            int local_scale = 1;
            FilterMode filter_mode = FilterMode.Bilinear;
            bool local_not_readable = false;

            bool.TryParse(mipmapsString, out local_mipmaps);
            bool.TryParse(compressString, out local_compress);
            int.TryParse(scaleString, out local_scale);
            filter_mode = (FilterMode)Enum.Parse(typeof(FilterMode), filter_modeString);
            bool.TryParse(make_not_readableString, out local_not_readable);

            Texture2D tex = Texture.texture;
            TextureFormat format = tex.format;

            UpdateTex(Texture, local_compress, local_mipmaps, local_scale, filter_mode, local_not_readable);

        }

        private void ApplySettings(GameDatabase.TextureInfo Texture)
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
            
            UpdateTex(Texture, compress, mipmaps, scale, config_filter_mode, config_make_not_readable, max_size);

        }

        private void UpdateTex(GameDatabase.TextureInfo Texture, bool compress, bool mipmaps, int scale, FilterMode filterMode, bool makeNotReadable, int max_size = 0)
        {
            try { Texture.texture.GetPixel(0, 0); }
            catch { return; }
            Texture2D tex = Texture.texture;
            TextureFormat originalFormat = tex.format;
            TextureFormat format = tex.format;
            if (format == TextureFormat.DXT1 || format == TextureFormat.RGB24)
            {
                format = TextureFormat.RGB24;
            }
            else
            {
                format = TextureFormat.RGBA32;
            }
            int originalWidth = tex.width;
            int originalHeight = tex.height;
            int width = tex.width;
            int height = tex.height;
            bool hasMipmaps = tex.mipmapCount == 1 ? false : true;
            if ((mipmaps != hasMipmaps) && scale == 1 && (max_size == 0 || (width <= max_size && height <= max_size)))
            {
                Color32[] pixels = tex.GetPixels32();
                tex.Resize(width, height, format, mipmaps);
                tex.SetPixels32(pixels);
                tex.Apply(mipmaps);
            }
            else if (scale != 1 || (max_size != 0 && (width > max_size && height > max_size)))
            {
                width = tex.width / scale;
                height = tex.height / scale;
                if (max_size != 0)
                {
                    if(width > max_size)
                    {
                        width = max_size;
                    }
                    if(height > max_size)
                    {
                        height = max_size;
                    }
                }
                int tmpScale = scale;
                while (width < 2 && tmpScale > 0)
                {
                    width = tex.width / --tmpScale;
                }
                tmpScale = scale;
                while (height < 2 && tmpScale > 0)
                {
                    height = tex.height / --tmpScale;
                }
                TextureResizer.Resize(tex, width, height, format, mipmaps);
            }

            tex.filterMode = filterMode;
            if (compress && width > 1 && height > 1)
            {
                tex.Compress(true);
            }
            if (!makeNotReadable || Texture.isNormalMap || readableList.Contains(Texture.name))
            {
                tex.Apply(mipmaps);
                Texture.isReadable = true;
            }
            else
            {
                tex.Apply(mipmaps, true);
                Texture.isReadable = false;
            }

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
            }
            switch (tex.format)
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
            if(hasMipmaps)
            {
                oldSize += (int)(oldSize * .33f);
            }
            if(mipmaps)
            {
                newSize += (int)(newSize * .33f);
            }
            int saved = (oldSize - newSize);
            if (saved > 0)
            {
                memorySaved += saved;
            }

        }

        public static void Log(String message)
        {
            UnityEngine.Debug.Log("TextureCompressor: " + message);
        }

    }
}
