namespace JustDropItems
{
    internal static partial class Tokens
    {
        /// <summary>Why the host refused a request. The value is sent to the requester, who shows the matching line.</summary>
        internal enum Reject : byte
        {
            None, DropDisabled, GiveDisabled, NotYours, Blacklisted, Lunar, Void, Quest, NoCopies, SharedOnly, NeedsBody,
            DeadGiver, TargetDead, TargetSelf, TargetEquipmentFull, NothingThere, TooFast, Hidden, EquipmentDisabled, NoTarget,
            ConsumedNoDrop, ConsumedDisabled, Hallowed, Hex, Page, Meal, EliteAspect,
        }
    }

    /// <summary>The item tiers the rules tell apart (game ItemTier mapped in <see cref="Rules.CheckItem"/>).</summary>
    internal enum TierGroup : byte { Other, NoTier, Lunar, Void, Hallowed, Hex, Page, Meal }

    /// <summary>One item as the rules see it.</summary>
    internal struct ItemFacts
    {
        public bool hidden;
        /// <summary>A used-up item (Empty Bottle).</summary>
        public bool consumed;
        /// <summary>Has the ObjectiveRelated tag.</summary>
        public bool quest;
        /// <summary>In the host's blacklist.</summary>
        public bool blocked;
        public TierGroup tier;
    }

    /// <summary>The allow switches and the decisions made from them (pure logic, level-2 tested).</summary>
    internal sealed partial class Rules
    {
        public bool allowDrop, allowGive, allowGiveToDead, allowGiveWhileDead;
        public bool allowEquipment, allowLunar, allowVoid, allowQuest, allowTemporary, allowEthereal, allowGiveConsumed,
                    allowHallowed, allowHex, allowPages, allowMeals, allowAspects;

        /// <summary>Why an item can't be dropped (or given) under these rules, or <see cref="Tokens.Reject.None"/>.</summary>
        public Tokens.Reject Decide(in ItemFacts item, bool drop)
        {
            if (item.hidden) return Tokens.Reject.Hidden;
            if (item.consumed)
            {
                // A used-up item (Empty Bottle): nobody could do anything with it on the ground, so it can only be given.
                if (drop) return Tokens.Reject.ConsumedNoDrop;
                if (!allowGiveConsumed) return Tokens.Reject.ConsumedDisabled;
                return item.blocked ? Tokens.Reject.Blacklisted : Tokens.Reject.None;
            }
            if (item.tier == TierGroup.NoTier && !item.quest) return Tokens.Reject.Hidden;
            if (item.blocked) return Tokens.Reject.Blacklisted;
            if (item.quest && !allowQuest) return Tokens.Reject.Quest;
            switch (item.tier)
            {
                case TierGroup.Lunar: if (!allowLunar) return Tokens.Reject.Lunar; break;
                case TierGroup.Void: if (!allowVoid) return Tokens.Reject.Void; break;
                case TierGroup.Hallowed: if (!allowHallowed) return Tokens.Reject.Hallowed; break;
                case TierGroup.Hex: if (!allowHex) return Tokens.Reject.Hex; break;
                case TierGroup.Page: if (!allowPages) return Tokens.Reject.Page; break;
                case TierGroup.Meal: if (!allowMeals) return Tokens.Reject.Meal; break;
            }
            return Tokens.Reject.None;
        }

        /// <summary>Why an equipment can't be moved, or <see cref="Tokens.Reject.None"/>.</summary>
        public Tokens.Reject DecideEquipment(bool blocked, bool lunar, bool eliteAspect)
        {
            if (!allowEquipment) return Tokens.Reject.EquipmentDisabled;
            if (blocked) return Tokens.Reject.Blacklisted;
            if (lunar && !allowLunar) return Tokens.Reject.Lunar;
            if (eliteAspect && !allowAspects) return Tokens.Reject.EliteAspect;
            return Tokens.Reject.None;
        }
    }
}
