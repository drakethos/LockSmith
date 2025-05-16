using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LockSmith
{
    public class ItemLib
    {
        // Your mod's custom localization
        private CustomLocalization Localization;

        private void Localizations()
        {
            Localization = LocalizationManager.Instance.GetLocalization();
            LocalizationManager.Instance.AddLocalization(Localization);

            Localization.AddTranslation("English", new Dictionary<string, string>
            {
                { "item_setAccessKey", "Give Access Key" },
                {
                    "item_setAccessKey_desc",
                    "Use this key on an door or box to allow a player to access it without ward access"
                },
                { "locksmith_giveaccess", "Set Access" },
                { "locksmith_access_message", "Ready to set, other player touch the object!" },
            });
        }

        public void recolor()
        {
            var trollClone = new CustomItem("HelmetTrollLeatherRed", "HelmetTrollLeather", new ItemConfig
            {
                Name = "Red Troll Hood",
                Description = "Is red",
                CraftingStation = CraftingStations.Workbench,
                Requirements = new[] { new RequirementConfig("Wood", 1, 1, false) }
            });
            var trollObject = trollClone.ItemPrefab;
            if (trollObject != null)
            {
                
                Debug.Log("Found the troll prefab");
                var trollMat = trollClone.ItemPrefab.GetComponentInChildren<Material>();
                if (trollMat != null)
                {
                    
                    Debug.Log("Found the material lets make it red!");
                    trollMat.color = Color.red;
                    Debug.Log("WE MADE IT");
                }

                trollClone.ItemPrefab.GetComponentInChildren<Renderer>();   
            }

            ItemManager.Instance.AddItem(trollClone);
        }

        public void makeKeyItems()
        {
            ItemConfig baseKey = new ItemConfig();
            baseKey.Description = "use this key on a door or lock"; //"$item_setAccessKey_desc";
            baseKey.CraftingStation = CraftingStations.Workbench;
            RecipeConfig rec = new RecipeConfig()
            {
                RequireOnlyOneIngredient = true
            };
           var masterKeyCustom  = LockSmith.LoadPrefab("MasterKeyCustom", LockSmith.box);
           var masterKey  = LockSmith.LoadPrefab("MasterKey", LockSmith.box);
           var publicKey  = LockSmith.LoadPrefab("PublicKey", LockSmith.box);
           var publicKey2  = LockSmith.LoadPrefab("PublicKey2", LockSmith.box);
           var privateKey  = LockSmith.LoadPrefab("PrivateKey", LockSmith.box);
           
           string publicKeyPrefab = "CryptKey", privateKeyPrefab = "CryptKey", masterKeyPrefab = "CryptKey";
           
            baseKey.AddRequirement(new RequirementConfig("Stone", 2));
            baseKey.AddRequirement(new RequirementConfig("Wood", 1));
    

            baseKey.Name = "Public Key"; //"$item_setAccessKey";
            baseKey.Description = "place this key in a box to make it public"; //"$item_setAccessKey_desc";
          
            makeItem(baseKey, publicKey);

            baseKey.Name = "Personal Key"; //"$item_setAccessKey";
            baseKey.Description =
                "place this key in a box to make the owner, have access"; //"$item_setAccessKey_desc";
            makeItem (baseKey, privateKey);
            
            makeItem(baseKey, masterKey);
            
            makeItem( baseKey, masterKeyCustom);
            makeItem( baseKey, publicKey2);
            

            PrefabManager.OnVanillaPrefabsAvailable -= makeKeyItems;

            CreateKeyHints();
        }

        private void CreateKeyHints()
        {
            // Override "default" KeyHint with an empty config
            KeyHintConfig KHC_base = new KeyHintConfig
            {
                Item = "setAccessKey"
            };
            KeyHintManager.Instance.AddKeyHint(KHC_base);
        }

        private void makeItem(string name, ItemConfig itemConfig, string prefab)
        {
            makeItem(name, itemConfig.Name, itemConfig.Description, prefab,
                new List<RequirementConfig>(itemConfig.Requirements), itemConfig.CraftingStation);
        }

        private void makeItem(ItemConfig itemConfig, GameObject prefab)
        {
            CustomItem customItem = new CustomItem(prefab, true, itemConfig);
            ItemManager.Instance.AddItem(customItem);
        }

        private void makeItem(string name, string gameName, string description, string prefab,
            List<RequirementConfig> requirements, string craftingStation = "piece_workbench")
        {
            ItemConfig itemConfig = new ItemConfig();
            itemConfig.Name = gameName;
            itemConfig.Description = description;
            itemConfig.CraftingStation = craftingStation;
            foreach (var requirement in requirements)
            {
                itemConfig.AddRequirement(requirement);
            }

            CustomItem customItem = new CustomItem(name, prefab, itemConfig);
            ItemManager.Instance.AddItem(customItem);
        }

        private void makePiece(string name, string gameName, string prefab)
        {
            PieceConfig pieceConfig = new PieceConfig
            {
                PieceTable = "Hammer", // Add to the Hammer build menu
                Category = "Public", // Optional category
                Enabled = true,
                Name = gameName
            };
            CustomPiece customPiece = new CustomPiece(name, prefab, pieceConfig);

            if (customPiece.Piece.GetComponentInChildren<Door>() != null)
            {
                Debug.Log($"Public Door version of piece {customPiece.Piece.name}");
                customPiece.Piece.GetComponentInChildren<Door>().m_checkGuardStone = false;
            }

            if (customPiece.Piece.GetComponentInChildren<Container>())
            {
                Debug.Log($"Public Door version of piece {customPiece.Piece.name}");
                customPiece.Piece.GetComponentInChildren<Container>().m_checkGuardStone = false;
            }

            if (customPiece.Piece.GetComponentInChildren<PrivateArea>())
            {
                Debug.Log($" Removing ward from {customPiece.Piece.name}");
                var privateArea = customPiece.Piece.GetComponentInChildren<PrivateArea>();
                Object.Destroy(privateArea);
            }

            PieceManager.Instance.AddPiece(customPiece);
        }

        public void addPublicPieces()
        {
            makePiece("piece_chest_wood_public", "Chest (public)", "piece_chest_wood");
            makePiece("piece_chest_public", "Reinforced Chest (public)", "piece_chest");
            makePiece("wood_door_public", "Wood Door (public)", "wood_door");
            makePiece("wood_gate_public", "Wood Gate (public)", "wood_gate");
            //makePlainWard();
            PrefabManager.OnVanillaPrefabsAvailable -= addPublicPieces;
        }
        
        private void makePlainWard()
        {
            Debug.Log($"Making plain ward from guard_stone");

            PieceConfig wardConfig = new PieceConfig();
            wardConfig.Name = "Statue Odin";
            wardConfig.Description = "Nothing to see here just a statue";
            wardConfig.PieceTable = PieceTables.Hammer;
            wardConfig.Category = PieceCategories.Furniture;
            wardConfig.Requirements = new RequirementConfig[1]
            {
                new RequirementConfig()
                {
                    Item = "Stone",
                    Amount = 20,
                    Recover = true
                }
            };
            CustomPiece wardPiece = new CustomPiece("odinStatue", "guard_stone", wardConfig);
            wardConfig.Name = "Odin Statue 2";
            CustomPiece wardPiece2 = new CustomPiece("odinStatue2", "dverger_guardstone", wardConfig);

            removePrivateArea(wardPiece);
            removePrivateArea(wardPiece2);

            PieceManager.Instance.AddPiece(wardPiece);
            PieceManager.Instance.AddPiece(wardPiece2);
        }

        private static void removePrivateArea(CustomPiece wardPiece)
        {
            if (wardPiece.Piece.GetComponentInChildren<PrivateArea>() != null)
            {
                var privateArea = wardPiece.Piece.GetComponentInChildren<PrivateArea>();
                Object.Destroy(privateArea);
            }
        }
    }
}