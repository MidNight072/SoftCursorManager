using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace SoftCursorManager
{
    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SystemParametersInfo(uint action, uint param, ref int value, uint ini);
        [DllImport("user32.dll")]
        static extern bool SystemParametersInfo(uint action, uint param, IntPtr value, uint ini);

        const uint SPI_SETMOUSETRAILS = 0x5D;
        const uint SPI_GETMOUSETRAILS = 0x5E;
        const uint SPIF_UPDATEINIFILE = 0x1;

        const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValueName = "SoftCursorManager";

        static NotifyIcon tray;
        static System.Windows.Forms.Timer timer;
        static ToolStripMenuItem menuSoft;
        static ToolStripMenuItem menuHard;
        static ToolStripMenuItem menuAuto;
        static Mutex mutex;

        static string LogPath
        {
            get { return Path.Combine(Path.GetTempPath(), "SoftCursorManager.log"); }
        }

        static int GetTrails()
        {
            int v = -99;
            SystemParametersInfo(SPI_GETMOUSETRAILS, 0, ref v, 0);
            return v;
        }

        static void SetTrails(int v)
        {
            SystemParametersInfo(SPI_SETMOUSETRAILS, unchecked((uint)v), IntPtr.Zero, SPIF_UPDATEINIFILE);
        }

        static void Log(string msg)
        {
            try
            {
                File.AppendAllText(LogPath,
                    string.Format("{0:yyyy-MM-dd HH:mm:ss} {1}\r\n", DateTime.Now, msg));
            }
            catch { }
        }

        static Icon MakeIcon()
        {
            Bitmap bmp = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                Point[] pts = new Point[] {
                    new Point(3,1), new Point(3,12), new Point(6,9), new Point(8,14),
                    new Point(10,13), new Point(8,8), new Point(12,8) };
                g.FillPolygon(Brushes.White, pts);
                g.DrawPolygon(Pens.Black, pts);
            }
            Icon icon = Icon.FromHandle(bmp.GetHicon());
            return icon;
        }

        static void UpdateMenuChecks()
        {
            int v = GetTrails();
            menuSoft.Checked = (v != 0 && v != 1);
            menuHard.Checked = !menuSoft.Checked;
            menuAuto.Checked = IsAutoStart();
        }

        static bool IsAutoStart()
        {
            using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath))
            {
                return k != null && k.GetValue(RunValueName) != null;
            }
        }

        static void SetAutoStart(bool on)
        {
            using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
            {
                if (on)
                    k.SetValue(RunValueName, "\"" + Application.ExecutablePath + "\"");
                else if (k.GetValue(RunValueName) != null)
                    k.DeleteValue(RunValueName);
            }
        }

        [STAThread]
        static void Main()
        {
            bool created;
            mutex = new Mutex(true, "SoftCursorManager", out created);
            if (!created) return;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool software = !(GetTrails() == 0 || GetTrails() == 1);
            if (!software) { SetTrails(-1); Log("startup: reverted state found, applied software cursor"); }
            else Log("startup: software cursor already active");

            ContextMenuStrip menu = new ContextMenuStrip();

            menuSoft = new ToolStripMenuItem("软件光标(保活中)");
            menuSoft.Click += delegate { SetTrails(-1); timer.Start(); UpdateMenuChecks(); };
            menuHard = new ToolStripMenuItem("硬件光标(暂停保活)");
            menuHard.Click += delegate { SetTrails(0); timer.Stop(); UpdateMenuChecks(); };
            menuAuto = new ToolStripMenuItem("开机自启");
            menuAuto.Click += delegate { SetAutoStart(!IsAutoStart()); UpdateMenuChecks(); };
            ToolStripMenuItem menuLog = new ToolStripMenuItem("打开日志");
            menuLog.Click += delegate
            {
                if (!File.Exists(LogPath)) File.WriteAllText(LogPath, "");
                Process.Start("notepad.exe", LogPath);
            };
            ToolStripMenuItem menuExit = new ToolStripMenuItem("退出");
            menuExit.Click += delegate { tray.Visible = false; Application.Exit(); };

            menu.Items.Add(menuSoft);
            menu.Items.Add(menuHard);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(menuAuto);
            menu.Items.Add(menuLog);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(menuExit);

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 3000;
            timer.Tick += delegate
            {
                int v = GetTrails();
                if (v == 0 || v == 1)
                {
                    SetTrails(-1);
                    Log(string.Format("reapplied (was {0})", v));
                }
                UpdateMenuChecks();
            };
            timer.Start();

            tray = new NotifyIcon();
            tray.Icon = MakeIcon();
            tray.Text = "软光标保活:运行中";
            tray.ContextMenuStrip = menu;
            tray.Visible = true;
            tray.MouseClick += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) UpdateMenuChecks();
            };

            UpdateMenuChecks();
            tray.ShowBalloonTip(2000, "SoftCursor 管理器", "软件光标保活已启动,游戏重置光标后会自动恢复", ToolTipIcon.Info);

            Application.Run();
        }
    }
}
