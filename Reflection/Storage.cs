using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using System;
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
        Task GetRelated(DateTime date, Action<ClipboardSnapshot[]> onChanged);
    }

    public class TableClipStorage : IStorage
    {
        CloudTable Table;
        CloudBlobContainer BlobContainer;
        string partitionKey;

        public TableClipStorage()
        {
            partitionKey = ConfigReader.Read<string>("azureStorage.partition");
            var container = ConfigReader.Read<string>("azureStorage.container");
            var connectionString = ConfigReader.Read<string>("azureStorage.connectionString");
            var storageAccount = CloudStorageAccount.Parse(connectionString);

            var blobClient = storageAccount.CreateCloudBlobClient();
            BlobContainer = blobClient.GetContainerReference(container);
            BlobContainer.CreateIfNotExists();

            var tableClient = storageAccount.CreateCloudTableClient();
            Table = tableClient.GetTableReference(container);
            Table.CreateIfNotExists();
        }

        public async Task Save(ClipboardSnapshot snapshot)
        {
            var pointer = new ClipboardSnapshotPointer(partitionKey, snapshot.Time);
            var reversePointer = new ClipboardSnapshotPointerReverse(partitionKey, snapshot.Time);

            var path1 = pointer.GetBlobStoragePath();
            var path2 = reversePointer.GetBlobStoragePath();

            var t1 = pointer.GetTime();
            var t2 = reversePointer.GetTime();



            var blob = BlobContainer.GetBlockBlobReference(pointer.GetBlobStoragePath());
            await blob.UploadFromStreamAsync(snapshot.Serialize());

            var insertOperation = TableOperation.Insert(pointer);
            var result = await Table.ExecuteAsync(insertOperation);

            insertOperation = TableOperation.Insert(reversePointer);
            await Table.ExecuteAsync(insertOperation);
        }

        public Task<ClipboardSnapshot> GetNext(DateTime value) => GetNeighbour(new ClipboardSnapshotPointer(partitionKey, value));
        public Task<ClipboardSnapshot> GetPrevious(DateTime value) => GetNeighbour(new ClipboardSnapshotPointerReverse(partitionKey, value));

        async Task<ClipboardSnapshot> GetNeighbour<T>(T pointer) where T : TableEntity, ISnapshotPointer, new()
        {
            var reverse = pointer is ClipboardSnapshotPointerReverse;

            var rangeQuery = new TableQuery<T>().Where(
                TableQuery.CombineFilters(
                    TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, pointer.PartitionKey),
                    TableOperators.And,
                    TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.GreaterThan, pointer.RowKey)));

            var result = await Table.ExecuteQuerySegmentedAsync(rangeQuery.Take(1), new TableContinuationToken());
            var targetPointer = result.FirstOrDefault();

            if (targetPointer == null)
                return null;


            var blob = BlobContainer.GetBlockBlobReference(targetPointer.GetBlobStoragePath());

            var data = new MemoryStream();
            await blob.DownloadToStreamAsync(data);
            if (data.Length == 0)
                return ClipboardSnapshot.CreateEmptySnapshot(targetPointer.GetTime());

            data.Position = 0;
            return ClipboardSnapshot.Deserialize(data);
        }

        static bool IsFetchingRelated = false;
        public async Task GetRelated(DateTime startDate, Action<ClipboardSnapshot[]> onChanged)
        {
            if (IsFetchingRelated) return;
            IsFetchingRelated = true;

            var list = new LinkedList<ClipboardSnapshot>();
            var currentDate = startDate;

            for (int i = 0; i < 10; i++)
            {
                var item = await GetPrevious(currentDate);
                if (item == null) break;

                if (list.Any() && list.Min(f => f.Time) < item.Time)
                    System.Diagnostics.Debugger.Break();

                list.AddFirst(item);
                currentDate = item.Time;
                onChanged(list.ToArray());
            }

            currentDate = startDate;
            for (int i = 0; i < 10; i++)
            {
                var item = await GetNext(currentDate);
                if (item == null) break;

                if (list.Any() && list.Max(f => f.Time) > item.Time)
                    System.Diagnostics.Debugger.Break();

                list.AddLast(item);
                currentDate = item.Time;
                onChanged(list.ToArray());
            }

            IsFetchingRelated = false;
        }
    }
}
