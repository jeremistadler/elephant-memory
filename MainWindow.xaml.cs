using Raven.Client;
using Raven.Client.Document;
using Raven.Client.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace elephant_memory
{
    public partial class MainWindow : MahApps.Metro.Controls.MetroWindow
    {
        LinkedList<ClipboardSnapshot> CachedSnapshots = new LinkedList<ClipboardSnapshot>();
        IDocumentStore Store;
        LinkedListNode<ClipboardSnapshot> Current;

        public MainWindow()
        {
            InitializeComponent();
            AllowsTransparency = true;

            Store = new DocumentStore
            {
                Url = "http://raven.jeremi.se/",
                DefaultDatabase = "elephant-memory",
            }.Initialize();
        }

        void ClipboardChanged(ClipboardSnapshot snapshot)
        {
            Current = CachedSnapshots.AddLast(snapshot);
            UpdateUI(snapshot);

            using (var session = Store.OpenSession())
            {
                session.Store(snapshot);
                session.SaveChanges();
            }
        }

        private void ShortcutManager_ToggleVisibility(bool visible)
        {
            if (visible)
            {
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
                Hide();
            }
        }


        private void GoToPreviousClipboard()
        {
            if (Current == null) return;
            if (Current.Previous != null)
            {
                Current = Current.Previous;
                UpdateUI(Current.Value);
            }
            else
            {
                using (var session = Store.OpenSession())
                {
                    var itemTask = session.Query<ClipboardSnapshot>().Where(f => f.Time < Current.Value.Time).OrderByDescending(f => f.Time).FirstOrDefault();
                    if (itemTask != null)
                    {
                        Current = Current.List.AddBefore(Current, itemTask);
                        UpdateUI(Current.Value);
                        Current.Value.SetToClipboard();
                    }
                }
            }
        }

        private void GoToNextClipboard()
        {
            if (Current == null) return;
            if (Current.Next != null)
            {
                Current = Current.Next;
                UpdateUI(Current.Value);
            }
            else
            {
                using (var session = Store.OpenSession())
                {
                    var time = Current.Value.Time;
                    var itemTask = session.Query<ClipboardSnapshot>().Where(f => f.Time > time).OrderBy(f => f.Time).FirstOrDefault();
                    if (itemTask != null)
                    {
                        Current = Current.List.AddAfter(Current, itemTask);
                        UpdateUI(Current.Value);
                        Current.Value.SetToClipboard();
                    }
                }
            }
        }


        private void Button_PrevClick(object sender, RoutedEventArgs e)
        {
            GoToPreviousClipboard();
        }

        private void Button_NextClick(object sender, RoutedEventArgs e)
        {
            GoToNextClipboard();
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

        void UpdateUI(ClipboardSnapshot data)
        {
            TimeText.Text = TimeToText(data.Time);

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

        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var source = PresentationSource.FromVisual(this) as HwndSource;
            ClipboardHook.Start(ClipboardChanged, source);

            ShortcutManager.Next += GoToNextClipboard;
            ShortcutManager.Previous += GoToPreviousClipboard;
            ShortcutManager.ToggleVisibility += ShortcutManager_ToggleVisibility;

            ShortcutManager.Start();
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
