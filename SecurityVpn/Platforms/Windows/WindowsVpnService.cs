using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Maui.Storage;
using VPN.Services;

namespace VPN.WinUI
{
    /// <summary>
    /// VPN на Windows: запускает xray.exe с клиентским конфигом (VLESS + Reality)
    /// и направляет на него системный прокси Windows.
    /// xray.exe лежит в Resources\Raw (его кладёт tools\get-xray.ps1) и при первом запуске
    /// копируется из пакета приложения в %LOCALAPPDATA%\...\core.
    /// </summary>
    public sealed class WindowsVpnService : IVpnService
    {
        private Process? _process;
        private readonly Queue<string> _tail = new();
        private long _lastUp, _lastDown;
        private DateTime _lastSample;

        private static string CoreDir => Path.Combine(FileSystem.AppDataDirectory, "core");
        private static string XrayExe => Path.Combine(CoreDir, "xray.exe");

        public bool IsRunning => _process is { HasExited: false };

        public Task<bool> GetStatusAsync() => Task.FromResult(IsRunning);

        public async Task StartAsync(string configJson)
        {
            await StopAsync();

            Directory.CreateDirectory(CoreDir);
            await EnsureXrayAsync();
            KillStrays();

            var configPath = Path.Combine(CoreDir, "config.json");
            await File.WriteAllTextAsync(configPath, configJson);

            var info = new ProcessStartInfo(XrayExe)
            {
                ArgumentList = { "run", "-config", configPath },
                WorkingDirectory = CoreDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            info.Environment["XRAY_LOCATION_ASSET"] = CoreDir;

            var process = Process.Start(info) ?? throw new InvalidOperationException("Не удалось запустить ядро VPN.");
            process.OutputDataReceived += (_, e) => Remember(e.Data);
            process.ErrorDataReceived += (_, e) => Remember(e.Data);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _process = process;

            var deadline = DateTime.UtcNow.AddSeconds(8);
            while (DateTime.UtcNow < deadline)
            {
                if (process.HasExited)
                    throw new InvalidOperationException("Ядро VPN сразу остановилось. " + LastLogLine());
                if (await PortOpenAsync(XrayConfigBuilder.HttpPort))
                {
                    SystemProxy.Enable();
                    return;
                }
                await Task.Delay(150);
            }

            await StopAsync();
            throw new InvalidOperationException("Ядро VPN запустилось, но не открыло локальный порт. " + LastLogLine());
        }

        public Task StopAsync()
        {
            try { SystemProxy.Restore(); } catch { /* не критично */ }

            var process = _process;
            _process = null;
            if (process is not null)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(true);
                        process.WaitForExit(3000);
                    }
                }
                catch { /* уже завершён */ }
                process.Dispose();
            }

            _lastSample = default;
            return Task.CompletedTask;
        }

        /// <summary>Скорость в байтах/с по счётчикам Xray. null, пока нет двух замеров.</summary>
        public async Task<(double Down, double Up)?> SampleSpeedAsync()
        {
            if (!IsRunning) return null;

            var info = new ProcessStartInfo(XrayExe)
            {
                ArgumentList = { "api", "statsquery", "-s", $"127.0.0.1:{XrayConfigBuilder.ApiPort}", "-pattern", "outbound>>>proxy>>>traffic" },
                WorkingDirectory = CoreDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var timeout = new CancellationTokenSource(3000);
            string output;
            try
            {
                using var process = Process.Start(info);
                if (process is null) return null;
                output = await process.StandardOutput.ReadToEndAsync(timeout.Token);
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                return null; // не дождались ответа: пропускаем этот замер
            }

            long up = 0, down = 0;
            try
            {
                using var doc = JsonDocument.Parse(output);
                if (doc.RootElement.TryGetProperty("stat", out var stats) && stats.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in stats.EnumerateArray())
                    {
                        var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        var value = ReadLong(item);
                        if (name.EndsWith("uplink", StringComparison.Ordinal)) up = value;
                        else if (name.EndsWith("downlink", StringComparison.Ordinal)) down = value;
                    }
                }
            }
            catch (JsonException)
            {
                return null;
            }

            var now = DateTime.UtcNow;
            (double, double)? result = null;
            if (_lastSample != default)
            {
                var seconds = Math.Max(0.1, (now - _lastSample).TotalSeconds);
                result = (Math.Max(0, down - _lastDown) / seconds, Math.Max(0, up - _lastUp) / seconds);
            }

            _lastSample = now;
            _lastDown = down;
            _lastUp = up;
            return result;
        }

        private static long ReadLong(JsonElement item)
        {
            if (!item.TryGetProperty("value", out var v)) return 0;
            return v.ValueKind switch
            {
                JsonValueKind.Number => v.GetInt64(),
                JsonValueKind.String when long.TryParse(v.GetString(), out var parsed) => parsed,
                _ => 0,
            };
        }

        private static async Task EnsureXrayAsync()
        {
            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync("xray.exe");

                if (File.Exists(XrayExe) && stream.CanSeek && new FileInfo(XrayExe).Length == stream.Length)
                    return; // уже скопирован и совпадает по размеру

                using var fs = File.Create(XrayExe);
                await stream.CopyToAsync(fs);
            }
            catch (FileNotFoundException)
            {
                throw new InvalidOperationException(
                    "Не найден xray.exe. Запустите tools\\get-xray.ps1 (он положит ядро в Resources\\Raw) и пересоберите приложение.");
            }
        }

        private static async Task<bool> PortOpenAsync(int port)
        {
            try
            {
                using var client = new TcpClient();
                using var cts = new CancellationTokenSource(400);
                await client.ConnectAsync("127.0.0.1", port, cts.Token);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Если прошлый запуск закрылся аварийно, в системе мог остаться наш xray.exe, занимающий порты.</summary>
        private static void KillStrays()
        {
            foreach (var p in Process.GetProcessesByName("xray"))
            {
                try
                {
                    if (string.Equals(p.MainModule?.FileName, XrayExe, StringComparison.OrdinalIgnoreCase))
                    {
                        p.Kill(true);
                        p.WaitForExit(2000);
                    }
                }
                catch { /* нет доступа или уже завершён */ }
                finally { p.Dispose(); }
            }
        }

        private void Remember(string? line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            lock (_tail)
            {
                _tail.Enqueue(line.Trim());
                while (_tail.Count > 20) _tail.Dequeue();
            }
        }

        private string LastLogLine()
        {
            lock (_tail)
            {
                var last = _tail.LastOrDefault();
                if (last is null) return "";
                return "Подробности: " + (last.Length > 220 ? last[..220] + "…" : last);
            }
        }
    }
}
