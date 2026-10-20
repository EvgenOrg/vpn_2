using VPN.Services;

namespace VPN
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new MainPage()) { Title = "Security VPN" };

#if WINDOWS
            window.Width = 1040;
            window.Height = 800;
            window.MinimumWidth = 420;
            window.MinimumHeight = 640;
#endif

            // Закрыли окно — гасим VPN и возвращаем системный прокси
            window.Destroying += (_, _) =>
            {
                var vpn = IPlatformApplication.Current?.Services.GetService<VpnController>();
                vpn?.Shutdown();
            };

            return window;
        }
    }
}
