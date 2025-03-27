using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace REPO_SyncUpgrades
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class REPO_SyncUpgrades : BaseUnityPlugin
    {
        private readonly Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
        private static ConfigEntry<bool> configEnabled;
        private static Dictionary<ulong, List<string>> playerUpgrades = new Dictionary<ulong, List<string>>();

        private void Awake()
        {
            // 載入配置
            configEnabled = Config.Bind("General", "Enabled", true, "是否啟用模組");

            // 應用補丁
            harmony.PatchAll();

            Logger.LogInfo($"模組 {PluginInfo.PLUGIN_NAME} 已載入!");
        }

        [HarmonyPatch(typeof(PlayerUpgradeManager), "PurchaseUpgrade")]
        public class PurchaseUpgradePatch
        {
            public static void Postfix(PlayerUpgradeManager __instance, string upgradeId)
            {
                if (!configEnabled.Value) return;

                // 獲取當前玩家ID
                ulong playerId = __instance.GetComponent<NetworkIdentity>().netId;

                // 記錄升級
                if (!playerUpgrades.ContainsKey(playerId))
                {
                    playerUpgrades[playerId] = new List<string>();
                }
                playerUpgrades[playerId].Add(upgradeId);

                // 同步給所有玩家
                SyncUpgradesToAll();
            }
        }

        [HarmonyPatch(typeof(NetworkManager), "OnServerAddPlayer")]
        public class OnServerAddPlayerPatch
        {
            public static void Postfix(NetworkManager __instance, NetworkConnection conn)
            {
                if (!configEnabled.Value) return;

                // 獲取房主ID
                ulong hostId = NetworkServer.connections[0].connectionId;

                // 如果房主有升級，同步給新玩家
                if (playerUpgrades.ContainsKey(hostId))
                {
                    var player = conn.playerController.gameObject;
                    var upgradeManager = player.GetComponent<PlayerUpgradeManager>();
                    
                    foreach (var upgradeId in playerUpgrades[hostId])
                    {
                        upgradeManager.ApplyUpgrade(upgradeId);
                    }
                }
            }
        }

        private static void SyncUpgradesToAll()
        {
            // 獲取所有玩家
            var players = GameObject.FindObjectsOfType<PlayerUpgradeManager>();

            // 獲取房主ID
            ulong hostId = NetworkServer.connections[0].connectionId;

            // 如果房主有升級，同步給所有玩家
            if (playerUpgrades.ContainsKey(hostId))
            {
                foreach (var player in players)
                {
                    if (player.GetComponent<NetworkIdentity>().netId != hostId)
                    {
                        foreach (var upgradeId in playerUpgrades[hostId])
                        {
                            player.ApplyUpgrade(upgradeId);
                        }
                    }
                }
            }
        }
    }
} 