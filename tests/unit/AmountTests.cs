using System;
using Xunit;

namespace JustDropItems.Tests
{
    public class AmountTests
    {
        private static readonly int[] None = Array.Empty<int>();

        private static int Next(int amount, int max, int dir, int step, int[] borders = null) =>
            Amount.Next(amount, max, dir, step, borders ?? None, out _);

        [Fact]
        public void StepSize_PlainFastPercent()
        {
            Assert.Equal(1, Amount.StepSize(50, false, false, wheelStep: 1, fastStep: 5, percentStep: 10));
            Assert.Equal(5, Amount.StepSize(50, true, false, 1, 5, 10));
            Assert.Equal(5, Amount.StepSize(50, false, true, 1, 5, 10));
        }

        [Fact]
        public void StepSize_FastWinsOverPercent()
        {
            Assert.Equal(7, Amount.StepSize(1000, true, true, 1, 7, 10));
        }

        [Theory]
        [InlineData(3, 10, 1)]   // 0.3 → at least 1
        [InlineData(25, 10, 2)]  // 2.5 → 2 (to even, as Mathf.RoundToInt)
        [InlineData(35, 10, 4)]  // 3.5 → 4
        [InlineData(1000, 10, 100)]
        public void StepSize_PercentOfMax(int max, int percent, int expected)
        {
            Assert.Equal(expected, Amount.StepSize(max, false, true, 1, 5, percent));
        }

        [Fact]
        public void Next_StaysInOneToMax()
        {
            Assert.Equal(10, Next(8, 10, +1, 5));
            Assert.Equal(1, Next(3, 10, -1, 5));
            Assert.Equal(4, Next(3, 10, +1, 1));
        }

        [Fact]
        public void Next_AmountAboveNewMax_StartsFromMax() // the stack shrank since the last notch
        {
            Assert.Equal(4, Next(20, 5, -1, 1));
            Assert.Equal(5, Next(20, 5, +1, 1));
        }

        [Fact]
        public void Next_FromZeroDown_GoesToOne()
        {
            Assert.Equal(1, Next(0, 10, -1, 5));
            Assert.Equal(5, Next(0, 10, +1, 5));
        }

        [Fact]
        public void Next_StopsAtBorderBothWays()
        {
            int[] borders = { 3, 6 };
            Assert.Equal(3, Amount.Next(1, 10, +1, 5, borders, out bool hitUp));
            Assert.True(hitUp);
            Assert.Equal(6, Amount.Next(9, 10, -1, 5, borders, out bool hitDown));
            Assert.True(hitDown);
        }

        [Fact]
        public void Next_LeavingABorder_NotStoppedByIt()
        {
            int[] borders = { 3 };
            Assert.Equal(4, Amount.Next(3, 10, +1, 1, borders, out bool up));
            Assert.False(up);
            Assert.Equal(2, Amount.Next(3, 10, -1, 1, borders, out bool down));
            Assert.False(down);
        }

        [Fact]
        public void Next_LandingExactlyOnBorder_CountsAsStop()
        {
            Assert.Equal(3, Amount.Next(2, 10, +1, 1, new[] { 3 }, out bool hit));
            Assert.True(hit);
        }

        [Fact]
        public void Next_StepOverTwoBorders_StopsAtTheNearerOne()
        {
            int[] borders = { 3, 6 }; // as StackModel.Borders(): ascending
            Assert.Equal(3, Next(1, 10, +1, 9, borders));
            Assert.Equal(6, Next(9, 10, -1, 9, borders));
        }

        [Fact]
        public void Next_BorderOrderDoesntMatter()
        {
            int[] borders = { 6, 3 };
            Assert.Equal(3, Next(1, 10, +1, 9, borders));
            Assert.Equal(6, Next(9, 10, -1, 9, borders));
        }

        [Fact]
        public void ClampRequest_TrustsNothing()
        {
            Assert.Equal(5, Amount.ClampRequest(5, 10, 100));
            Assert.Equal(10, Amount.ClampRequest(9999, 10, 100));
            Assert.Equal(1, Amount.ClampRequest(-7, 10, 100));
            Assert.Equal(1, Amount.ClampRequest(0, 10, 100));
            Assert.Equal(3, Amount.ClampRequest(50, 10, 3));
        }

        [Fact]
        public void Max_MovableCappedByRule()
        {
            Assert.Equal(4, Amount.Max(4, 100));
            Assert.Equal(100, Amount.Max(400, 100));
            Assert.Equal(0, Amount.Max(0, 100));
        }
    }
}
