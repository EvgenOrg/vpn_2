using System.Globalization;

namespace VPN.Services
{
    /// <summary>Форматирование скорости, объёма трафика и времени для интерфейса.</summary>
    public static class Fmt
    {
        public static string Speed(double bytesPerSecond, LocalizationService loc)
        {
            var kb = bytesPerSecond / 1024.0;
            if (kb < 1000) return $"{kb.ToString("0", CultureInfo.InvariantCulture)} {loc.T("unit.kbs")}";
            return $"{(kb / 1024.0).ToString("0.0", CultureInfo.InvariantCulture)} {loc.T("unit.mbs")}";
        }

        public static string Bytes(long bytes, LocalizationService loc)
        {
            double mb = bytes / 1024.0 / 1024.0;
            if (mb < 1000) return $"{mb.ToString("0.0", CultureInfo.InvariantCulture)} {loc.T("unit.mb")}";
            return $"{(mb / 1024.0).ToString("0.0", CultureInfo.InvariantCulture)} {loc.T("unit.gb")}";
        }

        public static string Duration(TimeSpan t) =>
            $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
    }
}
