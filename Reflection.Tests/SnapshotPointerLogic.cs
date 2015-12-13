using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Reflection.Tests
{
    [TestClass]
    public class SnapshotPointerLogic
    {
        [TestMethod]
        public void TestsFormatAsRowKey()
        {
            var expectedDate = DateTime.Now;
            var rowKey = expectedDate.FormatAsRowKey();
            var actualDate = rowKey.FormatTicksAsDatetime();
            Assert.AreEqual(expectedDate.ToUniversalTime(), actualDate);
        }

        [TestMethod]
        public void TestsFormatAsRowKeyUTC()
        {
            var expectedDate = DateTime.UtcNow;
            var rowKey = expectedDate.FormatAsRowKey();
            var actualDate = rowKey.FormatTicksAsDatetime();
            Assert.AreEqual(expectedDate, actualDate);
        }

        [TestMethod]
        public void TestsFormatAsRowKeyReverse()
        {
            var expectedDate = DateTime.Now;
            var rowKey = expectedDate.FormatAsRowKeyReverse();
            var actualDate = rowKey.FormatTicksAsDatetimeReversed();
            Assert.AreEqual(expectedDate.ToUniversalTime(), actualDate);
        }

        [TestMethod]
        public void TestsFormatAsRowKeyReverseUTC()
        {
            var expectedDate = DateTime.UtcNow;
            var rowKey = expectedDate.FormatAsRowKeyReverse();
            var actualDate = rowKey.FormatTicksAsDatetimeReversed();
            Assert.AreEqual(expectedDate, actualDate);
        }

        [TestMethod]
        public void ForwardSortsCorrect()
        {
            var rand = new Random(0);
            var list = Enumerable.Range(0, 1000)
                .Select(i => new
                {
                    date = DateTime.Now.AddHours(rand.NextDouble() * 100),
                    i
                }).Select(f => new
                {
                    f.i,
                    key = f.date.FormatAsRowKey(),
                    f.date
                }).ToArray();

            var expectedOrder = list.OrderBy(f => f.date).ToArray();
            var actualOrder = list.OrderBy(f => f.key).ToArray();

            for (int i = 0; i < expectedOrder.Length; i++)
            {
                if (expectedOrder[i].i != actualOrder[i].i)
                    Assert.Fail();
            }
        }

        [TestMethod]
        public void ForwardSortsCorrectReversed()
        {
            var rand = new Random(0);
            var list = Enumerable.Range(0, 1000)
                .Select(i => new
                {
                    date = DateTime.Now.AddHours(rand.NextDouble() * 100),
                    i
                }).Select(f => new
                {
                    f.i,
                    key = f.date.FormatAsRowKeyReverse(),
                    f.date
                }).ToArray();

            var expectedOrder = list.OrderBy(f => f.date).ToArray();
            var actualOrder = list.OrderBy(f => f.key).ToArray();

            for (int i = 0; i < expectedOrder.Length; i++)
            {
                if (expectedOrder[i].i != actualOrder[expectedOrder.Length - i - 1].i)
                    Assert.Fail();
            }
        }
    }
}
