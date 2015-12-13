using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Reflection
{
    public partial class MainWindow : MahApps.Metro.Controls.MetroWindow
    {
        ClipboardSnapshotPointer[] RelatedSnapshots;
        ClipboardSnapshot Current;
        IStorage Storage;
        bool IsLoading = false;

        public MainWindow()
        {
            InitializeComponent();
            AllowsTransparency = true;
        }


        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Storage = new TableClipStorage();
            var source = PresentationSource.FromVisual(this) as HwndSource;
            ClipboardHook.Start(ClipboardChanged, source);

            ShortcutManager.Next += GoToNextClipboard;
            ShortcutManager.Previous += GoToPreviousClipboard;
            ShortcutManager.ToggleVisibility += ShortcutManager_ToggleVisibility;

            ShortcutManager.Start();
        }


        void ClipboardChanged(ClipboardSnapshot snapshot)
        {
            System.Diagnostics.Debug.WriteLine("Clipboard changed");

            if (snapshot.IsNew)
                Storage.Save(snapshot);
            else
            {
                snapshot = Current;
            }

            Current = snapshot;
            UpdateUI(snapshot);
            UpdateRelatedSnapshots();
        }

        private void ShortcutManager_ToggleVisibility(bool visible)
        {
            if (visible)
            {
                System.Diagnostics.Debug.WriteLine("Showing UI");

                Point mousePositionInApp = NativeMethods.GetMousePosition();
                mousePositionInApp.Offset(-this.Width / 2, -this.Height / 2);

                this.Left = Math.Max(0, mousePositionInApp.X);
                this.Top = Math.Max(0, mousePositionInApp.Y);

                WindowState = WindowState.Normal;
                Show();
                Topmost = true;
                Activate();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Hiding UI");
                Hide();
            }
        }

        void UpdateRelatedSnapshots()
        {
            var date = Current?.Date ?? DateTime.UtcNow;
            Storage.GetRelated(date, result =>
            {
                RelatedSnapshots = result;
                UpdateUIAsync();
            });
        }

        private void GoToPreviousClipboard()
        {
            if (IsLoading) return; IsLoading = true;

            var date = Current?.Date ?? DateTime.UtcNow;
            Storage.GetPrevious(date).ContinueWith(task =>
            {
                IsLoading = false;
                if (task.Result != null)
                {
                    Current = task.Result;
                    UpdateUIAndSetToClipboardAsync();
                }
                else
                    UpdateUIAsync();

                UpdateRelatedSnapshots();
            });
        }

        private void GoToNextClipboard()
        {
            if (Current == null) return;
            if (IsLoading) return; IsLoading = true;

            Storage.GetNext(Current.Date).ContinueWith(task =>
            {
                IsLoading = false;
                if (task.Result != null)
                {
                    Current = task.Result;
                    UpdateUIAndSetToClipboardAsync();
                }
                else
                    UpdateUIAsync();

                UpdateRelatedSnapshots();
            });
        }

        string TimeToText(DateTime pastTime)
        {
            var time = DateTime.UtcNow - pastTime;
            var date = pastTime.Date;
            var today = DateTime.Today;

            if (time < TimeSpan.FromMinutes(120)) return Math.Ceiling(time.TotalMinutes) + " minuter sedan";
            else if (time < TimeSpan.FromHours(48)) return Math.Ceiling(time.TotalHours) + " timmar sedan";
            else return Math.Ceiling(time.TotalDays) + " dagar sedan";
        }

        void UpdateUIAsync()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateUI(Current);
            });
        }

        void UpdateUIAndSetToClipboardAsync()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateUI(Current);
                Current.SetToClipboard();
            });
        }

        void UpdateUI(ClipboardSnapshot data)
        {
            UpdateTimeline();

            DataText.Text = "";
            DataFiles.Text = "";
            DataHtml.Text = "";
            DataRtf.Document.Blocks.Clear();

            if (data == null || data.Data == null)
            {
                if (data != null)
                    TimeText.Text = data.Date.ToLocalTime().ToString("HH:mm:ss") + "  (" + TimeToText(data.Date) + ")";

                TabText.Visibility = Visibility.Collapsed;
                TabFiles.Visibility = Visibility.Collapsed;
                TabRtf.Visibility = Visibility.Collapsed;
                TabImage.Visibility = Visibility.Collapsed;
                TabHtml.Visibility = Visibility.Collapsed;
                return;
            }

            TimeText.Text = data.Date.ToLocalTime().ToString("HH:mm:ss") + "  (" + TimeToText(data.Date) + ")";


            var text = (data.Data.FirstOrDefault(f => f.Format == DataFormats.Text) as SnapshotStringData)?.Data;
            if (text.NotEmpty()) DataText.Text = text;
            TabText.Visibility = text.NotEmpty() ? Visibility.Visible : Visibility.Collapsed;

            var files = (data.Data.FirstOrDefault(f => f.Format == DataFormats.FileDrop) as SnapshotStringArrayData)?.Data;
            if (files.NotEmpty()) DataFiles.Text = string.Join(Environment.NewLine, files);
            TabFiles.Visibility = files.NotEmpty() ? Visibility.Visible : Visibility.Collapsed;

            var html = (data.Data.FirstOrDefault(f => f.Format == DataFormats.Html) as SnapshotStringData)?.Data;
            if (html.NotEmpty()) DataHtml.Text = data.FormatHtml(html);
            TabHtml.Visibility = html.NotEmpty() ? Visibility.Visible : Visibility.Collapsed;

            var rtf = (data.Data.FirstOrDefault(f => f.Format == DataFormats.Rtf) as SnapshotStringData)?.Data;
            if (rtf.NotEmpty())
            {
                DataRtf.SelectAll();
                using (var stream = new MemoryStream(Encoding.Default.GetBytes(rtf)))
                    DataRtf.Selection.Load(stream, DataFormats.Rtf);
            }
            TabRtf.Visibility = rtf.NotEmpty() ? Visibility.Visible : Visibility.Collapsed;

            var bitmap = ((SnapshotBitmapData)data.Data.FirstOrDefault(f => f.Format == DataFormats.Bitmap))?.Data;
            if (bitmap != null)
                using (MemoryStream stream = new MemoryStream(bitmap))
                    DataImage.Source = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            TabImage.Visibility = bitmap != null ? Visibility.Visible : Visibility.Collapsed;


            for (int i = 0; i < TabController.Items.Count; i++)
            {
                var item = TabController.Items[i] as TabItem;
                if (item != null && item.Visibility == Visibility.Visible)
                {
                    TabController.SelectedIndex = i;
                    break;
                }
            }
        }

        private void UpdateTimeline()
        {
            Timeline.Children.Clear();

            if (RelatedSnapshots != null && RelatedSnapshots.Any())
            {
                var itemWidth = 8;
                var ballSize = 6;
                var positions = GetTimelinePositions(itemWidth);

                var currentIndex = RelatedSnapshots.Select((a, i) => new { a, i }).FirstOrDefault(f => Current != null && f.a.GetId() == Current.Id)?.i ?? -1;

                if (currentIndex >= 0)
                {
                    var shift = (Timeline.ActualWidth - 100) - positions[currentIndex];
                    for (int i = 0; i < positions.Length; i++) positions[i] += shift;
                }

                for (int i = 0; i < RelatedSnapshots.Length; i++)
                {
                    var left = positions[i];
                    var item = RelatedSnapshots[i];

                    if (left < 0) continue;
                    var top = 3;

                    if (Current.Id == item.GetId())
                        AddTimelineBox(-1, left, 2, itemWidth, Brushes.Gray, 0);

                    if (item.GetFormats().Any(f => f == DataFormats.Rtf))
                    {
                        AddTimelineCircle(top, left + 1, ballSize, Brushes.CornflowerBlue, false);
                        top += ballSize + 1;
                    }

                    if (item.GetFormats().Any(f => f == DataFormats.Bitmap))
                    {
                        AddTimelineCircle(top, left + 1, ballSize, Brushes.Purple, false);
                        top += ballSize + 1;
                    }

                    if (item.GetFormats().Any(f => f == DataFormats.Html))
                    {
                        AddTimelineCircle(top, left + 1, ballSize, Brushes.Brown, false);
                        top += ballSize + 1;
                    }

                    if (item.GetFormats().Any(f => f == DataFormats.FileDrop))
                    {
                        AddTimelineCircle(top, left + 1, ballSize, Brushes.DarkRed, false);
                        top += ballSize + 1;
                    }

                    if (top == 3)
                        AddTimelineCircle(top, left, 5, Brushes.White, true);
                }
            }
        }

        double[] GetTimelinePositions(int itemWidth)
        {
            var positions = new double[RelatedSnapshots.Length];

            var left = 0.0;
            for (int i = RelatedSnapshots.Length - 1; i >= 0; i--)
            {
                left += itemWidth;
                if (i < positions.Length - 1)
                {
                    var timeDiff = RelatedSnapshots[i + 1].Date - RelatedSnapshots[i].Date;
                    var diff = Math.Log(timeDiff.TotalMinutes) * 10;
                    if (diff > 0)
                        left += Math.Min(30, diff);
                }
                positions[i] = left;
            }

            for (int i = 0; i < positions.Length; i++) positions[i] = left - positions[i];

            return positions;
        }

        private void AddTimelineCircle(double top, double left, double size, Brush color, bool stroke)
        {
            var elm = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = color,
                Width = size,
                Height = size,
                BorderBrush = Brushes.Black,
                BorderThickness = stroke ? new Thickness(1) : new Thickness()
            };

            Canvas.SetLeft(elm, left);
            Canvas.SetTop(elm, top);
            Timeline.Children.Add(elm);
        }

        private void AddTimelineBox(double top, double left, double height, double width, Brush color, double borderRadius)
        {
            var elm = new Border
            {
                CornerRadius = new CornerRadius(borderRadius),
                Background = color,
                Width = width,
                Height = height,
            };

            Canvas.SetLeft(elm, left);
            Canvas.SetTop(elm, top);
            Timeline.Children.Add(elm);
        }

        bool isFirst = true;
        void MetroWindow_Activated(object sender, EventArgs e)
        {
            if (isFirst)
            {
                Hide();
                isFirst = false;
            }
        }
    }
}
