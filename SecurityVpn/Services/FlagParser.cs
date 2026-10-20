using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VPN.Services
{
    static public class FlagParser
    {
        public static string GetCountryFlag(string countryCode)
        {
            if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Length != 2)
            {
                return "🏳"; // Возвращаем белый флаг по умолчанию
            }

            // Переводим в верхний регистр и сдвигаем каждый символ в диапазон эмодзи
            return string.Concat(countryCode
                .ToUpper()
                .Select(ch => char.ConvertFromUtf32(ch + 0x1F1A5)));
        }

    }
}
