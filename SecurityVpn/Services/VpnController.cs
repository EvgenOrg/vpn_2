using VPN.Components.Models;

namespace VPN.Services
{
    public enum VpnState
    {
        Disconnected,
        Connecting,
        Connected
    }

    /// <summary>
    /// Единое место, где живёт состояние VPN для всего интерфейса:
    /// подключение / отключение, время сессии, скорость и объём трафика.
    /// Страницы только подписываются на OnChange и перерисовываются.
    /// Для демо-серверов трафик имитируется, для своих — реально запускается Xray через IVpnService.
    /// </summary>
    public class VpnController : IDisposable
    {
        private readonly IVpnService _vpn;
        private readonly VpnSettingsService _settings;
        private readonly Random _random = new();
        private readonly Timer _timer;

        private int _generation;
        private DateTime _connectedAt;
        private double _demoDown, _demoUp;

        public VpnState State { get; private set; } = VpnState.Disconnected;

        public Node? Active { get; private set; }

        /// <summary>Текст последней ошибки подключения (null — ошибки нет).</summary>
        public string? Error { get; private set; }

        public TimeSpan Uptime { get; private set; }

        public double DownBps { get; private set; }

        public double UpBps { get; private set; }

        public long TotalDown { get; private set; }

        public long TotalUp { get; private set; }

        /// <summary>Автоподключение при запуске срабатывает один раз за запуск приложения.</summary>
        public bool AutoConnectTried { get; set; }

        public event Action? OnChange;

        public VpnController(IVpnService vpn, VpnSettingsService settings)
        {
            _vpn = vpn;
            _settings = settings;
            _timer = new Timer(_ => _ = TickAsync(), null, 1000, 1000);
        }

        public async Task ConnectAsync(Node node)
        {
            if (State != VpnState.Disconnected) return;

            var gen = ++_generation;
            State = VpnState.Connecting;
            Active = node;
            Error = null;
            ResetStats();
            Notify();

            try
            {
                if (node.IsDemo)
                {
                    await Task.Delay(1800);
                }
                else
                {
                    var json = XrayConfigBuilder.Build(node, _settings, desktop: !OperatingSystem.IsAndroid());
                    await _vpn.StartAsync(json);

                    // На Android состояние приходит из сервиса асинхронно (после диалога разрешения VPN),
                    // поэтому ждём, пока IsRunning станет true.
                    var deadline = DateTime.UtcNow.AddSeconds(25);
                    while (!_vpn.IsRunning && DateTime.UtcNow < deadline && gen == _generation)
                        await Task.Delay(200);

                    if (gen != _generation) return;
                    if (!_vpn.IsRunning) throw new InvalidOperationException("Не удалось установить соединение с сервером.");
                }

                if (gen != _generation) return;

                _connectedAt = DateTime.UtcNow;
                State = VpnState.Connected;
            }
            catch (Exception ex)
            {
                try { await _vpn.StopAsync(); } catch { /* уже остановлен */ }
                if (gen == _generation)
                {
                    State = VpnState.Disconnected;
                    Active = null;
                    Error = ex.Message;
                }
            }

            Notify();
        }

        public async Task DisconnectAsync()
        {
            _generation++;
            var wasDemo = Active?.IsDemo == true;

            State = VpnState.Disconnected;
            Active = null;
            ResetStats(keepTotals: true);
            Notify();

            if (!wasDemo)
            {
                try { await _vpn.StopAsync(); } catch { /* уже остановлен */ }
            }
        }

        /// <summary>Переключение на другой сервер без ручного «выключить — включить».</summary>
        public async Task SwitchAsync(Node node)
        {
            await DisconnectAsync();
            await ConnectAsync(node);
        }

        /// <summary>Синхронная остановка при закрытии окна: возвращаем системный прокси и гасим ядро.</summary>
        public void Shutdown()
        {
            _generation++;
            try { _vpn.StopAsync().GetAwaiter().GetResult(); } catch { /* закрываемся */ }
        }

        private async Task TickAsync()
        {
            if (State != VpnState.Connected || Active is null) return;

            Uptime = DateTime.UtcNow - _connectedAt;

            if (Active.IsDemo)
            {
                // Плавно «дышащая» скорость: небольшие случайные колебания вокруг среднего значения
                _demoDown = Math.Clamp(_demoDown * 0.7 + (6_000_000 + _random.NextDouble() * 22_000_000) * 0.3, 0, 60_000_000);
                _demoUp = Math.Clamp(_demoUp * 0.7 + (700_000 + _random.NextDouble() * 4_500_000) * 0.3, 0, 12_000_000);
                DownBps = _demoDown;
                UpBps = _demoUp;
            }
            else
            {
                if (!_vpn.IsRunning)
                {
                    // VPN выключили не из приложения (системный тумблер, ядро упало)
                    State = VpnState.Disconnected;
                    Active = null;
                    Error = "Соединение прервано.";
                    ResetStats(keepTotals: true);
                    Notify();
                    return;
                }

                var sample = await _vpn.SampleSpeedAsync();
                if (sample is { } s)
                {
                    DownBps = s.Down;
                    UpBps = s.Up;
                }
            }

            TotalDown += (long)DownBps;
            TotalUp += (long)UpBps;
            Notify();
        }

        private void ResetStats(bool keepTotals = false)
        {
            Uptime = TimeSpan.Zero;
            DownBps = UpBps = 0;
            _demoDown = _demoUp = 0;
            if (!keepTotals) TotalDown = TotalUp = 0;
        }

        private void Notify() => OnChange?.Invoke();

        public void Dispose() => _timer.Dispose();
    }
}
