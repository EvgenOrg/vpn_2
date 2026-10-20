using Android.Content;
using Android.Net;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VPN.Services;

namespace VPN.Platforms.Android;

// --- Модели запросов/ответов ---

public class RunXrayRequest
{
    [JsonPropertyName("datDir")]
    public string DatDir { get; set; } = "";

    [JsonPropertyName("configPath")]
    public string ConfigPath { get; set; } = "";
}

public class PingRequest
{
    [JsonPropertyName("datDir")]
    public string DatDir { get; set; } = "";

    [JsonPropertyName("configPath")]
    public string ConfigPath { get; set; } = "";

    [JsonPropertyName("timeout")]
    public int Timeout { get; set; } = 10;

    [JsonPropertyName("url")]
    public string Url { get; set; } = "https://www.google.com";

    [JsonPropertyName("proxy")]
    public string Proxy { get; set; } = "";
}

// Структура CallResponse<T> которую возвращает Go
public class CallResponse<T>
{
    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("error")]
    public string Error { get; set; } = "";

    public bool IsSuccess => string.IsNullOrEmpty(Error);
}

// --- Хелпер для работы с LibXray ---

public static class LibXrayHelper
{
    // Кодируем объект → JSON → Base64
    public static string EncodeRequest<T>(T request)
    {
        var json = JsonSerializer.Serialize(request);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    // Декодируем Base64 → JSON → CallResponse<T>
    public static CallResponse<T>? DecodeResponse<T>(string base64Response)
    {
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64Response));
        return JsonSerializer.Deserialize<CallResponse<T>>(json);
    }
}

// --- Реализация VPN сервиса ---

public class VpnServiceImplementation : IVpnService
{
    // _running — static: и StartAsync (через инстанс из DI), и MainActivity.OnActivityResult
    // (статический вызов LaunchServiceAsync без инстанса) должны видеть одно и то же
    // состояние. Плюс подписка на XrayVpnService.StateChanged — чтобы статус оставался
    // верным, даже если VPN был выключен не из приложения, а из системного тумблера
    // Android (тогда сработает XrayVpnService.OnRevoke, а не наш StopAsync).
    static bool _running;

    static VpnServiceImplementation()
    {
        XrayVpnService.StateChanged += isRunning => _running = isRunning;
    }

    public bool IsRunning => _running;

    // Запрос разрешения и старт
    public async Task StartAsync(string rawConfigJson)
    {
        var context = global::Android.App.Application.Context;

        // Проверяем разрешение на VPN
        var vpnIntent = VpnService.Prepare(context);
        if (vpnIntent != null)
        {
            // Нужно показать системный диалог разрешения.
            // Это надо делать из Activity, не из сервиса.
            // Передаём управление в MainActivity.
            VpnPermissionHelper.PendingConfigJson = rawConfigJson;
            VpnPermissionHelper.RequestPermission?.Invoke(vpnIntent);
            return;
        }

        // Разрешение уже есть — запускаем сразу
        await LaunchServiceAsync(rawConfigJson);
    }

    public static async Task LaunchServiceAsync(string rawConfigJson)
    {
        var context = global::Android.App.Application.Context;

        var intent = new Intent(context, typeof(XrayVpnService));
        intent.SetAction(XrayVpnService.ActionStart);
        intent.PutExtra(XrayVpnService.ExtraConfig, rawConfigJson);

        context.StartForegroundService(intent);
        await Task.CompletedTask;

        // Реальное подтверждение "запустилось" придёт через XrayVpnService.StateChanged
        // (после того как Xray правда поднимется), но выставляем true уже сейчас,
        // чтобы UI не завис в состоянии загрузки, если сервис ответит с задержкой.
        _running = true;
    }

    public Task StopAsync()
    {
        var context = global::Android.App.Application.Context;
        var intent = new Intent(context, typeof(XrayVpnService));
        intent.SetAction(XrayVpnService.ActionStop);
        context.StartService(intent);
        _running = false;
        return Task.CompletedTask;
    }

    public async Task<bool> GetStatusAsync()
    {
        return _running;
    }
}

// Хелпер для передачи разрешения между сервисом и Activity
public static class VpnPermissionHelper
{
    public static string? PendingConfigJson { get; set; }
    public static Action<Intent>? RequestPermission { get; set; }
}
