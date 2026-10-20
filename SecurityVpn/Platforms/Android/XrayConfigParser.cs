using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

// --- Модели конфига Xray ---

public class XrayInbound
{
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = "";

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = "";

    [JsonPropertyName("listen")]
    public string Listen { get; set; } = "127.0.0.1";
}

public class XrayConfig
{
    [JsonPropertyName("inbounds")]
    public List<XrayInbound> Inbounds { get; set; } = new();
}

// --- Хелпер для парсинга портов ---



public static class XrayConfigParser
{
    /// <summary>
    /// ConvertShareLinksToXrayJson может вернуть несколько JSON-объектов подряд.
    /// Этот метод объединяет их в один, мержа поля верхнего уровня.
    /// </summary>
    public static JsonObject MergeNdJson(string rawJson)
    {
        var merged = new JsonObject();

        var options = new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        };

        // Разбиваем поток на отдельные документы
        var reader = new Utf8JsonReader(
            System.Text.Encoding.UTF8.GetBytes(rawJson),
            new JsonReaderOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

        while (true)
        {
            JsonNode? node;
            try
            {
                node = JsonNode.Parse(ref reader);
            }
            catch
            {
                break;
            }

            if (node is not JsonObject obj) continue;

            // Мержим все поля в один объект
            foreach (var kvp in obj)
            {
                // Клонируем значение через сериализацию
                var valueJson = kvp.Value?.ToJsonString();
                if (valueJson != null)
                    merged[kvp.Key] = JsonNode.Parse(valueJson);
            }

            // Если reader дочитан до конца — выходим
            if (reader.BytesConsumed >= System.Text.Encoding.UTF8.GetByteCount(rawJson))
                break;
        }

        return merged;
    }

    public static void PrintInboundInfo(string rawConfigJson)
    {
        try
        {
            var merged = MergeNdJson(rawConfigJson);
            var mergedString = merged.ToJsonString();

            Debug.WriteLine($"[Xray] Merged config preview: {mergedString[..Math.Min(200, mergedString.Length)]}...");

            var inbounds = merged["inbounds"]?.AsArray();
            if (inbounds == null || inbounds.Count == 0)
            {
                Debug.WriteLine("[Xray] No inbounds found");
                return;
            }

            Debug.WriteLine("[Xray] === Inbound listeners ===");
            foreach (var item in inbounds)
            {
                var protocol = item?["protocol"]?.GetValue<string>() ?? "unknown";
                var port = item?["port"]?.GetValue<int>() ?? 0;
                var listen = item?["listen"]?.GetValue<string>() ?? "0.0.0.0";
                var tag = item?["tag"]?.GetValue<string>() ?? "-";

                if (string.IsNullOrEmpty(listen)) listen = "0.0.0.0";

                Debug.WriteLine($"[Xray] [{tag}] {protocol.ToUpper()} → {listen}:{port}");

                if (protocol == "socks")
                    Debug.WriteLine($"[Xray]   → socks5://{listen}:{port}");
                else if (protocol == "http")
                    Debug.WriteLine($"[Xray]   → http://{listen}:{port}");
            }
            Debug.WriteLine("[Xray] ==========================");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Xray] PrintInboundInfo error: {ex.Message}");
        }
    }

    public static int? GetPort(string rawConfigJson, string protocol)
    {
        try
        {
            var merged = MergeNdJson(rawConfigJson);
            var inbounds = merged["inbounds"]?.AsArray();

            foreach (var item in inbounds ?? new JsonArray())
            {
                var proto = item?["protocol"]?.GetValue<string>() ?? "";
                if (proto.Equals(protocol, StringComparison.OrdinalIgnoreCase))
                    return item?["port"]?.GetValue<int>();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Xray] GetPort error: {ex.Message}");
        }
        return null;
    }

    // Получить итоговый merged JSON для передачи в RunXrayFromJSON
    public static string GetMergedConfigJson(string rawConfigJson)
    {
        var merged = MergeNdJson(rawConfigJson);

        // Патчим listen: 127.0.0.1 → 0.0.0.0 чтобы был доступен с устройства
        var inbounds = merged["inbounds"]?.AsArray();
        if (inbounds != null)
        {
            foreach (var item in inbounds)
            {
                if (item is JsonObject obj)
                {
                    obj["listen"] = "0.0.0.0";
                }
            }
        }

        return merged.ToJsonString();
    }
}