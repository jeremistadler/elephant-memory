using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Reflection
{
    public partial class MainWindow : MahApps.Metro.Controls.MetroWindow
    {
        List<ClipboardSnapshot> Cache = new List<ClipboardSnapshot>();
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

            if (!Cache.Any(f => f.EqualsExceptTime(snapshot)))
                Storage.Save(snapshot);
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


        private void GoToPreviousClipboard()
        {
            if (IsLoading) return; IsLoading = true;

            var date = Current?.Time ?? DateTime.Now;
            Storage.GetPrevious(date).ContinueWith(task =>
            {
                IsLoading = false;
                if (task.Result != null)
                {
                    Current = task.Result;
                    Cache.Add(Current);
                    UpdateUIAndSetToClipboardAsync(Current);
                }
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
                    Cache.Add(Current);
                    UpdateUIAndSetToClipboardAsync(Current);
                }
            });
        }

        string TimeToText(DateTime pastTime)
        {
            var time = DateTime.Now - pastTime;
            var date = pastTime.Date;
            var today = DateTime.Today;

            if (time < TimeSpan.FromMinutes(120)) return Math.Ceiling(time.TotalMinutes) + " minuter sedan";
            else if (time < TimeSpan.FromHours(48)) return Math.Ceiling(time.TotalHours) + " timmar sedan";
            else return Math.Ceiling(time.TotalDays) + " dagar sedan";
        }


        void UpdateUIAndSetToClipboardAsync(ClipboardSnapshot data)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateUI(data);
                data.SetToClipboard();
            });
        }

        void UpdateUI(ClipboardSnapshot data)
        {
            TimeText.Text = TimeToText(data.Time);
            TimeText.Text = data.Time.ToLocalTime().ToString("HH:mm:ss") + "  (" + TimeToText(data.Time.ToLocalTime()) + ")";

            DataText.Text = "";
            DataFiles.Text = "";
            DataHtml.Text = "";

            if (data.Text.NotEmpty()) DataText.Text = data.Text;
            TabText.Visibility = data.Text.NotEmpty() ? Visibility.Visible : Visibility.Collapsed;

            if (data.Files.NotEmpty()) DataFiles.Text = string.Join(Environment.NewLine, data.Files);
            TabFiles.Visibility = data.Files.NotEmpty() ? Visibility.Visible : Visibility.Collapsed;

            if (data.Html.NotEmpty()) DataHtml.Text = data.FormatHtml();
            TabHtml.Visibility = data.Html.NotEmpty() ? Visibility.Visible : Visibility.Collapsed;

            if (data.Rtf.NotEmpty())
            {
                DataRtf.SelectAll();
                using (var stream = new MemoryStream(Encoding.Default.GetBytes(data.Rtf)))
                    DataRtf.Selection.Load(stream, DataFormats.Rtf);
            }
            TabRtf.Visibility = data.Rtf.NotEmpty() ? Visibility.Visible : Visibility.Collapsed;

            if (data.PngImageData != null)
                using (MemoryStream stream = new MemoryStream(data.PngImageData))
                    DataImage.Source = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            TabImage.Visibility = data.PngImageData != null ? Visibility.Visible : Visibility.Collapsed;


            for (int i = 0; i < TabController.Items.Count; i++)
            {
                var item = TabController.Items[i] as TabItem;
                if (item != null && item.Visibility == Visibility.Visible)
                {
                    TabController.SelectedIndex = i;
                    break;
                }
            }

            //PrevButton.Visibility = PrevData == null ? Visibility.Hidden : Visibility.Visible;
            //NextButton.Visibility = NextData == null ? Visibility.Hidden : Visibility.Visible;
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
