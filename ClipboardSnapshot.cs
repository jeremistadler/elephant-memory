using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;

namespace elephant_memory
{
    public class ClipboardSnapshot
    {
        public int Id { get; set; }

        public DateTime Time { get; set; }

        public string Rtf { get; set; }
        public string Text { get; set; }
        public string Html { get; set; }
        public string[] Files { get; set; }
        public byte[] PngImageData { get; set; }

        public string[] ExactFormats { get; set; }
        public string[] ConvertableFormats { get; set; }
        
        public static ClipboardSnapshot CreateSnapshot(IDataObject data)
        {
            try
            {
                var clip = new ClipboardSnapshot { 
                    Time = DateTime.Now,
                    ConvertableFormats = data.GetFormats(true),
                    ExactFormats = data.GetFormats(false),
                };

                if (clip.ConvertableFormats.Contains(DataFormats.Bitmap))
                {
                    var image = data.GetData(DataFormats.Bitmap, true) as System.Windows.Interop.InteropBitmap;

                    using (var pngStream = new MemoryStream())
                    {
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(image));
                        encoder.Save(pngStream);
                        clip.PngImageData = pngStream.ToArray();
                    }
                }

                if (clip.ExactFormats.Contains(DataFormats.UnicodeText))
                    clip.Text = (string)data.GetData(DataFormats.UnicodeText, false);

                if (clip.ExactFormats.Contains(DataFormats.Rtf))
                    clip.Rtf = (string)data.GetData(DataFormats.Rtf, false);

                if (clip.ExactFormats.Contains(DataFormats.Html))
                    clip.Html = (string)data.GetData(DataFormats.Html, false);

                if (clip.ExactFormats.Contains(DataFormats.FileDrop))
                    clip.Files = (string[])data.GetData(DataFormats.FileDrop, false);

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
            var data = new DataObject();

            if (ConvertableFormats.Contains(DataFormats.FileDrop))
                data.SetData(DataFormats.FileDrop, Files);

            if (ConvertableFormats.Contains(DataFormats.Html))
                data.SetData(DataFormats.Html, Html);

            if (ConvertableFormats.Contains(DataFormats.UnicodeText))
                data.SetData(DataFormats.UnicodeText, Text);

            if (ConvertableFormats.Contains(DataFormats.Bitmap))
            {
                using (MemoryStream stream = new MemoryStream(PngImageData))
                {
                    BitmapImage bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.StreamSource = stream;
                    bi.EndInit();
                    data.SetImage(bi);
                }
            }

            Clipboard.SetDataObject(data, true);
        }

        string ExtractHtmlInfo()
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

        public string FormatHtml()
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
            return Text == other.Text &&
                   Html == other.Html &&
                   Rtf == other.Rtf &&

                  Files.ArraysEquals(other.Files) &&
                  PngImageData.ArraysEquals(other.PngImageData) &&
                  ConvertableFormats.ArraysEquals(other.ConvertableFormats) &&
                  ExactFormats.ArraysEquals(other.ExactFormats);
        }

        public override string ToString()
        {
            string type = "Type: ";
            if (!string.IsNullOrEmpty(Text)) type += "Text, ";
            if (PngImageData != null) type += "Image, ";
            if (Files.NotEmpty()) type += "Files, ";
            if (string.IsNullOrWhiteSpace(Html)) type += "Html, ";
            if (string.IsNullOrWhiteSpace(Rtf)) type += "Rtf, ";

            type = type.Remove(type.Length - 2, 2);

            return (type + Environment.NewLine + "Time: " + Time.ToLongDateString() + " " + Time.ToLongTimeString() + Environment.NewLine/* + HtmlInfo*/).Trim();
        }
    }
}
