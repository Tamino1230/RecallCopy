using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;


namespace RecallCopy
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            MainForm form = new MainForm();

            HotKeyManager.RegisterHotKey(form.Handle, HotKeyManager.HOTKEY_ID, HotKeyManager.MOD_ALT, HotKeyManager.VK_Q);

            Application.Run(form);
        }

    }
    static class HotKeyManager
    {
        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public const int HOTKEY_ID = 9000;
        public const uint MOD_ALT = 0x0001;
        public const uint VK_Q = 0x51;
    }
}
