using System;
using BepInEx;
using H1xl.Shared;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.Networking;

namespace JustDropItems
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency(RooPage.GUID, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string GUID = "com.H1xl.JustDropItems";
        public const string NAME = "JustDropItems";
        public const string VERSION = "1.0.0";

        private void Awake()
        {
            Log.Init(Logger);
            Log.IsVerbose = () => Settings.Verbose;
            Settings.Init(Config);
            Exchange.Init(); // before Net: the cached rules must be reset before Net sends the new ones
            Net.Init();

            HookUtil.Add(GameMembers.Language_LoadStrings, typeof(Tokens), nameof(Tokens.OnLoadStrings));
            HookUtil.Add(GameMembers.GenericPickupController_AttemptGrant, typeof(Recycling), nameof(Recycling.OnAttemptGrant));
            HookUtil.Add(GameMembers.ItemIcon_ItemClicked, typeof(Plugin), nameof(OnItemClicked));
            HookUtil.Add(GameMembers.EquipmentIcon_ItemClicked, typeof(Plugin), nameof(OnEquipmentClicked));
            HookUtil.Add(GameMembers.Chat_CCSay, typeof(ChatCommands), nameof(ChatCommands.OnSay));

            AddSlot("RoR2/Base/UI/ItemIcon.prefab", go => go.AddComponent<TradeSlot>());
            AddSlot("RoR2/Base/UI/ItemIconScoreboard_InGame.prefab", go => go.AddComponent<TradeSlot>());
            AddSlot("RoR2/Base/UI/ScoreboardStrip.prefab", go =>
            {
                foreach (EquipmentIcon icon in go.GetComponentsInChildren<EquipmentIcon>(true))
                    icon.gameObject.AddComponent<TradeSlot>().isEquipment = true;
            });
            AddSlot("RoR2/Base/UI/HUDSimple.prefab", go =>
            {
                foreach (EquipmentIcon icon in go.GetComponentsInChildren<EquipmentIcon>(true))
                    icon.gameObject.AddComponent<TradeSlot>().isEquipment = true;
            });

            string folder = System.IO.Path.GetDirectoryName(Info.Location);
            RooPage.TryRegister(() => RiskOfOptionsCompat.Register(folder));

            RoR2Application.onLoad += () =>
            {
                Tokens.ApplyLoaded();
                Settings.RebuildBlacklist();
                Exchange.CreatePickupPrefab();
            };
            Run.onRunStartGlobal += _ => { Exchange.Reset(); TradeSlot.ResetNotices(); };
            Inventory.onInventoryChangedGlobal += Recycling.OnInventoryChanged;
            Stage.onServerStageComplete += _ => Exchange.Flush();
            SceneExitController.onFinishExit += _ => Exchange.Flush();
            Run.onRunDestroyGlobal += _ => { Exchange.Flush(); Exchange.Reset(); };

            Log.Info($"{NAME} {VERSION} ready.");
        }

        private void Update()
        {
            try { Gamepad.Tick(); }
            catch (Exception e) { Log.ErrorOnce("gamepad", "Gamepad: " + e); }
        }

        private void FixedUpdate()
        {
            if (NetworkServer.active) Exchange.Tick();
        }

        /// <summary>Adds our component to a UI prefab, so every icon the game creates from it has it.</summary>
        private static void AddSlot(string key, Action<GameObject> add)
        {
            try
            {
                Addressables.LoadAssetAsync<GameObject>(key).Completed += handle =>
                {
                    if (handle.Result) add(handle.Result);
                    else Log.Warn($"UI prefab {key} not found (game updated?).");
                };
            }
            catch (Exception e)
            {
                Log.Error($"UI prefab {key}: {e}");
            }
        }

        // The vanilla click on an icon opens the inspect panel. When the click drops the item, skip it.
        private static void OnItemClicked(Action<ItemIcon> orig, ItemIcon self)
        {
            if (Claimed(self)) return;
            orig(self);
        }

        private static void OnEquipmentClicked(Action<EquipmentIcon> orig, EquipmentIcon self)
        {
            if (Claimed(self)) return;
            orig(self);
        }

        private static bool Claimed(Component icon)
        {
            TradeSlot slot = icon ? icon.GetComponent<TradeSlot>() : null;
            // The gamepad's A (submit) on a selected icon also lands here: our buttons handle it (see Gamepad).
            // A host without the mod (no rules from it): the click stays vanilla.
            return slot && Net.HostRules != null && (slot.handledFrame == Time.frameCount || slot.WantsClick(PointerEventData.InputButton.Left) ||
                            (slot.padSelected && Settings.gamepadEnabled.Value && slot.WantsPad()));
        }
    }
}
