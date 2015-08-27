using System;
using System.Diagnostics;

namespace elephant_memory
{
    internal static class ShortcutManager
    {
        public static event Action<bool> ToggleVisibility;
        public static event Action Next;
        public static event Action Previous;
        public static bool Visible { get; set; }

        static DateTime? LastCtrlDown = null;

        const int CtrlKey = 162;
        const int ShiftKey = 160;
        const int LeftKey = 37;
        const int RightKey = 39;


        static ShortcutManager()
        {
            Previous += () => { };
            Next += () => { };
            ToggleVisibility += (b) => { };
        }

        public static bool Start() =>
            KeyboardHook.Start(ShouldCancelKeyDown, ShouldCancelKeyUp);

        public static void Stop() =>
            KeyboardHook.Stop();


        static bool ShouldCancelKeyDown(int keyCode)
        {
            if (!Visible && keyCode == ShiftKey)
            {
                if (LastCtrlDown.HasValue && (DateTime.UtcNow - LastCtrlDown.Value) < TimeSpan.FromSeconds(0.5))
                {
                    Visible = true;
                    ToggleVisibility(true);
                    LastCtrlDown = null;
                }
                else
                    LastCtrlDown = DateTime.UtcNow;
            }
            else if (Visible && keyCode == LeftKey)
            {
                Previous();
                return true;
            }
            else if (Visible && keyCode == RightKey)
            {
                Next();
                return true;
            }

            return false;
        }

        static bool ShouldCancelKeyUp(int keyCode)
        {
            if (Visible)
            {
                if (keyCode == ShiftKey)
                {
                    Visible = false;
                    ToggleVisibility(false);
                }

                if (keyCode == LeftKey || keyCode == RightKey)
                {
                    return true;
                }
            }

            return false;
        }


        static int hookProc(int code, int wParam, ref NativeMethods.keyboardHookStruct lParam)
        {
            Debug.WriteLine(code + " " + wParam + "  " + lParam.vkCode + " " + lParam.scanCode + " " + lParam);
            return 0;

            //if (code >= 0 && wParam == 257) // KeyUp
            //{
            //    bool skipSet = false;

            //    if (lParam.vkCode == 162 &&
            //        LastKey == 162)
            //    {
            //        Visible = !Visible;
            //        ToggleVisibility(Visible);
            //        LastKey = -1;
            //        skipSet = true;
            //    }

            //    if (lParam.vkCode == 37 &&
            //        LastKey == -1 &&
            //        Visible)
            //    {
            //        Debug.WriteLine("Prev Begin");
            //        Previous();
            //        Debug.WriteLine("Prev End");
            //        skipSet = true;
            //        //return 1;
            //    }

            //    if (lParam.vkCode == 39 &&
            //        LastKey == -1 &&
            //        Visible)
            //    {
            //        Debug.WriteLine("Next Begin");
            //        Next();
            //        Debug.WriteLine("Next End");
            //        skipSet = true;
            //        //return 1;
            //    }

            //    if (!skipSet)
            //    {
            //        LastKey = lParam.vkCode;

            //        //if (Visible)
            //        //{
            //        //    Visible = false;
            //        //    ToggleVisibility(Visible);
            //        //}
            //    }
            //}


            //return NativeMethods.CallNextHookEx(KeyboardHook, code, wParam, ref lParam);
        }
    }
}
