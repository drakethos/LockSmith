using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Configs;
using UnityEngine;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;

namespace LockSmith
{
    [BepInPlugin("com.drakemods.LockSmith", "LockSmith", "0.0.1")]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    [BepInProcess("valheim.exe")]
    public class LockSmith : BaseUnityPlugin
    {
        private readonly Harmony harmony = new Harmony("drakesmod.LockSmith");

        // Your mod's custom localization
        private CustomLocalization Localization;

        private void Awake()
        {
            //Localizations();
            PrefabManager.OnVanillaPrefabsAvailable += makeKeyItems;

            harmony.PatchAll();
        }

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

        private void makeKeyItems()
        {
            ItemConfig setAccessKeyConfig = new ItemConfig();
            setAccessKeyConfig.Name = "Set Access Key"; //"$item_setAccessKey";
            setAccessKeyConfig.Description = "use this key on a door or lock"; //"$item_setAccessKey_desc";
            setAccessKeyConfig.CraftingStation = "piece_workbench";
            setAccessKeyConfig.AddRequirement(new RequirementConfig("Stone", 1));
            setAccessKeyConfig.AddRequirement(new RequirementConfig("Wood", 1));
            makeItem("setAccessKey", setAccessKeyConfig, "CryptKey");
            // ItemManager.Instance.AddRecipesFromJson("LockSmith/Assets/recipes.json");

            setAccessKeyConfig.Name = "Public Key"; //"$item_setAccessKey";
            setAccessKeyConfig.Description = "place this key in a box to make it public"; //"$item_setAccessKey_desc";
            makeItem("publicKey", setAccessKeyConfig, "CryptKey");

            setAccessKeyConfig.Name = "Personal Key"; //"$item_setAccessKey";
            setAccessKeyConfig.Description =
                "place this key in a box to make the owner, have access"; //"$item_setAccessKey_desc";
            makeItem("personalKey", setAccessKeyConfig, "CryptKey");

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

            // Add custom KeyHints for specific pieces
            KeyHintConfig KHC_make = new KeyHintConfig
            {
                Item = "setAccessKey",
                //Piece = "make_testblueprint",
                ButtonConfigs = new[]
                {
                    // Override vanilla "Attack" key text
                    new ButtonConfig { Name = "Attack", HintToken = "$bprune_make" }
                }
            };
            KeyHintManager.Instance.AddKeyHint(KHC_make);
        }


        private void makeItem(string name, ItemConfig itemConfig, string prefab)
        {
            makeItem(name, itemConfig.Name, itemConfig.Description, prefab,
                new List<RequirementConfig>(itemConfig.Requirements), itemConfig.CraftingStation);
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
            Debug.Log(customItem.ItemPrefab.name);
            Debug.Log(customItem.ItemDrop.m_itemData.m_shared.m_name);
            ItemManager.Instance.AddItem(customItem);
        }

        [HarmonyPatch(typeof(Container))]
        // [HarmonyPatch(typeof(Container), "GetHoverText")]
        class Private_mod
        {
            [HarmonyPatch(typeof(Container), "CheckAccess")]
            static bool Prefix(ref Container.PrivacySetting ___m_privacy, ref Container __instance, ref bool __result,
                long playerID)
            {
                if (__instance.GetInventory().ContainsItemByName("Public Key"))
                {
                    ___m_privacy = Container.PrivacySetting.Public;
                    __instance.m_checkGuardStone = false;
                    Debug.Log(__instance.m_checkGuardStone);
                }
                else
                {
                    __instance.m_checkGuardStone = true;
                    if (__instance.name == "piece_chest_private(Clone)")
                    {
                        ___m_privacy = Container.PrivacySetting.Private;
                        Debug.Log(__instance.m_checkGuardStone);
                    }

                    if (__instance.GetInventory().ContainsItemByName("Personal Key"))
                    {
                        Debug.Log(("Found personal key! check acess"));
                        foreach (ItemDrop.ItemData itemData in __instance.GetInventory().GetAllItems())
                        {
                            if (itemData.m_shared.m_name == "Personal Key")
                                Debug.Log(itemData.m_crafterName);
                            Debug.Log(itemData.m_shared.m_name);
                            if (itemData.m_crafterID == playerID)
                            {
                                Debug.Log("Found one for individual");
                                __instance.m_checkGuardStone = false;
                                __result = true;
                            }
                        }
                    }
                }

                return true;
            }

            [HarmonyPatch(typeof(Container), "GetHoverText")]
            [HarmonyPrefix]
            static bool PrefixHover(ref Container.PrivacySetting ___m_privacy, ref Container __instance)
            {
                if (__instance.GetInventory().ContainsItemByName("Public Key"))
                {
                    ___m_privacy = Container.PrivacySetting.Public;
                    __instance.m_checkGuardStone = false;
                }
                else
                {
                    __instance.m_checkGuardStone = true;
                    if (__instance.name == "piece_chest_private(Clone)")
                    {
                        ___m_privacy = Container.PrivacySetting.Private;
                    }

                    if (__instance.GetInventory().ContainsItemByName("Personal Key"))
                    {
                        Debug.Log(("Found personal key! from hover"));
                        foreach (ItemDrop.ItemData itemData in __instance.GetInventory().GetAllItems())
                        {
                            if (itemData.m_shared.m_name == "Personal Key")
                                Debug.Log(itemData.m_crafterName);
                            Debug.Log(itemData.m_crafterID);
                            Debug.Log(itemData.m_shared.m_name);
                            ZNetView netView = __instance.GetComponent<ZNetView>();
                            Debug.Log(Game.instance.GetPlayerProfile().GetPlayerID());

                            if (itemData.m_crafterID == Game.instance.GetPlayerProfile().GetPlayerID())
                            {
                                Debug.Log("Found one for individual");
                                __instance.m_checkGuardStone = false;
                            }
                        }
                    }
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Door), "Interact")]
        [HarmonyPatch(typeof(Door), "GetHoverText")]
        class PublicDoor
        {
            private static bool Prefix(ref Door __instance)
            {
                /*if (__instance.name == "Hayze_gate_01(Clone)" || __instance.name == "wood_door(Clone)")
                {
                    __instance.m_checkGuardStone = false;
                }*/

                ZNetView netView = __instance.GetComponent<ZNetView>();
                Debug.Log(netView.GetZDO().GetBool("bypassward"));
                if (netView.GetZDO().GetBool("bypassward"))
                {
                    __instance.m_checkGuardStone = false;
                    Debug.Log("triggerd bypassward");
                }

                return true;
            }

            [HarmonyPatch(typeof(Door), "RPC_UseDoor")]
            class patch3
            {
                [HarmonyPrefix]
                static bool Postfix(bool forward, Door __instance)
                {
                    if (__instance.m_checkGuardStone)
                    {
                        return true;
                    }

                    ZNetView netView = __instance.GetComponent<ZNetView>();
                    if (netView.GetZDO().GetInt(ZDOVars.s_state) == 0)
                    {
                        Debug.Log(forward);
                        if (forward)
                        {
                            netView.GetZDO().Set(ZDOVars.s_state, 1, __instance.m_checkGuardStone);
                        }
                        else
                        {
                            netView.GetZDO().Set(ZDOVars.s_state, -1, __instance.m_checkGuardStone);
                        }
                    }
                    else
                        netView.GetZDO().Set(ZDOVars.s_state, 0, __instance.m_checkGuardStone);

                    return false;
                }
            }
        }


        //    [HarmonyPatch(typeof(Container), "UseItem")]
        [HarmonyPatch(typeof(Door), "UseItem")]
        class patch2
        {
            private ZNetView m_nview;

            static bool Prefix(ItemDrop.ItemData item, ref bool __result, ref bool ___m_checkGuardStone,
                ref Humanoid user, ref Door __instance)
            {
                Debug.Log("We made it here prefix");
                if (item.m_shared.m_name == "Set Access Key")
                {
                    if (__instance.m_checkGuardStone && !PrivateArea.CheckAccess(__instance.transform.position))
                    {
                        __result = false;
                        return true;
                    }

                    ___m_checkGuardStone = !___m_checkGuardStone;


                    user.Message(MessageHud.MessageType.Center,
                        "You have switched the door to be " + (___m_checkGuardStone == true ? "Private" : "Public"));
                    __result = true;
                    ZNetView netView = __instance.GetComponent<ZNetView>();
                    netView.GetZDO().Set("bypassward", ___m_checkGuardStone);
                    Debug.Log(netView.GetZDO().GetBool("bypassward"));
                    return false;
                }

                return true;
            }
        }

        // [HarmonyPatch(typeof(Container),"Interact")]
        // [HarmonyPrefix]
        // public bool Interact(Humanoid human, ref bool __result, ref Piece __m_piece)
        // {
        //     if (accessMode)
        //     {
        //         Player player = human as Player;
        //         // if (__m_piece.IsCreator())
        //         // {
        //         //     this.m_nview.InvokeRPC("ToggleEnabled", (object)player.GetPlayerID());
        //         //     __result = true;
        //         // }
        //
        //         this.m_nview.InvokeRPC("TogglePermitted", (object)player.GetPlayerID(),
        //             (object)player.GetPlayerName());
        //         AddPermitted(player.GetPlayerID(), player.GetPlayerName());
        //         return false;
        //     }
        //     else
        //     {
        //         return true;
        //     }
        // }

        /*
        private void AddUserList(StringBuilder text)
        {
            List<KeyValuePair<long, string>> permittedPlayers = GetPermittedPlayers();
            text.Append("\n$piece_guardstone_additional: ");
            for (int index = 0; index < permittedPlayers.Count; ++index)
            {
                text.Append(permittedPlayers[index].Value);
                if (index != permittedPlayers.Count - 1)
                    text.Append(", ");
            }
        }

        private List<KeyValuePair<long, string>> GetPermittedPlayers()
        {
            List<KeyValuePair<long, string>> permittedPlayers = new List<KeyValuePair<long, string>>();
            int num = m_nview.GetZDO().GetInt(ZDOVars.s_permitted);
            for (int index = 0; index < num; ++index)
            {
                long key = m_nview.GetZDO().GetLong("pu_id" + index.ToString());
                string str = m_nview.GetZDO().GetString("pu_name" + index.ToString());
                if (key != 0L)
                    permittedPlayers.Add(new KeyValuePair<long, string>(key, str));
            }

            return permittedPlayers;
        }

        private void AddPermitted(long playerID, string playerName)
        {
            List<KeyValuePair<long, string>> permittedPlayers = this.GetPermittedPlayers();
            foreach (KeyValuePair<long, string> keyValuePair in permittedPlayers)
            {
                if (keyValuePair.Key == playerID)
                    return;
            }

            permittedPlayers.Add(new KeyValuePair<long, string>(playerID, playerName));
            SetPermittedPlayers(permittedPlayers);
        }

        private void SetPermittedPlayers(List<KeyValuePair<long, string>> users)
        {
            this.m_nview.GetZDO().Set(ZDOVars.s_permitted, users.Count, false);
            for (int index = 0; index < users.Count; ++index)
            {
                KeyValuePair<long, string> user = users[index];
                this.m_nview.GetZDO().Set("pu_id" + index.ToString(), user.Key);
                this.m_nview.GetZDO().Set("pu_name" + index.ToString(), user.Value);
            }
        }

        private void RemovePermitted(long playerID)
        {
            List<KeyValuePair<long, string>> permittedPlayers = this.GetPermittedPlayers();
            if (permittedPlayers.RemoveAll((Predicate<KeyValuePair<long, string>>)(x => x.Key == playerID)) <= 0)
                return;
            this.SetPermittedPlayers(permittedPlayers);
        }

        [HarmonyPatch(typeof(Door), "UseItem")]
        static void PrefixDoor()
        {
        }
    }*/
        /*
         *  static void PostFix(ref )

            { public bool UseItem(Humanoid user, ItemDrop.ItemData item) =>}
         */
    }
}