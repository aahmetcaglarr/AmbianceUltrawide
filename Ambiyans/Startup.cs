// Windows ile baslat: normal surumde kayit defteri (Run), Store (MSIX) surumunde StartupTask.

using System;
using System.Runtime.InteropServices;
using System.Text;
using Windows.ApplicationModel;
using Windows.Foundation;

namespace Ambiyans
{
    partial class App
    {
        const string StartupTaskId = "AmbiyansStartup";

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern int GetCurrentPackageFullName(ref int length, StringBuilder name);

        static bool? packaged;
        static bool IsPackaged()
        {
            if (packaged == null)
            {
                try { int len = 0; packaged = GetCurrentPackageFullName(ref len, null) != 15700; /*APPMODEL_ERROR_NO_PACKAGE*/ }
                catch { packaged = false; }
            }
            return packaged.Value;
        }

        // Store surumu Windows acilisinda mi baslatildi? (o zaman pencere gostermeden tepside basla)
        static bool LaunchedByStartupTask()
        {
            if (!IsPackaged()) return false;
            return CheckStartupActivation();
        }
        static bool CheckStartupActivation()
        {
            try
            {
                var a = AppInstance.GetActivatedEventArgs();
                return a != null && a.Kind == Windows.ApplicationModel.Activation.ActivationKind.StartupTask;
            }
            catch { return false; }
        }

        static bool IsOn(StartupTaskState s) { return s == StartupTaskState.Enabled || s == StartupTaskState.EnabledByPolicy; }

        // WinRT islemini bitince arayuz is parcaciginda calistir
        static void OnUi(Action a) { if (win != null) win.Dispatcher.BeginInvoke(a); }

        static void WithStartupTask(Action<StartupTask> then)
        {
            try
            {
                var op = StartupTask.GetAsync(StartupTaskId);
                op.Completed = delegate (IAsyncOperation<StartupTask> info, AsyncStatus st)
                {
                    if (st != AsyncStatus.Completed) { OnUi(delegate { UiMessage("Başlangıç ayarı değiştirilemedi."); }); return; }
                    var t = info.GetResults();
                    OnUi(delegate { then(t); });
                };
            }
            catch { UiMessage("Başlangıç ayarı değiştirilemedi."); }
        }

        static void StoreStartupSet(bool on)
        {
            WithStartupTask(delegate (StartupTask t)
            {
                if (!on) { t.Disable(); return; }
                var req = t.RequestEnableAsync();
                req.Completed = delegate (IAsyncOperation<StartupTaskState> info, AsyncStatus st)
                {
                    bool ok = st == AsyncStatus.Completed && IsOn(info.GetResults());
                    if (!ok) OnUi(delegate
                    {
                        UiMessage("Windows ile başlatma bu bilgisayarda kapatılmış. Görev Yöneticisi > Başlangıç uygulamaları'ndan açabilirsin.");
                        StoreStartupRefreshToggle();
                    });
                };
            });
        }

        // arayuzdeki anahtari Store durumuna gore guncelle
        static void StoreStartupRefreshToggle()
        {
            WithStartupTask(delegate (StartupTask t)
            {
                if (ui == null) return;
                uiSync = true;
                try { Check("AutostartToggle", IsOn(t.State)); } finally { uiSync = false; }
            });
        }
    }
}
