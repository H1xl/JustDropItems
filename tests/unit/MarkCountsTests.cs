using System.Collections.Generic;
using Xunit;

namespace JustDropItems.Tests
{
    public class MarkCountsTests
    {
        private static int StackOf(Dictionary<int, int> stacks, int item) => stacks.TryGetValue(item, out int n) ? n : 0;

        [Fact]
        public void Add_Accumulates_IgnoresZeroAndNegative()
        {
            var m = new MarkCounts<int>();
            m.Add(1, 2);
            m.Add(1, 3);
            m.Add(2, 0);
            m.Add(3, -4);
            Assert.Equal(5, m.Of(1));
            Assert.Equal(0, m.Of(2));
            Assert.Equal(1, m.Count);
        }

        [Fact]
        public void Take_MarkedCopiesLeaveFirst()
        {
            var m = new MarkCounts<int>();
            m.Add(1, 2);
            Assert.Equal(2, m.Take(1, stackBefore: 5, amount: 3)); // 3 leave: both marked ones among them
            Assert.Equal(0, m.Of(1));
            Assert.Equal(0, m.Count); // emptied entries are removed
        }

        [Fact]
        public void Take_PartOfTheMarks()
        {
            var m = new MarkCounts<int>();
            m.Add(1, 4);
            Assert.Equal(1, m.Take(1, 10, 1));
            Assert.Equal(3, m.Of(1));
        }

        [Fact]
        public void Take_MarksAboveStackAreCappedFirst()
        {
            // Stale marks (stack shrank, Clamp not run yet): never more marked copies than the stack had.
            var m = new MarkCounts<int>();
            m.Add(1, 5);
            Assert.Equal(2, m.Take(1, stackBefore: 2, amount: 9));
            Assert.Equal(0, m.Of(1));
        }

        [Fact]
        public void Take_RestAboveStackIsDroppedToo()
        {
            var m = new MarkCounts<int>();
            m.Add(1, 5);
            Assert.Equal(1, m.Take(1, stackBefore: 3, amount: 1));
            Assert.Equal(2, m.Of(1)); // min(5, 3) - 1
        }

        [Fact]
        public void Take_UnmarkedItem_Nothing()
        {
            var m = new MarkCounts<int>();
            Assert.Equal(0, m.Take(7, 3, 3));
        }

        [Fact]
        public void Clamp_LowersToStackAndDropsEmptyStacks()
        {
            var m = new MarkCounts<int>();
            m.Add(1, 5);
            m.Add(2, 2);
            m.Add(3, 1);
            var stacks = new Dictionary<int, int> { [1] = 3, [2] = 4 }; // item 3 is gone
            m.Clamp(stacks, StackOf);
            Assert.Equal(3, m.Of(1));
            Assert.Equal(2, m.Of(2));
            Assert.Equal(0, m.Of(3));
            Assert.Equal(2, m.Count);
        }

        [Fact]
        public void Clamp_Empty_DoesntAskForStacks()
        {
            var m = new MarkCounts<int>();
            int asked = 0;
            m.Clamp(0, (_, item) => { asked++; return 0; });
            Assert.Equal(0, asked);
        }

        [Fact]
        public void FreshCopiesAfterShrink_StayUnmarked()
        {
            // 3 rerolled copies, 3 used by a printer (Clamp → 0), then 2 fresh copies picked up: dropping them carries no mark.
            var m = new MarkCounts<int>();
            m.Add(1, 3);
            m.Clamp(new Dictionary<int, int>(), StackOf);
            Assert.Equal(0, m.Take(1, stackBefore: 2, amount: 2));
        }
    }
}
