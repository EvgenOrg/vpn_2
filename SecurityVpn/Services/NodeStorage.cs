using Microsoft.Maui.Storage;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using VPN.Components.Models;

namespace VPN.Services
{
    /// <summary>
    /// Список серверов и выбранный сервер.
    /// Демо-серверы встроены (подключение к ним имитируется — чтобы можно было проверить интерфейс),
    /// свои серверы пользователь добавляет vless:// ссылкой, они хранятся в Preferences.
    /// </summary>
    public class NodeStorage
    {
        private const string CustomKey = "nodes_custom";
        private const string SelectedKey = "node_selected";

        private readonly List<Node> _demo = CreateDemo();
        private readonly List<Node> _custom = new();
        private Node? _selected;

        public event Action? OnChange;

        /// <summary>Выбранный сервер изменился после последнего подключения — Home перезапустит VPN.</summary>
        public bool HasChanged { get; set; }

        public NodeStorage()
        {
            try
            {
                var json = Preferences.Get(CustomKey, "");
                if (!string.IsNullOrWhiteSpace(json))
                    _custom.AddRange(JsonSerializer.Deserialize<List<Node>>(json) ?? new());
            }
            catch (JsonException)
            {
                // повреждённые данные — начинаем с пустого списка своих серверов
            }

            if (Guid.TryParse(Preferences.Get(SelectedKey, ""), out var id))
                _selected = All.FirstOrDefault(n => n.Id == id);
        }

        public IReadOnlyList<Node> All => _demo.Concat(_custom).ToList();

        public IReadOnlyList<Node> Custom => _custom;

        public Node? SelectedNode
        {
            get => _selected;
            set
            {
                _selected = value;
                Preferences.Set(SelectedKey, value is null ? "" : value.Id.ToString());
                OnChange?.Invoke();
            }
        }

        public void Add(Node node)
        {
            _custom.Add(node);
            SaveCustom();
            OnChange?.Invoke();
        }

        public void Remove(Guid id)
        {
            _custom.RemoveAll(n => n.Id == id);
            if (_selected?.Id == id) SelectedNode = null;
            SaveCustom();
            OnChange?.Invoke();
        }

        /// <summary>Проверяет доступность своих серверов (TCP-соединение с host:port) и меряет отклик.</summary>
        public async Task RefreshStatusAsync()
        {
            await Task.WhenAll(_custom.ToList().Select(CheckAsync));
        }

        public Task CheckAsync(Node node) => CheckInternalAsync(node);

        private async Task CheckInternalAsync(Node node)
        {
            node.IsChecking = true;
            OnChange?.Invoke();

            node.PingMs = await PingAsync(node);
            node.IsOnline = node.PingMs >= 0;
            node.IsChecking = false;
            OnChange?.Invoke();
        }

        private static async Task<int> PingAsync(Node node)
        {
            try
            {
                using var client = new TcpClient();
                using var cts = new CancellationTokenSource(2500);
                var sw = Stopwatch.StartNew();
                await client.ConnectAsync(node.Host, node.Port, cts.Token).ConfigureAwait(false);
                return (int)Math.Max(1, sw.ElapsedMilliseconds);
            }
            catch
            {
                return -1;
            }
        }

        private void SaveCustom() => Preferences.Set(CustomKey, JsonSerializer.Serialize(_custom));

        private static List<Node> CreateDemo()
        {
            Node Make(int n, string name, string country, string host) => new()
            {
                Id = new Guid($"00000000-0000-0000-0000-{n:D12}"),
                Name = name,
                Country = country,
                Host = host,
                IsDemo = true,
                IsOnline = true,
                PingMs = 24 + (n * 37 % 96),
            };

            return new List<Node>
            {
                Make(1, "Frankfurt", "DE", "demo-de"),
                Make(2, "Amsterdam", "NL", "demo-nl"),
                Make(3, "Helsinki", "FI", "demo-fi"),
                Make(4, "Singapore", "SG", "demo-sg"),
                Make(5, "Tokyo", "JP", "demo-jp"),
            };
        }
    }
}
