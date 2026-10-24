using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

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
        const uint SPI_SETCURSORS = 0x57;
        const uint SPIF_UPDATEINIFILE = 0x1;

        const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValueName = "SoftCursorManager";
        const string CursorsKeyPath = @"Control Panel\Cursors";
        const string SchemesKeyPath = @"Control Panel\Cursors\Schemes";

        // registry slots in Windows scheme-string order
        static readonly string[] Slots = {
            "Arrow", "Help", "AppStarting", "Wait", "Crosshair", "IBeam", "NWPen", "No",
            "SizeAll", "SizeNESW", "SizeNS", "SizeNWSE", "SizeWE", "UpArrow", "Hand",
            "Person", "Pin" };

        static readonly string[] AeroDefaults = {
            "%SYSTEMROOT%\\Cursors\\aero_arrow.cur",
            "%SYSTEMROOT%\\Cursors\\aero_helpsel.cur",
            "%SYSTEMROOT%\\Cursors\\aero_working.ani",
            "%SYSTEMROOT%\\Cursors\\aero_busy.ani",
            "%SYSTEMROOT%\\Cursors\\aero_cross.cur",
            "%SYSTEMROOT%\\Cursors\\aero_ibeam.cur",
            "%SYSTEMROOT%\\Cursors\\aero_pen.cur",
            "%SYSTEMROOT%\\Cursors\\aero_unavail.cur",
            "%SYSTEMROOT%\\Cursors\\aero_move.cur",
            "%SYSTEMROOT%\\Cursors\\aero_nesw.cur",
            "%SYSTEMROOT%\\Cursors\\aero_ns.cur",
            "%SYSTEMROOT%\\Cursors\\aero_nwse.cur",
            "%SYSTEMROOT%\\Cursors\\aero_ew.cur",
            "%SYSTEMROOT%\\Cursors\\aero_up.cur",
            "%SYSTEMROOT%\\Cursors\\aero_link.cur",
            "", "" };

        // install.inf [Strings] key -> slot index
        static readonly Dictionary<string, int> InfKeyToSlot = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "pointer", 0 }, { "help", 1 }, { "working", 2 }, { "busy", 3 },
            { "precision", 4 }, { "text", 5 }, { "hand", 6 }, { "unavailable", 7 },
            { "move", 8 }, { "dgn2", 9 }, { "vert", 10 }, { "dgn1", 11 },
            { "horz", 12 }, { "alternate", 13 }, { "link", 14 },
            { "person", 15 }, { "pin", 16 }
        };

        static NotifyIcon tray;
        static System.Windows.Forms.Timer timer;
        static ToolStripMenuItem menuSoft;
        static ToolStripMenuItem menuHard;
        static ToolStripMenuItem menuAuto;
        static ToolStripMenuItem menuSchemes;
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
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKeyPath))
            {
                return k != null && k.GetValue(RunValueName) != null;
            }
        }

        static void SetAutoStart(bool on)
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
            {
                if (on)
                    k.SetValue(RunValueName, "\"" + Application.ExecutablePath + "\"");
                else if (k.GetValue(RunValueName) != null)
                    k.DeleteValue(RunValueName);
            }
        }

        // ---------------- cursor scheme management ----------------

        static string ExpandPath(string p)
        {
            if (p == null) return "";
            p = p.Trim().Trim('"').Trim();
            if (p.Length == 0) return "";
            try { return Environment.ExpandEnvironmentVariables(p); }
            catch { return p; }
        }

        static string GetActiveSchemeName()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(CursorsKeyPath))
            {
                object v = k == null ? null : k.GetValue(null);
                return v as string ?? "";
            }
        }

        static Dictionary<string, string> GetSchemes()
        {
            Dictionary<string, string> dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ReadSchemesFrom(Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Cursors\Schemes"), dict);
            ReadSchemesFrom(Registry.CurrentUser.OpenSubKey(SchemesKeyPath), dict);
            return dict;
        }

        static void ReadSchemesFrom(RegistryKey k, Dictionary<string, string> dict)
        {
            if (k == null) return;
            foreach (string name in k.GetValueNames())
            {
                object v = k.GetValue(name);
                if (v is string && name.Length > 0 && !dict.ContainsKey(name))
                    dict[name] = (string)v;
            }
        }

        static void RefreshCursors()
        {
            int v = 0;
            SystemParametersInfo(SPI_SETCURSORS, 0, ref v, 0);
        }

        static string BuildAeroRaw()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 15; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(AeroDefaults[i]);
            }
            return sb.ToString();
        }

        static void ApplySchemeRaw(string name, string raw)
        {
            string[] parts = raw.Split(',');
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(CursorsKeyPath))
            {
                for (int i = 0; i < Slots.Length; i++)
                {
                    string path = i < parts.Length ? ExpandPath(parts[i]) : "";
                    if (path.Length == 0 || !File.Exists(path))
                        path = ExpandPath(AeroDefaults[i]);
                    if (i >= 15)
                    {
                        // Person / Pin: Windows default does not define them
                        if (File.Exists(path))
                            k.SetValue(Slots[i], path, RegistryValueKind.ExpandString);
                        else
                            { try { k.DeleteValue(Slots[i], false); } catch { } }
                        continue;
                    }
                    k.SetValue(Slots[i], path, RegistryValueKind.ExpandString);
                }
                k.SetValue(null, name, RegistryValueKind.String);
            }
            RefreshCursors();
            Log("scheme applied: " + name);
        }

        static void RebuildSchemeMenu()
        {
            menuSchemes.DropDownItems.Clear();

            string active = GetActiveSchemeName();

            ToolStripMenuItem defItem = new ToolStripMenuItem("Windows 默认");
            defItem.Checked = (active.Length == 0 || active == "Windows 默认");
            defItem.Click += delegate { ApplySchemeRaw("Windows 默认", BuildAeroRaw()); };
            menuSchemes.DropDownItems.Add(defItem);
            menuSchemes.DropDownItems.Add(new ToolStripSeparator());

            Dictionary<string, string> schemes = GetSchemes();
            List<string> names = new List<string>(schemes.Keys);
            names.Sort(StringComparer.CurrentCulture);
            foreach (string n in names)
            {
                string capturedName = n;
                string capturedRaw = schemes[n];
                ToolStripMenuItem it = new ToolStripMenuItem(n);
                it.Checked = (active == n);
                it.Click += delegate { ApplySchemeRaw(capturedName, capturedRaw); };
                menuSchemes.DropDownItems.Add(it);
            }
        }

        // ---------------- skin import ----------------

        static Dictionary<string, string> ParseInfStrings(string path)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            byte[] b = File.ReadAllBytes(path);
            string text;
            if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE)
                text = Encoding.Unicode.GetString(b, 2, b.Length - 2);
            else
                text = Encoding.GetEncoding(936).GetString(b);
            bool inStrings = false;
            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.Trim().TrimEnd('\r');
                if (line.Length == 0 || line.StartsWith(";")) continue;
                if (line.StartsWith("["))
                {
                    inStrings = (line.ToLowerInvariant() == "[strings]");
                    continue;
                }
                if (!inStrings) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim().Trim('"');
                dict[key] = val;
            }
            return dict;
        }

        static int GuessSlot(string n)
        {
            if (n.Contains("normal") || n.Contains("arrow")) return 0;
            if (n.Contains("help")) return 1;
            if (n.Contains("working") || n.Contains("appstart")) return 2;
            if (n.Contains("busy") || n.Contains("wait")) return 3;
            if (n.Contains("precision") || n.Contains("crosshair")) return 4;
            if (n.Contains("text") || n.Contains("ibeam") || n.Contains("beam")) return 5;
            if (n.Contains("handwrit") || n.Contains("nwpen") || n.Contains("pen_")) return 6;
            if (n.Contains("unavail") || n == "no" || n.Contains("no_m")) return 7;
            if (n.Contains("move") || n.Contains("sizeall")) return 8;
            if (n.Contains("nesw") || n.Contains("diag2") || n.Contains("diagonal2")) return 9;
            if (n.Contains("nwse") || n.Contains("diag1") || n.Contains("diagonal1")) return 11;
            if (n.Contains("vertical") || n.Contains("vert") || n.EndsWith("ns")) return 10;
            if (n.Contains("horizontal") || n.Contains("horz") || n.EndsWith("we")) return 12;
            if (n.Contains("up") || n.Contains("alternate")) return 13;
            if (n.Contains("link")) return 14;
            if (n.Contains("hand")) return 6;
            if (n.Contains("person")) return 15;
            if (n.Contains("pin")) return 16;
            return -1;
        }

        static string LocalCursorDir(string name)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cursors", name);
        }

        static string ImportSkinFromFolder(string dir)
        {
            Dictionary<int, string> slotFile = new Dictionary<int, string>();

            // 1) install.inf mapping first (most accurate)
            string infPath = null;
            foreach (string f in Directory.GetFiles(dir, "*.inf"))
            {
                infPath = f;
                break;
            }
            if (infPath != null)
            {
                Dictionary<string, string> inf = ParseInfStrings(infPath);
                foreach (KeyValuePair<string, int> pair in InfKeyToSlot)
                {
                    string fn;
                    if (inf.TryGetValue(pair.Key, out fn))
                    {
                        string full = Path.Combine(dir, fn);
                        if (File.Exists(full) && !slotFile.ContainsKey(pair.Value))
                            slotFile[pair.Value] = full;
                    }
                }
            }

            // 2) filename guessing for anything still missing
            foreach (string f in Directory.GetFiles(dir))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext != ".cur" && ext != ".ani") continue;
                string baseName = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                int slot = GuessSlot(baseName);
                if (slot >= 0 && !slotFile.ContainsKey(slot))
                    slotFile[slot] = f;
            }

            if (slotFile.Count == 0)
            {
                MessageBox.Show("该文件夹里没有识别出指针文件（.cur / .ani）。\n\n如果文件在子文件夹里，请直接选择包含 .cur / .ani 的那一层。",
                    "导入失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            // 3) unique install dir
            string name = Path.GetFileName(dir.TrimEnd('\\', '/'));
            if (name.Length == 0) name = "导入的皮肤";
            string target = LocalCursorDir(name);
            int suffix = 2;
            while (Directory.Exists(target))
            {
                target = LocalCursorDir(name + "-" + suffix);
                suffix++;
            }
            string finalName = Path.GetFileName(target);
            Directory.CreateDirectory(target);

            // 4) copy files and build scheme string
            string[] parts = new string[Slots.Length];
            for (int i = 0; i < Slots.Length; i++)
            {
                string src;
                if (slotFile.TryGetValue(i, out src))
                {
                    string dst = Path.Combine(target, Path.GetFileName(src));
                    File.Copy(src, dst, true);
                    parts[i] = "%LOCALAPPDATA%\\Cursors\\" + finalName + "\\" + Path.GetFileName(src);
                }
                else
                {
                    parts[i] = AeroDefaults[i];
                }
            }
            string raw = string.Join(",", parts);

            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(SchemesKeyPath))
            {
                k.SetValue(finalName, raw);
            }
            ApplySchemeRaw(finalName, raw);
            return finalName;
        }

        static void ImportSkin()
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "选择包含指针文件的文件夹（.cur / .ani，可含 install.inf）";
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try
                {
                    string imported = ImportSkinFromFolder(dlg.SelectedPath);
                    if (imported != null)
                    {
                        tray.ShowBalloonTip(2000, "SoftCursor 管理器",
                            "皮肤已导入并应用：" + imported, ToolTipIcon.Info);
                        Log("skin imported: " + imported);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("导入失败：" + ex.Message, "导入失败",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ---------------- main ----------------

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

            menuSchemes = new ToolStripMenuItem("指针方案");
            menuSchemes.DropDownOpening += delegate { RebuildSchemeMenu(); };

            ToolStripMenuItem menuImport = new ToolStripMenuItem("导入指针皮肤...");
            menuImport.Click += delegate { ImportSkin(); };

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
            menu.Items.Add(menuSchemes);
            menu.Items.Add(menuImport);
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
