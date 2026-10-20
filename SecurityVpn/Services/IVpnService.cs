namespace VPN.Services
{
    public interface IVpnService
    {
        bool IsRunning { get; }

        Task StartAsync(string configJson);

        Task StopAsync();

        Task<bool> GetStatusAsync();

        /// <summary>Текущая скорость в байтах/с (скачивание, отдача). null — данных нет или платформа не умеет.</summary>
        Task<(double Down, double Up)?> SampleSpeedAsync() => Task.FromResult<(double Down, double Up)?>(null);
    }
}
