using Microsoft.WindowsAzure.Storage.Table;
using ProtoBuf;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Reflection
{
    public interface ISnapshotPointer
    {
        string GetBlobStoragePath();
        DateTime GetTime();
    }

    public class ClipboardSnapshotPointer : TableEntity, ISnapshotPointer
    {
        public ClipboardSnapshotPointer() { }
        public ClipboardSnapshotPointer(string partitionKey, DateTime time)
        {
            PartitionKey = partitionKey;
            RowKey = time.FormatAsRowKey();
        }

        public string GetBlobStoragePath() => PartitionKey + "/" + RowKey.FormatTicksAsDatetime().Ticks;
        public DateTime GetTime() => RowKey.FormatTicksAsDatetime();
    }

    public class ClipboardSnapshotPointerReverse : TableEntity, ISnapshotPointer
    {
        public ClipboardSnapshotPointerReverse() { }
        public ClipboardSnapshotPointerReverse(string partitionKey, DateTime time)
        {
            PartitionKey = partitionKey + "-reversed";
            RowKey = time.FormatAsRowKeyReverse();
        }

        public string GetBlobStoragePath() => PartitionKey.Replace("-reversed", "") + "/" + RowKey.FormatTicksAsDatetimeReversed().Ticks;
        public DateTime GetTime() => RowKey.FormatTicksAsDatetimeReversed();
    }

    [ProtoContract]
    [ProtoInclude(10, typeof(SnapshotStringData))]
    [ProtoInclude(20, typeof(SnapshotStringArrayData))]
    [ProtoInclude(30, typeof(SnapshotRawData))]
    [ProtoInclude(40, typeof(SnapshotBoolData))]
    [ProtoInclude(50, typeof(SnapshotBitmapData))]
    [ProtoInclude(60, typeof(SnapshotBitmapDrawingData))]
    [ProtoInclude(70, typeof(SnapshotNullData))]
    public abstract class SnapshotDataBase
    {
        [ProtoMember(1)]
        public string Format { get; set; }
        [ProtoMember(2)]
        public bool IsConverted { get; set; }

        public abstract object GetClipboardData();
        public override int GetHashCode() => (Format ?? "").GetHashCode() + IsConverted.GetHashCode() * 7345;
        public override bool Equals(object other)
        {
            var o = other as SnapshotDataBase;
            return o != null && o.Format == Format && o.IsConverted == IsConverted;
        }
    }

    [ProtoContract]
    public class SnapshotStringData : SnapshotDataBase
    {
        [ProtoMember(11)]
        public string Data { get; set; }

        public override object GetClipboardData() => Data;
        public override int GetHashCode() => base.GetHashCode() * 7345 + (Data ?? "").GetHashCode();
        public override bool Equals(object other) => Equals(other as SnapshotStringData);
        public bool Equals(SnapshotStringData other) => base.Equals(other) && other.Data == Data;
    }

    [ProtoContract]
    public class SnapshotStringArrayData : SnapshotDataBase
    {
        [ProtoMember(21)]
        public string[] Data { get; set; }
        public override object GetClipboardData() => Data;
        public override int GetHashCode() => base.GetHashCode() * 7345 + (Data?.Length ?? -1);
        public override bool Equals(object other) => Equals(other as SnapshotStringArrayData);
        public bool Equals(SnapshotStringArrayData other) => base.Equals(other) && other.Data.ArraysEquals(Data);
    }

    [ProtoContract]
    public class SnapshotRawData : SnapshotDataBase
    {
        [ProtoMember(31)]
        public byte[] Data { get; set; }
        public override object GetClipboardData() => Data;
        public override int GetHashCode() => base.GetHashCode() * 7345 + (Data?.Length ?? -1);
        public override bool Equals(object other) => Equals(other as SnapshotRawData);
        public bool Equals(SnapshotRawData other) => base.Equals(other) && other.Data.ArraysEquals(Data);
    }

    [ProtoContract]
    public class SnapshotBoolData : SnapshotDataBase
    {
        [ProtoMember(41)]
        public bool Data { get; set; }
        public override object GetClipboardData() => Data;
        public override int GetHashCode() => base.GetHashCode() * 7345 + (Data ? 1 : 0);
        public override bool Equals(object other) => Equals(other as SnapshotBoolData);
        public bool Equals(SnapshotBoolData other) => base.Equals(other) && other.Data == Data;
    }

    [ProtoContract]
    public class SnapshotBitmapData : SnapshotDataBase
    {
        [ProtoMember(51)]
        public byte[] Data { get; set; }
        public override object GetClipboardData() {
            using (MemoryStream stream = new MemoryStream(Data))
            {
                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = stream;
                bi.EndInit();
                return bi;
            }
        }

        public override int GetHashCode() => base.GetHashCode() * 7345 + Data?.Length ?? -1;
        public override bool Equals(object other) => Equals(other as SnapshotBitmapData);
        public bool Equals(SnapshotBitmapData other) => base.Equals(other) && other.Data.ArraysEquals(Data);
    }

    [ProtoContract]
    public class SnapshotNullData : SnapshotDataBase
    {
        public override object GetClipboardData() => null;
        public override int GetHashCode() => base.GetHashCode();
        public override bool Equals(object other) => base.Equals(other as SnapshotNullData);
    }

    [ProtoContract]
    public class SnapshotBitmapDrawingData : SnapshotDataBase
    {
        [ProtoMember(61)]
        public byte[] Data { get; set; }
        public override object GetClipboardData() => new System.Drawing.Bitmap(new MemoryStream(Data));

        public override int GetHashCode() => base.GetHashCode() * 7345 + Data?.Length ?? -1;
        public override bool Equals(object other) => Equals(other as SnapshotBitmapDrawingData);
        public bool Equals(SnapshotBitmapDrawingData other) => base.Equals(other) && other.Data.ArraysEquals(Data);
    }



    [ProtoContract]
    public class ClipboardSnapshot
    {
        [ProtoMember(1)]
        public DateTime Time { get; set; }

        [ProtoMember(3)]
        public SnapshotDataBase[] Data { get; set; }


        const string InternalFormat = "reflectionid";
        public bool IsNew => !Data.Any(f => f.Format == InternalFormat);

        public static ClipboardSnapshot CreateEmptySnapshot(DateTime time)
        {
            return new ClipboardSnapshot
            {
                Time = time,
                Data = new SnapshotDataBase[0],
            };
        }

        static SnapshotDataBase ClipboardDataToSnapshot(IDataObject clipboard, string format, bool isConverted)
        {
            try
            {
                var data = clipboard.GetData(format, true);

                if (data is string) return new SnapshotStringData { Format = format, Data = (string)data, IsConverted = isConverted };
                if (data is string[]) return new SnapshotStringArrayData { Format = format, Data = (string[])data, IsConverted = isConverted };
                if (data is bool) return new SnapshotBoolData { Format = format, Data = (bool)data, IsConverted = isConverted };
                if (data is InteropBitmap)
                {
                    var pngStream = new MemoryStream();
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(data as InteropBitmap));
                    encoder.Save(pngStream);
                    return new SnapshotBitmapData { Format = format, Data = pngStream.ToArray(), IsConverted = isConverted };
                }
                if (data is System.Drawing.Bitmap)
                {
                    var bitmap = (System.Drawing.Bitmap)data;
                    var pngStream = new MemoryStream();
                    bitmap.Save(pngStream, bitmap.RawFormat);
                    return new SnapshotBitmapDrawingData { Format = format, Data = pngStream.ToArray(), IsConverted = isConverted };
                }
                if (data is MemoryStream)
                {
                    var mem = ((MemoryStream)data);
                    if (mem.Length > 1024 * 1024 * 10) return new SnapshotRawData { Format = format, IsConverted = isConverted };
                    return new SnapshotRawData { Format = format, Data = mem.ToArray(), IsConverted = isConverted };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debugger.Break();
            }

            return new SnapshotNullData { Format = format, IsConverted = isConverted };
        }

        public static ClipboardSnapshot TryCreateSnapshot(IDataObject data)
        {
            try
            {
                var exactFormats = data.GetFormats(false);
                var convertableFormats = data.GetFormats(true);

                var clip = new ClipboardSnapshot { 
                    Time = DateTime.UtcNow
                };

                clip.Data = convertableFormats.Select(f => ClipboardDataToSnapshot(data, f, exactFormats.Contains(f))).ToArray();
                return clip;
            }
            catch (Exception)
            {
                throw;
                //return null;
            }
        }
        
        public void SetToClipboard()
        {
            if (Data == null || !Data.Any())
            {
                System.Diagnostics.Debug.WriteLine("Could not set empty clipboard: " + Time.ToString("HH:mm:ss"));
                return;
            }

            var data = new DataObject();
            foreach (var item in Data)
            {
                if (item != null && !item.IsConverted)
                    data.SetData(item.Format, item.GetClipboardData());
            }

            data.SetData(InternalFormat, Time.ToUniversalTime().Ticks);
            System.Diagnostics.Debug.WriteLine("Clipboard set to " + Time.ToString("HH:mm:ss"));
            Clipboard.SetDataObject(data, true);
        }

        public Stream Serialize()
        {
            var stream = new MemoryStream();
            using (var zipStream = new DeflateStream(stream, CompressionMode.Compress, true))
            using (var bs = new BufferedStream(zipStream, 64 * 1024))
                Serializer.Serialize(bs, this);
            stream.Position = 0;
            return stream;
        }

        public static ClipboardSnapshot Deserialize(Stream stream) =>
            Serializer.Deserialize<ClipboardSnapshot>(new DeflateStream(stream, CompressionMode.Decompress, false));

        string ExtractHtmlInfo(string Html)
        {
            string ContentStart = "<!--StartFragment-->";
            string ContentEnd = "<!--EndFragment-->";

            string UrlStart = "SourceURL:";
            string UrlEnd = Environment.NewLine;

            string TitleStart = "<TITLE>";
            string TitleEnd = "<\\TITLE>";


            int contentStartIndex = Html.IndexOf(ContentStart);
            int contentEndIndex = Html.IndexOf(ContentEnd, contentStartIndex);

            string result = "";

            if (contentStartIndex > 0 && contentEndIndex > 0)
            {
                int titleStartIndex = Html.IndexOf(TitleStart);
                int titleEndIndex = Html.IndexOf(TitleEnd, titleStartIndex + TitleStart.Length);

                if (titleStartIndex > 0 && titleEndIndex > 0)
                {
                    result += "Url: " + Html.Substring(titleStartIndex + TitleStart.Length, (titleEndIndex + TitleEnd.Length) - (titleStartIndex + TitleStart.Length)) + Environment.NewLine;
                }


                int urlStartIndex = Html.IndexOf(UrlStart);
                int urlEndIndex = Html.IndexOf(UrlEnd, urlStartIndex + UrlStart.Length);

                if (urlStartIndex > 0)
                {
                    result += "Url: " + Html.Substring(urlStartIndex + UrlStart.Length, (urlEndIndex + UrlEnd.Length) - (urlStartIndex + UrlStart.Length));
                }
            }

            return result;
        }

        public string FormatHtml(string Html)
        {
            string ContentStart = "<!--StartFragment-->";
            string ContentEnd = "<!--EndFragment-->";

            string UrlEnd = Environment.NewLine;

            int contentStartIndex = Html.IndexOf(ContentStart);
            int contentEndIndex = Html.IndexOf(ContentEnd, contentStartIndex);

            string result = "";

            if (contentStartIndex > 0 && contentEndIndex > 0)
            {
                result = Html.Substring(contentStartIndex + ContentStart.Length, (contentEndIndex + ContentEnd.Length) - (contentStartIndex + ContentStart.Length));
                result = result.Trim('\r', '\n', ' ');
            }
            else
                result = Html;

            return result;
        }

        public bool EqualsExceptTime(ClipboardSnapshot other)
        {
            if (other == null) return false;
            if (other.Data.Length != Data.Length) return false;

            for (int i = 0; i < Data.Length; i++)
                if (!Data[i].Equals(other.Data[i])) return false;

            return true;
        }

        public override string ToString()
        {
            string type = "Type: ";
            type = type.Remove(type.Length - 2, 2);

            return (type + Environment.NewLine + "Time: " + Time.ToLongDateString() + " " + Time.ToLongTimeString() + Environment.NewLine/* + HtmlInfo*/).Trim();
        }
    }
}
