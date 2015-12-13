using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace Reflection.Tests
{
    [TestClass]
    public class StorageTests
    {
        [TestMethod]
        public void TestsStorageNextAndPrev()
        {
            var snapps = Enumerable.Range(0, 10).Select(f => new ClipboardSnapshot
            {
                Time = DateTime.UtcNow.AddSeconds(f),
                Data = new SnapshotDataBase[] { new SnapshotStringData { Format = System.Windows.DataFormats.UnicodeText, Data = "UnitTest" + f } }
            }).ToArray();

            var tableClipStorage = new TableClipStorage();
            foreach (var item in snapps)
            {
                tableClipStorage.Save(item).Wait();
            }

            for (int i = 0; i < snapps.Length - 1; i++)
            {
                var next = tableClipStorage.GetNext(snapps[i].Time);
                Assert.AreEqual(next.Result.Time, snapps[i + 1].Time);
            }

            for (int i = snapps.Length - 1; i > 0; i--)
            {
                var next = tableClipStorage.GetPrevious(snapps[i].Time);
                Assert.AreEqual(next.Result.Time, snapps[i - 1].Time);
            }
        }

        [TestMethod]
        public void TestsStorageRelated()
        {
            var snapps = Enumerable.Range(0, 40).Select(f => new ClipboardSnapshot
            {
                Time = DateTime.UtcNow.AddSeconds(f),
                Data = new SnapshotDataBase[] { new SnapshotStringData { Format = System.Windows.DataFormats.UnicodeText, Data = "UnitTest" + f } }
            }).ToArray();

            var tableClipStorage = new TableClipStorage();
            foreach (var item in snapps)
            {
                tableClipStorage.Save(item).Wait();
            }

            ClipboardSnapshot[] list = null;
            tableClipStorage.GetRelated(snapps[snapps.Length / 2].Time, f => list = f).Wait();

            for (int i = 0; i < list.Length; i++)
            {
                if (i > 0)
                    Assert.IsTrue(list[i].Time > list[i - 1].Time);

                Assert.IsTrue(snapps.Any(f => f.Time == list[i].Time));
            }
        }
    }
}
