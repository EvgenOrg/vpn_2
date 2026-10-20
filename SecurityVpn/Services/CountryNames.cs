using System;
using System.Collections.Generic;

namespace VPN.Services
{
    // Человекочитаемые названия стран по ISO-коду, на русском и английском.
    // Используется в ServerCard/Servers для отображения и для поиска.
    public static class CountryNames
    {
        private static readonly Dictionary<string, (string Ru, string En)> Names = new(StringComparer.OrdinalIgnoreCase)
        {
            ["US"] = ("США", "United States"),
            ["GB"] = ("Великобритания", "United Kingdom"),
            ["DE"] = ("Германия", "Germany"),
            ["FR"] = ("Франция", "France"),
            ["NL"] = ("Нидерланды", "Netherlands"),
            ["RU"] = ("Россия", "Russia"),
            ["JP"] = ("Япония", "Japan"),
            ["SG"] = ("Сингапур", "Singapore"),
            ["CA"] = ("Канада", "Canada"),
            ["AU"] = ("Австралия", "Australia"),
            ["CH"] = ("Швейцария", "Switzerland"),
            ["SE"] = ("Швеция", "Sweden"),
            ["PL"] = ("Польша", "Poland"),
            ["TR"] = ("Турция", "Turkey"),
            ["UA"] = ("Украина", "Ukraine"),
            ["FI"] = ("Финляндия", "Finland"),
            ["IT"] = ("Италия", "Italy"),
            ["ES"] = ("Испания", "Spain"),
            ["IN"] = ("Индия", "India"),
            ["BR"] = ("Бразилия", "Brazil"),
            ["KR"] = ("Южная Корея", "South Korea"),
            ["HK"] = ("Гонконг", "Hong Kong"),
            ["AE"] = ("ОАЭ", "UAE"),
            ["EE"] = ("Эстония", "Estonia"),
            ["LV"] = ("Латвия", "Latvia"),
            ["LT"] = ("Литва", "Lithuania"),
            ["CZ"] = ("Чехия", "Czechia"),
            ["AT"] = ("Австрия", "Austria"),
            ["BE"] = ("Бельгия", "Belgium"),
            ["PT"] = ("Португалия", "Portugal"),
            ["IE"] = ("Ирландия", "Ireland"),
            ["NO"] = ("Норвегия", "Norway"),
            ["DK"] = ("Дания", "Denmark"),
            ["RO"] = ("Румыния", "Romania"),
            ["BG"] = ("Болгария", "Bulgaria"),
            ["GR"] = ("Греция", "Greece"),
            ["IL"] = ("Израиль", "Israel"),
            ["KZ"] = ("Казахстан", "Kazakhstan"),
        };

        public static string GetName(string? countryCode, AppLanguage lang = AppLanguage.Ru)
        {
            if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Length != 2)
            {
                return lang == AppLanguage.En ? "Unknown" : "Неизвестно";
            }

            if (Names.TryGetValue(countryCode, out var pair))
            {
                return lang == AppLanguage.En ? pair.En : pair.Ru;
            }

            return countryCode.ToUpperInvariant();
        }
    }
}
