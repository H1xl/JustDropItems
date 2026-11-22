using System;

namespace JustDropItems
{
    /// <summary>How many copies one action moves: the wheel steps on the client, the clamp on the host (pure logic, level-2 tested).</summary>
    internal static class Amount
    {
        /// <summary>The most one action can move: the movable copies, capped by the host's MaxPerAction.</summary>
        public static int Max(int movable, int maxPerAction) => Math.Min(movable, maxPerAction);

        /// <summary>
        /// One wheel notch: the fast step, a percentage of <paramref name="max"/> (at least 1), or the plain step.
        /// The fast key wins over the percent key.
        /// </summary>
        public static int StepSize(int max, bool fast, bool percent, int wheelStep, int fastStep, int percentStep)
        {
            if (fast) return fastStep;
            // share of what one action can move; Mathf.RoundToInt = Math.Round (to even on .5)
            if (percent) return Math.Max(1, (int)Math.Round(max * percentStep / 100f));
            return wheelStep;
        }

        /// <summary>
        /// The amount after one notch (<paramref name="dir"/> = +1 / -1), kept in 1..<paramref name="max"/> (max ≥ 1).
        /// A border between kinds stops the amount with any step; <paramref name="stoppedAtBorder"/> tells the caller to
        /// hold it there for a moment.
        /// </summary>
        public static int Next(int amount, int max, int dir, int step, int[] borders, out bool stoppedAtBorder)
        {
            stoppedAtBorder = false;
            int current = Math.Min(amount, max);
            int next = Clamp(current + dir * step, 1, max);
            if (current == 0 && dir < 0) next = 1;

            // The nearest border crossed in the direction of the step wins (a big step can cross two).
            int stop = next;
            foreach (int b in borders)
            {
                bool hit = dir > 0 ? current < b && b <= next : next <= b && b < current;
                if (!hit) continue;
                if (!stoppedAtBorder || (dir > 0 ? b < stop : b > stop)) stop = b;
                stoppedAtBorder = true;
            }
            return stop;
        }

        /// <summary>The host never trusts the client's amount: 1..min(movable, MaxPerAction).</summary>
        public static int ClampRequest(int requested, int movable, int maxPerAction) =>
            Clamp(requested, 1, Math.Min(movable, maxPerAction));

        /// <summary>Mathf.Clamp(int): the minimum wins when min &gt; max.</summary>
        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            return value > max ? max : value;
        }
    }
}
