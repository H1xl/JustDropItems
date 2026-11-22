using System.Collections.Generic;
using RoR2;
using UnityEngine;

namespace JustDropItems
{
    /// <summary>Formats the host's notices in the local player's language.</summary>
    internal static class Notices
    {
        internal static string FormatNotice(Net.NoticeType type, string actor, string owner, string recipient, Net.Target target, int index,
                                            int permanent, int ethereal, int temporary)
        {
            string what = target == Net.Target.Equipment ? EquipmentName(index) : ItemText(index, permanent, ethereal, temporary);
            string a = Player(actor);
            bool fromOther = !string.IsNullOrEmpty(owner) && owner != actor;
            if (type == Net.NoticeType.Dropped)
                return fromOther ? Language.GetStringFormatted(Tokens.DroppedFrom, a, what, Player(owner))
                                 : Language.GetStringFormatted(Tokens.Dropped, a, what);
            return fromOther ? Language.GetStringFormatted(Tokens.GaveFrom, a, what, Player(owner), Player(recipient))
                             : Language.GetStringFormatted(Tokens.Gave, a, what, Player(recipient));
        }

        internal static string FormatReject(Tokens.Reject reason, Net.Target target, int index, string name)
        {
            string arg = !string.IsNullOrEmpty(name) ? Player(name)
                : index < 0 ? string.Empty
                : target == Net.Target.Equipment ? EquipmentName(index) : ItemName((ItemIndex)index);
            return "<color=#FF8282>" + Language.GetStringFormatted(Tokens.R(reason), arg) + "</color>";
        }

        /// <summary>Starts a "name" that is a survivor's name token: the player has no name (see Exchange.NetName).</summary>
        internal const string SurvivorMarker = "\u0001";

        /// <summary>A player's name as the game writes it in its pickup lines: plain, in the line's colour.</summary>
        private static string Player(string name)
        {
            if (!string.IsNullOrEmpty(name) && name.StartsWith(SurvivorMarker)) name = Language.GetString(name.Substring(SurvivorMarker.Length));
            return Util.EscapeRichTextForTextMeshPro(name ?? string.Empty);
        }

        /// <summary>A player's name on this machine: their name, or their survivor's name in the local language.</summary>
        internal static string LocalName(CharacterMaster m)
        {
            if (!m || !m.playerCharacterMasterController) return string.Empty;
            string name = m.playerCharacterMasterController.GetDisplayName();
            if (!string.IsNullOrWhiteSpace(name)) return name;
            CharacterBody body = m.bodyPrefab ? m.bodyPrefab.GetComponent<CharacterBody>() : null;
            return body ? Language.GetString(body.baseNameToken) : "Player";
        }

        /// <summary>
        /// "Soldier's Syringe(3) (1 Ethereal, 2 Temporary)": the name in its tier colour with the count the way the game's
        /// pickup line writes it, then the copies that aren't permanent in the game's colours for them.
        /// </summary>
        internal static string ItemText(int index, int permanent, int ethereal, int temporary)
        {
            int total = permanent + ethereal + temporary;
            string text = ItemName((ItemIndex)index, total > 1 ? "(" + total + ")" : string.Empty);
            // All copies of one non-permanent kind: "Soldier's Syringe(2) (Temporary)".
            if (permanent == 0 && (ethereal == 0) != (temporary == 0))
                return text + " (" + Tokens.KindName(ethereal > 0 ? ItemKind.Ethereal : ItemKind.Temporary) + ")";
            var parts = new List<string>(2);
            if (ethereal > 0) parts.Add(ethereal + " " + Tokens.KindName(ItemKind.Ethereal));
            if (temporary > 0) parts.Add(temporary + " " + Tokens.KindName(ItemKind.Temporary));
            return parts.Count > 0 ? text + " (" + string.Join(", ", parts) + ")" : text;
        }

        internal static string ItemName(ItemIndex item, string suffix = "")
        {
            ItemDef def = ItemCatalog.GetItemDef(item);
            if (!def) return "?";
            PickupDef pickup = PickupCatalog.GetPickupDef(PickupCatalog.FindPickupIndex(item));
            return Colored(Language.GetString(def.nameToken) + suffix, pickup != null ? pickup.baseColor : Color.white);
        }

        internal static string EquipmentName(int index)
        {
            EquipmentDef def = EquipmentCatalog.GetEquipmentDef((EquipmentIndex)index);
            if (!def) return "?";
            PickupDef pickup = PickupCatalog.GetPickupDef(PickupCatalog.FindPickupIndex((EquipmentIndex)index));
            return Colored(Language.GetString(def.nameToken), pickup != null ? pickup.baseColor : Color.white);
        }

        private static string Colored(string text, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";
    }
}
