using System;
using System.Windows;
using System.Windows.Interop;

namespace elephant_memory
{
    internal static class ClipboardHook
    {
        static Action<ClipboardSnapshot> OnChanged;
        static IntPtr WindowHandle;
        static IntPtr NextClipboardViewer;

        static IDataObject LastClipData;
        static ClipboardSnapshot LastClip;

        public static bool Start(Action<ClipboardSnapshot> onChanged, HwndSource source)
        {
            OnChanged = onChanged;

            source.AddHook(WndProc);
            WindowHandle = source.Handle;

            NextClipboardViewer = NativeMethods.SetClipboardViewer(WindowHandle);
            return NextClipboardViewer != IntPtr.Zero;
        }


        static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_Drawclipboard)
            {
                ClipboardChanged();
                return IntPtr.Zero;
            }

            if (msg == NativeMethods.WM_Changecbchain)
            {
                if (wParam == NextClipboardViewer)
                    NextClipboardViewer = lParam;
                else
                    NativeMethods.SendMessage(NextClipboardViewer, msg, wParam, lParam);

                return IntPtr.Zero;
            }

            return IntPtr.Zero;
        }

        static void ClipboardChanged()
        {
            if (LastClipData == null || !Clipboard.IsCurrent(LastClipData))
            {
                LastClipData = Clipboard.GetDataObject();
                var data = ClipboardSnapshot.CreateSnapshot(LastClipData);

                if (LastClip != null && data.EqualsExceptTime(LastClip))
                    return;

                LastClip = data;
                OnChanged(data);
            }
        }
    }

}
