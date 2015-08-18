using Raven.Client;
using Raven.Client.Document;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace elephant_memory
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MahApps.Metro.Controls.MetroWindow
    {
        LinkedList<ClipboardSnapshot> CachedSnapshots = new LinkedList<ClipboardSnapshot>();

        IntPtr WindowHandle;
        IntPtr NextClipboardViewer;

        const int WmChangecbchain = 0x030D;
        const int WmDrawclipboard = 0x308;

        IDataObject LastClipData;
        ClipboardSnapshot LastClip;

        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            source.AddHook(WndProc);
            WindowHandle = source.Handle;

            NextClipboardViewer = NativeMethods.SetClipboardViewer(WindowHandle);

            ShortcutManager.Next += ShortcutManager_Next;
            ShortcutManager.Previous += ShortcutManager_Previous;
            ShortcutManager.ToggleVisibility += ShortcutManager_ToggleVisibility;

            //ShortcutManager.Start();
        }

        private void ShortcutManager_ToggleVisibility(bool obj)
        {
        }

        private void ShortcutManager_Previous()
        {
        }

        private void ShortcutManager_Next()
        {
        }

        void SaveItem(ClipboardSnapshot snapshot)
        {
            using (IDocumentStore store = new DocumentStore
            {
                Url = "http://localhost:8080/",
                DefaultDatabase = "elephant-memory",
            }.Initialize())
            {
                using (var session = store.OpenSession())
                {
                    session.Store(snapshot);
                    session.SaveChanges();
                }
            }
        }

        void ClipboardChanged()
        {
            if (LastClipData == null || !Clipboard.IsCurrent(LastClipData))
            {
                LastClipData = Clipboard.GetDataObject();
                var data = ClipboardSnapshot.CreateSnapshot(LastClipData);

                if (LastClip != null && data.EqualsExceptTime(LastClip))
                    return;

                LastClip = data;
                CachedSnapshots.AddLast(data);

                SaveItem(data);
            }
        }


        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmDrawclipboard)
            {
                ClipboardChanged();
                return IntPtr.Zero;
            }
            if (msg == WmChangecbchain)
            {
                if (wParam == NextClipboardViewer)
                    NextClipboardViewer = lParam;
                else
                    NativeMethods.SendMessage(NextClipboardViewer, msg, wParam, lParam);

                return IntPtr.Zero;
            }

            return IntPtr.Zero;
        }

        private void Button_SetToClipClick(object sender, RoutedEventArgs e)
        {


        }

        private void Button_PrevClick(object sender, RoutedEventArgs e)
        {
        }

        private void Button_NextClick(object sender, RoutedEventArgs e)
        {
        }
    }
}
