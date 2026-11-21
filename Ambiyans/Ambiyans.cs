// Ambiyans - pencereli oyunun etrafini oyunun renkleriyle aydinlatir (YouTube ambiyans modu / Ambilight gibi).
// Kullanim: oyunu pencere modunda (ornegin 1920x1080) ac, oyun penceresine tikla, Ctrl+Alt+A'ya bas.
// Oyun ekranin ortasina alinir, cevresi oyunun kenar renkleriyle aydinlanir. Tekrar Ctrl+Alt+A ile kapanir.
// Oyunun bellegine dokunmaz; sadece ekrandaki pikselleri okur ve pencereyi ortalar.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SD = System.Drawing;
using WF = System.Windows.Forms;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]

namespace Ambiyans
{
    static class N
    {
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] public struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public int dwFlags; }
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int biSize; public int biWidth; public int biHeight; public short biPlanes; public short biBitCount;
            public int biCompression; public int biSizeImage; public int biXPelsPerMeter; public int biYPelsPerMeter;
            public int biClrUsed; public int biClrImportant;
        }

        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hwnd, out RECT r);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hwnd, ref POINT p);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] public static extern bool GetMonitorInfo(IntPtr mon, ref MONITORINFO mi);
        [DllImport("user32.dll")] public static extern int SetWindowRgn(IntPtr hwnd, IntPtr rgn, bool redraw);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int idx);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int idx, IntPtr val);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);
        [DllImport("Magnification.dll")] public static extern bool MagInitialize();
        [DllImport("Magnification.dll")] public static extern bool MagUninitialize();
        [DllImport("Magnification.dll")] public static extern bool MagSetFullscreenTransform(float scale, int x, int y);
        [DllImport("Magnification.dll")] public static extern bool MagSetFullscreenUseBitmapSmoothing(bool smooth);
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [DllImport("user32.dll")] public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int flags);

        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bi, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] public static extern int SetStretchBltMode(IntPtr hdc, int mode);
        [DllImport("gdi32.dll")] public static extern bool SetBrushOrgEx(IntPtr hdc, int x, int y, IntPtr prev);
        [DllImport("gdi32.dll")] public static extern bool StretchBlt(IntPtr dst, int dx, int dy, int dw, int dh, IntPtr src, int sx, int sy, int sw, int sh, uint rop);
        [DllImport("gdi32.dll")] public static extern bool GdiFlush();
        [DllImport("gdi32.dll")] public static extern IntPtr CreateRectRgn(int l, int t, int r, int b);
        [DllImport("gdi32.dll")] public static extern int CombineRgn(IntPtr dst, IntPtr a, IntPtr b, int mode);

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10, SWP_SHOWWINDOW = 0x40;
        public const int GWL_EXSTYLE = -20;
        public const long WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
        public const int WM_HOTKEY = 0x0312;
    }

    class StripData { public int X, Y, W, H; public byte[] Px; }

    // Oyunun kenarina binen, tiklamayi gecirgen, saydam gecis seridi
    class FeatherWin : WF.Form
    {
        IntPtr mdc = IntPtr.Zero, dib = IntPtr.Zero, bits = IntPtr.Zero;
        int bw, bh;
        public FeatherWin()
        {
            FormBorderStyle = WF.FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = WF.FormStartPosition.Manual;
            Size = new SD.Size(1, 1);
        }
        protected override WF.CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // LAYERED | TRANSPARENT (tiklama gecer) | TOOLWINDOW | NOACTIVATE | TOPMOST
                cp.ExStyle |= 0x80000 | 0x20 | 0x80 | 0x08000000 | 0x8;
                return cp;
            }
        }
        protected override bool ShowWithoutActivation { get { return true; } }

        public void Push(StripData s)
        {
            IntPtr h = Handle;
            if (s.W != bw || s.H != bh || mdc == IntPtr.Zero)
            {
                if (dib != IntPtr.Zero) N.DeleteObject(dib);
                if (mdc == IntPtr.Zero) mdc = N.CreateCompatibleDC(IntPtr.Zero);
                var bi = new N.BITMAPINFOHEADER();
                bi.biSize = Marshal.SizeOf(typeof(N.BITMAPINFOHEADER));
                bi.biWidth = s.W; bi.biHeight = -s.H; bi.biPlanes = 1; bi.biBitCount = 32;
                dib = N.CreateDIBSection(mdc, ref bi, 0, out bits, IntPtr.Zero, 0);
                N.SelectObject(mdc, dib);
                bw = s.W; bh = s.H;
            }
            Marshal.Copy(s.Px, 0, bits, s.W * s.H * 4);
            if (!Visible) Show();
            var pt = new N.POINT(); pt.X = s.X; pt.Y = s.Y;
            var sz = new N.SIZE(); sz.cx = s.W; sz.cy = s.H;
            var src = new N.POINT();
            var bf = new N.BLENDFUNCTION(); bf.SourceConstantAlpha = 255; bf.AlphaFormat = 1; // AC_SRC_ALPHA
            N.UpdateLayeredWindow(h, IntPtr.Zero, ref pt, ref sz, mdc, ref src, 0, ref bf, 2 /*ULW_ALPHA*/);
        }
    }

    partial class App
    {
        // ---- ayarlar ----
        const int CapW = 160, CapH = 90;    // oyundan alinan kucuk ornek
        const int Cell = 10;                // ambiyans izgarasi: 1 hucre = 10 ekran pikseli
        static volatile int FrameMs = 33;   // kare suresi (33 = ~30 fps)
        static float intensity = 0.85f;     // parlaklik
        static float Smooth = 0.22f;        // zamansal yumusatma (0-1, kucuk = daha yumusak)
        static bool notify = true;          // Windows bildirimleri
        static volatile int style = 0;             // 0 = sade ambiyans, 1 = detayli
        static volatile int feather = 0;          // oyun kenarindaki gecis seridi genisligi (piksel, 0 = kapali)
        static FeatherWin[] strips = new FeatherWin[4]; // ust, alt, sol, sag
        static float zoomPct = 0;        // 0 = kapali, -1 = ekrani doldur (tam yukseklik), digerleri: 1.10, 1.20 ...
        static bool zoomSmooth = false;  // false = keskin (piksel korunur), true = yumusak
        static bool zoomOn = true;       // Ctrl+Alt+Z ile anlik ac/kapat
        static bool magReady;
        static float lastZoomS = 1; static int lastZoomX, lastZoomY;

        static Window win;
        static Image img;
        static IntPtr hwnd;
        static WriteableBitmap bmp;
        static WF.NotifyIcon tray;
        static WF.ToolStripMenuItem toggleItem;

        static volatile bool active;
        static volatile bool running = true;
        static IntPtr game;
        static N.RECT mon;                  // aktif monitor (fiziksel piksel)
        static int gridW, gridH;
        static N.RECT lastHole;
        static int pendingFrame;            // UI'ya giden kare bekliyor mu
        static bool centerOnStart = true;
        static volatile bool upscaleMode = false;  // Magpie / Lossless Scaling ile buyutulmus oyun icin
        static int forceW = 1920, forceH = 1080;   // acarken oyun penceresinin ic boyutu (0 = dokunma)
        static IntPtr styledWin = IntPtr.Zero;      // kenarlarini kaldirdigimiz pencere
        static long savedStyle, savedExStyle;

        [STAThread]
        static void Main(string[] args)
        {
            // tani: Ambiyans.exe --ddshot dosya.png  (ekran kartiyla tum ekrani yakalayip kaydeder)
            if (args.Length == 2 && args[0] == "--ddshot")
            {
                var bnd = WF.Screen.PrimaryScreen.Bounds;
                var gc = GpuCapture.Create(bnd.Left, bnd.Top);
                if (gc == null) { System.IO.File.WriteAllText(args[1] + ".txt", "GPU capture init failed"); return; }
                bool ok = false;
                for (int i = 0; i < 20 && !ok; i++) ok = gc.Grab(bnd.Left, bnd.Top, bnd.Right, bnd.Bottom, 800, 300, 200);
                if (!ok) { System.IO.File.WriteAllText(args[1] + ".txt", "no frame"); return; }
                var bm = new SD.Bitmap(gc.LastW, gc.LastH, SD.Imaging.PixelFormat.Format32bppRgb);
                var bd = bm.LockBits(new SD.Rectangle(0, 0, gc.LastW, gc.LastH), SD.Imaging.ImageLockMode.WriteOnly, SD.Imaging.PixelFormat.Format32bppRgb);
                for (int y = 0; y < gc.LastH; y++) Marshal.Copy(gc.Last, y * gc.LastW * 4, new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), gc.LastW * 4);
                bm.UnlockBits(bd);
                bm.Save(args[1], SD.Imaging.ImageFormat.Png);
                gc.Dispose();
                return;
            }
            bool created;
            var mutex = new Mutex(true, "Ambiyans_TekKopya", out created);
            var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Ambiyans_Goster");
            if (!created) { showEvent.Set(); return; }   // zaten calisiyor: onun penceresini one getir

            bool trayMode = false;
            foreach (string a in args) if (a == "--tray" || a == "--hwnd") trayMode = true;
            if (LaunchedByStartupTask()) trayMode = true;

            LoadSettings();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--buyutme") upscaleMode = true;
                if (args[i] == "--zoom" && i + 1 < args.Length) zoomPct = float.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture);
                if (args[i] == "--boyut" && i + 1 < args.Length)
                {
                    string[] wh = args[i + 1].Split('x');
                    forceW = int.Parse(wh[0]); forceH = int.Parse(wh[1]); upscaleMode = false;
                }
            }
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            win = new Window();
            win.WindowStyle = WindowStyle.None;
            win.ResizeMode = ResizeMode.NoResize;
            win.ShowInTaskbar = false;
            win.ShowActivated = false;
            win.Topmost = true;
            win.Background = Brushes.Black;
            win.Title = "Ambiyans";
            img = new Image();
            img.Stretch = Stretch.Fill;
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.Linear);
            // dithering: goze gorunmeyen sabit gren, koyu gecislerdeki renk basamaklarini (banding) dagitir
            var noise = new WriteableBitmap(256, 256, 96, 96, PixelFormats.Pbgra32, null);
            var npx = new byte[256 * 256 * 4];
            var rnd = new Random(12345);
            for (int i = 0; i < 256 * 256; i++)
            {
                byte a = (byte)(rnd.Next(0, 4)); // 0..3 / 255
                npx[i * 4] = a; npx[i * 4 + 1] = a; npx[i * 4 + 2] = a; npx[i * 4 + 3] = a;
            }
            noise.WritePixels(new Int32Rect(0, 0, 256, 256), npx, 256 * 4, 0);
            var nb = new ImageBrush(noise);
            nb.TileMode = TileMode.Tile; nb.Stretch = Stretch.None;
            nb.Viewport = new Rect(0, 0, 256, 256); nb.ViewportUnits = BrushMappingMode.Absolute;
            var grain = new System.Windows.Shapes.Rectangle();
            grain.Fill = nb;
            RenderOptions.SetBitmapScalingMode(grain, BitmapScalingMode.NearestNeighbor);
            var root = new System.Windows.Controls.Grid();
            root.Children.Add(img);
            root.Children.Add(grain);
            win.Content = root;

            hwnd = new WindowInteropHelper(win).EnsureHandle();
            long ex = N.GetWindowLongPtr(hwnd, N.GWL_EXSTYLE).ToInt64();
            N.SetWindowLongPtr(hwnd, N.GWL_EXSTYLE, new IntPtr(ex | N.WS_EX_TOOLWINDOW | N.WS_EX_NOACTIVATE));
            HwndSource.FromHwnd(hwnd).AddHook(WndProc);
            SetGameFrame(hwnd, true); // ambiyans penceresinde de Windows 11 cercevesi ve yuvarlak kose olmasin

            // Ctrl+Alt+A  (MOD_ALT=1 | MOD_CONTROL=2 | MOD_NOREPEAT=0x4000)
            bool hk = N.RegisterHotKey(hwnd, 1, 0x1 | 0x2 | 0x4000, 0x41);
            N.RegisterHotKey(hwnd, 2, 0x1 | 0x2 | 0x4000, 0x5A); // Ctrl+Alt+Z: buyutmeyi ac/kapat
            try { magReady = N.MagInitialize(); } catch { magReady = false; }

            for (int i = 0; i < 4; i++) strips[i] = new FeatherWin();
            SetupTray();
            if (!hk) Notify("Kısayol kullanılamıyor", "Ctrl+Alt+A başka bir program tarafından kullanılıyor. Ambiyansı ana pencereden başlatabilirsin.", true);
            if (!trayMode) ShowSettings();

            // ikinci kez acilirsa pencereyi one getir
            var evTh = new Thread(delegate ()
            {
                while (running)
                {
                    if (showEvent.WaitOne(500) && running)
                        win.Dispatcher.BeginInvoke(new Action(ShowSettings));
                }
            });
            evTh.IsBackground = true;
            evTh.Start();

            var th = new Thread(CaptureLoop);
            th.IsBackground = true;
            th.Priority = ThreadPriority.BelowNormal;
            th.Start();

            // test icin: Ambiyans.exe --hwnd <sayi>
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "--hwnd")
                {
                    long h;
                    string s = args[i + 1];
                    if (s.StartsWith("0x")) h = Convert.ToInt64(s.Substring(2), 16); else h = Convert.ToInt64(s);
                    IntPtr target = new IntPtr(h);
                    win.Dispatcher.BeginInvoke(new Action(delegate { Activate(target); }));
                }

            app.Run();
            running = false;
            ResetZoom();
            if (magReady) N.MagUninitialize();
            N.UnregisterHotKey(hwnd, 1);
            N.UnregisterHotKey(hwnd, 2);
            tray.Visible = false;
            tray.Dispose();
            GC.KeepAlive(mutex);
            GC.KeepAlive(showEvent);
        }

        static void SetupTray()
        {
            tray = new WF.NotifyIcon();
            tray.Icon = MakeIcon();
            tray.Text = "Ambiyans (Ctrl+Alt+A)";
            var menu = new WF.ContextMenuStrip();
            var open = new WF.ToolStripMenuItem(L("Ambiyans'ı aç"));
            open.Font = new SD.Font(open.Font, SD.FontStyle.Bold);
            open.Click += delegate { ShowSettings(); };
            menu.Items.Add(open);
            toggleItem = new WF.ToolStripMenuItem(L("Başlat / Durdur") + "   (Ctrl+Alt+A)");
            toggleItem.Click += delegate { Toggle(); };
            menu.Items.Add(toggleItem);
            menu.Items.Add(new WF.ToolStripSeparator());
            var exit = new WF.ToolStripMenuItem(L("Çıkış"));
            exit.Click += delegate { Deactivate(); Application.Current.Shutdown(); };
            menu.Items.Add(exit);

            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ShowSettings(); };
            tray.Visible = true;
        }

        // Bildirim gosterir (kapali degilse); ayar penceresi aciksa durum satirina da yazar
        static void Notify(string title, string text, bool warn)
        {
            UiMessage(text);
            if (notify && tray != null) tray.ShowBalloonTip(3500, L(title), L(text), warn ? WF.ToolTipIcon.Warning : WF.ToolTipIcon.Info);
        }

        // Ambiyans acikken pencere boyutu ayari degisirse hemen uygula
        static void ApplyForceSizeLive()
        {
            if (active && forceW > 0 && N.IsWindow(game) && !upscaleMode)
            {
                IntPtr g = game;
                SizeGame(g, forceW, forceH);
                if (centerOnStart) CenterGame(g, mon.Right - mon.Left, mon.Bottom - mon.Top);
            }
        }

        // Windows'un tam ekran buyutecini (DWM) kullanir: oyun ayni pikselleri cizer, ekranda buyuk gorunur.
        static void ApplyZoom(N.RECT hole)
        {
            if (!magReady) return;
            int mw = mon.Right - mon.Left, mh = mon.Bottom - mon.Top;
            int hw = hole.Right - hole.Left, hh = hole.Bottom - hole.Top;
            float s = 1;
            if (zoomOn && zoomPct != 0 && hw > 0 && hh > 0 && !upscaleMode)
            {
                float fit = Math.Min(mw / (float)hw, mh / (float)hh);
                s = zoomPct < 0 ? fit : Math.Min(zoomPct, fit);
            }
            int x = 0, y = 0;
            if (s > 1.001f)
            {
                float vw = mw / s, vh = mh / s;
                x = (int)Math.Round((hole.Left + hole.Right) / 2f - vw / 2f);
                y = (int)Math.Round((hole.Top + hole.Bottom) / 2f - vh / 2f);
                x = Math.Max(mon.Left, Math.Min(x, (int)(mon.Right - vw)));
                y = Math.Max(mon.Top, Math.Min(y, (int)(mon.Bottom - vh)));
            }
            else s = 1;
            if (Math.Abs(s - lastZoomS) < 0.0001f && x == lastZoomX && y == lastZoomY) return;
            try { N.MagSetFullscreenUseBitmapSmoothing(zoomSmooth); } catch { }
            N.MagSetFullscreenTransform(s, x, y);
            lastZoomS = s; lastZoomX = x; lastZoomY = y;
        }

        static void ResetZoom()
        {
            if (!magReady) return;
            N.MagSetFullscreenTransform(1, 0, 0);
            lastZoomS = 1; lastZoomX = 0; lastZoomY = 0;
        }

        static void HideStrips()
        {
            for (int i = 0; i < 4; i++) if (strips[i] != null && strips[i].Visible) strips[i].Hide();
        }

        // Pencerenin kenar/baslik cubugunu kaldirir ve ic boyutunu tam w x h yapar.
        static void SizeGame(IntPtr g, int w, int h)
        {
            const long WS_CAPTION = 0x00C00000, WS_THICKFRAME = 0x00040000, WS_BORDER = 0x00800000;
            // eski tip 3B kenarlar: DLGMODALFRAME | WINDOWEDGE | CLIENTEDGE | STATICEDGE
            const long EX_EDGES = 0x1 | 0x100 | 0x200 | 0x20000;
            long st = N.GetWindowLongPtr(g, -16 /*GWL_STYLE*/).ToInt64();
            long ex = N.GetWindowLongPtr(g, N.GWL_EXSTYLE).ToInt64();
            if ((st & (WS_CAPTION | WS_THICKFRAME | WS_BORDER)) != 0 || (ex & EX_EDGES) != 0)
            {
                if (styledWin != g) { styledWin = g; savedStyle = st; savedExStyle = ex; }
                N.SetWindowLongPtr(g, -16, new IntPtr(st & ~(WS_CAPTION | WS_THICKFRAME | WS_BORDER)));
                N.SetWindowLongPtr(g, N.GWL_EXSTYLE, new IntPtr(ex & ~EX_EDGES));
            }
            const uint f = N.SWP_NOMOVE | N.SWP_NOZORDER | N.SWP_NOACTIVATE | 0x20 /*SWP_FRAMECHANGED*/;
            N.SetWindowPos(g, IntPtr.Zero, 0, 0, w, h, f);
            // kalan kenar farkini duzelt
            N.RECT wr, cr;
            N.GetWindowRect(g, out wr);
            N.GetClientRect(g, out cr);
            int dw = w - cr.Right, dh = h - cr.Bottom;
            if (dw != 0 || dh != 0)
                N.SetWindowPos(g, IntPtr.Zero, 0, 0, (wr.Right - wr.Left) + dw, (wr.Bottom - wr.Top) + dh, f);
        }

        static void RestoreGameStyle()
        {
            if (styledWin != IntPtr.Zero && N.IsWindow(styledWin))
            {
                N.SetWindowLongPtr(styledWin, -16, new IntPtr(savedStyle));
                N.SetWindowLongPtr(styledWin, N.GWL_EXSTYLE, new IntPtr(savedExStyle));
                N.SetWindowPos(styledWin, IntPtr.Zero, 0, 0, 0, 0, N.SWP_NOMOVE | N.SWP_NOSIZE | N.SWP_NOZORDER | N.SWP_NOACTIVATE | 0x20);
            }
            styledWin = IntPtr.Zero;
        }

        static SD.Icon MakeIcon()
        {
            // exe'ye gomulu ikonun kucuk boyutu (tepsi icin)
            try
            {
                using (var big = SD.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location))
                    return new SD.Icon(big, WF.SystemInformation.SmallIconSize);
            }
            catch { return SD.SystemIcons.Application; }
        }

        static IntPtr WndProc(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
        {
            if (msg == N.WM_HOTKEY && w.ToInt32() == 1) { Toggle(); handled = true; }
            if (msg == N.WM_HOTKEY && w.ToInt32() == 2) { zoomOn = !zoomOn; if (active) ApplyZoom(lastHole); handled = true; }
            return IntPtr.Zero;
        }

        static void Toggle()
        {
            if (active) { Deactivate(); return; }
            IntPtr fg = N.GetForegroundWindow();
            if (fg == IntPtr.Zero || fg == hwnd || fg == N.GetShellWindow() || !N.IsWindowVisible(fg))
            {
                Notify("Ambiyans", "Önce oyun penceresine tıkla, sonra Ctrl+Alt+A'ya bas.", false);
                return;
            }
            Activate(fg);
        }

        static void Activate(IntPtr target)
        {
            if (!N.IsWindow(target)) return;
            if (N.IsZoomed(target))
            {
                Notify("Pencere ekranı kaplıyor", "Oyunu pencereli ya da kenarlıksız modda, örneğin 1920x1080'de aç.", true);
                return;
            }
            game = target;

            IntPtr hm = N.MonitorFromWindow(target, 2 /*NEAREST*/);
            var mi = new N.MONITORINFO();
            mi.cbSize = Marshal.SizeOf(typeof(N.MONITORINFO));
            N.GetMonitorInfo(hm, ref mi);
            mon = mi.rcMonitor;
            int mw = mon.Right - mon.Left, mh = mon.Bottom - mon.Top;

            if (forceW > 0 && forceH > 0 && !upscaleMode) SizeGame(target, Math.Min(forceW, mw), Math.Min(forceH, mh));
            if (centerOnStart) CenterGame(target, mw, mh);
            SetGameFrame(target, true);

            gridW = Math.Max(8, (mw + Cell - 1) / Cell);
            gridH = Math.Max(8, (mh + Cell - 1) / Cell);
            bmp = new WriteableBitmap(gridW * 2, gridH * 2, 96, 96, PixelFormats.Bgr32, null);
            img.Source = bmp;

            lastHole = HoleRect(target);
            ApplyRegion(lastHole);
            win.Show();
            N.SetWindowPos(hwnd, N.HWND_TOPMOST, mon.Left, mon.Top, mw, mh, N.SWP_NOACTIVATE | N.SWP_SHOWWINDOW);
            N.SetForegroundWindow(target);
            zoomOn = true;
            ApplyZoom(lastHole);
            active = true;
            toggleItem.Checked = true;
            UiStatus();
        }

        static void Deactivate()
        {
            active = false;
            ResetZoom();
            if (win != null) win.Hide();
            HideStrips();
            SetGameFrame(game, false);
            RestoreGameStyle();
            if (toggleItem != null) toggleItem.Checked = false;
            UiStatus();
        }

        static void CenterGame(IntPtr g, int mw, int mh)
        {
            N.RECT wr, cr;
            N.GetWindowRect(g, out wr);
            N.GetClientRect(g, out cr);
            var p = new N.POINT();
            N.ClientToScreen(g, ref p);
            int offX = p.X - wr.Left, offY = p.Y - wr.Top;
            int cw = cr.Right, ch = cr.Bottom;
            int x = mon.Left + (mw - cw) / 2 - offX;
            int y = mon.Top + (mh - ch) / 2 - offY;
            N.SetWindowPos(g, IntPtr.Zero, x, y, 0, 0, N.SWP_NOSIZE | N.SWP_NOZORDER | N.SWP_NOACTIVATE);
        }

        static N.RECT HoleRect(IntPtr g)
        {
            N.RECT cr;
            N.GetClientRect(g, out cr);
            var r = new N.RECT();
            if (upscaleMode && cr.Right > 0 && cr.Bottom > 0)
            {
                // Magpie / Lossless Scaling oyunu ekrana sigacak sekilde (en-boy oranini koruyarak) buyutur:
                // delik = ekranin ortasinda, oyunun oranina sahip, ekrana sigan en buyuk dikdortgen
                int mw = mon.Right - mon.Left, mh = mon.Bottom - mon.Top;
                double a = cr.Right / (double)cr.Bottom;
                int w = (int)Math.Round(mh * a), h = mh;
                if (w > mw) { w = mw; h = (int)Math.Round(mw / a); }
                r.Left = mon.Left + (mw - w) / 2; r.Top = mon.Top + (mh - h) / 2;
                r.Right = r.Left + w; r.Bottom = r.Top + h;
                return r;
            }
            var p = new N.POINT();
            N.ClientToScreen(g, ref p);
            r.Left = p.X; r.Top = p.Y; r.Right = p.X + cr.Right; r.Bottom = p.Y + cr.Bottom;
            return r;
        }

        static string SettingsPath()
        {
            return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ambiyans.ini");
        }
        static void LoadSettings()
        {
            try
            {
                foreach (string line in System.IO.File.ReadAllLines(SettingsPath()))
                {
                    string[] kv = line.Split('=');
                    if (kv.Length != 2) continue;
                    if (kv[0] == "parlaklik") intensity = float.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture);
                    else if (kv[0] == "buyutme") upscaleMode = kv[1] == "1";
                    else if (kv[0] == "ortala") centerOnStart = kv[1] == "1";
                    else if (kv[0] == "gecis2") feather = int.Parse(kv[1]);
                    else if (kv[0] == "stil") style = int.Parse(kv[1]);
                    else if (kv[0] == "fps") FrameMs = 1000 / Math.Max(10, int.Parse(kv[1]));
                    else if (kv[0] == "tepki") Smooth = float.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture);
                    else if (kv[0] == "bildirim") notify = kv[1] == "1";
                    else if (kv[0] == "buyut") zoomPct = float.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture);
                    else if (kv[0] == "yumusak") zoomSmooth = kv[1] == "1";
                    else if (kv[0] == "boyut")
                    {
                        string[] wh = kv[1].Split('x');
                        if (wh.Length == 2) { forceW = int.Parse(wh[0]); forceH = int.Parse(wh[1]); }
                    }
                }
            }
            catch { }
        }
        static void SaveSettings()
        {
            try
            {
                System.IO.File.WriteAllLines(SettingsPath(), new string[] {
                    "parlaklik=" + intensity.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "buyutme=" + (upscaleMode ? "1" : "0"),
                    "ortala=" + (centerOnStart ? "1" : "0"),
                    "boyut=" + forceW + "x" + forceH,
                    "gecis2=" + feather,
                    "stil=" + style,
                    "buyut=" + zoomPct.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "yumusak=" + (zoomSmooth ? "1" : "0"),
                    "fps=" + (1000 / FrameMs),
                    "tepki=" + Smooth.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "bildirim=" + (notify ? "1" : "0") });
            }
            catch { }
        }

        static void ApplyRegion(N.RECT hole)
        {
            int mw = mon.Right - mon.Left, mh = mon.Bottom - mon.Top;
            IntPtr full = N.CreateRectRgn(0, 0, mw, mh);
            IntPtr h = N.CreateRectRgn(hole.Left - mon.Left, hole.Top - mon.Top, hole.Right - mon.Left, hole.Bottom - mon.Top);
            N.CombineRgn(full, full, h, 4 /*RGN_DIFF*/);
            N.DeleteObject(h);
            N.SetWindowRgn(hwnd, full, true); // sistem 'full'un sahibi olur
        }

        static bool Same(N.RECT a, N.RECT b) { return a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom; }

        // ---------------- yakalama + isleme (arka plan) ----------------
        static void CaptureLoop()
        {
            IntPtr sdc = N.GetDC(IntPtr.Zero);
            IntPtr mdc = N.CreateCompatibleDC(sdc);
            // nokta ornekleme ile 3 kat buyuk al, sonra 3x3 ortalama: HALFTONE'dan cok daha ucuz
            const int SS = 3, RW = CapW * SS, RH = CapH * SS;
            var bi = new N.BITMAPINFOHEADER();
            bi.biSize = Marshal.SizeOf(typeof(N.BITMAPINFOHEADER));
            bi.biWidth = RW; bi.biHeight = -RH; bi.biPlanes = 1; bi.biBitCount = 32;
            IntPtr bits;
            IntPtr dib = N.CreateDIBSection(mdc, ref bi, 0, out bits, IntPtr.Zero, 0);
            N.SelectObject(mdc, dib);
            N.SetStretchBltMode(mdc, 3 /*COLORONCOLOR*/);

            byte[] raw = new byte[RW * RH * 4];
            // geometri onbellegi (delik/izgara degismedikce yeniden hesaplanmaz)
            float[] su = null, sv = null, suf = null, svf = null;
            N.RECT geoHole = new N.RECT(); int geoGW = 0, geoGH = 0, geoStyle = -1; float geoInt = -1;
            float[] cr = new float[CapW * CapH], cg = new float[CapW * CapH], cb = new float[CapW * CapH];
            float[] tmp = new float[4096 * 4];
            float[] fr = new float[CapW * CapH], fgc = new float[CapW * CapH], fb = new float[CapW * CapH];
            float[] baseR = new float[CapW * CapH], baseG = new float[CapW * CapH], baseB = new float[CapW * CapH];
            bool haveBase = false;
            GpuCapture gpu = null; int gpuFails = 0;
            float[] or = null, og = null, ob = null, pr = null, pg = null, pb = null;
            float[] nr = null, ng = null, nb = null, wN = null, wF = null;
            int curGW = 0, curGH = 0, tick = 0;
            var sw = Stopwatch.StartNew();
            string profile = Environment.GetEnvironmentVariable("AMBIYANS_PROFILE");
            long profCap = 0, profProc = 0; int profN = 0;

            while (running)
            {
                if (!active) { Thread.Sleep(100); continue; }
                long t0 = sw.ElapsedMilliseconds;
                long tStart = sw.ElapsedTicks;
                IntPtr g = game;
                if (!N.IsWindow(g))
                {
                    win.Dispatcher.BeginInvoke(new Action(Deactivate));
                    Thread.Sleep(200);
                    continue;
                }
                if (N.IsIconic(g)) { Thread.Sleep(100); continue; }

                N.RECT hole = HoleRect(g);
                int hw = hole.Right - hole.Left, hh = hole.Bottom - hole.Top;
                if (hw < 16 || hh < 16) { Thread.Sleep(100); continue; }
                if (!Same(hole, lastHole))
                {
                    lastHole = hole;
                    N.RECT hcopy = hole;
                    win.Dispatcher.BeginInvoke(new Action(delegate { if (active) { ApplyRegion(hcopy); ApplyZoom(hcopy); } }));
                }
                if (++tick % 30 == 0)
                    win.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (!active) return;
                        N.SetWindowPos(hwnd, N.HWND_TOPMOST, 0, 0, 0, 0, N.SWP_NOMOVE | N.SWP_NOSIZE | N.SWP_NOACTIVATE);
                        for (int s = 0; s < 4; s++)
                            if (strips[s].Visible) N.SetWindowPos(strips[s].Handle, N.HWND_TOPMOST, 0, 0, 0, 0, N.SWP_NOMOVE | N.SWP_NOSIZE | N.SWP_NOACTIVATE);
                    }));

                // hangi kenarlarda ambiyans alani var (oyun ekran kenarina dayaliysa o kenarda serit olmaz)
                int F = feather;
                if (F * 3 > Math.Min(hw, hh)) F = Math.Min(hw, hh) / 3;
                // buyutme aciksa ekranda gorunen (buyutulmus) delik; ekran yakalama da buyutulmus goruntuyu verir
                float zs = lastZoomS; int zx = lastZoomX, zy = lastZoomY;
                if (zs < 1.001f) { zs = 1; zx = mon.Left; zy = mon.Top; }
                int mL = mon.Left + (int)Math.Round((hole.Left - zx) * zs), mT = mon.Top + (int)Math.Round((hole.Top - zy) * zs);
                int mR = mon.Left + (int)Math.Round((hole.Right - zx) * zs), mB = mon.Top + (int)Math.Round((hole.Bottom - zy) * zs);
                bool topOn = mT - mon.Top >= Cell, botOn = mon.Bottom - mB >= Cell;
                bool leftOn = mL - mon.Left >= Cell, rightOn = mon.Right - mR >= Cell;

                // 1) oyunun kucuk bir kopyasini al (gecis seritlerinin altindan degil, icinden)
                int Fs = (int)(F * zs);
                int sl = Math.Max(mon.Left, mL + (leftOn ? Fs : 0)), st = Math.Max(mon.Top, mT + (topOn ? Fs : 0));
                int sr = Math.Min(mon.Right, mR - (rightOn ? Fs : 0)), sbm = Math.Min(mon.Bottom, mB - (botOn ? Fs : 0));
                if (sr - sl < 8 || sbm - st < 8) { Thread.Sleep(50); continue; }
                // once ekran karti ile dene (cok ucuz); olmazsa GDI
                if (gpu == null && gpuFails < 3)
                {
                    gpu = GpuCapture.Create(mon.Left, mon.Top);
                    if (gpu == null) gpuFails = 3;
                }
                bool gotGpu = false;
                if (gpu != null)
                {
                    try
                    {
                        if (gpu.Grab(sl, st, sr, sbm, CapW, CapH, 0) || haveBase)
                        {
                            if (gpu.Last != null) { ResampleBGRA(gpu.Last, gpu.LastW, gpu.LastH, baseR, baseG, baseB); haveBase = true; }
                            gotGpu = haveBase;
                        }
                    }
                    catch
                    {
                        gpu.Dispose(); gpu = null; gpuFails++;
                    }
                }
                if (!gotGpu)
                {
                    N.StretchBlt(mdc, 0, 0, RW, RH, sdc, sl, st, sr - sl, sbm - st, 0x00CC0020 /*SRCCOPY*/);
                    N.GdiFlush();
                    Marshal.Copy(bits, raw, 0, raw.Length);
                    const float inv9 = 1f / (SS * SS);
                    for (int y = 0; y < CapH; y++)
                        for (int x = 0; x < CapW; x++)
                        {
                            int sR = 0, sG = 0, sB = 0;
                            for (int yy = 0; yy < SS; yy++)
                            {
                                int j = ((y * SS + yy) * RW + x * SS) * 4;
                                for (int xx = 0; xx < SS; xx++, j += 4) { sB += raw[j]; sG += raw[j + 1]; sR += raw[j + 2]; }
                            }
                            int i = y * CapW + x;
                            baseR[i] = sR * inv9; baseG[i] = sG * inv9; baseB[i] = sB * inv9;
                        }
                    haveBase = true;
                }
                long tCap = sw.ElapsedTicks;
                // iki katman: "yakin" isik kenar detayini korur, "uzak" isik genis ve yumusak yayilir
                Array.Copy(baseR, cr, cr.Length); Array.Copy(baseG, cg, cg.Length); Array.Copy(baseB, cb, cb.Length);
                Array.Copy(baseR, fr, cr.Length); Array.Copy(baseG, fgc, cg.Length); Array.Copy(baseB, fb, cb.Length);
                // stil: 0 = sade (sadece renk), 1 = detayli
                int sty = style; bool soft = sty == 0;
                int capNear = soft ? 3 : 1, capFar = soft ? 10 : 5, gridNear = soft ? 4 : 1, gridFar = soft ? 14 : 7;
                float nearPx = soft ? 35f : 90f, inK = soft ? 0.25f : 0.6f, inCap = soft ? 0.12f : 0.35f;
                float dfK = soft ? 0.42f : 0.55f, floorB = soft ? 0.08f : 0.18f, sat = soft ? 1.1f : 1.2f;
                Blur(cr, CapW, CapH, capNear, tmp); Blur(cg, CapW, CapH, capNear, tmp); Blur(cb, CapW, CapH, capNear, tmp);
                for (int pass = 0; pass < 2; pass++)
                {
                    Blur(fr, CapW, CapH, capFar, tmp); Blur(fgc, CapW, CapH, capFar, tmp); Blur(fb, CapW, CapH, capFar, tmp);
                }

                // 2) ambiyans izgarasi
                int GW = gridW, GH = gridH;
                if (GW != curGW || GH != curGH)
                {
                    curGW = GW; curGH = GH;
                    int n = GW * GH;
                    nr = new float[n]; ng = new float[n]; nb = new float[n];
                    or = new float[n]; og = new float[n]; ob = new float[n];
                    pr = new float[n]; pg = new float[n]; pb = new float[n];
                    wN = new float[n]; wF = new float[n];
                }
                float hx0 = (hole.Left - mon.Left) / (float)Cell, hy0 = (hole.Top - mon.Top) / (float)Cell;
                float hx1 = (hole.Right - mon.Left) / (float)Cell, hy1 = (hole.Bottom - mon.Top) / (float)Cell;
                float hwG = Math.Max(0.001f, hx1 - hx0), hhG = Math.Max(0.001f, hy1 - hy0);
                float Dn = nearPx / Cell;                            // yakin isigin erimi
                float side = Math.Max(Math.Max(hx0, GW - hx1), Math.Max(hy0, GH - hy1));
                float Df = Math.Max(6f, side * dfK);                 // uzak isigin sonme mesafesi
                float inten = intensity;
                bool geoOk = su != null && su.Length == GW * GH && Same(geoHole, hole) && geoGW == GW && geoGH == GH && geoInt == inten && geoStyle == sty;
                if (geoOk)
                {
                    for (int k = 0; k < GW * GH; k++)
                    {
                        float u = su[k], v = sv[k], uf = suf[k], vf = svf[k];
                        nr[k] = Sample(cr, u, v); ng[k] = Sample(cg, u, v); nb[k] = Sample(cb, u, v);
                        float r2 = Sample(fr, uf, vf), g2 = Sample(fgc, uf, vf), b2 = Sample(fb, uf, vf);
                        float lum = 0.299f * r2 + 0.587f * g2 + 0.114f * b2;
                        or[k] = lum + (r2 - lum) * sat; og[k] = lum + (g2 - lum) * sat; ob[k] = lum + (b2 - lum) * sat;
                    }
                }
                else
                {
                su = new float[GW * GH]; sv = new float[GW * GH]; suf = new float[GW * GH]; svf = new float[GW * GH];
                geoHole = hole; geoGW = GW; geoGH = GH; geoInt = inten; geoStyle = sty;
                for (int y = 0; y < GH; y++)
                {
                    float py = y + 0.5f;
                    float qy = py < hy0 ? hy0 : (py > hy1 ? hy1 : py);
                    float dy = py - qy;
                    // uzak isik, uzaklastikca oyunun daha icinden renk alir (genis, dogal yayilim)
                    float qyf = qy;
                    if (dy < 0) qyf = hy0 + Math.Min(-dy * inK, hhG * inCap);
                    else if (dy > 0) qyf = hy1 - Math.Min(dy * inK, hhG * inCap);
                    float v = (qy - hy0) / hhG * (CapH - 1), vf = (qyf - hy0) / hhG * (CapH - 1);
                    for (int x = 0; x < GW; x++)
                    {
                        float px = x + 0.5f;
                        float qx = px < hx0 ? hx0 : (px > hx1 ? hx1 : px);
                        float dx = px - qx;
                        float qxf = qx;
                        if (dx < 0) qxf = hx0 + Math.Min(-dx * inK, hwG * inCap);
                        else if (dx > 0) qxf = hx1 - Math.Min(dx * inK, hwG * inCap);
                        float u = (qx - hx0) / hwG * (CapW - 1), uf = (qxf - hx0) / hwG * (CapW - 1);
                        float d = (float)Math.Sqrt(dx * dx + dy * dy);
                        int k = y * GW + x;
                        nr[k] = Sample(cr, u, v); ng[k] = Sample(cg, u, v); nb[k] = Sample(cb, u, v);
                        float r2 = Sample(fr, uf, vf), g2 = Sample(fgc, uf, vf), b2 = Sample(fb, uf, vf);
                        float lum = 0.299f * r2 + 0.587f * g2 + 0.114f * b2;   // hafif doygunluk artisi
                        or[k] = lum + (r2 - lum) * sat; og[k] = lum + (g2 - lum) * sat; ob[k] = lum + (b2 - lum) * sat;
                        wN[k] = (float)Math.Exp(-d / Dn);
                        wF[k] = inten * (floorB + (1 - floorB) * (float)Math.Exp(-d / Df));
                        su[k] = u; sv[k] = v; suf[k] = uf; svf[k] = vf;
                    }
                }
                }
                for (int pass = 0; pass < (soft ? 2 : 1); pass++)
                {
                    Blur(nr, GW, GH, gridNear, tmp); Blur(ng, GW, GH, gridNear, tmp); Blur(nb, GW, GH, gridNear, tmp);
                }
                for (int pass = 0; pass < 3; pass++)
                {
                    Blur(or, GW, GH, gridFar, tmp); Blur(og, GW, GH, gridFar, tmp); Blur(ob, GW, GH, gridFar, tmp);
                }

                // 3) katmanlari birlestir + uyarlanabilir zamansal yumusatma + piksel dizisi
                for (int k = 0; k < GW * GH; k++)
                {
                    float a = wN[k], fw = wF[k] * (1 - a);
                    float tr = nr[k] * a * 0.96f + or[k] * fw;
                    float tg = ng[k] * a * 0.96f + og[k] * fw;
                    float tb = nb[k] * a * 0.96f + ob[k] * fw;
                    // kucuk degisimler yavas (titreme yok), sahne degisimi hizli
                    float diff = Math.Max(Math.Abs(tr - pr[k]), Math.Max(Math.Abs(tg - pg[k]), Math.Abs(tb - pb[k])));
                    float kk = Smooth + (1 - Smooth) * Math.Min(1f, diff / 110f) * 0.7f;
                    pr[k] += (tr - pr[k]) * kk;
                    pg[k] += (tg - pg[k]) * kk;
                    pb[k] += (tb - pb[k]) * kk;
                }
                // 2 kat yumusak buyutme (Chaikin): ekran kartinin dogrusal buyutmesindeki kirisiklari giderir
                byte[] outPx = new byte[GW * 2 * GH * 2 * 4];
                Up2(pb, GW, GH, outPx, 0); Up2(pg, GW, GH, outPx, 1); Up2(pr, GW, GH, outPx, 2);

                if (Interlocked.CompareExchange(ref pendingFrame, 1, 0) == 0)
                {
                    int w2 = GW * 2, h2 = GH * 2;
                    StripData[] sd = new StripData[4];
                    if (F > 0)
                    {
                        Grid gr = new Grid(pr, pg, pb, GW, GH);
                        int L = hole.Left - mon.Left, T = hole.Top - mon.Top, R = hole.Right - mon.Left, B = hole.Bottom - mon.Top;
                        if (topOn) sd[0] = HStrip(gr, L, T, R, B, F, true, leftOn, rightOn);
                        if (botOn) sd[1] = HStrip(gr, L, T, R, B, F, false, leftOn, rightOn);
                        int y0 = T + (topOn ? F : 0), y1 = B - (botOn ? F : 0);
                        if (leftOn && y1 > y0) sd[2] = VStrip(gr, L, R, y0, y1, F, true);
                        if (rightOn && y1 > y0) sd[3] = VStrip(gr, L, R, y0, y1, F, false);
                        for (int s = 0; s < 4; s++) if (sd[s] != null) { sd[s].X += mon.Left; sd[s].Y += mon.Top; }
                    }
                    win.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        try
                        {
                            if (!active) return;
                            if (bmp != null && bmp.PixelWidth == w2 && bmp.PixelHeight == h2)
                                bmp.WritePixels(new Int32Rect(0, 0, w2, h2), outPx, w2 * 4, 0);
                            for (int s = 0; s < 4; s++)
                            {
                                if (sd[s] != null) strips[s].Push(sd[s]);
                                else if (strips[s].Visible) strips[s].Hide();
                            }
                        }
                        finally { Interlocked.Exchange(ref pendingFrame, 0); }
                    }));
                }

                if (profile != null)
                {
                    long tEnd = sw.ElapsedTicks;
                    profCap += tCap - tStart; profProc += tEnd - tCap; profN++;
                    if (profN == 60)
                    {
                        double f = 1000.0 / Stopwatch.Frequency / profN;
                        try { System.IO.File.AppendAllText(profile, string.Format("{2} capture {0:F2} ms, process {1:F2} ms\r\n", profCap * f, profProc * f, gpu != null ? "GPU" : "GDI")); } catch { }
                        profCap = profProc = 0; profN = 0;
                    }
                }
                int wait = FrameMs - (int)(sw.ElapsedMilliseconds - t0);
                Thread.Sleep(wait > 1 ? wait : 1);
            }
            if (gpu != null) gpu.Dispose();
            N.DeleteObject(dib); N.DeleteDC(mdc); N.ReleaseDC(IntPtr.Zero, sdc);
        }

        static byte Clamp(float v) { return v <= 0 ? (byte)0 : (v >= 255 ? (byte)255 : (byte)v); }

        [ThreadStatic] static float[] upTmp;
        // w x h kanali 2w x 2h'ye Chaikin (0.75/0.25) ile buyutur, BGRA dizisinin 'ch' kanalina yazar (alfa 255)
        static void Up2(float[] a, int w, int h, byte[] dst, int ch)
        {
            int W2 = w * 2;
            if (upTmp == null || upTmp.Length < W2 * h) upTmp = new float[W2 * h];
            float[] t = upTmp;
            for (int y = 0; y < h; y++)
            {
                int row = y * w, orow = y * W2;
                for (int x = 0; x < w; x++)
                {
                    float c = a[row + x];
                    float l = a[row + (x > 0 ? x - 1 : 0)], r = a[row + (x < w - 1 ? x + 1 : w - 1)];
                    t[orow + 2 * x] = 0.75f * c + 0.25f * l;
                    t[orow + 2 * x + 1] = 0.75f * c + 0.25f * r;
                }
            }
            for (int y = 0; y < h; y++)
            {
                int up = (y > 0 ? y - 1 : 0) * W2, cur = y * W2, dn = (y < h - 1 ? y + 1 : h - 1) * W2;
                int o0 = (2 * y) * W2 * 4, o1 = (2 * y + 1) * W2 * 4;
                for (int x = 0; x < W2; x++)
                {
                    float c = t[cur + x];
                    dst[o0 + x * 4 + ch] = Clamp(0.75f * c + 0.25f * t[up + x]);
                    dst[o1 + x * 4 + ch] = Clamp(0.75f * c + 0.25f * t[dn + x]);
                    if (ch == 0) { dst[o0 + x * 4 + 3] = 255; dst[o1 + x * 4 + 3] = 255; }
                }
            }
        }

        // kucuk BGRA goruntuyu CapW x CapH boyutuna alan ortalamasi ile indirir
        static void ResampleBGRA(byte[] src, int sw, int sh, float[] r, float[] g, float[] b)
        {
            float fx = sw / (float)CapW, fy = sh / (float)CapH;
            for (int y = 0; y < CapH; y++)
            {
                int y0 = (int)(y * fy), y1 = Math.Max(y0 + 1, Math.Min(sh, (int)((y + 1) * fy)));
                for (int x = 0; x < CapW; x++)
                {
                    int x0 = (int)(x * fx), x1 = Math.Max(x0 + 1, Math.Min(sw, (int)((x + 1) * fx)));
                    int sR = 0, sG = 0, sB = 0, n = 0;
                    for (int yy = y0; yy < y1; yy++)
                    {
                        int j = (yy * sw + x0) * 4;
                        for (int xx = x0; xx < x1; xx++, j += 4) { sB += src[j]; sG += src[j + 1]; sR += src[j + 2]; n++; }
                    }
                    int i = y * CapW + x;
                    float inv = 1f / n;
                    r[i] = sR * inv; g[i] = sG * inv; b[i] = sB * inv;
                }
            }
        }

        // ---------------- gecis seritleri ----------------
        class Grid
        {
            float[] r, g, b; int w, h;
            public Grid(float[] r, float[] g, float[] b, int w, int h) { this.r = r; this.g = g; this.b = b; this.w = w; this.h = h; }
            // monitor-goreli ekran pikselinde ambiyans rengi
            public void At(float sx, float sy, out float R, out float G, out float B)
            {
                float u = sx / Cell - 0.5f, v = sy / Cell - 0.5f;
                if (u < 0) u = 0; if (v < 0) v = 0;
                if (u > w - 1) u = w - 1; if (v > h - 1) v = h - 1;
                int x0 = (int)u, y0 = (int)v, x1 = Math.Min(x0 + 1, w - 1), y1 = Math.Min(y0 + 1, h - 1);
                float fx = u - x0, fy = v - y0;
                int a = y0 * w + x0, bb = y0 * w + x1, c = y1 * w + x0, d = y1 * w + x1;
                float w00 = (1 - fx) * (1 - fy), w10 = fx * (1 - fy), w01 = (1 - fx) * fy, w11 = fx * fy;
                R = r[a] * w00 + r[bb] * w10 + r[c] * w01 + r[d] * w11;
                G = g[a] * w00 + g[bb] * w10 + g[c] * w01 + g[d] * w11;
                B = b[a] * w00 + b[bb] * w10 + b[c] * w01 + b[d] * w11;
            }
        }

        // kenarda 1 (tam ambiyans rengi), ice dogru yumusakca 0
        static float Ramp(int d, int F)
        {
            float s = 1f - (d + 0.5f) / F;
            if (s <= 0) return 0;
            return s * s * (3 - 2 * s);
        }

        static void Put(byte[] px, int j, float r, float g, float b, float a)
        {
            px[j] = Clamp(b * a); px[j + 1] = Clamp(g * a); px[j + 2] = Clamp(r * a); px[j + 3] = Clamp(a * 255f);
        }

        // ust (top=true) veya alt serit; koselerde yan seritle harmanlanir
        static StripData HStrip(Grid gr, int L, int T, int R, int B, int F, bool top, bool leftOn, bool rightOn)
        {
            int W = R - L;
            var s = new StripData(); s.X = L; s.Y = top ? T : B - F; s.W = W; s.H = F; s.Px = new byte[W * F * 4];
            float o = Cell * 0.75f;
            float edgeY = top ? T - o : B + o;
            float[] er = new float[W], eg = new float[W], eb = new float[W];
            for (int x = 0; x < W; x++) gr.At(L + x + 0.5f, edgeY, out er[x], out eg[x], out eb[x]);
            for (int i = 0; i < F; i++)
            {
                int dv = top ? i : F - 1 - i;
                float av = Ramp(dv, F);
                float yy = s.Y + i + 0.5f;
                int row = i * W * 4;
                for (int x = 0; x < W; x++)
                {
                    float ah = 0; bool left = false;
                    if (leftOn && x < F) { ah = Ramp(x, F); left = true; }
                    else if (rightOn && x >= W - F) ah = Ramp(W - 1 - x, F);
                    if (ah <= 0.001f) { Put(s.Px, row + x * 4, er[x], eg[x], eb[x], av); continue; }
                    float sr, sg, sb;
                    gr.At(left ? L - o : R + o, yy, out sr, out sg, out sb);
                    float tw = av + ah;
                    if (tw < 0.0001f) { Put(s.Px, row + x * 4, 0, 0, 0, 0); continue; }
                    float rr = (er[x] * av + sr * ah) / tw, gg = (eg[x] * av + sg * ah) / tw, bb2 = (eb[x] * av + sb * ah) / tw;
                    Put(s.Px, row + x * 4, rr, gg, bb2, Math.Max(av, ah));
                }
            }
            return s;
        }

        static StripData VStrip(Grid gr, int L, int R, int y0, int y1, int F, bool left)
        {
            int H = y1 - y0;
            var s = new StripData(); s.X = left ? L : R - F; s.Y = y0; s.W = F; s.H = H; s.Px = new byte[F * H * 4];
            float o = Cell * 0.75f;
            float edgeX = left ? L - o : R + o;
            float[] al = new float[F];
            for (int x = 0; x < F; x++) al[x] = Ramp(left ? x : F - 1 - x, F);
            for (int y = 0; y < H; y++)
            {
                float cr, cg, cb;
                gr.At(edgeX, y0 + y + 0.5f, out cr, out cg, out cb);
                int row = y * F * 4;
                for (int x = 0; x < F; x++) Put(s.Px, row + x * 4, cr, cg, cb, al[x]);
            }
            return s;
        }

        static float Sample(float[] a, float u, float v)
        {
            if (u < 0) u = 0; if (v < 0) v = 0;
            if (u > CapW - 1) u = CapW - 1; if (v > CapH - 1) v = CapH - 1;
            int x0 = (int)u, y0 = (int)v;
            int x1 = Math.Min(x0 + 1, CapW - 1), y1 = Math.Min(y0 + 1, CapH - 1);
            float fx = u - x0, fy = v - y0;
            float a0 = a[y0 * CapW + x0] + (a[y0 * CapW + x1] - a[y0 * CapW + x0]) * fx;
            float a1 = a[y1 * CapW + x0] + (a[y1 * CapW + x1] - a[y1 * CapW + x0]) * fx;
            return a0 + (a1 - a0) * fy;
        }

        // ayrilabilir kutu bulanikligi (kenarlar sabitlenir)
        static void Blur(float[] a, int w, int h, int r, float[] tmp)
        {
            // kayan toplam: yaricaptan bagimsiz O(n)
            float inv = 1f / (2 * r + 1);
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++) tmp[x] = a[row + x];
                float s = 0;
                for (int i = -r; i <= r; i++) s += tmp[i < 0 ? 0 : (i >= w ? w - 1 : i)];
                for (int x = 0; x < w; x++)
                {
                    a[row + x] = s * inv;
                    int add = x + r + 1, rem = x - r;
                    s += tmp[add >= w ? w - 1 : add] - tmp[rem < 0 ? 0 : rem];
                }
            }
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++) tmp[y] = a[y * w + x];
                float s = 0;
                for (int i = -r; i <= r; i++) s += tmp[i < 0 ? 0 : (i >= h ? h - 1 : i)];
                for (int y = 0; y < h; y++)
                {
                    a[y * w + x] = s * inv;
                    int add = y + r + 1, rem = y - r;
                    s += tmp[add >= h ? h - 1 : add] - tmp[rem < 0 ? 0 : rem];
                }
            }
        }
    }
}
