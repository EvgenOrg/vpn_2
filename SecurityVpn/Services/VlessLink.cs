using VPN.Components.Models;

namespace VPN.Services
{
    /// <summary>Ошибка разбора ссылки. Key — ключ перевода для LocalizationService.</summary>
    public sealed class VlessParseException : Exception
    {
        public string Key { get; }

        public VlessParseException(string key) : base(key) => Key = key;
    }

    /// <summary>
    /// Разбирает ссылку вида
    /// vless://UUID@host:443?security=reality&amp;sni=...&amp;pbk=...&amp;sid=...&amp;fp=chrome&amp;flow=xtls-rprx-vision#Название
    /// Поддерживается только то, с чем умеет работать наш сервер: VLESS + Reality по TCP.
    /// </summary>
    public static class VlessLink
    {
        public static Node Parse(string? link)
        {
            link = (link ?? "").Trim();
            if (link.Length == 0) throw new VlessParseException("servers.add.err.empty");
            if (!link.StartsWith("vless://", StringComparison.OrdinalIgnoreCase))
                throw new VlessParseException("servers.add.err.scheme");
            if (!Uri.TryCreate(link, UriKind.Absolute, out var uri))
                throw new VlessParseException("servers.add.err.format");

            var uuid = Uri.UnescapeDataString(uri.UserInfo);
            if (!Guid.TryParse(uuid, out _)) throw new VlessParseException("servers.add.err.uuid");

            var host = uri.DnsSafeHost;
            if (string.IsNullOrWhiteSpace(host)) throw new VlessParseException("servers.add.err.host");

            var query = ParseQuery(uri.Query);

            if (!query.TryGetValue("security", out var security) ||
                !security.Equals("reality", StringComparison.OrdinalIgnoreCase))
                throw new VlessParseException("servers.add.err.security");

            if (query.TryGetValue("type", out var type) &&
                !type.Equals("tcp", StringComparison.OrdinalIgnoreCase))
                throw new VlessParseException("servers.add.err.network");

            if (!query.TryGetValue("pbk", out var pbk) || string.IsNullOrWhiteSpace(pbk))
                throw new VlessParseException("servers.add.err.pbk");

            if (!query.TryGetValue("sni", out var sni) || string.IsNullOrWhiteSpace(sni))
                throw new VlessParseException("servers.add.err.sni");

            var name = Uri.UnescapeDataString(uri.Fragment.TrimStart('#')).Trim();

            return new Node
            {
                Name = name.Length > 0 ? name : host,
                Host = host,
                Port = uri.Port > 0 ? uri.Port : 443,
                Uuid = uuid,
                Sni = sni.Trim(),
                PublicKey = pbk.Trim(),
                ShortId = query.GetValueOrDefault("sid", "").Trim(),
                SpiderX = query.GetValueOrDefault("spx", ""),
                Fingerprint = query.GetValueOrDefault("fp", "chrome") is { Length: > 0 } fp ? fp : "chrome",
                Flow = query.GetValueOrDefault("flow", ""),
            };
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var i = part.IndexOf('=');
                var key = i < 0 ? part : part[..i];
                var value = i < 0 ? "" : part[(i + 1)..];
                result[Uri.UnescapeDataString(key)] = Uri.UnescapeDataString(value);
            }
            return result;
        }
    }
}
