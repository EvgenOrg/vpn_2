using Microsoft.Extensions.Logging;
using VPN.Services;

namespace VPN
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
#if WINDOWS
            // Если в прошлый раз приложение закрылось аварийно, системный прокси мог остаться включённым
            VPN.WinUI.SystemProxy.RestoreIfStale();
#endif

            var builder = MauiApp.CreateBuilder();
            builder.UseMauiApp<App>();

            #region Выбор ОС
#if WINDOWS
            builder.Services.AddSingleton<IVpnService, VPN.WinUI.WindowsVpnService>();
#endif

#if ANDROID
            builder.Services.AddSingleton<IVpnService, VPN.Platforms.Android.VpnServiceImplementation>();
#endif
            #endregion

            builder.Services.AddSingleton<NodeStorage>();
            builder.Services.AddSingleton<AuthService>();
            builder.Services.AddSingleton<ThemeService>();
            builder.Services.AddSingleton<LocalizationService>();
            builder.Services.AddSingleton<VpnSettingsService>();
            builder.Services.AddSingleton<VpnController>();
            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
