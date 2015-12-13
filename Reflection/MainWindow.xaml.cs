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
        ClipboardSnapshot[] RelatedSnapshots;
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

            Current = snapshot;
            UpdateUI(snapshot);

            if (snapshot.IsNew)
                Storage.Save(snapshot);

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
            var date = Current?.Time ?? DateTime.UtcNow;
            Storage.GetRelated(date, result =>
            {
                RelatedSnapshots = result;
                UpdateUIAsync();
            });
        }

        private void GoToPreviousClipboard()
        {
            if (IsLoading) return; IsLoading = true;

            var date = Current?.Time ?? DateTime.UtcNow;
            Storage.GetPrevious(date).ContinueWith(task =>
            {
                IsLoading = false;
                if (task.Result != null)
                {
                    Current = task.Result;
                    UpdateUIAndSetToClipboardAsync();
                }

                UpdateRelatedSnapshots();
            });
        }

        private void GoToNextClipboard()
        {
            if (Current == null) return;
            if (IsLoading) return; IsLoading = true;

            Storage.GetNext(Current.Time).ContinueWith(task =>
            {
                IsLoading = false;
                if (task.Result != null)
                {
                    Current = task.Result;
                    UpdateUIAndSetToClipboardAsync();
                }

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
            TimeText.Text = data.Time.ToLocalTime().ToString("HH:mm:ss") + "  (" + TimeToText(data.Time) + ")";

            DataText.Text = "";
            DataFiles.Text = "";
            DataHtml.Text = "";

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

            Timeline.Children.Clear();

            if (RelatedSnapshots != null && RelatedSnapshots.Any())
            {
                for (int i = 0; i < RelatedSnapshots.Length; i++)
                {
                    var item = RelatedSnapshots[i];
                    double left = 0;
                    if (i > 0)
                    {
                        var timeDiff = item.Time - RelatedSnapshots[i - 1].Time;
                        var diff = Math.Log(timeDiff.TotalMinutes) * 10;
                        left = i * 7 + Math.Min(50, Math.Max(0, diff));
                    }

                    {
                        var elm = new Border
                        {
                            CornerRadius = new CornerRadius(10),
                            Background = Brushes.CornflowerBlue,
                            Width = 5,
                            Height = 5,
                            BorderBrush = Brushes.Black
                        };

                        if (item.EqualsExceptTime(Current))
                            elm.BorderThickness = new Thickness(1);

                        Canvas.SetLeft(elm, left);
                        Canvas.SetTop(elm, 0);
                        Timeline.Children.Add(elm);
                    }

                    if (item.Data.Any(f => f.Format == DataFormats.Bitmap))
                    {
                        var elm2 = new Border
                        {
                            CornerRadius = new CornerRadius(10),
                            Background = Brushes.Pink,
                            Width = 3,
                            Height = 3
                        };

                        Canvas.SetLeft(elm2, left);
                        Canvas.SetTop(elm2, 5);
                        Timeline.Children.Add(elm2);
                    }

                    if (item.Data.Any(f => f.Format == DataFormats.Html))
                    {
                        var elm2 = new Border
                        {
                            CornerRadius = new CornerRadius(10),
                            Background = Brushes.Brown,
                            Width = 3,
                            Height = 3
                        };

                        Canvas.SetLeft(elm2, left);
                        Canvas.SetTop(elm2, 8);
                        Timeline.Children.Add(elm2);
                    }
                }
            }
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
