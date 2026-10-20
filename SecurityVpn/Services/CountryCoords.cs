namespace VPN.Services
{
    /// <summary>Примерные координаты страны (широта, долгота) — чтобы показать сервер на планете.</summary>
    public static class CountryCoords
    {
        private static readonly Dictionary<string, (double Lat, double Lon)> Coords = new(StringComparer.OrdinalIgnoreCase)
        {
            ["US"] = (38, -97), ["GB"] = (54, -2), ["DE"] = (51, 10), ["FR"] = (46, 2), ["NL"] = (52, 5),
            ["RU"] = (60, 90), ["JP"] = (36, 138), ["SG"] = (1.3, 103.8), ["CA"] = (56, -106), ["AU"] = (-25, 134),
            ["CH"] = (47, 8), ["SE"] = (62, 15), ["PL"] = (52, 19), ["TR"] = (39, 35), ["UA"] = (49, 32),
            ["FI"] = (64, 26), ["IT"] = (42, 12), ["ES"] = (40, -4), ["IN"] = (21, 78), ["BR"] = (-14, -52),
            ["KR"] = (36, 128), ["HK"] = (22.3, 114), ["AE"] = (24, 54), ["EE"] = (59, 26), ["LV"] = (57, 25),
            ["LT"] = (55, 24), ["CZ"] = (50, 15), ["AT"] = (47.5, 14), ["BE"] = (50.5, 4.5), ["PT"] = (39.5, -8),
            ["IE"] = (53, -8), ["NO"] = (61, 9), ["DK"] = (56, 10), ["RO"] = (46, 25), ["BG"] = (43, 25),
            ["GR"] = (39, 22), ["IL"] = (31, 35), ["KZ"] = (48, 68),
        };

        public static bool TryGet(string? countryCode, out double lat, out double lon)
        {
            lat = lon = 0;
            if (string.IsNullOrWhiteSpace(countryCode) || !Coords.TryGetValue(countryCode, out var c)) return false;
            (lat, lon) = c;
            return true;
        }
    }
}
