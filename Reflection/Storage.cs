using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Reflection
{
    public interface IStorage
    {
        Task<ClipboardSnapshot> GetNext(DateTime value);
        Task<ClipboardSnapshot> GetPrevious(DateTime value);
        Task Save(ClipboardSnapshot snapshot);
        Task GetRelated(DateTime date, Action<ClipboardSnapshotPointer[]> onChanged);
    }

    public class TableClipStorage : IStorage
    {
        CloudTable Table;
        CloudBlobContainer BlobContainer;
        string basePartitonKey;

        SortedSet<ClipboardSnapshotPointer> KnownPointers = new SortedSet<ClipboardSnapshotPointer>();
        ConcurrentDictionary<ClipboardSnapshotPointer, ClipboardSnapshot> Cache = new ConcurrentDictionary<ClipboardSnapshotPointer, ClipboardSnapshot>();

        public TableClipStorage()
        {
            basePartitonKey = ConfigReader.Read<string>("azureStorage.partition");
            var container = ConfigReader.Read<string>("azureStorage.container");
            var connectionString = ConfigReader.Read<string>("azureStorage.connectionString");
            var storageAccount = CloudStorageAccount.Parse(connectionString);

            var blobClient = storageAccount.CreateCloudBlobClient();
            BlobContainer = blobClient.GetContainerReference(container);
            BlobContainer.CreateIfNotExists();

            var tableClient = storageAccount.CreateCloudTableClient();
            Table = tableClient.GetTableReference(container);
            Table.CreateIfNotExists();

            LoadDay(DateTime.Now);
            LoadDay(DateTime.Now.AddDays(-1));
            LoadDay(DateTime.Now.AddDays(1));
        }

        string GetPartitionKey(DateTime date) => basePartitonKey + "-" + date.ToUniversalTime().ToString("yyyyMMdd");

        public async Task Save(ClipboardSnapshot snapshot)
        {
            var pointer = new ClipboardSnapshotPointer(GetPartitionKey(snapshot.Date), snapshot.Date, snapshot.Data.Select(f => f.Format).ToArray());
            snapshot.Id = pointer.GetId();
            KnownPointers.Add(pointer);
            Cache[pointer] = snapshot;

            var blob = BlobContainer.GetBlockBlobReference(pointer.GetId());
            await blob.UploadFromStreamAsync(snapshot.Serialize());

            var insertOperation = TableOperation.Insert(pointer);
            var result = await Table.ExecuteAsync(insertOperation);
        }

        public async Task<ClipboardSnapshot> GetNext(DateTime time)
        {
            var targetPointer = KnownPointers.OrderBy(f => f.Date).FirstOrDefault(f => f.Date > time);

            if (targetPointer == null)
                return null;

            return await GetSnapshot(targetPointer);
        }

        public async Task<ClipboardSnapshot> GetPrevious(DateTime time)
        {
            var targetPointer = KnownPointers.OrderByDescending(f => f.Date).FirstOrDefault(f => f.Date < time);

            if (targetPointer == null)
                return null;

            return await GetSnapshot(targetPointer);
        }

        async Task LoadDay(DateTime time)
        {
            var rangeQuery = new TableQuery<ClipboardSnapshotPointer>().Where(
                TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, GetPartitionKey(time)));

            var result = await Table.ExecuteQuerySegmentedAsync(rangeQuery, new TableContinuationToken());

            foreach (var item in result)
            {
                lock (KnownPointers)
                {
                    KnownPointers.Add(item);
                }
            }
        }

        async Task<ClipboardSnapshot> GetSnapshot(ClipboardSnapshotPointer pointer)
        {
            ClipboardSnapshot snapshot;
            if (Cache.TryGetValue(pointer, out snapshot))
                return snapshot;

            var blob = BlobContainer.GetBlockBlobReference(pointer.GetId());

            var data = new MemoryStream();
            await blob.DownloadToStreamAsync(data);
            if (data.Length == 0)
                return ClipboardSnapshot.CreateEmptySnapshot(pointer.Date, pointer.GetId());

            data.Position = 0;
            snapshot = ClipboardSnapshot.Deserialize(data);
            snapshot.Id = pointer.GetId();
            Cache[pointer] = snapshot;
            return snapshot;
        }

        static bool IsFetchingRelated = false;
        public async Task GetRelated(DateTime startDate, Action<ClipboardSnapshotPointer[]> onChanged)
        {
            if (IsFetchingRelated) return;
            IsFetchingRelated = true;

            Action<Task> updateView = task => onChanged(KnownPointers.ToArray());

            await Task.WhenAll(new[] {
                LoadDay(startDate.AddDays(0)).ContinueWith(updateView),
                LoadDay(startDate.AddDays(-1)).ContinueWith(updateView),
                LoadDay(startDate.AddDays(1)).ContinueWith(updateView)
            });

            //IsFetchingRelated = false;
        }
    }
}
