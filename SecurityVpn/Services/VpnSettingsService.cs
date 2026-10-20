using System;
using Microsoft.Maui.Storage;

namespace VPN.Services
{
    // Хранит настройки страницы Settings (протокол, DNS, split tunneling,
    // kill switch, автоподключение, блокировка рекламы) и сохраняет их
    // в Preferences, чтобы они переживали перезапуск приложения —
    // по тому же принципу, что ThemeService и LocalizationService.
    public class VpnSettingsService
    {
        private const string ProtocolKey = "settings_protocol";
        private const string DnsKey = "settings_dns";
        private const string SplitTunnelingKey = "settings_split_tunneling";
        private const string KillSwitchKey = "settings_kill_switch";
        private const string AutoConnectKey = "settings_auto_connect";
        private const string BlockAdsKey = "settings_block_ads";

        public string Protocol { get; private set; }
        public string DnsServer { get; private set; }
        public bool SplitTunneling { get; private set; }
        public bool KillSwitch { get; private set; }
        public bool AutoConnect { get; private set; }
        public bool BlockAds { get; private set; }

        public event Action? OnChange;

        public VpnSettingsService()
        {
            Protocol = Preferences.Get(ProtocolKey, "vless");
            DnsServer = Preferences.Get(DnsKey, "1.1.1.1");
            SplitTunneling = Preferences.Get(SplitTunnelingKey, false);
            KillSwitch = Preferences.Get(KillSwitchKey, true);
            AutoConnect = Preferences.Get(AutoConnectKey, false);
            BlockAds = Preferences.Get(BlockAdsKey, true);
        }

        public void SetProtocol(string value)
        {
            if (Protocol == value) return;
            Protocol = value;
            Preferences.Set(ProtocolKey, value);
            NotifyStateChanged();
        }

        public void SetDnsServer(string value)
        {
            if (DnsServer == value) return;
            DnsServer = value;
            Preferences.Set(DnsKey, value);
            NotifyStateChanged();
        }

        public void SetSplitTunneling(bool value)
        {
            if (SplitTunneling == value) return;
            SplitTunneling = value;
            Preferences.Set(SplitTunnelingKey, value);
            NotifyStateChanged();
        }

        public void SetKillSwitch(bool value)
        {
            if (KillSwitch == value) return;
            KillSwitch = value;
            Preferences.Set(KillSwitchKey, value);
            NotifyStateChanged();
        }

        public void SetAutoConnect(bool value)
        {
            if (AutoConnect == value) return;
            AutoConnect = value;
            Preferences.Set(AutoConnectKey, value);
            NotifyStateChanged();
        }

        public void SetBlockAds(bool value)
        {
            if (BlockAds == value) return;
            BlockAds = value;
            Preferences.Set(BlockAdsKey, value);
            NotifyStateChanged();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
