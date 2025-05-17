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

            PieceManager.Instance.AddPiece(customPiece);
        }

        public void addPublicPieces()
        {
            makePiece("piece_chest_wood_public", "Chest (public)", "piece_chest_wood");
            makePiece("piece_chest_public", "Reinforced Chest (public)", "piece_chest");
            makePiece("wood_door_public", "Wood Door (public)", "wood_door");
            makePiece("wood_gate_public", "Wood Gate (public)", "wood_gate");
        }
    }
}