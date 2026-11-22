using System;
using System.Collections.Generic;

namespace JustDropItems
{
    /// <summary>
    /// One player's marks: how many copies of each item carry a mark (pure logic, level-2 tested; <see cref="MarkLedger"/>
    /// keeps one per player). Marks never outnumber the copies, so old marks never stick to fresh copies picked up later.
    /// </summary>
    internal sealed class MarkCounts<TItem>
    {
        private readonly Dictionary<TItem, int> marks = new Dictionary<TItem, int>();
        private static readonly List<TItem> keys = new List<TItem>();

        public int Count => marks.Count;

        public int Of(TItem item) => marks.TryGetValue(item, out int n) ? n : 0;

        public void Add(TItem item, int count)
        {
            if (count <= 0) return;
            marks.TryGetValue(item, out int have);
            marks[item] = have + count;
        }

        /// <summary>
        /// Takes up to <paramref name="amount"/> marks for copies leaving a stack of <paramref name="stackBefore"/> copies:
        /// marked copies leave first. Returns how many of the leaving copies are marked.
        /// </summary>
        public int Take(TItem item, int stackBefore, int amount)
        {
            if (!marks.TryGetValue(item, out int have)) return 0;
            have = Math.Min(have, stackBefore);
            int taken = Math.Min(have, amount);
            have -= taken;
            if (have > 0) marks[item] = have;
            else marks.Remove(item);
            return taken;
        }

        /// <summary>Lowers every item's marks to its stack size now (<paramref name="stackOf"/>); an empty stack drops its marks.</summary>
        /// <remarks>Runs on every inventory change: pass a static (non-capturing) <paramref name="stackOf"/>, no allocation.</remarks>
        public void Clamp<TState>(TState state, Func<TState, TItem, int> stackOf)
        {
            if (marks.Count == 0) return;
            keys.Clear();
            keys.AddRange(marks.Keys);
            foreach (TItem item in keys)
            {
                int stack = stackOf(state, item);
                if (stack <= 0) marks.Remove(item);
                else if (marks[item] > stack) marks[item] = stack;
            }
        }
    }
}
