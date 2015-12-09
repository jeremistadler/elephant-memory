using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Reflection
{
    internal static class KeyboardHook
    {
        static IntPtr NextHookPointer;
        static NativeMethods.keyboardHookProc KeyboardHookProcedure = new NativeMethods.keyboardHookProc(HookCallback);

        static Func<int,bool> ShouldCancelKeyUp;
        static Func<int,bool> ShouldCancelKeyDown;
        static Dictionary<int, bool> KeysDown = new Dictionary<int, bool>();


        public static bool Start(Func<int, bool> shouldCancelKeyDown, Func<int, bool> shouldCancelKeyUp)
        {
            ShouldCancelKeyDown = shouldCancelKeyDown;
            ShouldCancelKeyUp = shouldCancelKeyUp;

            Stop();

            IntPtr module;
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
                module = NativeMethods.GetModuleHandle(curModule.ModuleName);

            NextHookPointer = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, KeyboardHookProcedure, module, 0);

            return NextHookPointer != IntPtr.Zero;
        }

        public static void Stop()
        {
            bool KeyboardResult = true;

            if (NextHookPointer != IntPtr.Zero)
                KeyboardResult = NativeMethods.UnhookWindowsHookEx(NextHookPointer);

            NextHookPointer = IntPtr.Zero;
        }

        public static int HookCallback(int code, int wParam, ref NativeMethods.keyboardHookStruct lParam)
        {
            Debug.WriteLine(code + " " + wParam + "  " + lParam.vkCode + " " + lParam.scanCode + " " + lParam.flags);

            if (code >= 0 && wParam == 256) // KeyDown
            {
                if ((!KeysDown.ContainsKey(lParam.vkCode) ||
                    KeysDown[lParam.vkCode] == false) &&
                    ShouldCancelKeyDown(lParam.vkCode))
                {
                    return 0;
                }

                KeysDown[lParam.vkCode] = true;
            }

            if (code >= 0 && wParam == 257) // KeyUp
            {
                KeysDown[lParam.vkCode] = false;
                if (ShouldCancelKeyUp(lParam.vkCode))
                    return 0;
            }

            return NativeMethods.CallNextHookEx(NextHookPointer, code, wParam, ref lParam);
        }
    }
}
