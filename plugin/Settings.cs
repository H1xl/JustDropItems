using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using H1xl.Shared;
using RoR2;
using UnityEngine;

namespace JustDropItems
{
    public enum MouseButton { Left, Right, Middle }

    public enum PopupSide { Right, Below, Left, Above }

    /// <summary>Gamepad buttons by their place on the pad (Xbox names; on a DualShock A is Cross, B Circle, X Square, Y Triangle).</summary>
    public enum PadButton { A, B, X, Y, LB, RB, LT, RT, LeftStick, RightStick }

    /// <summary>Whose inventory a player may drop or give from.</summary>
    public enum TakeFromMode { OwnOnly, HostAll, Everyone }

    public enum RecyclerMode { OncePerItem, Never, Unlimited }

    /// <summary>
    /// Config entries. Client settings (controls, popup) apply to the local player; everything else is used only on the host,
    /// which sends the rules the UI needs to every player who has the mod (see <see cref="Net"/>).
    /// </summary>
    internal static class Settings
    {
        // 1. Controls (client)
        internal static ConfigEntry<MouseButton> dropButton;
        internal static ConfigEntry<KeyboardShortcut> dropAllKey, fastStepKey, percentStepKey;
        internal static ConfigEntry<int> wheelStep, fastStep, percentStep;
        internal static ConfigEntry<bool> invertWheel, resetOnLeave, dragToGive, dragOutToDrop;
        internal static ConfigEntry<float> boundaryPause;

        // 2. Popup (client)
        internal static ConfigEntry<PopupSide> popupSide;
        internal static ConfigEntry<float> popupScale;
        internal static ConfigEntry<bool> showBreakdown;
        internal static ConfigEntry<KindOrder> barOrder;
        internal static ConfigEntry<float> popupOpacity;

        // 3. Animation (host)
        internal static ConfigEntry<bool> animation, notify;
        internal static ConfigEntry<float> travelTime;

        // 4. Permissions (host)
        internal static ConfigEntry<TakeFromMode> takeFrom;
        internal static ConfigEntry<bool> allowDrop, allowGive, allowGiveToDead, allowGiveWhileDead;
        internal static ConfigEntry<int> maxPerAction;

        // 5. Item rules (host)
        internal static ConfigEntry<bool> allowEquipment, allowLunar, allowVoid, allowQuest, allowTemporary, allowEthereal, allowGiveConsumed,
                                          allowHallowed, allowHex, allowPages, allowMeals, allowAspects;
        internal static ConfigEntry<string> blacklist;

        // 6. Recycler (host)
        internal static ConfigEntry<RecyclerMode> recycler;

        // 7. Chat (host)
        internal static ConfigEntry<bool> chatCommands, announceDrops, announceGives;

        // 9. Gamepad (client)
        internal static ConfigEntry<bool> gamepadEnabled, gamepadHints;
        internal static ConfigEntry<PadButton> padDrop, padDropAll, padGive, padCancel, padLess, padMore, padFastStep, padPercentStep;

        // 8. Debug
        internal static ConfigEntry<bool> verbose;

        /// <summary>Raised when a host setting the clients need changes, so the host can send the new rules.</summary>
        internal static event Action RulesChanged;

        private static readonly HashSet<ItemIndex> blockedItems = new HashSet<ItemIndex>();
        private static readonly HashSet<EquipmentIndex> blockedEquipment = new HashSet<EquipmentIndex>();

        internal const string DefaultBlacklist = "CaptainDefenseMatrix";

        internal static void Init(ConfigFile cfg)
        {
            const string C = "1. Controls", P = "2. Popup", A = "3. Animation", R = "4. Permissions", I = "5. Item rules",
                         Y = "6. Recycler", H = "7. Chat", D = "8. Debug", G = "9. Gamepad";
            const string client = " Your own setting.", host = " Only the host's setting matters.";

            dropButton = cfg.Bind(C, "DropButton", MouseButton.Left,
                "Mouse button that drops the hovered item: one item, or the amount chosen with the wheel." + client);
            dropAllKey = cfg.Bind(C, "DropAllKey", new KeyboardShortcut(KeyCode.LeftControl), "Hold with the drop button to drop the whole stack." + client);
            wheelStep = cfg.Bind(C, "WheelStep", 1,
                new ConfigDescription("Amount change per wheel step." + client, new AcceptableValueRange<int>(1, 50)));
            fastStepKey = cfg.Bind(C, "FastStepKey", new KeyboardShortcut(KeyCode.LeftControl), "Hold while turning the wheel for a bigger step." + client);
            fastStep = cfg.Bind(C, "FastStep", 5,
                new ConfigDescription("The bigger wheel step." + client, new AcceptableValueRange<int>(1, 50)));
            percentStepKey = cfg.Bind(C, "PercentStepKey", new KeyboardShortcut(KeyCode.LeftAlt),
                "Hold while turning the wheel to step by a share of the stack (or of MaxPerAction, if the stack is bigger)." + client);
            percentStep = cfg.Bind(C, "PercentStep", 10,
                new ConfigDescription("That share in percent (at least 1 item)." + client, new AcceptableValueRange<int>(1, 50)));
            invertWheel = cfg.Bind(C, "InvertWheel", false, "Turn the wheel the other way to increase the amount." + client);
            boundaryPause = cfg.Bind(C, "BoundaryPause", 0.2f,
                new ConfigDescription("Seconds the amount stops at the border between Permanent, Ethereal and Temporary copies, " +
                                      "with any wheel step." + client, new AcceptableValueRange<float>(0f, 1f)));
            resetOnLeave = cfg.Bind(C, "ResetAmountOnLeave", true, "The chosen amount resets when the cursor leaves the icon." + client);
            dragToGive = cfg.Bind(C, "DragToGive", true, "Drag an item onto a player in the scoreboard (Tab) to give it." + client);
            dragOutToDrop = cfg.Bind(C, "DragOutToDrop", true,
                "Drag an item to an empty place and release it to drop it. Releasing it on your own items cancels." + client);

            popupSide = cfg.Bind(P, "PopupSide", PopupSide.Right,
                "Side of the icon where the amount window opens. If it doesn't fit, the next side clockwise is used." + client);
            popupScale = cfg.Bind(P, "PopupScale", 1f,
                new ConfigDescription("Size of the amount window." + client, new AcceptableValueRange<float>(0.5f, 2f)));
            showBreakdown = cfg.Bind(P, "ShowBreakdown", true,
                "Show how many Permanent, Ethereal and Temporary copies the amount takes." + client);
            barOrder = cfg.Bind(P, "BarOrder", KindOrder.PermanentEtherealTemporary,
                "Order of Permanent, Ethereal and Temporary copies on the amount bar. Copies leave the stack in this order " +
                "(the first kind first). Shared copies are always last: they can't be moved." + client);
            popupOpacity = cfg.Bind(P, "PopupOpacity", 0.7f,
                new ConfigDescription("Opacity of the amount window's background (the panel of the game's item description): " +
                                      "1 is as in the game." + client,
                                      new AcceptableValueRange<float>(0.3f, 1f)));

            animation = cfg.Bind(A, "FlyingItemAnimation", true,
                "A given item flies to the player as an icon. If off, it is given instantly." + host);
            travelTime = cfg.Bind(A, "AnimationDuration", 1f,
                new ConfigDescription("Flight time of the icon, in seconds." + host, new AcceptableValueRange<float>(0.1f, 5f)));
            notify = cfg.Bind(A, "PickupNotification", true, "The recipient sees the usual pickup popup and chat line." + host);

            takeFrom = cfg.Bind(R, "WhoCanTakeFrom", TakeFromMode.OwnOnly,
                "Whose items a player can drop or give. OwnOnly: their own. HostAll: the host can also use anyone's. " +
                "Everyone: anyone can use anyone's." + host);
            allowDrop = cfg.Bind(R, "AllowDrop", true, "Players can drop items on the ground." + host);
            allowGive = cfg.Bind(R, "AllowGive", true, "Players can give items to other players." + host);
            allowGiveToDead = cfg.Bind(R, "AllowGiveToDead", true, "Items can be given to a dead player." + host);
            allowGiveWhileDead = cfg.Bind(R, "AllowGiveWhileDead", true,
                "A dead player can give items (dropping always needs a living character)." + host);
            maxPerAction = cfg.Bind(R, "MaxPerAction", 100,
                new ConfigDescription("Most items one drop or give can move." + host, new AcceptableValueRange<int>(1, 1000)));

            allowEquipment = cfg.Bind(I, "AllowEquipment", true,
                "Equipment can be dropped and given. A player receives equipment only into an empty slot." + host);
            allowLunar = cfg.Bind(I, "AllowLunar", false, "Lunar items and lunar equipment." + host);
            allowVoid = cfg.Bind(I, "AllowVoid", false, "Void items." + host);
            allowQuest = cfg.Bind(I, "AllowQuestItems", true, "Quest and key items." + host);
            allowHallowed = cfg.Bind(I, "AllowHallowed", true, "Hallowed items." + host);
            allowHex = cfg.Bind(I, "AllowHex", false, "Hexes. Off by default: a Hex could be pushed onto another player." + host);
            allowPages = cfg.Bind(I, "AllowPages", true, "N'tormat Pages." + host);
            allowMeals = cfg.Bind(I, "AllowMeals", true, "Meals (Wandering CHEF dishes)." + host);
            allowAspects = cfg.Bind(I, "AllowEliteAspects", true, "Elite aspects (elite equipment)." + host);
            allowTemporary = cfg.Bind(I, "AllowTemporary", true, "Temporary copies. They stay temporary and keep their decay." + host);
            allowEthereal = cfg.Bind(I, "AllowEthereal", true, "Ethereal copies. They stay Ethereal and vanish at the end of the stage." + host);
            allowGiveConsumed = cfg.Bind(I, "AllowGiveConsumed", true,
                "Used-up items (Empty Bottle, Delicate Watch (Broken) and the like) can be given. They can never be dropped." + host);
            blacklist = cfg.Bind(I, "Blacklist", DefaultBlacklist,
                "Items and equipment that can never be dropped or given: comma-separated names (English or internal)." + host);

            recycler = cfg.Bind(Y, "RecyclerMode", RecyclerMode.OncePerItem,
                "OncePerItem: every item can be rerolled by the Recycler (or the Drifter) once; the mark stays on it when it is " +
                "picked up, dropped or given. Never: dropped items can't be rerolled. Unlimited: as in the base game, with the " +
                "endless reroll exploit." + host);

            chatCommands = cfg.Bind(H, "ChatCommands", false, "Enable the /drop and /give chat commands." + host);
            announceDrops = cfg.Bind(H, "AnnounceDrops", true,
                "Chat line when someone drops items. Shown in each player's language, only to players with the mod." + host);
            announceGives = cfg.Bind(H, "AnnounceGives", true, "Chat line when someone gives items." + host);

            verbose = cfg.Bind(D, "VerboseLogging", false, "Log every request, check and result.");

            gamepadEnabled = cfg.Bind(G, "GamepadEnabled", true,
                "Gamepad controls: in the scoreboard (hold Back), move to an item with the D-pad and use the buttons below." + client);
            padDrop = cfg.Bind(G, "DropButton", PadButton.A, "Drops the chosen amount (or one). Confirms the player when giving." + client);
            padDropAll = cfg.Bind(G, "DropAllButton", PadButton.Y, "Drops the whole stack." + client);
            padGive = cfg.Bind(G, "GiveButton", PadButton.X, "Starts giving: choose the player with the amount buttons, confirm with the drop button." + client);
            padCancel = cfg.Bind(G, "CancelButton", PadButton.B, "Cancels giving." + client);
            padLess = cfg.Bind(G, "LessButton", PadButton.LB, "Lowers the amount (hold to repeat). Previous player when giving." + client);
            padMore = cfg.Bind(G, "MoreButton", PadButton.RB, "Raises the amount (hold to repeat). Next player when giving." + client);
            padFastStep = cfg.Bind(G, "FastStepButton", PadButton.LT, "Hold for the bigger step (FastStep)." + client);
            padPercentStep = cfg.Bind(G, "PercentStepButton", PadButton.RT, "Hold to step by a share of the stack (PercentStep)." + client);
            gamepadHints = cfg.Bind(G, "ShowButtonHints", true, "Show the buttons in the amount window." + client);

            cfg.SettingChanged += (_, e) =>
            {
                string section = e.ChangedSetting.Definition.Section;
                if (section != R && section != I) return;
                // the blacklist walks every item and equipment: only when it changed, not on every slider step
                if (e.ChangedSetting == blacklist) RebuildBlacklist();
                RulesChanged?.Invoke();
            };
        }

        /// <summary>Resolves the blacklist names once the catalogs are loaded, and again whenever it changes.</summary>
        internal static void RebuildBlacklist()
        {
            blockedItems.Clear();
            blockedEquipment.Clear();
            if (ItemCatalog.itemCount == 0) return;
            var wanted = new Dictionary<string, string>(); // normalized name -> name as typed
            foreach (string raw in blacklist.Value.Split(','))
            {
                string name = Normalize(raw);
                if (name.Length > 0 && !wanted.ContainsKey(name)) wanted.Add(name, raw.Trim());
            }
            if (wanted.Count == 0) return;
            var found = new HashSet<string>();
            bool Match(string internalName, string token)
            {
                string a = Normalize(internalName), b = Normalize(Language.GetString(token, "en"));
                bool hit = false;
                if (wanted.ContainsKey(a)) { found.Add(a); hit = true; }
                if (wanted.ContainsKey(b)) { found.Add(b); hit = true; }
                return hit;
            }
            foreach (ItemDef def in ItemCatalog.allItemDefs)
                if (def && Match(def.name, def.nameToken)) blockedItems.Add(def.itemIndex);
            foreach (EquipmentIndex index in EquipmentCatalog.allEquipment)
            {
                EquipmentDef def = EquipmentCatalog.GetEquipmentDef(index);
                if (def && Match(def.name, def.nameToken)) blockedEquipment.Add(def.equipmentIndex);
            }
            foreach (var w in wanted)
                if (!found.Contains(w.Key)) Log.Warn($"Blacklist: no item or equipment named '{w.Value}'.");
        }

        /// <summary>Lower case letters and digits only: case, spaces and punctuation are ignored in names.</summary>
        internal static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var chars = new List<char>(s.Length);
            foreach (char c in s) if (char.IsLetterOrDigit(c)) chars.Add(char.ToLowerInvariant(c));
            return new string(chars.ToArray());
        }

        internal static IEnumerable<ItemIndex> BlockedItems => blockedItems;
        internal static IEnumerable<EquipmentIndex> BlockedEquipment => blockedEquipment;

        internal static bool Verbose => verbose.Value;

        /// <summary>True while the key of a hold setting is down (only its main key counts: it is held, not pressed as a combo).</summary>
        internal static bool Held(ConfigEntry<KeyboardShortcut> key) => key.Value.MainKey != KeyCode.None && Input.GetKey(key.Value.MainKey);
    }
}
