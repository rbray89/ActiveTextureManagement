using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ActiveTextureManagement
{
    public class TexInfo
    {
        public string name;
        public int width;
        public int height;
        public int resizeWidth;
        public int resizeHeight;
        public string filename;
        public GameDatabase.TextureInfo texture;

        public int scale;
        public int maxSize;
        public int minSize;
        public bool isNormalMap;

        public bool loadOriginalFirst;
        public bool needsResize;

        public TexInfo(string name)
        {
            this.name = name;
            this.isNormalMap = DatabaseLoaderTexture_ATM.IsNormal(name);
            this.width = 1;
            this.height = 1;
            loadOriginalFirst = false;
            needsResize = false;
        }

        public void SetScalingParams(int scale, int maxSize, int minSize)
        {
            this.scale = scale;
            this.maxSize = maxSize;
            this.minSize = minSize;
        }

        public void Resize(int width, int height)
        {
            this.width = width;
            this.height = height;
            this.Resize();
        }

        public void Resize()
        {
            resizeWidth = width / scale;
            resizeHeight = height / scale;

            int tmpScale = scale - 1;
            while (resizeWidth < minSize && tmpScale > 0)
            {
                resizeWidth = width / tmpScale--;
            }
            tmpScale = scale - 1;
            while (resizeHeight < minSize && tmpScale > 0)
            {
                resizeHeight = height / tmpScale--;
            }

            if (maxSize != 0)
            {
                if (resizeWidth > maxSize)
                {
                    resizeWidth = maxSize;
                }
                if (resizeHeight > maxSize)
                {
                    resizeHeight = maxSize;
                }
            }

            needsResize = (resizeHeight != height || resizeWidth != width);
        }
    }

    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    public class ActiveTextureManagement : MonoBehaviour
    {
        static bool Compressed = false;
        static int LastTextureIndex = -1;
        static int gcCount = 0;
        static long memorySaved = 0;
        public static bool DBL_LOG = false;
        
        const int GC_COUNT_TRIGGER = 20;
        
        
        static Dictionary<String, long> folderBytesSaved = new Dictionary<string, long>();

        static List<String> foldersExList = new List<string>();

        protected void Start()
        {
            if (HighLogic.LoadedScene == GameScenes.LOADING)
            {
                DatabaseLoaderTexture_ATM.PopulateConfig();
                SetupLoaders();
            }
            else if (HighLogic.LoadedScene == GameScenes.MAINMENU && !Compressed)
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
                long bSaved = memorySaved;
                long kbSaved = (long)(bSaved / 1024f);
                long mbSaved = (long)(kbSaved / 1024f);
                Log("Memory Saved : " + bSaved.ToString() + "B");
                Log("Memory Saved : " + kbSaved.ToString() + "kB");
                Log("Memory Saved : " + mbSaved.ToString() + "MB");

                TextureConverter.DestroyImageBuffer();
                Resources.UnloadUnusedAssets();
                System.GC.Collect();
            }
        }

        private void SetupLoaders()
        {

            // Get the list where the Texture DatabaseLoader are stored
            Type gdType = typeof(GameDatabase);
            List<DatabaseLoader<GameDatabase.TextureInfo>> textureLoaders =
                (from fld in gdType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                 where fld.FieldType == typeof(List<DatabaseLoader<GameDatabase.TextureInfo>>)
                 select (List<DatabaseLoader<GameDatabase.TextureInfo>>)fld.GetValue(GameDatabase.Instance)).FirstOrDefault();

            foreach (var textureLoader in textureLoaders)
            {
                if (textureLoader.GetType().Name != "DatabaseLoaderTexture_ATM")
                {
                    Log("Disabling " + textureLoader.GetType().Name);
                    textureLoader.extensions.Clear();
                }
            }
        }

        private GUISkin _mySkin;
        private Rect _mainWindowRect = new Rect(5, 5, 640, 240);
        static Vector2 ScrollFolderList = Vector2.zero;
        int selectedFolder = 0;
        int selectedMode = 0;
        bool guiEnabled = false;
        ConfigNode guiConfig = null;
        private void OnGUI()
        {
            GUI.skin = _mySkin;
            if (HighLogic.LoadedScene == GameScenes.MAINMENU && guiEnabled)
            {
                GUI.backgroundColor = new Color(0, 0, 0, 1);
                String memFormatString = "{0,7}kB {1,4}MB";
                long bSaved = memorySaved;
                long kbSaved = (long)(bSaved / 1024f);
                long mbSaved = (long)(kbSaved / 1024f);
                String totalMemoryString = String.Format("Total Memory Saved: " + memFormatString, kbSaved, mbSaved);
                _mainWindowRect = GUI.Window(0x8100, _mainWindowRect, DrawMainWindow, totalMemoryString);
                
            }
        }

        private void DrawMainWindow(int windowID)
        {
            GUIStyle gs = new GUIStyle(GUI.skin.label);
            GUIStyle gsBox = new GUIStyle(GUI.skin.box);

            int itemFullWidth = (int)_mainWindowRect.width - 30;
            int itemHalfWidth = (int)_mainWindowRect.width/2 - 20;
            int itemQuarterWidth = (int)_mainWindowRect.width / 4 - 20;
            int itemMidStart = (int)_mainWindowRect.width - (15 + itemHalfWidth);
            int itemThirdWidth = (int)_mainWindowRect.width / 3 - 20;
            int itemTwoThirdStart = itemThirdWidth + 20;
            int itemTwoThirdWidth = (int)_mainWindowRect.width - (35+itemThirdWidth);
            int itemQuarterThirdWidth = itemHalfWidth + 5 - itemTwoThirdStart;

            GUI.Box(new Rect(0, 0, _mainWindowRect.width, _mainWindowRect.height), "");

            GUI.Box(new Rect(10, 20, itemThirdWidth, 210), "");
            String[] folderList = foldersExList.ToArray();
            ScrollFolderList = GUI.BeginScrollView(new Rect(15, 25, itemThirdWidth - 10, 195), ScrollFolderList, new Rect(0, 0, itemThirdWidth - 30, 25 * folderList.Length));
            float folderWidth = folderList.Length > 7 ? itemThirdWidth - 30 : itemThirdWidth - 10;
            selectedFolder = selectedFolder >= folderList.Length ? 0 : selectedFolder;
            int OldSelectedFolder = selectedFolder;
            selectedFolder = GUI.SelectionGrid(new Rect(0, 0, folderWidth, 25 * folderList.Length), selectedFolder, folderList, 1);
            GUI.EndScrollView();

            String folder = folderList[selectedFolder];
            

            String memFormatString = "{0,7}kB {1,4}MB";
            long bSaved = folderBytesSaved[folderList[selectedFolder]];
            long kbSaved = (long)(bSaved / 1024f);
            long mbSaved = (long)(kbSaved / 1024f);
            String memoryString = String.Format("Memory Saved: " + memFormatString, kbSaved, mbSaved);
            GUI.Label(new Rect(itemMidStart, 55, itemHalfWidth, 25), memoryString);
            
            String[] Modes = {"Normal List", "Overrides"};
            //selectedMode = GUI.SelectionGrid(new Rect(itemTwoThirdStart, 25, itemQuarterThirdWidth, 25 * Modes.Length), selectedMode, Modes, 1);
            selectedMode = GUI.Toolbar(new Rect(itemMidStart, 25, itemHalfWidth, 25), selectedMode, Modes);
            if(selectedMode == 0)
            {
                GUI.Box(new Rect(itemTwoThirdStart, 85, itemTwoThirdWidth, 145), "");

            }
            GUI.DragWindow(new Rect(0, 0, 10000, 10000));

        }

        protected void Update()
        {
            if (HighLogic.LoadedScene == GameScenes.MAINMENU)
            {
                bool alt = (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt));
                if (alt && Input.GetKeyDown(KeyCode.M))
                {
                    guiEnabled = !guiEnabled;
                }
            }
        }

        
        private void updateMemoryCount(int originalWidth, int originalHeight, TextureFormat originalFormat, bool originalMipmaps, GameDatabase.TextureInfo Texture, String folder)
        {
            int saved = CacheController.MemorySaved(originalWidth, originalHeight, originalFormat, originalMipmaps, Texture);
            memorySaved += saved;

            if (!folderBytesSaved.ContainsKey(folder))
            {
                folderBytesSaved.Add(folder, 0);
            }
            long folderSaved = folderBytesSaved[folder] + saved;
            folderBytesSaved[folder] = folderSaved;

            Log("Saved " + saved + "B");
            Log("Accumulated Saved " + memorySaved + "B");
        }

        public static void DBGLog(String message)
        {
            if (DBL_LOG)
            {
                UnityEngine.Debug.Log("ActiveTextureManagement: " + message);
            }
        }
        public static void Log(String message)
        {
            UnityEngine.Debug.Log("ActiveTextureManagement: " + message);
        }

    }
}
