# Security VPN

«Безопасность личных данных превыше всего». Клиент VPN на .NET MAUI Blazor Hybrid (Windows, Android; iOS позже).
Протокол: VLESS + Reality (Xray-core). Пурпурная тема, планета за кнопкой питания, плавные анимации.

## Запуск на Windows
1. Visual Studio 2022 (последняя) с рабочей нагрузкой «.NET Multi-platform App UI», .NET SDK 10.
2. Скачайте ядро: `powershell -ExecutionPolicy Bypass -File .\tools\get-xray.ps1` (xray.exe попадёт в `SecurityVpn\Resources\Raw`).
3. Скопируйте `Emoji-fonts.ttf` в `SecurityVpn\wwwroot\fonts\` (см. README.txt там).
4. `dotnet restore`, затем `dotnet build SecurityVpn -f net10.0-windows10.0.19041.0` или F5 в Visual Studio.

Без своего сервера работают 5 демо-серверов (имитация подключения и трафика).

## Свой сервер (Aeza, Ubuntu 24.04)
```
scp server/install-xray.sh root@IP:/root/
ssh root@IP "bash /root/install-xray.sh"
```
Скрипт поставит Xray, включит VLESS+Reality на 443 и выведет ссылку `vless://...`.
В приложении: Серверы → «+» → вставить ссылку. Ещё устройство: `bash install-xray.sh add phone`.

## Android
Нужен ваш проект `LibXrayBinding` рядом с `SecurityVpn` (`..\LibXrayBinding\LibXrayBinding.csproj`); без него сборка идёт, но VPN на Android не поднимется.

## Ограничения
- Windows работает через системный прокси (не TUN); восстанавливается при выходе.
- Kill Switch, раздельное туннелирование, блок рекламы — пока только переключатели интерфейса.
- График трафика — пример; тариф Pro — заглушка.
- Аккаунты локальные (до появления серверного API).
- Платформенный код (Windows/Android) и внешний вид в этой среде не компилировались; конфиг Xray проверен настоящим xray, сквозной тест клиент→сервер пройден.
