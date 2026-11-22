using System.Collections.Generic;
using RoR2;
using UnityEngine.Networking;

namespace JustDropItems
{
    /// <summary>
    /// The host's rules. The host builds them from its config and sends them to every player who has the mod, so the UI can
    /// show what is allowed. The host itself always checks a request against its own config, never against a client's copy.
    /// </summary>
    internal sealed partial class Rules
    {
        // The allow switches and the item decisions: plugin/Logic/ItemRules.cs.
        public TakeFromMode takeFrom;
        public int maxPerAction = 100;
        public readonly HashSet<ItemIndex> blockedItems = new HashSet<ItemIndex>();
        public readonly HashSet<EquipmentIndex> blockedEquipment = new HashSet<EquipmentIndex>();

        /// <summary>The rules from the local config (used by the host).</summary>
        public static Rules FromConfig()
        {
            var r = new Rules
            {
                takeFrom = Settings.takeFrom.Value,
                allowDrop = Settings.allowDrop.Value,
                allowGive = Settings.allowGive.Value,
                allowGiveToDead = Settings.allowGiveToDead.Value,
                allowGiveWhileDead = Settings.allowGiveWhileDead.Value,
                maxPerAction = Settings.maxPerAction.Value,
                allowEquipment = Settings.allowEquipment.Value,
                allowLunar = Settings.allowLunar.Value,
                allowVoid = Settings.allowVoid.Value,
                allowQuest = Settings.allowQuest.Value,
                allowTemporary = Settings.allowTemporary.Value,
                allowEthereal = Settings.allowEthereal.Value,
                allowGiveConsumed = Settings.allowGiveConsumed.Value,
                allowHallowed = Settings.allowHallowed.Value,
                allowHex = Settings.allowHex.Value,
                allowPages = Settings.allowPages.Value,
                allowMeals = Settings.allowMeals.Value,
                allowAspects = Settings.allowAspects.Value,
            };
            foreach (ItemIndex i in Settings.BlockedItems) r.blockedItems.Add(i);
            foreach (EquipmentIndex e in Settings.BlockedEquipment) r.blockedEquipment.Add(e);
            return r;
        }

        public void Write(NetworkWriter w)
        {
            w.Write((byte)takeFrom);
            byte flags = 0;
            if (allowDrop) flags |= 1;
            if (allowGive) flags |= 2;
            if (allowGiveToDead) flags |= 4;
            if (allowGiveWhileDead) flags |= 8;
            if (allowEquipment) flags |= 16;
            if (allowLunar) flags |= 32;
            if (allowVoid) flags |= 64;
            if (allowQuest) flags |= 128;
            w.Write(flags);
            byte kinds = 0;
            if (allowTemporary) kinds |= 1;
            if (allowEthereal) kinds |= 2;
            if (allowGiveConsumed) kinds |= 4;
            if (allowHallowed) kinds |= 8;
            if (allowHex) kinds |= 16;
            if (allowPages) kinds |= 32;
            if (allowMeals) kinds |= 64;
            if (allowAspects) kinds |= 128;
            w.Write(kinds);
            w.WritePackedUInt32((uint)maxPerAction);
            w.WritePackedUInt32((uint)blockedItems.Count);
            foreach (ItemIndex i in blockedItems) w.WritePackedUInt32((uint)i);
            w.WritePackedUInt32((uint)blockedEquipment.Count);
            foreach (EquipmentIndex e in blockedEquipment) w.WritePackedUInt32((uint)e);
        }

        public static Rules Read(NetworkReader r)
        {
            var rules = new Rules { takeFrom = (TakeFromMode)r.ReadByte() };
            byte flags = r.ReadByte();
            rules.allowDrop = (flags & 1) != 0;
            rules.allowGive = (flags & 2) != 0;
            rules.allowGiveToDead = (flags & 4) != 0;
            rules.allowGiveWhileDead = (flags & 8) != 0;
            rules.allowEquipment = (flags & 16) != 0;
            rules.allowLunar = (flags & 32) != 0;
            rules.allowVoid = (flags & 64) != 0;
            rules.allowQuest = (flags & 128) != 0;
            byte kinds = r.ReadByte();
            rules.allowTemporary = (kinds & 1) != 0;
            rules.allowEthereal = (kinds & 2) != 0;
            rules.allowGiveConsumed = (kinds & 4) != 0;
            rules.allowHallowed = (kinds & 8) != 0;
            rules.allowHex = (kinds & 16) != 0;
            rules.allowPages = (kinds & 32) != 0;
            rules.allowMeals = (kinds & 64) != 0;
            rules.allowAspects = (kinds & 128) != 0;
            rules.maxPerAction = (int)r.ReadPackedUInt32();
            uint n = r.ReadPackedUInt32();
            for (uint k = 0; k < n; k++) rules.blockedItems.Add((ItemIndex)r.ReadPackedUInt32());
            n = r.ReadPackedUInt32();
            for (uint k = 0; k < n; k++) rules.blockedEquipment.Add((EquipmentIndex)r.ReadPackedUInt32());
            return rules;
        }

        /// <summary>Why an item can't be dropped (or given) under these rules, or <see cref="Tokens.Reject.None"/>.</summary>
        public Tokens.Reject CheckItem(ItemIndex item, bool drop)
        {
            ItemDef def = ItemCatalog.GetItemDef(item);
            if (!def) return Tokens.Reject.NothingThere;
            var facts = new ItemFacts
            {
                hidden = def.hidden,
                consumed = def.isConsumed,
                quest = def.ContainsTag(ItemTag.ObjectiveRelated),
                blocked = blockedItems.Contains(item),
                tier = GroupOf(def.tier),
            };
            return Decide(in facts, drop);
        }

        private static TierGroup GroupOf(ItemTier tier)
        {
            switch (tier)
            {
                case ItemTier.NoTier: return TierGroup.NoTier;
                case ItemTier.Lunar: return TierGroup.Lunar;
                case ItemTier.VoidTier1: case ItemTier.VoidTier2: case ItemTier.VoidTier3: case ItemTier.VoidBoss: return TierGroup.Void;
                case ItemTier.Cursed: return TierGroup.Hallowed; // shown in game as "Hallowed"
                case ItemTier.Hex: return TierGroup.Hex;
                case ItemTier.Page: return TierGroup.Page;
                case ItemTier.FoodTier: return TierGroup.Meal;
                default: return TierGroup.Other;
            }
        }

        public Tokens.Reject CheckEquipment(EquipmentIndex equipment)
        {
            EquipmentDef def = EquipmentCatalog.GetEquipmentDef(equipment);
            if (!def) return Tokens.Reject.NothingThere;
            return DecideEquipment(blockedEquipment.Contains(equipment), def.isLunar,
                                   def.passiveBuffDef && def.passiveBuffDef.eliteDef);
        }
    }
}
