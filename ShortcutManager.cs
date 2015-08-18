using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace elephant_memory
{
    internal class ShortcutManager
    {
        static IntPtr KeyboardHook;

        public static event Action<bool> ToggleVisibility;
        public static event Action Next;
        public static event Action Previous;

        static NativeMethods.keyboardHookProc KeyboardHookProcedure;

        static long LastKey = 0;
        public static bool Visible { get; set; }


        static ShortcutManager()
        {
            KeyboardHookProcedure = new NativeMethods.keyboardHookProc(hookProc);

            Previous += () => { };
            Next += () => { };
            ToggleVisibility += (b) => { };
        }

        public static bool Start()
        {
            Stop();

            IntPtr module;

            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
                module = NativeMethods.GetModuleHandle(curModule.ModuleName);


            KeyboardHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, KeyboardHookProcedure, module, 0);

            return KeyboardHook != IntPtr.Zero;
        }

        public static void Stop()
        {
            bool KeyboardResult = true;

            if (KeyboardHook != IntPtr.Zero)
                KeyboardResult = NativeMethods.UnhookWindowsHookEx(KeyboardHook);

            KeyboardHook = IntPtr.Zero;
        }

        public static int hookProc(int code, int wParam, ref NativeMethods.keyboardHookStruct lParam)
        {
            Debug.WriteLine(code + " " + wParam + "  " + lParam.vkCode + " " + lParam.scanCode + " " + lParam);


            if (code >= 0 && wParam == 257) // KeyUp
            {
                bool skipSet = false;

                if (lParam.vkCode == 162 &&
                    LastKey == 162)
                {
                    Visible = !Visible;
                    ToggleVisibility(Visible);
                    LastKey = -1;
                    skipSet = true;
                }

                if (lParam.vkCode == 37 &&
                    LastKey == -1 &&
                    Visible)
                {
                    Debug.WriteLine("Prev Begin");
                    Previous();
                    Debug.WriteLine("Prev End");
                    skipSet = true;
                    //return 1;
                }

                if (lParam.vkCode == 39 &&
                    LastKey == -1 &&
                    Visible)
                {
                    Debug.WriteLine("Next Begin");
                    Next();
                    Debug.WriteLine("Next End");
                    skipSet = true;
                    //return 1;
                }

                if (!skipSet)
                {
                    LastKey = lParam.vkCode;

                    //if (Visible)
                    //{
                    //    Visible = false;
                    //    ToggleVisibility(Visible);
                    //}
                }
            }


            return NativeMethods.CallNextHookEx(KeyboardHook, code, wParam, ref lParam);
        }
    }
}
