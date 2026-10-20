using Microsoft.Maui.Storage;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace VPN.Services
{
    /// <summary>
    /// Локальные аккаунты: логин + пароль хранятся на устройстве (пароль — только в виде PBKDF2-хеша).
    /// Это заглушка до появления серверного API: когда он будет, достаточно заменить тела
    /// LoginAsync / RegisterAsync на запросы к серверу — страницы входа менять не придётся.
    /// Методы возвращают null при успехе или ключ перевода ошибки.
    /// </summary>
    public class AuthService
    {
        private const string UsersKey = "auth_users";
        private const string CurrentKey = "auth_current_user";
        private const int Iterations = 100_000;

        private static readonly Regex LoginRegex = new("^[A-Za-z0-9_]{3,30}$", RegexOptions.Compiled);

        private sealed record Account(string Name, string Salt, string Hash, DateTime Created);

        private readonly Dictionary<string, Account> _users = new();

        public string? UserName { get; private set; }

        public bool IsAuthenticated => UserName is not null;

        public DateTime? RegisteredAt =>
            UserName is not null && _users.TryGetValue(Key(UserName), out var a) ? a.Created.ToLocalTime() : null;

        public event Action? OnChange;

        public AuthService()
        {
            try
            {
                var json = Preferences.Get(UsersKey, "");
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var saved = JsonSerializer.Deserialize<Dictionary<string, Account>>(json);
                    if (saved is not null)
                        foreach (var (k, v) in saved) _users[k] = v;
                }
            }
            catch (JsonException)
            {
                // повреждённые данные — аккаунтов нет
            }

            var current = Preferences.Get(CurrentKey, "");
            if (current.Length > 0 && _users.TryGetValue(Key(current), out var acc))
                UserName = acc.Name;
        }

        public async Task<string?> LoginAsync(string login, string password, bool remember)
        {
            login = (login ?? "").Trim();
            if (!_users.TryGetValue(Key(login), out var acc)) return "auth.error.invalid";

            var ok = await Task.Run(() => Verify(password ?? "", acc));
            if (!ok) return "auth.error.invalid";

            SignIn(acc.Name, remember);
            return null;
        }

        public async Task<string?> RegisterAsync(string login, string password)
        {
            login = (login ?? "").Trim();
            if (!LoginRegex.IsMatch(login)) return "auth.error.loginFormat";
            if ((password ?? "").Length < 8) return "auth.error.passwordShort";
            if (_users.ContainsKey(Key(login))) return "auth.error.exists";

            var account = await Task.Run(() => CreateAccount(login, password!));
            _users[Key(login)] = account;
            Preferences.Set(UsersKey, JsonSerializer.Serialize(_users));

            SignIn(account.Name, remember: true);
            return null;
        }

        public void Logout()
        {
            UserName = null;
            Preferences.Remove(CurrentKey);
            OnChange?.Invoke();
        }

        private void SignIn(string name, bool remember)
        {
            UserName = name;
            if (remember) Preferences.Set(CurrentKey, name);
            else Preferences.Remove(CurrentKey);
            OnChange?.Invoke();
        }

        private static string Key(string login) => login.Trim().ToLowerInvariant();

        private static Account CreateAccount(string login, string password)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
            return new Account(login, Convert.ToBase64String(salt), Convert.ToBase64String(hash), DateTime.UtcNow);
        }

        private static bool Verify(string password, Account acc)
        {
            var salt = Convert.FromBase64String(acc.Salt);
            var expected = Convert.FromBase64String(acc.Hash);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
    }
}
