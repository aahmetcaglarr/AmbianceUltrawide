// Ekran kartinda yakalama: DXGI Desktop Duplication + GPU'da mipmap ile kucultme.
// Islemciye sadece kucuk (ornegin 240x135) bir goruntu gelir.

using System;
using System.Runtime.InteropServices;

namespace Ambiyans
{
    class GpuCapture : IDisposable
    {
        // ---- vtable cagrilari ----
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int EnumFn(IntPtr self, uint idx, out IntPtr obj);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetDescFn(IntPtr self, IntPtr desc);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int DuplicateOutputFn(IntPtr self, IntPtr device, out IntPtr dup);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int AcquireFn(IntPtr self, uint timeout, IntPtr info, out IntPtr res);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int ReleaseFrameFn(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateTex2DFn(IntPtr self, ref TEX2D desc, IntPtr init, out IntPtr tex);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateSrvFn(IntPtr self, IntPtr res, IntPtr desc, out IntPtr srv);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void CopySubFn(IntPtr self, IntPtr dst, uint dstSub, uint x, uint y, uint z, IntPtr src, uint srcSub, ref BOX box);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void GenMipsFn(IntPtr self, IntPtr srv);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int MapFn(IntPtr self, IntPtr res, uint sub, int type, uint flags, out MAPPED m);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void UnmapFn(IntPtr self, IntPtr res, uint sub);

        [StructLayout(LayoutKind.Sequential)]
        struct TEX2D
        {
            public uint Width, Height, MipLevels, ArraySize, Format, SampleCount, SampleQuality, Usage, BindFlags, CPUAccessFlags, MiscFlags;
        }
        [StructLayout(LayoutKind.Sequential)] struct BOX { public uint left, top, front, right, bottom, back; }
        [StructLayout(LayoutKind.Sequential)] struct MAPPED { public IntPtr pData; public uint RowPitch, DepthPitch; }

        [DllImport("dxgi.dll")] static extern int CreateDXGIFactory1(ref Guid riid, out IntPtr factory);
        [DllImport("d3d11.dll")]
        static extern int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr software, uint flags, IntPtr levels, uint numLevels,
            uint sdk, out IntPtr device, out int level, out IntPtr context);

        static T Fn<T>(IntPtr obj, int idx) where T : class
        {
            IntPtr vt = Marshal.ReadIntPtr(obj);
            IntPtr f = Marshal.ReadIntPtr(vt, idx * IntPtr.Size);
            return Marshal.GetDelegateForFunctionPointer(f, typeof(T)) as T;
        }

        static readonly Guid IID_Factory1 = new Guid("770aae78-f26f-4dba-a829-253c83d1b387");
        static readonly Guid IID_Tex2D = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

        IntPtr device, ctx, dup;
        IntPtr mipTex, mipSrv, staging;
        int mipW, mipH, level, outW, outH;
        public int OutLeft, OutTop;          // monitorun masaustu koordinati
        IntPtr frameInfo = Marshal.AllocHGlobal(64);

        AcquireFn acquire; ReleaseFrameFn releaseFrame; CopySubFn copySub; GenMipsFn genMips; MapFn map; UnmapFn unmap;
        CreateTex2DFn createTex; CreateSrvFn createSrv;

        public byte[] Last;                  // en son kucuk goruntu (BGRA)
        public int LastW, LastH;

        // Verilen monitore (masaustu koordinatlari) bagli ciktiyi bulur ve kopyalamayi baslatir.
        public static GpuCapture Create(int monLeft, int monTop)
        {
            var g = new GpuCapture();
            try { if (g.Init(monLeft, monTop)) return g; } catch { }
            g.Dispose();
            return null;
        }

        bool Init(int monLeft, int monTop)
        {
            Guid iid = IID_Factory1;
            IntPtr factory;
            if (CreateDXGIFactory1(ref iid, out factory) != 0) return false;
            IntPtr foundAdapter = IntPtr.Zero, foundOutput = IntPtr.Zero;
            IntPtr desc = Marshal.AllocHGlobal(256);
            try
            {
                var enumAdapters = Fn<EnumFn>(factory, 7);
                for (uint a = 0; foundOutput == IntPtr.Zero; a++)
                {
                    IntPtr adapter;
                    if (enumAdapters(factory, a, out adapter) != 0) break;
                    var enumOutputs = Fn<EnumFn>(adapter, 7);
                    for (uint o = 0; ; o++)
                    {
                        IntPtr output;
                        if (enumOutputs(adapter, o, out output) != 0) break;
                        Fn<GetDescFn>(output, 7)(output, desc);
                        int l = Marshal.ReadInt32(desc, 64), t = Marshal.ReadInt32(desc, 68);
                        int r = Marshal.ReadInt32(desc, 72), b = Marshal.ReadInt32(desc, 76);
                        if (l == monLeft && t == monTop)
                        {
                            foundAdapter = adapter; foundOutput = output;
                            OutLeft = l; OutTop = t; outW = r - l; outH = b - t;
                            break;
                        }
                        Marshal.Release(output);
                    }
                    if (foundOutput == IntPtr.Zero) Marshal.Release(adapter);
                }
            }
            finally { Marshal.FreeHGlobal(desc); Marshal.Release(factory); }
            if (foundOutput == IntPtr.Zero) return false;

            int lvl;
            int hr = D3D11CreateDevice(foundAdapter, 0 /*UNKNOWN*/, IntPtr.Zero, 0x20 /*BGRA*/, IntPtr.Zero, 0, 7, out device, out lvl, out ctx);
            Marshal.Release(foundAdapter);
            if (hr != 0) { Marshal.Release(foundOutput); return false; }

            // IDXGIOutput1::DuplicateOutput (vtable 22)
            hr = Fn<DuplicateOutputFn>(foundOutput, 22)(foundOutput, device, out dup);
            Marshal.Release(foundOutput);
            if (hr != 0) return false;

            acquire = Fn<AcquireFn>(dup, 8);
            releaseFrame = Fn<ReleaseFrameFn>(dup, 14);
            createTex = Fn<CreateTex2DFn>(device, 5);
            createSrv = Fn<CreateSrvFn>(device, 7);
            copySub = Fn<CopySubFn>(ctx, 46);
            genMips = Fn<GenMipsFn>(ctx, 54);
            map = Fn<MapFn>(ctx, 14);
            unmap = Fn<UnmapFn>(ctx, 15);
            return true;
        }

        void FreeTextures()
        {
            if (mipSrv != IntPtr.Zero) { Marshal.Release(mipSrv); mipSrv = IntPtr.Zero; }
            if (mipTex != IntPtr.Zero) { Marshal.Release(mipTex); mipTex = IntPtr.Zero; }
            if (staging != IntPtr.Zero) { Marshal.Release(staging); staging = IntPtr.Zero; }
            mipW = mipH = 0;
        }

        bool EnsureTextures(int w, int h, int minW, int minH)
        {
            if (w == mipW && h == mipH && mipTex != IntPtr.Zero) return true;
            FreeTextures();
            var d = new TEX2D();
            d.Width = (uint)w; d.Height = (uint)h; d.MipLevels = 0; d.ArraySize = 1; d.Format = 87; /*B8G8R8A8_UNORM*/
            d.SampleCount = 1; d.Usage = 0; d.BindFlags = 0x8 | 0x20; /*SRV|RTV*/ d.MiscFlags = 0x1; /*GENERATE_MIPS*/
            if (createTex(device, ref d, IntPtr.Zero, out mipTex) != 0) { mipTex = IntPtr.Zero; return false; }
            if (createSrv(device, mipTex, IntPtr.Zero, out mipSrv) != 0) { FreeTextures(); return false; }
            level = 0;
            while ((w >> (level + 1)) >= minW && (h >> (level + 1)) >= minH) level++;
            LastW = Math.Max(1, w >> level); LastH = Math.Max(1, h >> level);
            var s = new TEX2D();
            s.Width = (uint)LastW; s.Height = (uint)LastH; s.MipLevels = 1; s.ArraySize = 1; s.Format = 87;
            s.SampleCount = 1; s.Usage = 3; /*STAGING*/ s.CPUAccessFlags = 0x20000; /*READ*/
            if (createTex(device, ref s, IntPtr.Zero, out staging) != 0) { FreeTextures(); return false; }
            mipW = w; mipH = h;
            Last = new byte[LastW * LastH * 4];
            return true;
        }

        // Ekrandaki (masaustu koordinatli) dikdortgeni yakalar; en az minW x minH boyutunda kucultulmus goruntu uretir.
        // true: yeni kare alindi; false: degisiklik yok (Last eski kareyi tutar). Hata olursa exception.
        public bool Grab(int left, int top, int right, int bottom, int minW, int minH, uint timeoutMs)
        {
            int l = Math.Max(0, left - OutLeft), t = Math.Max(0, top - OutTop);
            int r = Math.Min(outW, right - OutLeft), b = Math.Min(outH, bottom - OutTop);
            if (r - l < minW || b - t < minH) return false;
            if (!EnsureTextures(r - l, b - t, minW, minH)) throw new Exception("texture");

            IntPtr res;
            int hr = acquire(dup, timeoutMs, frameInfo, out res);
            if (hr == unchecked((int)0x887A0027)) return false;   // WAIT_TIMEOUT: ekran degismedi
            if (hr != 0) throw new Exception("acquire " + hr.ToString("X"));
            try
            {
                Guid iid = IID_Tex2D;
                IntPtr tex;
                if (Marshal.QueryInterface(res, ref iid, out tex) != 0) return false;
                try
                {
                    var box = new BOX(); box.left = (uint)l; box.top = (uint)t; box.right = (uint)r; box.bottom = (uint)b; box.back = 1;
                    copySub(ctx, mipTex, 0, 0, 0, 0, tex, 0, ref box);
                }
                finally { Marshal.Release(tex); }
            }
            finally { Marshal.Release(res); releaseFrame(dup); }

            genMips(ctx, mipSrv);
            var sb = new BOX(); sb.right = (uint)LastW; sb.bottom = (uint)LastH; sb.back = 1;
            copySub(ctx, staging, 0, 0, 0, 0, mipTex, (uint)level, ref sb);
            MAPPED m;
            if (map(ctx, staging, 0, 1 /*READ*/, 0, out m) != 0) throw new Exception("map");
            try
            {
                for (int y = 0; y < LastH; y++)
                    Marshal.Copy(new IntPtr(m.pData.ToInt64() + (long)y * m.RowPitch), Last, y * LastW * 4, LastW * 4);
            }
            finally { unmap(ctx, staging, 0); }
            return true;
        }

        public void Dispose()
        {
            FreeTextures();
            if (dup != IntPtr.Zero) { Marshal.Release(dup); dup = IntPtr.Zero; }
            if (ctx != IntPtr.Zero) { Marshal.Release(ctx); ctx = IntPtr.Zero; }
            if (device != IntPtr.Zero) { Marshal.Release(device); device = IntPtr.Zero; }
            if (frameInfo != IntPtr.Zero) { Marshal.FreeHGlobal(frameInfo); frameInfo = IntPtr.Zero; }
        }
    }
}
