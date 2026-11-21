// Ayar penceresi (Ui.xaml) ile programin cekirdegi arasindaki baglanti.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

[assembly: System.Reflection.AssemblyTitle("Ambiance Ultrawide")]
[assembly: System.Reflection.AssemblyProduct("Ambiance Ultrawide")]
[assembly: System.Reflection.AssemblyDescription("Ultrawide ekranlar için keskin oyun görüntüsü ve ambiyans ışığı")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]

namespace Ambiance
{
    public class WinItem
    {
        public string Title { get; set; }
        public string Info { get; set; }
        public IntPtr Hwnd;
    }

    partial class App
    {
        const string Version = "1.0";
        static Window ui;
        static bool uiSync;                 // kod tarafindan deger atanirken olaylari yok say
        static bool hideTipShown;
        static WriteableBitmap demoBmp;
        // ekran goruntusu cekerken listede sadece demo pencere gorunsun (kisisel pencere basliklari gorunmesin)
        static readonly string listFilter = Environment.GetEnvironmentVariable("AMBIYANS_LISTFILTER");

        static T F<T>(string name) where T : class { return ui.FindName(name) as T; }

        // ---------------- pencereyi ac ----------------
        static void ShowSettings()
        {
            if (ui == null) BuildUi();
            if (!ui.IsVisible) ui.Show();
            if (ui.WindowState == WindowState.Minimized) ui.WindowState = WindowState.Normal;
            ui.Activate();
            RefreshWindows();
            UiStatus();
        }

        static void BuildUi()
        {
            using (var s = typeof(App).Assembly.GetManifestResourceStream("Ambiance.Ui.xaml"))
                ui = (Window)XamlReader.Load(s);

            var logo = LoadLogo();
            F<Image>("Logo").Source = logo;
            F<Image>("AboutLogo").Source = logo;
            ui.Icon = logo;
            TranslateTree(ui);
            F<TextBlock>("VersionText").Text = L("Sürüm") + " " + Version;
            F<TextBlock>("AboutVersion").Text = L("Sürüm") + " " + Version + "  ·  " + L("Ücretsiz");

            ui.SourceInitialized += delegate { ApplyMica(ui); };
            ui.Closing += delegate (object o, System.ComponentModel.CancelEventArgs e)
            {
                if (!running) return;
                e.Cancel = true;
                ui.Hide();
                if (!hideTipShown)
                {
                    hideTipShown = true;
                    if (notify) tray.ShowBalloonTip(3000, L("Ambiance Ultrawide arka planda çalışıyor"),
                        L("Saatin yanındaki simgeden tekrar açabilir ya da Çıkış diyebilirsin."), System.Windows.Forms.ToolTipIcon.Info);
                }
            };

            // gezinme
            string[] navs = { "NavHome", "NavLight", "NavDisplay", "NavGeneral", "NavAbout" };
            string[] pages = { "PageHome", "PageLight", "PageDisplay", "PageGeneral", "PageAbout" };
            for (int i = 0; i < navs.Length; i++)
            {
                int idx = i;
                F<RadioButton>(navs[i]).Checked += delegate
                {
                    for (int j = 0; j < pages.Length; j++)
                        F<FrameworkElement>(pages[j]).Visibility = j == idx ? Visibility.Visible : Visibility.Collapsed;
                };
            }

            // ana sayfa
            F<Button>("StartBtn").Click += delegate { OnStartClick(); };
            F<Button>("RefreshBtn").Click += delegate { RefreshWindows(); };
            F<ListBox>("WinList").MouseDoubleClick += delegate { if (!active) OnStartClick(); };

            // isik
            Seg("StyleSoft", delegate { style = 0; });
            Seg("StyleDetail", delegate { style = 1; });
            var bs = F<Slider>("BrightSlider");
            bs.ValueChanged += delegate
            {
                F<TextBlock>("BrightVal").Text = Pct(bs.Value);
                if (uiSync) return;
                intensity = (float)(bs.Value / 100.0);
                SaveSettings();
            };
            var ss = F<Slider>("SpeedSlider");
            ss.ValueChanged += delegate
            {
                F<TextBlock>("SpeedVal").Text = ((int)Math.Round(ss.Value)).ToString();
                if (uiSync) return;
                Smooth = SpeedToSmooth((int)Math.Round(ss.Value));
                SaveSettings();
            };
            Seg("Fps20", delegate { FrameMs = 50; });
            Seg("Fps30", delegate { FrameMs = 33; });
            Seg("Fps60", delegate { FrameMs = 16; });
            Seg("Feather0", delegate { feather = 0; });
            Seg("Feather32", delegate { feather = 32; });
            Seg("Feather64", delegate { feather = 64; });
            Seg("Feather110", delegate { feather = 110; });

            // goruntu
            Seg("Size1920", delegate { forceW = 1920; forceH = 1080; ApplyForceSizeLive(); });
            Seg("Size2240", delegate { forceW = 2240; forceH = 1260; ApplyForceSizeLive(); });
            Seg("Size2400", delegate { forceW = 2400; forceH = 1350; ApplyForceSizeLive(); });
            Seg("Size2560", delegate { forceW = 2560; forceH = 1440; ApplyForceSizeLive(); });
            Seg("Size0", delegate { forceW = 0; forceH = 0; });
            Toggle("CenterToggle", delegate (bool v) { centerOnStart = v; });
            Seg("Zoom0", delegate { SetZoom(0); });
            Seg("Zoom110", delegate { SetZoom(1.10f); });
            Seg("Zoom120", delegate { SetZoom(1.20f); });
            Seg("ZoomFit", delegate { SetZoom(-1); });
            Toggle("SmoothZoomToggle", delegate (bool v) { zoomSmooth = v; lastZoomS = 0; if (active) ApplyZoom(lastHole); });
            if (!magReady)
            {
                foreach (var n in new[] { "Zoom0", "Zoom110", "Zoom120", "ZoomFit", "SmoothZoomToggle" }) F<UIElement>(n).IsEnabled = false;
            }

            // genel
            Toggle("AutostartToggle", delegate (bool v) { SetAutostart(v); });
            Toggle("NotifyToggle", delegate (bool v) { notify = v; });
            F<Button>("ResetBtn").Click += delegate
            {
                try { System.IO.File.Delete(SettingsPath()); } catch { }
                intensity = 0.85f; Smooth = 0.22f; FrameMs = 33; style = 0; feather = 0;
                forceW = 1920; forceH = 1080; centerOnStart = true; zoomPct = 0; zoomSmooth = false; notify = true;
                if (active) { ApplyZoom(lastHole); ApplyForceSizeLive(); }
                SaveSettings();
                SyncUi();
                UiMessage("Ayarlar varsayılana döndü.");
            };

            SyncUi();
        }

        static void SetZoom(float v)
        {
            zoomPct = v; zoomOn = true;
            if (active) ApplyZoom(lastHole);
            UiStatus();
        }

        // tepki hizi 1..10  <->  yumusatma katsayisi
        static float SpeedToSmooth(int v) { return 0.06f + (v - 1) * (0.44f / 9f); }
        static int SmoothToSpeed(float s) { return Math.Max(1, Math.Min(10, (int)Math.Round((s - 0.06f) / (0.44f / 9f)) + 1)); }

        static void Seg(string name, Action onPick)
        {
            F<RadioButton>(name).Checked += delegate
            {
                if (uiSync) return;
                onPick();
                SaveSettings();
            };
        }

        static void Toggle(string name, Action<bool> onChange)
        {
            var c = F<CheckBox>(name);
            RoutedEventHandler h = delegate
            {
                if (uiSync) return;
                onChange(c.IsChecked == true);
                SaveSettings();
            };
            c.Checked += h; c.Unchecked += h;
        }

        static void Check(string name, bool v) { var r = F<ToggleButton>(name); if (r != null) r.IsChecked = v; }

        // ayarlari arayuze yansit
        static void SyncUi()
        {
            if (ui == null) return;
            uiSync = true;
            try
            {
                Check("StyleSoft", style == 0); Check("StyleDetail", style == 1);
                F<Slider>("BrightSlider").Value = Math.Round(intensity * 100);
                F<TextBlock>("BrightVal").Text = Pct(intensity * 100);
                int sp = SmoothToSpeed(Smooth);
                F<Slider>("SpeedSlider").Value = sp;
                F<TextBlock>("SpeedVal").Text = sp.ToString();
                int fps = 1000 / FrameMs;
                Check("Fps20", fps <= 25); Check("Fps30", fps > 25 && fps < 45); Check("Fps60", fps >= 45);
                Check("Feather0", feather == 0); Check("Feather32", feather == 32); Check("Feather64", feather == 64); Check("Feather110", feather == 110);
                Check("Size1920", forceW == 1920); Check("Size2240", forceW == 2240); Check("Size2400", forceW == 2400);
                Check("Size2560", forceW == 2560); Check("Size0", forceW == 0);
                Check("CenterToggle", centerOnStart);
                Check("Zoom0", zoomPct == 0); Check("Zoom110", Math.Abs(zoomPct - 1.1f) < 0.01f);
                Check("Zoom120", Math.Abs(zoomPct - 1.2f) < 0.01f); Check("ZoomFit", zoomPct < 0);
                Check("SmoothZoomToggle", zoomSmooth);
                Check("AutostartToggle", IsAutostart());
                Check("NotifyToggle", notify);
            }
            finally { uiSync = false; }
        }

        // ---------------- durum ----------------
        static void UiStatus()
        {
            if (ui == null) return;
            var green = new SolidColorBrush(Color.FromRgb(74, 222, 128));
            var grey = new SolidColorBrush(Color.FromArgb(0x80, 255, 255, 255));
            string title = active ? WindowTitle(game) : "";
            F<Ellipse>("StatusDot").Fill = active ? green : grey;
            F<Ellipse>("SideDot").Fill = active ? green : grey;
            F<TextBlock>("StatusText").Text = L(active ? "Çalışıyor" : "Kapalı");
            F<TextBlock>("SideStatus").Text = L(active ? "Çalışıyor" : "Kapalı");
            F<TextBlock>("StatusTitle").Text = active ? (title.Length > 0 ? title : L("Oyun")) : L("Başlamaya hazır");
            F<TextBlock>("StatusSub").Text = L(active
                ? "Ambiyans ışığı açık. Durdurmak için Ctrl\u2060+\u2060Alt\u2060+\u2060A'ya bas ya da aşağıdaki butonu kullan."
                : "Aşağıdan oyununu seç ya da oyundayken Ctrl\u2060+\u2060Alt\u2060+\u2060A'ya bas.");
            F<TextBlock>("StartText").Text = L(active ? "Durdur" : "Başlat");
            F<TextBlock>("StartIcon").Text = active ? "" : "";

            // onizleme: canli ambiyans ya da ornek
            var prev = F<Image>("Preview");
            var box = F<Border>("GameBox");
            if (active && bmp != null)
            {
                prev.Source = bmp;
                float mw = mon.Right - mon.Left, mh = mon.Bottom - mon.Top;
                float s = lastZoomS > 1.001f ? lastZoomS : 1f;
                float hw = (lastHole.Right - lastHole.Left) * s, hh = (lastHole.Bottom - lastHole.Top) * s;
                box.Width = Math.Max(20, Math.Min(344, 344 * hw / mw));
                box.Height = Math.Max(10, Math.Min(144, 144 * hh / mh));
            }
            else
            {
                prev.Source = DemoBitmap();
                box.Width = 192; box.Height = 108;
            }
        }

        static void UiMessage(string text)
        {
            if (ui == null || !ui.IsVisible) return;
            F<TextBlock>("StatusSub").Text = L(text);
        }

        static void OnStartClick()
        {
            if (active) { Deactivate(); return; }
            var item = F<ListBox>("WinList").SelectedItem as WinItem;
            if (item == null || !N.IsWindow(item.Hwnd))
            {
                UiMessage("Önce listeden oyun penceresini seç. Görmüyorsan oyunu açıp Yenile'ye bas.");
                RefreshWindows();
                return;
            }
            Activate(item.Hwnd);
            if (active) ui.WindowState = WindowState.Minimized;
        }

        // ---------------- pencere listesi ----------------
        delegate bool EnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int val, int size);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
        [StructLayout(LayoutKind.Sequential)] struct MARGINS { public int l, r, t, b; }
        [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr h, ref MARGINS m);

        static string WindowTitle(IntPtr h)
        {
            int n = GetWindowTextLength(h);
            if (n <= 0) return "";
            var sb = new StringBuilder(n + 1);
            GetWindowText(h, sb, sb.Capacity);
            return sb.ToString();
        }

        static void RefreshWindows()
        {
            if (ui == null) return;
            var list = F<ListBox>("WinList");
            var prevSel = list.SelectedItem as WinItem;
            IntPtr keep = prevSel != null ? prevSel.Hwnd : (active ? game : IntPtr.Zero);
            var items = new List<WinItem>();
            uint myPid = (uint)Process.GetCurrentProcess().Id;
            IntPtr uiHwnd = new WindowInteropHelper(ui).Handle;
            EnumWindows(delegate (IntPtr h, IntPtr l)
            {
                if (!N.IsWindowVisible(h) || h == uiHwnd || h == hwnd) return true;
                if (GetWindow(h, 4 /*GW_OWNER*/) != IntPtr.Zero) return true;
                long ex = N.GetWindowLongPtr(h, N.GWL_EXSTYLE).ToInt64();
                if ((ex & N.WS_EX_TOOLWINDOW) != 0) return true;
                int cloaked;
                if (DwmGetWindowAttribute(h, 14 /*CLOAKED*/, out cloaked, 4) == 0 && cloaked != 0) return true;
                string t = WindowTitle(h);
                if (t.Length == 0 || t == "Program Manager") return true;
                if (listFilter != null && t.IndexOf(listFilter, StringComparison.OrdinalIgnoreCase) < 0) return true;
                uint pid; GetWindowThreadProcessId(h, out pid);
                if (pid == myPid) return true;
                N.RECT cr; N.GetClientRect(h, out cr);
                if (cr.Right < 200 || cr.Bottom < 150) return true;
                string proc = "";
                try { proc = Process.GetProcessById((int)pid).ProcessName; } catch { }
                items.Add(new WinItem { Title = t, Info = proc + "  ·  " + cr.Right + "×" + cr.Bottom, Hwnd = h });
                return true;
            }, IntPtr.Zero);
            list.ItemsSource = items;
            foreach (var it in items) if (it.Hwnd == keep) { list.SelectedItem = it; break; }
            if (list.SelectedItem == null && items.Count > 0) list.SelectedIndex = 0;
        }

        // Oyun penceresinde Windows 11'in kenar cizgisini ve yuvarlak koselerini kapat / geri ac
        static void SetGameFrame(IntPtr g, bool clean)
        {
            if (g == IntPtr.Zero || !N.IsWindow(g)) return;
            int corner = clean ? 1 /*DONOTROUND*/ : 0 /*DEFAULT*/;
            DwmSetWindowAttribute(g, 33 /*WINDOW_CORNER_PREFERENCE*/, ref corner, 4);
            int border = clean ? unchecked((int)0xFFFFFFFE) /*COLOR_NONE*/ : unchecked((int)0xFFFFFFFF) /*COLOR_DEFAULT*/;
            DwmSetWindowAttribute(g, 34 /*BORDER_COLOR*/, ref border, 4);
        }

        // ---------------- gorunum ----------------
        static void ApplyMica(Window w)
        {
            IntPtr h = new WindowInteropHelper(w).Handle;
            int dark = 1;
            DwmSetWindowAttribute(h, 20 /*IMMERSIVE_DARK_MODE*/, ref dark, 4);
            int corner = 2; // yuvarlak koseler
            DwmSetWindowAttribute(h, 33, ref corner, 4);
            int mica = 2;   // DWMSBT_MAINWINDOW (Mica)
            if (DwmSetWindowAttribute(h, 38 /*SYSTEMBACKDROP_TYPE*/, ref mica, 4) == 0)
            {
                w.Background = Brushes.Transparent;
                var src = HwndSource.FromHwnd(h);
                if (src != null && src.CompositionTarget != null) src.CompositionTarget.BackgroundColor = Colors.Transparent;
                var m = new MARGINS { l = -1, r = -1, t = -1, b = -1 };
                DwmExtendFrameIntoClientArea(h, ref m);
            }
        }

        static BitmapSource LoadLogo()
        {
            try
            {
                using (var s = typeof(App).Assembly.GetManifestResourceStream("Ambiance.logo.png"))
                {
                    var b = new BitmapImage();
                    b.BeginInit(); b.CacheOption = BitmapCacheOption.OnLoad; b.StreamSource = s; b.EndInit(); b.Freeze();
                    return b;
                }
            }
            catch { return null; }
        }

        // kapaliyken onizlemede gosterilen ornek ambiyans
        static WriteableBitmap DemoBitmap()
        {
            if (demoBmp != null) return demoBmp;
            int W = 172, H = 72;
            var px = new byte[W * H * 4];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)(W - 1), v = y / (float)(H - 1);
                    float dl = (float)Math.Exp(-u * 4.0) * (0.55f + 0.45f * (1 - Math.Abs(v - 0.55f) * 2));
                    float dr = (float)Math.Exp(-(1 - u) * 4.0) * (0.55f + 0.45f * (1 - Math.Abs(v - 0.4f) * 2));
                    float r = 255 * dl * 0.95f + 160 * dr * 0.85f;
                    float g = 120 * dl * 0.95f + 60 * dr * 0.85f;
                    float b = 50 * dl * 0.95f + 255 * dr * 0.85f;
                    int j = (y * W + x) * 4;
                    px[j] = Clamp(b); px[j + 1] = Clamp(g); px[j + 2] = Clamp(r); px[j + 3] = 255;
                }
            demoBmp = new WriteableBitmap(W, H, 96, 96, PixelFormats.Bgr32, null);
            demoBmp.WritePixels(new Int32Rect(0, 0, W, H), px, W * 4, 0);
            return demoBmp;
        }

        // ---------------- Windows ile baslat ----------------
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        static bool IsAutostart()
        {
            if (IsPackaged()) { StoreStartupRefreshToggle(); return false; } // asenkron guncellenir
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue("Ambiance Ultrawide") != null;
            }
            catch { return false; }
        }

        static void SetAutostart(bool on)
        {
            if (IsPackaged()) { StoreStartupSet(on); return; }
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (on) k.SetValue("Ambiance Ultrawide", "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\" --tray");
                    else k.DeleteValue("Ambiance Ultrawide", false);
                }
            }
            catch { UiMessage("Başlangıç ayarı değiştirilemedi."); }
        }
    }
}
