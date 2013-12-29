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

        static bool mipmaps = false;
        static bool compress = true;
        static bool discard_alpha = false;
        static int scale = 1;
        static int max_size = 1;
        static bool mipmaps_normals = false;
        static bool compress_normals = true;
        static bool discard_alpha_normals = false;
        static int scale_normals = 1;
        static int max_size_normals = 1;

        protected void Awake()
        {
            PopulateConfig();
            if (HighLogic.LoadedScene == GameScenes.MAINMENU && !Compressed)
            {
                Update();
                Compressed = true;
                int kbSaved = (int) (memorySaved / 1024f);
                int mbSaved = (int)((memorySaved / 1024f) / 1024f);
                Log("Memory Saved : "+ memorySaved.ToString() + "B");
                Log("Memory Saved : " + kbSaved.ToString() + "kB");
                Log("Memory Saved : " + mbSaved.ToString() + "MB");
                foreach(GameDatabase.TextureInfo Texture in GameDatabase.Instance.databaseTexture)
                {
                    Texture2D texture = Texture.texture;
                    Log("--------------------------------------------------------");
                    Log("Name: " + texture.name);
                    Log("Format: " + texture.format.ToString());
                    Log("MipMaps: " + texture.mipmapCount.ToString());
                    Log("Size: " + texture.width.ToString() + "x" + texture.height);
                }
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
                        stream.Position = 12;
                        String unixPath = file.Replace('\\', '/');
                        stream.Close();
                        MBMToTGA(file);
                        File.Move(file, file+".origN");
                    }
                    else
                    {
                        stream.Close();
                        MBMToPNG(file);
                        File.Move(file, file + ".orig");
                    }
                }
                List<String> mbms = new List<string>();
                mbms.AddRange(System.IO.Directory.GetFiles(KSPUtil.ApplicationRootPath + "GameData/", "*mbm.origN", System.IO.SearchOption.AllDirectories));
                mbms.AddRange(System.IO.Directory.GetFiles(KSPUtil.ApplicationRootPath + "GameData/", "*mbm.orig", System.IO.SearchOption.AllDirectories));
                String pathStart = (KSPUtil.ApplicationRootPath + "GameData/").Replace('\\', '/');
                foreach (String file in mbms)
                {
                    String path = file.Replace(pathStart, "");
                    String unixPath = path.Replace('\\', '/');

                    if (!foldersList.Exists(n => unixPath.StartsWith(n)))
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
                imageBuffer = null;
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

                        if (Texture.name.Length > 0 && foldersList.Exists(n => Texture.name.StartsWith(n)))
                        {
                            String path = KSPUtil.ApplicationRootPath + "GameData/" + Texture.name + ".mbm";
                            if (File.Exists(path + ".origN"))
                            {
                                Texture.isNormalMap = true;
                                //Texture.texture.SetPixels32(GameDatabase.Instance.GetTexture(Texture.name, true).GetPixels32());
                            }
                            else
                            {
                                Texture.isNormalMap = false;
                            }
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
                        }
                    }
                }
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
                foreach (ConfigNode node in overridesFolders.nodes)
                {
                    overridesFolderList.Add(node.name);
                }
                foreach (ConfigNode.Value folder in folders.values)
                {
                    foldersList.Add(folder.value);
                }


                String mipmapsString = config.GetValue("mipmaps");
                String compressString = config.GetValue("compress");
                String discard_alphaString = config.GetValue("discard_alpha");
                String scaleString = config.GetValue("scale");
                String max_sizeString = config.GetValue("max_size");

                bool.TryParse(mipmapsString, out mipmaps);
                bool.TryParse(compressString, out compress);
                bool.TryParse(discard_alphaString, out discard_alpha);
                int.TryParse(scaleString, out scale);
                int.TryParse(max_sizeString, out max_size);

                String mipmapsString_normals = config.GetValue("mipmaps_normals");
                String compressString_normals = config.GetValue("compress_normals");
                String discard_alphaString_normals = config.GetValue("discard_alpha_normals");
                String scaleString_normals = config.GetValue("scale_normals");
                String max_sizeString_normals = config.GetValue("max_size_normals");

                bool.TryParse(mipmapsString_normals, out mipmaps_normals);
                bool.TryParse(compressString_normals, out compress_normals);
                bool.TryParse(discard_alphaString_normals, out discard_alpha_normals);
                int.TryParse(scaleString_normals, out scale_normals);
                int.TryParse(max_sizeString_normals, out max_size_normals);
            }
        }

        private void ApplyNodeSettings(GameDatabase.TextureInfo Texture, ConfigNode overrideNode)
        {
            String mipmapsString = overrideNode.GetValue("mipmaps");
            String compressString = overrideNode.GetValue("compress");
            String discard_alphaString = overrideNode.GetValue("discard_alpha");
            String scaleString = overrideNode.GetValue("scale");
            bool mipmaps = false;
            bool compress = true;
		    bool discard_alpha = false;
            int scale = 1;
            bool.TryParse(mipmapsString, out mipmaps);
            bool.TryParse(compressString, out compress);
            bool.TryParse(discard_alphaString, out discard_alpha);
            int.TryParse(scaleString, out scale);

            Texture2D tex = Texture.texture;
            TextureFormat format = tex.format;
            if (discard_alpha || format == TextureFormat.DXT1 || format == TextureFormat.RGB24)
            {
                format = TextureFormat.RGB24;
            }
            else
            {
                format = TextureFormat.RGBA32;
            }

            UpdateTex(Texture, format, compress, mipmaps, scale);

        }

        private void ApplySettings(GameDatabase.TextureInfo Texture)
        {
            bool mipmaps = TextureCompressor.mipmaps;
            bool compress = TextureCompressor.compress;
            bool discard_alpha = TextureCompressor.discard_alpha;
            int scale = TextureCompressor.scale;
            int max_size = TextureCompressor.max_size;
            if(Texture.isNormalMap)
            {
                mipmaps = TextureCompressor.mipmaps_normals;
                compress = TextureCompressor.compress_normals;
                discard_alpha = TextureCompressor.discard_alpha_normals;
                scale = TextureCompressor.scale_normals;
                max_size = TextureCompressor.max_size_normals;
            }
            Texture2D tex = Texture.texture;
            TextureFormat format = tex.format;
            if (discard_alpha || format == TextureFormat.DXT1 || format == TextureFormat.RGB24)
            {
                format = TextureFormat.RGB24;
            }
            else
            {
                format = TextureFormat.RGBA32;
            }

            UpdateTex(Texture, format, compress, mipmaps, scale, max_size);

        }

        private void UpdateTex(GameDatabase.TextureInfo Texture, TextureFormat format, bool compress, bool mipmaps, int scale, int max_size = 0)
        {
            Texture2D tex = Texture.texture;
            TextureFormat originalFormat = tex.format;
            int originalWidth = tex.width;
            int originalHeight = tex.height;
            int width = tex.width;
            int height = tex.height;
            bool hasMipmaps = tex.mipmapCount == 1 ? false : true;
            if ((mipmaps != hasMipmaps || format != originalFormat) && scale == 1 && (max_size == 0 || (width <= max_size && height <= max_size)))
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
                TextureResizer.Resize(tex, width, height, format, mipmaps);
            }
            
            if (compress)
            {
                tex.Compress(true);
            }

            String path = KSPUtil.ApplicationRootPath + "GameData/" + Texture.name + ".mbm";
            if (!File.Exists(path + ".orig"))
            {
                if (originalFormat == TextureFormat.RGB24)
                {
                    originalFormat = TextureFormat.DXT1;
                }
                else
                {
                    originalFormat = TextureFormat.RGBA32;
                }
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
