using System.Net;
using System.Text.Json;
using VPN.Components.Models;

namespace VPN.Services
{
    /// <summary>
    /// Собирает клиентский конфиг Xray для выбранного сервера.
    ///  • Windows / desktop: локальные socks (10808) и http (10809) входы + api со статистикой (10085);
    ///    системный прокси Windows направляется на http-вход.
    ///  • Android: входов нет — tun-вход добавляет XrayVpnService (InjectTunInbound).
    /// </summary>
    public static class XrayConfigBuilder
    {
        public const int SocksPort = 10808;
        public const int HttpPort = 10809;
        public const int ApiPort = 10085;

        public static string Build(Node node, VpnSettingsService settings, bool desktop)
        {
            var inbounds = new List<object>();
            if (desktop)
            {
                inbounds.Add(new
                {
                    tag = "socks", listen = "127.0.0.1", port = SocksPort, protocol = "socks",
                    settings = new { udp = true, auth = "noauth" },
                    sniffing = new { enabled = true, destOverride = new[] { "http", "tls", "quic" } },
                });
                inbounds.Add(new
                {
                    tag = "http", listen = "127.0.0.1", port = HttpPort, protocol = "http",
                    settings = new { },
                });
                inbounds.Add(new
                {
                    tag = "api", listen = "127.0.0.1", port = ApiPort, protocol = "dokodemo-door",
                    settings = new { address = "127.0.0.1" },
                });
            }

            var rules = new List<object>();
            if (desktop)
                rules.Add(new { type = "field", inboundTag = new[] { "api" }, outboundTag = "api" });

            var config = new Dictionary<string, object>
            {
                ["log"] = new { loglevel = "warning" },
                ["inbounds"] = inbounds,
                ["outbounds"] = new object[]
                {
                    new
                    {
                        tag = "proxy", protocol = "vless",
                        settings = new
                        {
                            vnext = new[]
                            {
                                new
                                {
                                    address = node.Host, port = node.Port,
                                    users = new[] { new { id = node.Uuid, encryption = "none", flow = node.Flow } },
                                },
                            },
                        },
                        streamSettings = new
                        {
                            network = "tcp",
                            security = "reality",
                            realitySettings = new
                            {
                                serverName = node.Sni,
                                fingerprint = node.Fingerprint,
                                publicKey = node.PublicKey,
                                shortId = node.ShortId,
                                spiderX = node.SpiderX,
                            },
                        },
                    },
                    new { tag = "direct", protocol = "freedom", settings = new { } },
                },
                ["routing"] = new { domainStrategy = "AsIs", rules },
            };

            if (desktop)
            {
                config["stats"] = new { };
                config["api"] = new { tag = "api", services = new[] { "StatsService" } };
                config["policy"] = new { system = new { statsOutboundUplink = true, statsOutboundDownlink = true } };
            }

            if (IPAddress.TryParse(settings.DnsServer?.Trim(), out _))
                config["dns"] = new { servers = new[] { settings.DnsServer!.Trim() } };

            return JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
