using BepInEx;
using HarmonyLib;
using Trainworks.Interfaces;
using Trainworks.Managers;
using Trainworks.Utilities;
using System.Reflection;
using Trainworks.Builders;
using System.IO;
using System;
using System.Collections.Generic;

/* Card art swap Mod for Monster Train
 * 
 * This simply changes the picture on cards.
 * 
 * 1. Find the directory of this mod's dll
 * 2. Create an "assets" sub directory there.
 * 3. Put your alternate card art in "assets".
 * This will only affect card art, not the monster summoned or anything else.
 * 
 * Pictures can be in jpg or png format.
 * 972 (width) x 1176 (height) Pixels is the resolution of the base game art. Use the same aspect ratio for best results.
 *
 * Filename for your art should match the English name of the card you wish to change.
 * Ex: use "Deadweight.jpg" to change the card art for Deadweight.
 * Please make sure the extension (jpg, png) is in lower case.
 * 
 */

namespace ArtSwapMod
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class UnitSizePlugin : BaseUnityPlugin, IInitializable
    {
        public const string GUID = "this.looks.different";
        public const string NAME = "Art Swap";
        public const string VERSION = "1.0.0";

        public Assembly assembly;
        public string codeBase;
        public string assemblyPath;
        public string artPath;
        public string fullArtPath;

        public IDictionary<string, List<CardData>> cardDataByEnglishNames;

        public List<string> artFiles;

        private void Awake()
        {
            var harmony = new Harmony(GUID);
            harmony.PatchAll();

            // New art should go in an "assets" directory, right besides this mod's DLL
            this.artPath = "assets";

            this.assembly = Assembly.GetCallingAssembly();
            this.codeBase = Assembly.GetExecutingAssembly().CodeBase;
            UriBuilder uri = new UriBuilder(codeBase);
            string path = Uri.UnescapeDataString(uri.Path);
            this.assemblyPath = Path.GetDirectoryName(path);
            this.fullArtPath = Path.Combine(assemblyPath, this.artPath);

            // We're going to have a local structure linking English card names to internal data
            // It'll be easy and efficient to link that data to loose jpg files this way
            this.cardDataByEnglishNames = new Dictionary<string, List<CardData>>();
        }

        // Following function replaces the art for a Monster Train card
        // Largely inspired by Trainworks source code. By inspired I mean outright stolen.
        // The artPath string is relative to the DLL, such as "assets/Deadweight.jpg"
        public void ReplaceCardArt(CardData myCard, string artPath)
        {
            if (myCard == null || artPath == "")
            {
                Logger.LogError($"ReplaceCardArt() error in parameters");
                return;
            }

            var assetLoadingInfo = new AssetLoadingInfo()
            {
                FilePath = artPath,
                PluginPath = this.assemblyPath,
                AssetType = AssetRefBuilder.AssetTypeEnum.CardArt
            };
            var cardArtPrefabVariantRefBuilder = new AssetRefBuilder
            {
                AssetLoadingInfo = assetLoadingInfo
            };
            var cardArtPrefabVariantRef = cardArtPrefabVariantRefBuilder.BuildAndRegister();
            AccessTools.Field(typeof(CardData), "cardArtPrefabVariantRef").SetValue(myCard, cardArtPrefabVariantRef);
        }

        // Called at initialization, create a key/value structure for CardData, the key being the English name (user-friendly)
        // This is done to avoid calling Localize too often, I don't know how expensive this method is.
        // Also useful to lowercase the card's name, since our art files could be store on a case-ignorant filesystem
        public void IndexCardData()
        {
            // Logger.LogInfo("indexing card data");
            AllGameData agd = ProviderManager.SaveManager.GetAllGameData();
            List<CardData> themDatas = AccessTools.Field(typeof(AllGameData), "cardDatas").GetValue(agd) as List<CardData>;
            for (int i = 0; i < themDatas.Count; i++)
            {
                var key = themDatas[i].GetNameEnglish().ToLower();
                if (! this.cardDataByEnglishNames.ContainsKey(key))
                {
                    // Some cards have multiple instances with the same name, such as Blazing Bolts
                    // So we're going to store a list of CardData for each name
                    // Logger.LogInfo($"{i}: indexing card data for '{key}'");
                    var cdl = new List<CardData>();
                    cdl.Add(themDatas[i]);
                    this.cardDataByEnglishNames.Add(key, cdl);
                }
                else
                {
                    // Logger.LogInfo($"{i}: indexing alternate card data for '{key}'");
                    this.cardDataByEnglishNames[key].Add(themDatas[i]);
                }
            }
            // Logger.LogInfo($"indexed: {this.cardDataByEnglishNames.Count} cards");
        }

        // Following function will find image files in the "assets" directory (and subdirectories if any)
        public void ListArtFiles()
        {
            this.artFiles = new List<string>(Directory.GetFiles(this.fullArtPath, "*.jpg", SearchOption.AllDirectories));
            this.artFiles.AddRange(Directory.GetFiles(this.fullArtPath, "*.jpeg", SearchOption.AllDirectories));
            this.artFiles.AddRange(Directory.GetFiles(this.fullArtPath, "*.png", SearchOption.AllDirectories));
            // Logger.LogInfo($"there are {this.artFiles.Count} image files in {this.artPath}/ directory");
        }

        public void Initialize()
        {
            this.IndexCardData(); // Gather our CardData, indexed by English name
            this.ListArtFiles();  // Gather our image files

            foreach (string fullpath in this.artFiles) {
                var f = Path.GetFileName(fullpath);
                var n = Path.GetFileNameWithoutExtension(fullpath).ToLower();
                // Logger.LogInfo($"processing art file: {f}");
                try
                {
                    // most of the time we have a single card to update, there are exceptions like Blazing Bolts
                    foreach (CardData cd in this.cardDataByEnglishNames[n])
                    {
                        ReplaceCardArt(cd, Path.Combine(this.artPath, f));
                    }
                }
                catch
                {
                    Logger.LogError($"could not use {f}");
                }
                
            }
        }
    }


}