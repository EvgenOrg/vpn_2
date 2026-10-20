using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Maui.Storage;

namespace VPN.Services
{
    public enum AppLanguage
    {
        Ru,
        En
    }

    // Хранит выбранный пользователем язык интерфейса (RU/EN),
    // сохраняет выбор в Preferences (переживает перезапуск приложения)
    // и уведомляет подписчиков об изменении, чтобы они перерисовались.
    // Использование в разметке: @Loc.T("nav.home")
    public class LocalizationService
    {
        private const string PrefKey = "app_language";

        public AppLanguage CurrentLanguage { get; private set; }

        public event Action? OnChange;

        public LocalizationService()
        {
            var saved = Preferences.Get(PrefKey, "ru");
            CurrentLanguage = saved == "en" ? AppLanguage.En : AppLanguage.Ru;
        }

        public CultureInfo Culture => CurrentLanguage == AppLanguage.En ? new CultureInfo("en-US") : new CultureInfo("ru-RU");

        public string CurrentLanguageValue => CurrentLanguage == AppLanguage.En ? "en" : "ru";

        public void SetLanguage(AppLanguage lang)
        {
            if (CurrentLanguage == lang) return;

            CurrentLanguage = lang;
            Preferences.Set(PrefKey, CurrentLanguageValue);
            OnChange?.Invoke();
        }

        public void SetLanguage(string value)
        {
            SetLanguage(value == "en" ? AppLanguage.En : AppLanguage.Ru);
        }

        // Перевод по ключу. Если ключа нет в словаре — возвращает сам ключ,
        // чтобы отсутствие перевода было заметно, а не ломало верстку.
        public string T(string key)
        {
            return Translations.TryGetValue(key, out var pair)
                ? (CurrentLanguage == AppLanguage.En ? pair.En : pair.Ru)
                : key;
        }

        private static readonly Dictionary<string, (string Ru, string En)> Translations = new()
        {
            // ===== Навигация =====
            ["nav.home"] = ("Главная", "Home"),
            ["nav.servers"] = ("Серверы", "Servers"),
            ["nav.subscription"] = ("Подписка", "Subscription"),
            ["nav.settings"] = ("Настройки", "Settings"),
            ["nav.profile"] = ("Профиль", "Profile"),
            ["nav.dropdown.subscription"] = ("Подписка PRO", "PRO Subscription"),
            ["nav.dropdown.support"] = ("Поддержка", "Support"),
            ["nav.dropdown.logout"] = ("Выйти из аккаунта", "Log out"),
            ["nav.user.default"] = ("Пользователь", "User"),

            // ===== Главная =====
            ["home.status.connecting"] = ("ПОДКЛЮЧЕНИЕ", "CONNECTING"),
            ["home.status.protected"] = ("ЗАЩИЩЕНО", "PROTECTED"),
            ["home.status.unprotected"] = ("НЕ ЗАЩИЩЕНО", "UNPROTECTED"),
            ["home.title.connecting"] = ("Устанавливаем соединение", "Establishing connection"),
            ["home.title.protected"] = ("Вы под защитой", "You're protected"),
            ["home.title.unprotected"] = ("Нажмите для защиты", "Tap to get protected"),
            ["home.subtitle.protected"] = ("Ваш IP скрыт и зашифрован", "Your IP is hidden and encrypted"),
            ["home.subtitle.unprotected"] = ("Ваш IP открыт для всех", "Your IP is exposed"),
            ["home.subtitle.noOnlineServer"] = ("Выберите доступный сервер, чтобы подключиться", "Select an online server to connect"),
            ["home.section.currentServer"] = ("Текущий сервер", "Current server"),
            ["home.section.stats"] = ("Статистика соединения", "Connection stats"),
            ["home.stat.download"] = ("Загрузка", "Download"),
            ["home.stat.protocol"] = ("Протокол", "Protocol"),
            ["home.stat.encryption"] = ("Шифрование", "Encryption"),
            ["home.noServer.title"] = ("Сервер не выбран", "No server selected"),
            ["home.noServer.subtitle"] = ("Нажмите, чтобы выбрать локацию", "Tap to choose a location"),
            ["home.promo.title"] = ("Ускорьте соединение", "Speed up your connection"),
            ["home.promo.subtitle"] = ("Перейдите на PRO — выделенные сервера и максимальная скорость", "Upgrade to PRO — dedicated servers and maximum speed"),
            ["home.promo.button"] = ("Подробнее", "Learn more"),

            // ===== Серверы =====
            ["servers.title"] = ("Серверы", "Servers"),
            ["servers.subtitle"] = ("выберите подключение", "choose a connection"),
            ["servers.location.one"] = ("локация", "location"),
            ["servers.location.many"] = ("локаций", "locations"),
            ["servers.searchPlaceholder"] = ("Страна или город...", "Country or city..."),
            ["servers.filter.all"] = ("Все", "All"),
            ["servers.filter.online"] = ("🟢 Онлайн", "🟢 Online"),
            ["servers.loading"] = ("Загружаем список серверов…", "Loading servers…"),
            ["servers.empty.title"] = ("Ничего не найдено", "Nothing found"),
            ["servers.empty.subtitle"] = ("Попробуйте изменить запрос или фильтр", "Try a different search or filter"),
            ["servers.error.title"] = ("Не удалось загрузить сервера", "Couldn't load servers"),
            ["servers.error.subtitle"] = ("Проверьте подключение к интернету и попробуйте снова", "Check your internet connection and try again"),
            ["servers.error.retry"] = ("Повторить", "Retry"),
            ["servercard.online"] = ("Онлайн", "Online"),
            ["servercard.offline"] = ("Офлайн", "Offline"),

            // ===== Настройки =====
            ["settings.title"] = ("Настройки", "Settings"),
            ["settings.subtitle"] = ("Протокол, безопасность и персонализация", "Protocol, security and personalization"),
            ["settings.section.network"] = ("Протокол и сеть", "Protocol & network"),
            ["settings.section.security"] = ("Безопасность", "Security"),
            ["settings.section.appearance"] = ("Внешний вид", "Appearance"),
            ["settings.section.app"] = ("Приложение", "App"),
            ["settings.protocol.label"] = ("Протокол", "Protocol"),
            ["settings.protocol.sub"] = ("Метод подключения к серверу", "Server connection method"),
            ["settings.dns.label"] = ("DNS сервер", "DNS server"),
            ["settings.dns.sub"] = ("Используется во время подключения", "Used while connected"),
            ["settings.split.label"] = ("Разделение трафика", "Split tunneling"),
            ["settings.split.sub"] = ("Выбрать приложения вне VPN", "Choose apps to bypass the VPN"),
            ["settings.killswitch.label"] = ("Kill Switch", "Kill Switch"),
            ["settings.killswitch.sub"] = ("Блокирует интернет при разрыве VPN", "Blocks internet if the VPN drops"),
            ["settings.autoconnect.label"] = ("Автоподключение", "Auto-connect"),
            ["settings.autoconnect.sub"] = ("Включать VPN при запуске приложения", "Enable VPN when the app launches"),
            ["settings.blockads.label"] = ("Блокировка рекламы", "Ad blocking"),
            ["settings.blockads.sub"] = ("Фильтрация рекламы и трекеров", "Filters ads and trackers"),
            ["settings.theme.label"] = ("Тема", "Theme"),
            ["settings.theme.sub"] = ("Оформление приложения", "App appearance"),
            ["settings.theme.system"] = ("Системная", "System"),
            ["settings.theme.dark"] = ("Тёмная", "Dark"),
            ["settings.theme.light"] = ("Светлая", "Light"),
            ["settings.language.label"] = ("Язык", "Language"),
            ["settings.language.sub"] = ("Язык интерфейса", "Interface language"),
            ["settings.language.ru"] = ("Русский", "Russian"),
            ["settings.language.en"] = ("English", "English"),
            ["settings.clearCache.label"] = ("Очистить кэш", "Clear cache"),
            ["settings.checkUpdates.label"] = ("Проверить обновления", "Check for updates"),
            ["settings.cache.initial"] = ("12.4 МБ", "12.4 MB"),
            ["settings.cache.cleared"] = ("Очищено ✓", "Cleared ✓"),
            ["settings.cache.zero"] = ("0 МБ", "0 MB"),
            ["settings.update.initial"] = ("v1.0.0", "v1.0.0"),
            ["settings.update.checking"] = ("Проверка...", "Checking..."),
            ["settings.update.latest"] = ("Актуальная версия", "Up to date"),
            ["settings.logout"] = ("Выйти из аккаунта", "Log out"),

            // ===== Профиль =====
            ["profile.defaultUser"] = ("Пользователь", "User"),
            ["profile.noEmail"] = ("email не указан", "no email provided"),
            ["profile.validUntil"] = ("Действует до", "Valid until"),
            ["profile.stat.servers"] = ("серверов", "servers"),
            ["profile.stat.days"] = ("дней", "days"),
            ["profile.stat.protected"] = ("защищено", "protected"),
            ["profile.traffic.title"] = ("Трафик · 7 дней", "Traffic · 7 days"),
            ["profile.menu.subscription"] = ("Подписка", "Subscription"),
            ["profile.menu.subscription.sub"] = ("Pro · авто-продление", "Pro · auto-renew"),
            ["profile.menu.support"] = ("Поддержка", "Support"),
            ["profile.menu.support.sub"] = ("Помощь и FAQ", "Help & FAQ"),
            ["profile.menu.settings"] = ("Настройки", "Settings"),
            ["profile.menu.settings.sub"] = ("Протокол, DNS, Сплит-тоннель", "Protocol, DNS, split tunneling"),
            ["profile.notifications"] = ("Уведомления", "Notifications"),
            ["profile.notifications.on"] = ("Включены", "On"),
            ["profile.notifications.off"] = ("Выключены", "Off"),
            ["profile.killswitch"] = ("Kill Switch", "Kill Switch"),
            ["profile.killswitch.on"] = ("Активен", "Active"),
            ["profile.killswitch.off"] = ("Выключен", "Disabled"),
            ["profile.logout"] = ("Выйти из аккаунта", "Log out"),
            ["profile.day.mon"] = ("Пн", "Mon"),
            ["profile.day.tue"] = ("Вт", "Tue"),
            ["profile.day.wed"] = ("Ср", "Wed"),
            ["profile.day.thu"] = ("Чт", "Thu"),
            ["profile.day.fri"] = ("Пт", "Fri"),
            ["profile.day.sat"] = ("Сб", "Sat"),
            ["profile.day.sun"] = ("Вс", "Sun"),
            ["profile.day.mon.full"] = ("Понедельник", "Monday"),
            ["profile.day.tue.full"] = ("Вторник", "Tuesday"),
            ["profile.day.wed.full"] = ("Среда", "Wednesday"),
            ["profile.day.thu.full"] = ("Четверг", "Thursday"),
            ["profile.day.fri.full"] = ("Пятница", "Friday"),
            ["profile.day.sat.full"] = ("Суббота", "Saturday"),
            ["profile.day.sun.full"] = ("Воскресенье", "Sunday"),
            ["profile.traffic.download"] = ("Загрузка:", "Download:"),
            ["profile.traffic.upload"] = ("Отдача:", "Upload:"),
            ["profile.traffic.tapHint"] = ("Нажмите на день, чтобы посмотреть детали", "Tap a day to see details"),

            // ===== Поддержка =====
            ["support.title"] = ("Поддержка", "Support"),
            ["support.subtitle"] = ("Мы всегда на связи, если что-то пошло не так", "We're here whenever something goes wrong"),
            ["support.contact.title"] = ("Онлайн-чат поддержки", "Live support chat"),
            ["support.contact.sub"] = ("Отвечаем в течение 5 минут", "We reply within 5 minutes"),
            ["support.contact.button"] = ("Написать", "Message us"),
            ["support.faqTitle"] = ("Частые вопросы", "Frequently asked questions"),
            ["support.links.telegram"] = ("Telegram", "Telegram"),
            ["support.links.email"] = ("Email", "Email"),
            ["support.links.site"] = ("Сайт", "Website"),

            ["support.faq.q1"] = ("Почему упала скорость соединения?", "Why did my connection speed drop?"),
            ["support.faq.a1"] = ("Скорость зависит от загрузки выбранного сервера и качества вашего интернет-соединения. Попробуйте переключиться на ближайший сервер с меньшей нагрузкой в разделе «Серверы».", "Speed depends on the load of the selected server and your own internet connection. Try switching to a nearby, less busy server in the Servers tab."),
            ["support.faq.q2"] = ("Как включить Kill Switch?", "How do I enable Kill Switch?"),
            ["support.faq.a2"] = ("Перейдите в Настройки → Безопасность и включите переключатель «Kill Switch». Это заблокирует интернет-трафик при случайном разрыве VPN-соединения.", "Go to Settings → Security and turn on the \"Kill Switch\" toggle. This blocks internet traffic if the VPN connection drops unexpectedly."),
            ["support.faq.q3"] = ("Можно ли использовать на нескольких устройствах?", "Can I use this on multiple devices?"),
            ["support.faq.a3"] = ("Да, тариф PRO позволяет подключать до 5 устройств одновременно под одной учётной записью.", "Yes, the PRO plan lets you connect up to 5 devices at once under one account."),
            ["support.faq.q4"] = ("Как отменить подписку?", "How do I cancel my subscription?"),
            ["support.faq.a4"] = ("Откройте Профиль → Подписка и выберите «Отменить автопродление». Доступ сохранится до конца оплаченного периода.", "Open Profile → Subscription and choose \"Cancel auto-renewal\". Access remains until the end of the paid period."),
            ["support.faq.q5"] = ("Приложение не подключается", "The app won't connect"),
            ["support.faq.a5"] = ("Проверьте интернет-соединение, попробуйте другой сервер и протокол. Если проблема сохраняется, напишите нам в чат поддержки.", "Check your internet connection and try a different server or protocol. If the issue persists, message us in the support chat."),

            // ===== 404 =====
            ["notfound.title"] = ("Страница не найдена", "Page not found"),
            ["notfound.subtitle"] = ("Похоже, такой страницы не существует или она была перемещена.", "Looks like this page doesn't exist or has been moved."),
            ["notfound.button"] = ("На главную", "Go home"),

            // ===== Вход / регистрация =====
            ["auth.tagline"] = ("БЕЗОПАСНОСТЬ ЛИЧНЫХ ДАННЫХ ПРЕВЫШЕ ВСЕГО", "YOUR PRIVACY COMES FIRST"),
            ["auth.tab.login"] = ("Войти", "Log in"),
            ["auth.tab.register"] = ("Регистрация", "Sign up"),
            ["auth.name.placeholder"] = ("Ваше имя", "Your name"),
            ["auth.email.placeholder"] = ("Email адрес", "Email address"),
            ["auth.password.placeholder"] = ("Пароль", "Password"),
            ["auth.password.create.placeholder"] = ("Введите пароль", "Create a password"),
            ["auth.password.repeat.placeholder"] = ("Повторите пароль", "Repeat password"),
            ["auth.remember"] = ("Запомнить меня", "Remember me"),
            ["auth.forgot"] = ("Забыли пароль?", "Forgot password?"),
            ["auth.terms.prefix"] = ("Я принимаю", "I accept the"),
            ["auth.terms.tos"] = ("Условия использования", "Terms of Use"),
            ["auth.terms.and"] = ("и", "and"),
            ["auth.terms.privacy"] = ("Политику конфиденциальности", "Privacy Policy"),
            ["auth.submit.login"] = ("Войти", "Log in"),
            ["auth.submit.register"] = ("Создать аккаунт", "Create account"),
            ["auth.footer.noAccount"] = ("Нет аккаунта?", "Don't have an account?"),
            ["auth.footer.register"] = ("Зарегистрироваться", "Sign up"),
            ["auth.footer.hasAccount"] = ("Уже зарегистрированы?", "Already have an account?"),
            ["auth.footer.login"] = ("Войти", "Log in"),
            ["auth.error.emailPassword"] = ("Введите email и пароль", "Enter your email and password"),
            ["auth.error.fillAll"] = ("Заполните все поля", "Fill in all fields"),
            ["auth.error.passwordMismatch"] = ("Пароли не совпадают", "Passwords don't match"),
            ["auth.error.termsRequired"] = ("Необходимо принять условия использования", "You must accept the Terms of Use"),

            // ===== Security VPN: логин вместо email, ошибки аккаунта =====
            ["auth.login.placeholder"] = ("Логин", "Username"),
            ["auth.error.loginPassword"] = ("Введите логин и пароль", "Enter your username and password"),
            ["auth.error.invalid"] = ("Неверный логин или пароль", "Wrong username or password"),
            ["auth.error.exists"] = ("Такой логин уже занят", "This username is already taken"),
            ["auth.error.loginFormat"] = ("Логин: 3–30 символов, только латиница, цифры и _", "Username: 3–30 characters, letters, digits and _ only"),
            ["auth.error.passwordShort"] = ("Пароль должен быть не короче 8 символов", "Password must be at least 8 characters"),

            // ===== Единицы измерения =====
            ["unit.kbs"] = ("КБ/с", "KB/s"),
            ["unit.mbs"] = ("МБ/с", "MB/s"),
            ["unit.mb"] = ("МБ", "MB"),
            ["unit.gb"] = ("ГБ", "GB"),
            ["unit.ms"] = ("мс", "ms"),

            // ===== Главная / навигация / профиль: дополнения =====
            ["nav.plan"] = ("BETA-ДОСТУП", "BETA ACCESS"),
            ["home.stat.upload"] = ("Отдача", "Upload"),
            ["home.stat.time"] = ("Время", "Time"),
            ["profile.plan.sub"] = ("Локальный аккаунт", "Local account"),
            ["profile.badge"] = ("BETA", "BETA"),
            ["profile.since"] = ("Профиль создан", "Profile created"),
            ["profile.traffic.sample"] = ("Пример данных: реальная статистика по дням появится позже", "Sample data: real daily statistics are coming later"),

            // ===== Серверы: добавление своего сервера =====
            ["servers.ping.checking"] = ("Проверка…", "Checking…"),
            ["servers.delete.confirm"] = ("Удалить?", "Delete?"),
            ["servers.addHint.title"] = ("Добавьте свой сервер", "Add your own server"),
            ["servers.addHint.subtitle"] = ("Вставьте vless:// ссылку, которую выдал установщик на сервере. Серверы с пометкой DEMO только для проверки интерфейса.", "Paste the vless:// link printed by the server installer. Servers marked DEMO are only for previewing the interface."),
            ["servers.add.title"] = ("Новый сервер", "New server"),
            ["servers.add.sub"] = ("Вставьте ссылку vless:// (VLESS + Reality) — все параметры подключения возьмутся из неё.", "Paste a vless:// link (VLESS + Reality) — all connection parameters are taken from it."),
            ["servers.add.name.placeholder"] = ("Название (необязательно)", "Name (optional)"),
            ["servers.add.country.hint"] = ("Страна из двух букв (DE, NL, FI…) — для флага и положения на планете.", "Two-letter country code (DE, NL, FI…) — used for the flag and the position on the planet."),
            ["servers.add.cancel"] = ("Отмена", "Cancel"),
            ["servers.add.submit"] = ("Добавить", "Add"),
            ["servers.add.err.empty"] = ("Вставьте ссылку на сервер", "Paste the server link"),
            ["servers.add.err.scheme"] = ("Ссылка должна начинаться с vless://", "The link must start with vless://"),
            ["servers.add.err.format"] = ("Не удалось разобрать ссылку — проверьте, что она скопирована целиком", "Couldn't parse the link — make sure it was copied completely"),
            ["servers.add.err.uuid"] = ("В ссылке неверный UUID пользователя", "The link contains an invalid user UUID"),
            ["servers.add.err.host"] = ("В ссылке не указан адрес сервера", "The link has no server address"),
            ["servers.add.err.security"] = ("Поддерживается только security=reality", "Only security=reality is supported"),
            ["servers.add.err.network"] = ("Поддерживается только type=tcp", "Only type=tcp is supported"),
            ["servers.add.err.pbk"] = ("В ссылке нет публичного ключа (pbk)", "The link has no public key (pbk)"),
            ["servers.add.err.sni"] = ("В ссылке нет параметра sni", "The link has no sni parameter"),

            // ===== Подписка =====
            ["subscribe.title"] = ("Подписка", "Subscription"),
            ["subscribe.subtitle"] = ("Выберите тариф под свои задачи", "Choose a plan that fits you"),
            ["subscribe.current"] = ("Текущий", "Current"),
            ["subscribe.soon"] = ("Скоро", "Soon"),
            ["subscribe.button.soon"] = ("Скоро будет доступно", "Coming soon"),
            ["subscribe.plan.free"] = ("Бесплатный", "Free"),
            ["subscribe.plan.free.desc"] = ("Всё необходимое для безопасного соединения", "Everything you need for a secure connection"),
            ["subscribe.plan.free.f1"] = ("Свои серверы без ограничений", "Unlimited own servers"),
            ["subscribe.plan.free.f2"] = ("Протокол VLESS + Reality", "VLESS + Reality protocol"),
            ["subscribe.plan.free.f3"] = ("Windows и Android", "Windows and Android"),
            ["subscribe.plan.pro"] = ("Pro", "Pro"),
            ["subscribe.plan.pro.desc"] = ("Больше серверов и максимальная скорость", "More servers and maximum speed"),
            ["subscribe.plan.pro.f1"] = ("Больше серверов и локаций", "More servers and locations"),
            ["subscribe.plan.pro.f2"] = ("Приоритетная скорость", "Priority speed"),
            ["subscribe.plan.pro.f3"] = ("Несколько устройств", "Multiple devices"),
        };
    }
}
