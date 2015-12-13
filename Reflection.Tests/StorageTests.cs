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
                Date = DateTime.UtcNow.AddSeconds(f),
                Data = new SnapshotDataBase[] { new SnapshotStringData { Format = System.Windows.DataFormats.UnicodeText, Data = "UnitTest" + f } }
            }).ToArray();

            var tableClipStorage = new TableClipStorage();
            foreach (var item in snapps)
            {
                tableClipStorage.Save(item).Wait();
            }

            for (int i = 0; i < snapps.Length - 1; i++)
            {
                var next = tableClipStorage.GetNext(snapps[i].Date);
                Assert.AreEqual(next.Result.Date, snapps[i + 1].Date);
            }

            for (int i = snapps.Length - 1; i > 0; i--)
            {
                var next = tableClipStorage.GetPrevious(snapps[i].Date);
                Assert.AreEqual(next.Result.Date, snapps[i - 1].Date);
            }
        }

        [TestMethod]
        public void TestsStorageRelated()
        {
            var snapps = Enumerable.Range(0, 40).Select(f => new ClipboardSnapshot
            {
                Date = DateTime.UtcNow.AddSeconds(f),
                Data = new SnapshotDataBase[] { new SnapshotStringData { Format = System.Windows.DataFormats.UnicodeText, Data = "UnitTest" + f } }
            }).ToArray();

            var tableClipStorage = new TableClipStorage();
            foreach (var item in snapps)
            {
                tableClipStorage.Save(item).Wait();
            }

            ClipboardSnapshotPointer[] list = null;
            tableClipStorage.GetRelated(snapps[snapps.Length / 2].Date, f => list = f).Wait();

            for (int i = 0; i < list.Length; i++)
            {
                if (i > 0)
                    Assert.IsTrue(list[i].Date > list[i - 1].Date);

                Assert.IsTrue(snapps.Any(f => f.Date == list[i].Date));
            }
        }
    }
}
