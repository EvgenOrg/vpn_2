#!/usr/bin/env bash
#
# Security VPN: сервер на чистом Ubuntu 24.04 / Debian 12 (Aeza и любой другой VPS).
#
#   sudo bash install-xray.sh            установить / проверить и показать ссылку для приложения
#   sudo bash install-xray.sh add NAME   добавить ещё одного пользователя (отдельная ссылка на каждое устройство)
#   sudo bash install-xray.sh links      показать все ссылки
#
# Ставит Xray и включает VLESS + Reality на порту 443. Никаких баз данных и веб-серверов не нужно.
# Скрипт можно запускать повторно: ключи и пользователи не пересоздаются.
#
# Необязательные переменные окружения:
#   SERVER_HOST=...   адрес сервера (по умолчанию определяется автоматически)
#   REALITY_SNI=...   сайт для маскировки Reality (по умолчанию www.microsoft.com)
#   XRAY_PORT=443     порт VPN
#   SERVER_NAME=...   название сервера в приложении (по умолчанию Security VPN)

set -euo pipefail

XRAY_BIN="${XRAY_BIN:-/usr/local/bin/xray}"
XRAY_CONF="${XRAY_CONF:-/usr/local/etc/xray/config.json}"
STATE_DIR="${STATE_DIR:-/etc/securityvpn}"
XRAY_PORT="${XRAY_PORT:-443}"
REALITY_SNI="${REALITY_SNI:-www.microsoft.com}"
SERVER_NAME="${SERVER_NAME:-Security VPN}"
SKIP_SYSTEM="${SKIP_SYSTEM:-0}"   # 1 — не трогать пакеты, systemd и файрвол (для проверки скрипта)

say()  { printf '\n\033[1;35m==>\033[0m \033[1m%s\033[0m\n' "$*"; }
warn() { printf '\033[1;33m!\033[0m %s\n' "$*" >&2; }
die()  { printf '\033[1;31mОшибка:\033[0m %s\n' "$*" >&2; exit 1; }

if [[ "$SKIP_SYSTEM" != "1" && $EUID -ne 0 ]]; then
    die "Запустите от root:  sudo bash install-xray.sh"
fi

STATE_FILE="$STATE_DIR/server.json"

# Ссылка для приложения: vless://UUID@host:port?...#Название
make_link() {
    local uuid="$1" label="$2"
    local host port sni pbk sid
    host="$(jq -r .host "$STATE_FILE")"
    port="$(jq -r .port "$STATE_FILE")"
    sni="$(jq -r .sni "$STATE_FILE")"
    pbk="$(jq -r .publicKey "$STATE_FILE")"
    sid="$(jq -r .shortId "$STATE_FILE")"
    local name
    name="$(jq -rn --arg n "$SERVER_NAME" '$n | @uri')"
    printf 'vless://%s@%s:%s?encryption=none&flow=xtls-rprx-vision&security=reality&sni=%s&fp=chrome&pbk=%s&sid=%s&type=tcp#%s\n' \
        "$uuid" "$host" "$port" "$sni" "$pbk" "$sid" "$name"
}

print_links() {
    local count
    count="$(jq '.inbounds[0].settings.clients | length' "$XRAY_CONF")"
    echo
    for ((i = 0; i < count; i++)); do
        local uuid label
        uuid="$(jq -r ".inbounds[0].settings.clients[$i].id" "$XRAY_CONF")"
        label="$(jq -r ".inbounds[0].settings.clients[$i].email // \"user$i\"" "$XRAY_CONF")"
        printf '\033[1m%s\033[0m\n' "$label"
        make_link "$uuid" "$label"
        echo
    done
    echo "Вставьте ссылку в приложении: Серверы → «+»."
}

# Записать конфиг атомарно: проверить Xray'ем и только потом заменить рабочий
write_config() {
    local json="$1" tmp="${XRAY_CONF%.json}.new.json"
    mkdir -p "$(dirname "$XRAY_CONF")"
    umask 077
    printf '%s\n' "$json" > "$tmp"
    "$XRAY_BIN" run -test -config "$tmp" >/dev/null 2>&1 || { "$XRAY_BIN" run -test -config "$tmp" || true; rm -f "$tmp"; die "Xray не принял конфиг, оставляю прежний."; }

    if [[ "$SKIP_SYSTEM" != "1" ]]; then
        # Xray работает от непривилегированного пользователя — даём право читать конфиг только ему
        local user group
        user="$(systemctl show -p User --value xray 2>/dev/null || true)"
        if [[ -n "$user" && "$user" != "root" ]]; then
            group="$(id -gn "$user" 2>/dev/null || true)"
            if [[ -n "$group" ]]; then chgrp "$group" "$tmp"; chmod 0640 "$tmp"; fi
        fi
    fi
    mv "$tmp" "$XRAY_CONF"
    if [[ "$SKIP_SYSTEM" != "1" ]]; then
        systemctl enable xray >/dev/null 2>&1 || true
        systemctl restart xray
    fi
}

# ---------------------------------------------------------------- подкоманды, не требующие установки
cmd="${1:-install}"
case "$cmd" in
    links)
        [[ -f "$XRAY_CONF" && -f "$STATE_FILE" ]] || die "Сервер ещё не установлен: запустите  sudo bash install-xray.sh"
        print_links; exit 0 ;;
    add)
        NAME="${2:-}"
        [[ "$NAME" =~ ^[A-Za-z0-9_-]{1,30}$ ]] || die "Укажите имя пользователя (латиница, цифры, - и _):  sudo bash install-xray.sh add phone"
        [[ -f "$XRAY_CONF" && -f "$STATE_FILE" ]] || die "Сервер ещё не установлен: запустите  sudo bash install-xray.sh"
        jq -e --arg n "$NAME" '.inbounds[0].settings.clients[] | select(.email == $n)' "$XRAY_CONF" >/dev/null 2>&1 \
            && die "Пользователь $NAME уже есть. Ссылки: sudo bash install-xray.sh links"
        UUID="$("$XRAY_BIN" uuid)"
        write_config "$(jq --arg id "$UUID" --arg n "$NAME" \
            '.inbounds[0].settings.clients += [{id: $id, flow: "xtls-rprx-vision", email: $n}]' "$XRAY_CONF")"
        say "Пользователь $NAME добавлен"
        make_link "$UUID" "$NAME"
        exit 0 ;;
    install) ;;
    *) die "Неизвестная команда: $cmd (доступно: install, add NAME, links)" ;;
esac

# ---------------------------------------------------------------- установка
if [[ "$SKIP_SYSTEM" != "1" ]]; then
    . /etc/os-release
    case "${ID:-}" in
        ubuntu|debian) ;;
        *) warn "Скрипт проверялся на Ubuntu 24.04 и Debian 12, у вас: ${PRETTY_NAME:-неизвестно}" ;;
    esac

    say "Устанавливаю пакеты (curl, jq, openssl)"
    export DEBIAN_FRONTEND=noninteractive
    apt-get update -y
    apt-get install -y curl jq openssl ca-certificates

    if [[ ! -x "$XRAY_BIN" ]]; then
        say "Устанавливаю Xray (официальный установщик XTLS/Xray-install)"
        bash -c "$(curl -fsSL https://github.com/XTLS/Xray-install/raw/main/install-release.sh)" @ install
    fi
fi
[[ -x "$XRAY_BIN" ]] || die "Xray не установился. Проверьте, что сервер видит github.com."

mkdir -p "$STATE_DIR"

if [[ ! -f "$STATE_FILE" || ! -f "$XRAY_CONF" ]]; then
    # --- адрес сервера
    if [[ -n "${SERVER_HOST:-}" ]]; then
        HOST="$SERVER_HOST"
    else
        HOST="$(curl -fsS4 --max-time 8 https://api.ipify.org 2>/dev/null || true)"
        [[ -n "$HOST" ]] || HOST="$(hostname -I 2>/dev/null | awk '{print $1}')"
    fi
    [[ -n "$HOST" ]] || die "Не удалось определить адрес сервера. Задайте его: SERVER_HOST=1.2.3.4 sudo -E bash install-xray.sh"

    # --- ключи Reality
    say "Генерирую ключи Reality"
    KEYS="$("$XRAY_BIN" x25519)"
    # Версии Xray печатают по-разному: "Public key", "Password" или "Password (PublicKey)"; приватный ключ — "Private key"/"PrivateKey"
    PRIV="$(awk -F': *' 'tolower($1) ~ /^private ?key$/ {print $2}' <<<"$KEYS" | tr -d '[:space:]')"
    PUB="$(awk -F': *' 'tolower($1) ~ /(public|password)/ {print $2}' <<<"$KEYS" | head -n1 | tr -d '[:space:]')"
    [[ -n "$PRIV" && -n "$PUB" ]] || die "Не смог разобрать вывод 'xray x25519':
$KEYS"
    SHORT_ID="$(openssl rand -hex 8)"
    UUID="$("$XRAY_BIN" uuid)"

    umask 077
    jq -n --arg host "$HOST" --argjson port "$XRAY_PORT" --arg sni "$REALITY_SNI" --arg pub "$PUB" --arg sid "$SHORT_ID" \
        '{host: $host, port: $port, sni: $sni, publicKey: $pub, shortId: $sid}' > "$STATE_FILE"
    umask 022

    say "Записываю конфиг Xray и запускаю сервис"
    write_config "$(jq -n --arg priv "$PRIV" --arg uuid "$UUID" --arg sid "$SHORT_ID" \
        --arg sni "$REALITY_SNI" --argjson port "$XRAY_PORT" '
        {
          log: {loglevel: "warning"},
          inbounds: [{
            tag: "vless-reality", listen: "0.0.0.0", port: $port, protocol: "vless",
            settings: {clients: [{id: $uuid, flow: "xtls-rprx-vision", email: "main"}], decryption: "none"},
            streamSettings: {
              network: "tcp", security: "reality",
              realitySettings: {show: false, dest: ($sni + ":443"), xver: 0, serverNames: [$sni], privateKey: $priv, shortIds: [$sid]}
            },
            sniffing: {enabled: true, destOverride: ["http", "tls", "quic"]}
          }],
          outbounds: [{protocol: "freedom", tag: "direct"}, {protocol: "blackhole", tag: "block"}]
        }')"
fi

# ---------------------------------------------------------------- файрвол и итог
if [[ "$SKIP_SYSTEM" != "1" ]]; then
    if command -v ufw >/dev/null 2>&1 && ufw status 2>/dev/null | grep -q "Status: active"; then
        say "Открываю порт $(jq -r .port "$STATE_FILE")/tcp в ufw"
        ufw allow "$(jq -r .port "$STATE_FILE")/tcp" >/dev/null
    fi

    systemctl is-active --quiet xray && echo "Xray: работает" || warn "Xray не запущен (journalctl -u xray -n 30)"
fi

say "Готово. Ссылки для приложения:"
print_links
