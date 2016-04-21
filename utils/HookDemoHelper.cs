using System;
using System.Runtime.InteropServices;

namespace WarThunderParser.Utils
{
    internal class HookDemoHelper
    {
        private const int WH_KEYBOARD_LL = 13;


        private LowLevelKeyboardProcDelegate m_callback;
        private IntPtr m_hHook;


        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(
            int idHook,
            LowLevelKeyboardProcDelegate lpfn,
            IntPtr hMod, int dwThreadId);


        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);


        [DllImport("Kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(IntPtr lpModuleName);


        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallNextHookEx(
            IntPtr hhk,
            int nCode, IntPtr wParam, IntPtr lParam);


        private IntPtr LowLevelKeyboardHookProc(
            int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0)
            {
                return CallNextHookEx(m_hHook, nCode, wParam, lParam);
            }
            else
            {
                var khs = (KeyboardHookStruct)
                    Marshal.PtrToStructure(lParam,
                        typeof(KeyboardHookStruct));
                
                /*   Debug.Print("Hook: Code: {0}, WParam: {1},{2},{3},{4} ",
                            nCode, wParam, lParam,
                            khs.VirtualKeyCode,
                            khs.ScanCode, khs.Flags, khs.Time);

                Debug.Print(khs.VirtualKeyCode.ToString());*/
                OnHook(new HookEventArgs(khs.VirtualKeyCode));

                /*if (khs.VirtualKeyCode == 9 &&
                    wParam.ToInt32() == 260 &&
                    khs.ScanCode == 15) //alt+tab
                {
                    System.Console.WriteLine("Alt+Tab pressed!");
                    IntPtr val = new IntPtr(1);
                    return val;
                }*/
                //  else
                //{
                return CallNextHookEx(m_hHook, nCode, wParam, lParam);
                //    }

            }
        }


        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardHookStruct
        {
            public readonly int VirtualKeyCode;
            public readonly int ScanCode;
            public readonly int Flags;
            public readonly int Time;
            public readonly IntPtr ExtraInfo;
        }


        private delegate IntPtr LowLevelKeyboardProcDelegate(
            int nCode, IntPtr wParam, IntPtr lParam);


        public void SetHook()
        {
            m_callback = LowLevelKeyboardHookProc;
            m_hHook = SetWindowsHookEx(WH_KEYBOARD_LL,
                m_callback,
                GetModuleHandle(IntPtr.Zero), 0);
        }


        public void Unhook()
        {
            UnhookWindowsHookEx(m_hHook);
        }

        public class HookEventArgs : EventArgs
        {
            public HookEventArgs(int code)
            {
                Code = code;
            }
            public int Code;
        }

        public delegate void HookEventHandler(HookEventArgs eventArgs);

        public event HookEventHandler OnHook;

    }
}