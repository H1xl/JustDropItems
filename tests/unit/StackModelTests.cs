using Xunit;

namespace JustDropItems.Tests
{
    public class StackModelTests
    {
        private static StackModel Stack(int p, int e, int t, int s = 0, bool eOk = true, bool tOk = true,
                                        KindOrder order = KindOrder.PermanentEtherealTemporary) =>
            new StackModel { permanent = p, ethereal = e, temporary = t, shared = s, etherealAllowed = eOk, temporaryAllowed = tOk, order = order };

        private static (int p, int e, int t) Split(StackModel m, int amount)
        {
            m.Split(amount, out int p, out int e, out int t);
            return (p, e, t);
        }

        [Fact]
        public void Kinds_EveryOrderHasAllThreeOnce()
        {
            for (int o = 0; o < 6; o++)
            {
                ItemKind[] k = StackModel.Kinds((KindOrder)o);
                Assert.Equal(3, k.Length);
                Assert.Contains(ItemKind.Permanent, k);
                Assert.Contains(ItemKind.Ethereal, k);
                Assert.Contains(ItemKind.Temporary, k);
            }
        }

        [Fact]
        public void Kinds_UnknownOrderFromClient_FallsBackToDefault()
        {
            Assert.Equal(StackModel.Kinds(KindOrder.PermanentEtherealTemporary), StackModel.Kinds((KindOrder)200));
        }

        [Fact]
        public void TotalAndMovable_SharedNeverMoves()
        {
            StackModel m = Stack(2, 3, 4, s: 5);
            Assert.Equal(14, m.Total);
            Assert.Equal(9, m.Movable);
        }

        [Fact]
        public void Movable_ForbiddenKindsLeftOut()
        {
            Assert.Equal(2, Stack(2, 3, 4, eOk: false, tOk: false).Movable);
            Assert.Equal(6, Stack(2, 3, 4, eOk: false).Movable);
        }

        [Fact]
        public void Allowed_PermanentAlwaysSharedNever()
        {
            StackModel m = Stack(1, 1, 1, 1, eOk: false, tOk: false);
            Assert.True(m.Allowed(ItemKind.Permanent));
            Assert.False(m.Allowed(ItemKind.Ethereal));
            Assert.False(m.Allowed(ItemKind.Temporary));
            Assert.False(Stack(1, 1, 1, 1).Allowed(ItemKind.Shared));
        }

        [Fact]
        public void Split_FollowsOrder()
        {
            Assert.Equal((2, 1, 0), Split(Stack(2, 3, 4), 3));
            Assert.Equal((0, 0, 3), Split(Stack(2, 3, 4, order: KindOrder.TemporaryEtherealPermanent), 3));
            Assert.Equal((0, 2, 4), Split(Stack(2, 3, 4, order: KindOrder.TemporaryEtherealPermanent), 6));
            Assert.Equal((1, 3, 4), Split(Stack(2, 3, 4, order: KindOrder.EtherealTemporaryPermanent), 8));
        }

        [Fact]
        public void Split_SkipsForbiddenKind()
        {
            Assert.Equal((2, 0, 2), Split(Stack(2, 3, 4, eOk: false), 4));
        }

        [Fact]
        public void Split_MoreThanMovable_TakesAllMovable()
        {
            Assert.Equal((2, 3, 4), Split(Stack(2, 3, 4, s: 9), 100));
        }

        [Fact]
        public void Split_Zero_Nothing() // callers never pass a negative amount (the host clamps it to 1..movable)
        {
            Assert.Equal((0, 0, 0), Split(Stack(2, 3, 4), 0));
        }

        [Fact]
        public void Borders_EndOfEachKindButTheLast()
        {
            Assert.Equal(new[] { 2, 5 }, Stack(2, 3, 4).Borders());
            Assert.Equal(new[] { 4, 7 }, Stack(2, 3, 4, order: KindOrder.TemporaryEtherealPermanent).Borders());
        }

        [Fact]
        public void Borders_EmptyOrForbiddenKindsMakeNoBorder()
        {
            Assert.Equal(new[] { 2 }, Stack(2, 0, 4).Borders());
            Assert.Equal(new[] { 2 }, Stack(2, 3, 4, eOk: false).Borders());
            Assert.Empty(Stack(5, 0, 0, s: 3).Borders());
            Assert.Empty(Stack(0, 0, 0).Borders());
        }
    }
}
