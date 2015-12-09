using System;

namespace Reflection
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
        const int AKey = 65;
        const int DKey = 68;


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
            else if (Visible && (keyCode == LeftKey || keyCode == AKey))
            {
                Previous();
                return true;
            }
            else if (Visible && (keyCode == RightKey || keyCode == DKey))
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

                if (keyCode == LeftKey || keyCode == RightKey || keyCode == AKey || keyCode == DKey)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
