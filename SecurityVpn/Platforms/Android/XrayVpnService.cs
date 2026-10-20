using Android.App;
using Android.Content;
using Android.Net;
using Android.OS;
using System.Diagnostics;
using Debug = System.Diagnostics.Debug;

namespace VPN.Platforms.Android;

[Service(
    Name = "com.securityvpn.app.XrayVpnService",
    Permission = "android.permission.BIND_VPN_SERVICE")]
[IntentFilter(new[] { "android.net.VpnService" })]
public class XrayVpnService : VpnService
{
    public const string ActionStart = "START";
    public const string ActionStop = "STOP";
    public const string ExtraConfig = "CONFIG_JSON";

    // Сообщает реальное состояние Xray (не просто "мы попросили сервис стартовать").
    // Подписывается VpnServiceImplementation, чтобы IsRunning оставался верным даже
    // если VPN был отключён извне (системный тумблер Android → OnRevoke).
    public static event Action<bool>? StateChanged;

    ParcelFileDescriptor? _tunFd;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == ActionStop)
        {
            StopVpn();
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        if (intent?.Action == ActionStart)
        {
            var configJson = intent.GetStringExtra(ExtraConfig) ?? "";  
            StartForegroundNotification();
            // Запускаем в фоновом потоке — не блокируем UI
            Task.Run(() => StartVpn(configJson));
        }

        return StartCommandResult.Sticky;
    }

    async Task StartVpn(string rawConfigJson)
    {
        try
        {
            var mergedJson = XrayConfigParser.GetMergedConfigJson(rawConfigJson);

            // 1. Добавляем tun inbound в конфиг
            mergedJson = InjectTunInbound(mergedJson);
            System.Diagnostics.Debug.WriteLine($"[VPN] Config with tun: {mergedJson[..Math.Min(300, mergedJson.Length)]}");

            // 2. Строим TUN интерфейс
            var builder = new Builder(this)
                .SetSession("XrayVPN")
                .AddAddress("10.0.0.1", 24)
                .AddDnsServer("1.1.1.1")
                .AddDnsServer("8.8.8.8")
                .AddRoute("0.0.0.0", 0)
                .AddRoute("::", 0)
                .SetMtu(1500)
                .AddDisallowedApplication("com.securityvpn.app");

            _tunFd = builder.Establish();

            if (_tunFd == null)
            {
                System.Diagnostics.Debug.WriteLine("[VPN] TUN fd is null — нет разрешения?");
                StateChanged?.Invoke(false);
                return;
            }

            var fd = _tunFd.Fd;
            System.Diagnostics.Debug.WriteLine($"[VPN] TUN fd = {fd}");

            // 3. Передаём fd Xray ДО запуска
#if HAS_LIBXRAY
            global::LibXray.LibXray.SetTunFd(fd);      // protect-сокеты — оставляем
#endif
            System.Environment.SetEnvironmentVariable("XRAY_TUN_FD", fd.ToString());
            System.Diagnostics.Debug.WriteLine("[VPN] TunFd set");

            // 4. Запускаем Xray
            var appDir = FileSystem.AppDataDirectory;
            var datDir = Path.Combine(appDir, "dat");
            await EnsureDatFilesAsync(datDir);

#if HAS_LIBXRAY
            var request = new { datDir, configJSON = mergedJson };
            var encoded = LibXrayHelper.EncodeRequest(request);
            System.Diagnostics.Debug.WriteLine($"[VPN] Full config: {mergedJson}");
            var resultBase64 = global::LibXray.LibXray.RunXrayFromJSON(encoded);
            var result = LibXrayHelper.DecodeResponse<string>(resultBase64);

            if (result == null || !result.IsSuccess)
            {
                System.Diagnostics.Debug.WriteLine($"[VPN] RunXrayFromJSON failed: {result?.Error}");
                StateChanged?.Invoke(false);
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[VPN] Xray started: {global::LibXray.LibXray.XrayState}");
#else
            System.Diagnostics.Debug.WriteLine("[VPN] LibXray не подключён: положите проект LibXrayBinding рядом с SecurityVpn");
            StateChanged?.Invoke(false);
            return;
#endif
            XrayConfigParser.PrintInboundInfo(rawConfigJson);
            StateChanged?.Invoke(true);

        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VPN] StartVpn error: {ex}");
            StateChanged?.Invoke(false);
        }
    }

    // geoip.dat/geosite.dat нужны Xray для правил "geosite:"/"geoip:" в роутинге,
    // и он ищет их прямо в datDir — просто указать путь недостаточно, файлы
    // нужно один раз скопировать из бандла приложения (тот же приём, что уже
    // используется в WindowsVpnService для xray.exe).
    // ВАЖНО: предполагается, что geoip.dat и geosite.dat добавлены в проект как
    // MauiAsset (Raw Asset) по пути Resources/Raw/dat/geoip.dat и .../dat/geosite.dat.
    // Если у вас другое расположение — поправьте пути ниже.
    async Task EnsureDatFilesAsync(string datDir)
    {
        Directory.CreateDirectory(datDir);

        await EnsureAssetCopiedAsync("dat/geoip.dat", Path.Combine(datDir, "geoip.dat"));
        await EnsureAssetCopiedAsync("dat/geosite.dat", Path.Combine(datDir, "geosite.dat"));
    }

    static async Task EnsureAssetCopiedAsync(string packageFileName, string destinationPath)
    {
        if (File.Exists(destinationPath)) return;

        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(packageFileName);
            using var fs = File.Create(destinationPath);
            await stream.CopyToAsync(fs);
            System.Diagnostics.Debug.WriteLine($"[VPN] Скопирован {packageFileName} -> {destinationPath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VPN] Не удалось скопировать {packageFileName}: {ex.Message}");
        }
    }

    // Добавляем tun inbound в конфиг чтобы Xray знал как обрабатывать трафик из TUN
    string InjectTunInbound(string configJson)
    {
        try
        {
            var config = System.Text.Json.Nodes.JsonNode.Parse(configJson)
                         as System.Text.Json.Nodes.JsonObject;
            if (config == null) return configJson;

            // Получаем текущий массив inbounds
            var inbounds = config["inbounds"]?.AsArray();
            if (inbounds == null)
            {
                inbounds = new System.Text.Json.Nodes.JsonArray();
                config["inbounds"] = inbounds;
            }

            // Xray-core tun inbound — читает пакеты напрямую из TUN fd
            var tunInbound = System.Text.Json.Nodes.JsonNode.Parse("""
{
    "tag": "tun-in",
    "protocol": "tun",
    "port": 0,
    "settings": { "name": "tun0", "MTU": 1500 }
}
""");
            inbounds.Add(tunInbound);

            // Убеждаемся что routing направляет tun трафик в proxy
            var routing = config["routing"]?.AsObject();
            if (routing != null)
            {
                var rules = routing["rules"]?.AsArray();
                if (rules != null)
                {
                    // Вставляем в начало — чтобы сработало раньше других правил
                    var tunRule = System.Text.Json.Nodes.JsonNode.Parse("""
                {
                    "type": "field",
                    "inboundTag": ["tun-in"],
                    "outboundTag": "proxy"
                }
                """);
                    rules.Insert(0, tunRule);
                }
            }

            return config.ToJsonString();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VPN] InjectTunInbound error: {ex.Message}");
            return configJson;
        }
    }

    void StopVpn()
    {
        try
        {
#if HAS_LIBXRAY
            var resultBase64 = global::LibXray.LibXray.StopXray();
            var result = LibXrayHelper.DecodeResponse<string>(resultBase64);
            System.Diagnostics.Debug.WriteLine($"[VPN] StopXray: {(result?.IsSuccess == true ? "ok" : result?.Error)}");
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VPN] StopXray error: {ex.Message}");
        }
        finally
        {
            _tunFd?.Close();
            _tunFd = null;
            StateChanged?.Invoke(false);
            System.Diagnostics.Debug.WriteLine("[VPN] TUN closed");
        }
    }

    void StartForegroundNotification()
    {
        const string channelId = "vpn_channel";
        const int notifId = 1;

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            var channel = new NotificationChannel(channelId, "VPN Service", NotificationImportance.Low);
            var manager = (NotificationManager?)GetSystemService(NotificationService);
            manager?.CreateNotificationChannel(channel);
        }

        var notification = new Notification.Builder(this, channelId)
            .SetContentTitle("VPN активен")
            .SetContentText("Трафик защищён через Xray")
            .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
            .SetOngoing(true)
            .Build();

        if (OperatingSystem.IsAndroidVersionAtLeast(34))
            StartForeground(notifId, notification,
                global::Android.Content.PM.ForegroundService.TypeSpecialUse);
        else
            StartForeground(notifId, notification);
    }

    public override void OnRevoke()
    {
        StopVpn();
        base.OnRevoke();
    }

    public override void OnDestroy()
    {
        StopVpn();
        base.OnDestroy();
    }
}