using System.Text.Json.Serialization;

namespace VPN.Components.Models
{
    /// <summary>
    /// Один VPN-сервер: VLESS + Reality (тот самый, что поднимает server/install.sh).
    /// Сохраняется в Preferences как JSON, поэтому поля подключения — обычные свойства,
    /// а состояние проверки (онлайн / пинг) помечено [JsonIgnore] и живёт только в памяти.
    /// </summary>
    public class Node
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = "";

        /// <summary>ISO-код страны из двух букв (DE, NL, ...) — для флага и положения на планете.</summary>
        public string Country { get; set; } = "DE";

        public string Host { get; set; } = "";

        public int Port { get; set; } = 443;

        public string Uuid { get; set; } = "";

        /// <summary>serverName (SNI) для Reality.</summary>
        public string Sni { get; set; } = "";

        public string PublicKey { get; set; } = "";

        public string ShortId { get; set; } = "";

        public string SpiderX { get; set; } = "";

        public string Fingerprint { get; set; } = "chrome";

        public string Flow { get; set; } = "xtls-rprx-vision";

        /// <summary>Встроенный демо-сервер: подключение имитируется, реальный трафик не идёт.</summary>
        public bool IsDemo { get; set; }

        [JsonIgnore] public bool IsOnline { get; set; }

        [JsonIgnore] public bool IsChecking { get; set; }

        [JsonIgnore] public int PingMs { get; set; } = -1;

        [JsonIgnore] public string Protocol => "VLESS · Reality";

        public static Node GetNullNode() => new()
        {
            Id = Guid.Empty,
            Name = "—",
            Country = "",
            Host = "0.0.0.0",
            Port = 0,
        };
    }
}
