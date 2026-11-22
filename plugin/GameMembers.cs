using H1xl.Shared;
using RoR2;
using RoR2.UI;

namespace JustDropItems
{
    /// <summary>
    /// Every game member this mod reaches by name. Checked against the game's dlls by the level-1 test
    /// (_shared/tests/GameMembersTests); a missing one turns off only the feature that uses it.
    /// </summary>
    internal static class GameMembers
    {
        // ---------- hook targets (Plugin.Awake) ----------
        internal static readonly GameMethod Language_LoadStrings =
            new GameMethod(typeof(Language), nameof(Language.LoadStrings));
        internal static readonly GameMethod GenericPickupController_AttemptGrant =
            new GameMethod(typeof(GenericPickupController), "AttemptGrant", typeof(CharacterBody));
        internal static readonly GameMethod ItemIcon_ItemClicked =
            new GameMethod(typeof(ItemIcon), nameof(ItemIcon.ItemClicked));
        internal static readonly GameMethod EquipmentIcon_ItemClicked =
            new GameMethod(typeof(EquipmentIcon), nameof(EquipmentIcon.ItemClicked));
        internal static readonly GameMethod Chat_CCSay =
            new GameMethod(typeof(Chat), "CCSay", typeof(ConCommandArgs));

        // ---------- console commands registered by hand (Net.RegisterCommands) ----------
        internal static readonly GameField Console_concommandCatalog =
            new GameField(typeof(RoR2.Console), "concommandCatalog");
        // RoR2.Console.ConCommand is private; its Type is ConCommand_flags.Type
        internal static readonly GameField ConCommand_flags =
            new GameField(GameMember.Nested(typeof(RoR2.Console), "ConCommand"), "flags");
        internal static readonly GameField ConCommand_action =
            new GameField(GameMember.Nested(typeof(RoR2.Console), "ConCommand"), "action");
        internal static readonly GameField ConCommand_helpText =
            new GameField(GameMember.Nested(typeof(RoR2.Console), "ConCommand"), "helpText");

        // ---------- recycler/printer marks (Recycling) ----------
        internal static readonly GameField GenericPickupController_consumed =
            new GameField(typeof(GenericPickupController), "consumed");

        // ---------- scoreboard icons (TradeSlot) ----------
        internal static readonly GameField ItemInventoryDisplay_inventory =
            new GameField(typeof(ItemInventoryDisplay), "inventory");
        internal static readonly GameField EquipmentIcon_targetInventory =
            new GameField(typeof(EquipmentIcon), "targetInventory");
        internal static readonly GameField EquipmentIcon_currentDisplayData =
            new GameField(typeof(EquipmentIcon), "currentDisplayData");
        internal static readonly GameField DisplayData_equipmentDef =
            new GameField(GameMember.Nested(typeof(EquipmentIcon), "DisplayData"), "equipmentDef");
    }
}
