using System;
using Microsoft.Maui.Storage;

namespace VPN.Services
{
    public enum AppTheme
    {
        System,
        Light,
        Dark
    }

    // Хранит выбранную пользователем тему (Системная/Светлая/Тёмная),
    // сохраняет выбор в Preferences (переживает перезапуск приложения)
    // и уведомляет подписчиков (layout-компоненты) об изменении,
    // чтобы они применили тему через JS (data-theme атрибут на <html>).
    public class ThemeService
    {
        private const string PrefKey = "app_theme";

        public AppTheme CurrentTheme { get; private set; }

        public event Action? OnChange;

        public ThemeService()
        {
            var saved = Preferences.Get(PrefKey, "dark");
            CurrentTheme = saved switch
            {
                "dark" => AppTheme.Dark,
                "light" => AppTheme.Light,
                _ => AppTheme.System
            };
        }

        public string CurrentThemeValue => CurrentTheme switch
        {
            AppTheme.Dark => "dark",
            AppTheme.Light => "light",
            _ => "system"
        };

        public void SetTheme(AppTheme theme)
        {
            if (CurrentTheme == theme) return;

            CurrentTheme = theme;
            Preferences.Set(PrefKey, CurrentThemeValue);
            OnChange?.Invoke();
        }

        public void SetTheme(string value)
        {
            var theme = value switch
            {
                "dark" => AppTheme.Dark,
                "light" => AppTheme.Light,
                _ => AppTheme.System
            };
            SetTheme(theme);
        }
    }
}
