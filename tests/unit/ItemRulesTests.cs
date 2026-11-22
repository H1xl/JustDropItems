using Xunit;
using R = JustDropItems.Tokens.Reject;

namespace JustDropItems.Tests
{
    public class ItemRulesTests
    {
        private static Rules AllowAll() => new Rules
        {
            allowEquipment = true, allowLunar = true, allowVoid = true, allowQuest = true, allowGiveConsumed = true,
            allowHallowed = true, allowHex = true, allowPages = true, allowMeals = true, allowAspects = true,
        };

        private static ItemFacts Item(TierGroup tier = TierGroup.Other, bool hidden = false, bool consumed = false,
                                      bool quest = false, bool blocked = false) =>
            new ItemFacts { tier = tier, hidden = hidden, consumed = consumed, quest = quest, blocked = blocked };

        private static R Drop(Rules r, ItemFacts f) => r.Decide(in f, drop: true);
        private static R Give(Rules r, ItemFacts f) => r.Decide(in f, drop: false);

        [Fact]
        public void PlainItem_Allowed()
        {
            Assert.Equal(R.None, Drop(new Rules(), Item()));
            Assert.Equal(R.None, Give(new Rules(), Item()));
        }

        [Fact]
        public void Hidden_AlwaysRefusedFirst()
        {
            Assert.Equal(R.Hidden, Drop(AllowAll(), Item(hidden: true, consumed: true, blocked: true)));
        }

        [Fact]
        public void NoTier_HiddenUnlessQuest()
        {
            Assert.Equal(R.Hidden, Drop(AllowAll(), Item(TierGroup.NoTier)));
            Assert.Equal(R.None, Drop(AllowAll(), Item(TierGroup.NoTier, quest: true)));
        }

        [Fact]
        public void Consumed_NeverDropped()
        {
            Assert.Equal(R.ConsumedNoDrop, Drop(AllowAll(), Item(consumed: true)));
        }

        [Fact]
        public void Consumed_GivenOnlyWhenAllowedAndNotBlocked()
        {
            Assert.Equal(R.None, Give(AllowAll(), Item(consumed: true)));
            Assert.Equal(R.ConsumedDisabled, Give(new Rules(), Item(consumed: true)));
            Assert.Equal(R.Blacklisted, Give(AllowAll(), Item(consumed: true, blocked: true)));
        }

        [Fact]
        public void Consumed_TierRulesDontApply()
        {
            var r = new Rules { allowGiveConsumed = true }; // lunar not allowed
            Assert.Equal(R.None, Give(r, Item(TierGroup.Lunar, consumed: true)));
        }

        [Fact]
        public void Blacklist_BeforeQuestAndTier()
        {
            Assert.Equal(R.Blacklisted, Drop(new Rules(), Item(TierGroup.Lunar, quest: true, blocked: true)));
        }

        [Fact]
        public void Quest_NeedsSwitch()
        {
            Assert.Equal(R.Quest, Drop(new Rules(), Item(quest: true)));
            Assert.Equal(R.None, Drop(new Rules { allowQuest = true }, Item(quest: true)));
        }

        [Theory]
        [InlineData((byte)TierGroup.Lunar, (byte)R.Lunar)]
        [InlineData((byte)TierGroup.Void, (byte)R.Void)]
        [InlineData((byte)TierGroup.Hallowed, (byte)R.Hallowed)]
        [InlineData((byte)TierGroup.Hex, (byte)R.Hex)]
        [InlineData((byte)TierGroup.Page, (byte)R.Page)]
        [InlineData((byte)TierGroup.Meal, (byte)R.Meal)]
        public void TierSwitches(byte tierValue, byte refusalValue) // internal enums can't be public test parameters
        {
            var tier = (TierGroup)tierValue;
            var refusal = (R)refusalValue;
            Assert.Equal(refusal, Drop(new Rules(), Item(tier)));
            Assert.Equal(refusal, Give(new Rules(), Item(tier)));
            Assert.Equal(R.None, Drop(AllowAll(), Item(tier)));
        }

        [Fact]
        public void Equipment_Order()
        {
            Assert.Equal(R.EquipmentDisabled, new Rules().DecideEquipment(true, true, true));
            Assert.Equal(R.Blacklisted, new Rules { allowEquipment = true }.DecideEquipment(true, true, true));
            Assert.Equal(R.Lunar, new Rules { allowEquipment = true }.DecideEquipment(false, true, true));
            Assert.Equal(R.EliteAspect, new Rules { allowEquipment = true }.DecideEquipment(false, false, true));
            Assert.Equal(R.None, new Rules { allowEquipment = true }.DecideEquipment(false, false, false));
            Assert.Equal(R.None, AllowAll().DecideEquipment(false, true, true));
        }
    }
}
