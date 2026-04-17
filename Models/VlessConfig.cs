/*
 * VlessConfig.cs - Модель конфигурации сервера VLESS
 * 
 * Это простой класс для хранения данных, не выполняет никаких действий.
 * Все поля соответствуют стандартной спецификации VLESS протокола.
 */

using Newtonsoft.Json;

namespace VlessVPN.Models;

/// <summary>
/// Конфигурация VLESS сервера
/// </summary>
public class VlessConfig
{
    /// <summary>Уникальный ID записи (для списка серверов)</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>Отображаемое имя сервера</summary>
    public string Name { get; set; } = "New Server";
    
    /// <summary>IP адрес или доменное имя сервера</summary>
    public string Address { get; set; } = string.Empty;
    
    /// <summary>Порт сервера (обычно 443)</summary>
    public int Port { get; set; } = 443;
    
    /// <summary>UUID пользователя (выдаётся сервером)</summary>
    public string UserId { get; set; } = string.Empty;
    
    /// <summary>Тип потока (xtls-rprx-vision для XTLS)</summary>
    public string Flow { get; set; } = "xtls-rprx-vision";
    
    /// <summary>Шифрование (обычно none, т.к. шифрование на уровне TLS)</summary>
    public string Encryption { get; set; } = "none";
    
    /// <summary>Тип сетевого транспорта: tcp, ws (WebSocket), grpc</summary>
    public string Network { get; set; } = "tcp";
    
    /// <summary>Тип безопасности: tls, reality, none</summary>
    public string Security { get; set; } = "tls";
    
    /// <summary>Server Name Indication - имя хоста для TLS</summary>
    public string Sni { get; set; } = string.Empty;
    
    /// <summary>TLS fingerprint - имитация браузера (chrome, firefox, safari)</summary>
    public string Fingerprint { get; set; } = "chrome";
    
    /// <summary>Application-Layer Protocol Negotiation</summary>
    public string Alpn { get; set; } = "h2,http/1.1";
    
    /// <summary>Разрешить невалидные TLS сертификаты (не рекомендуется)</summary>
    public bool AllowInsecure { get; set; } = false;
    
    // ===== Настройки WebSocket =====
    
    /// <summary>Путь для WebSocket подключения</summary>
    public string WsPath { get; set; } = "/";
    
    /// <summary>Заголовок Host для WebSocket</summary>
    public string WsHost { get; set; } = string.Empty;
    
    // ===== Настройки gRPC =====
    
    /// <summary>Имя сервиса gRPC</summary>
    public string GrpcServiceName { get; set; } = string.Empty;
    
    // ===== Настройки Reality =====
    
    /// <summary>Публичный ключ сервера Reality</summary>
    public string PublicKey { get; set; } = string.Empty;
    
    /// <summary>Короткий ID для Reality</summary>
    public string ShortId { get; set; } = string.Empty;
    
    /// <summary>SpiderX параметр для Reality</summary>
    public string SpiderX { get; set; } = string.Empty;

    // ===== Метаданные =====
    
    /// <summary>Дата добавления сервера</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    /// <summary>Дата последнего подключения</summary>
    public DateTime LastConnected { get; set; }
    
    /// <summary>Если сервер загружен из URL подписки — сам URL (для обновления списка)</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? SourceSubscriptionUrl { get; set; }
    
    /// <summary>Возвращает адрес в формате "host:port"</summary>
    public string GetDisplayAddress() => $"{Address}:{Port}";
}

/// <summary>
/// Общие настройки приложения
/// Сохраняются в %AppData%\VlessVPN\settings.json
/// </summary>
public class AppSettings
{
    /// <summary>Порт локального SOCKS5 прокси (127.0.0.1:LocalPort)</summary>
    public int LocalPort { get; set; } = 10808;
    
    /// <summary>Порт локального HTTP прокси (127.0.0.1:HttpPort)</summary>
    public int HttpPort { get; set; } = 10809;
    
    /// <summary>Устанавливать системный прокси при подключении</summary>
    public bool EnableSystemProxy { get; set; } = true;
    
    /// <summary>Запускать приложение свёрнутым</summary>
    public bool StartMinimized { get; set; } = false;
    
    /// <summary>Автоподключение при запуске</summary>
    public bool AutoConnect { get; set; } = false;
    
    /// <summary>ID последнего подключённого сервера</summary>
    public string? LastConnectedServerId { get; set; }
    
    /// <summary>Последний URL подписки (импорт/обновление списка серверов)</summary>
    public string? LastSubscriptionUrl { get; set; }
    
    /// <summary>Сворачивать в трей при закрытии</summary>
    public bool MinimizeToTray { get; set; } = true;
    
    /// <summary>Список сохранённых серверов</summary>
    public List<VlessConfig> Servers { get; set; } = new();

    /// <summary>
    /// Список доменов, которые ВСЕГДА идут через VPN (даже если попадают под bypass).
    /// Правила проверяются ДО bypass-списка — первое совпадение побеждает.
    /// Это решает проблему: domain:.ru ловит google.ru, geoip:ru ловит Google CDN в России.
    /// </summary>
    public List<string> ForceProxyDomains { get; set; } = new()
    {
        // ===== Google / YouTube =====
        "domain:google.com",
        "domain:google.ru",
        "domain:google.co.uk",
        "domain:google.de",
        "domain:googleapis.com",
        "domain:googlevideo.com",
        "domain:googleusercontent.com",
        "domain:googleadservices.com",
        "domain:googlesyndication.com",
        "domain:googletagmanager.com",
        "domain:google-analytics.com",
        "domain:gstatic.com",
        "domain:ggpht.com",
        "domain:youtube.com",
        "domain:youtube.ru",
        "domain:youtu.be",
        "domain:ytimg.com",
        "domain:youtube-nocookie.com",
        "domain:youtube-ui.l.google.com",

        // ===== Google AI / Gemini =====
        "domain:gemini.google.com",
        "domain:aistudio.google.com",
        "domain:generativelanguage.googleapis.com",
        "domain:alkalimakersuite-pa.clients6.google.com",
        "domain:makersuite.google.com",
        "domain:ai.google.dev",
        "domain:deepmind.google",
        "domain:deepmind.com",

        // ===== Anthropic / Claude =====
        "domain:anthropic.com",
        "domain:claude.ai",
        "domain:claude.com",

        // ===== OpenAI / ChatGPT =====
        "domain:openai.com",
        "domain:chatgpt.com",
        "domain:oaiusercontent.com",
        "domain:oaistatic.com",

        // ===== Meta / Instagram / Facebook =====
        "domain:instagram.com",
        "domain:cdninstagram.com",
        "domain:facebook.com",
        "domain:facebook.net",
        "domain:fbcdn.net",
        "domain:fb.com",
        "domain:fb.me",
        "domain:threads.net",
        "domain:whatsapp.com",
        "domain:whatsapp.net",

        // ===== X (Twitter) =====
        "domain:twitter.com",
        "domain:x.com",
        "domain:twimg.com",
        "domain:t.co",

        // ===== Spotify =====
        "domain:spotify.com",
        "domain:spotifycdn.com",
        "domain:scdn.co",

        // ===== Discord =====
        "domain:discord.com",
        "domain:discord.gg",
        "domain:discordapp.com",
        "domain:discordapp.net",
        "domain:discord.media",

        // ===== GitHub / Dev =====
        "domain:github.com",
        "domain:github.io",
        "domain:githubusercontent.com",
        "domain:githubassets.com",
        "domain:npmjs.com",
        "domain:npmjs.org",

        // ===== Netflix =====
        "domain:netflix.com",
        "domain:nflxvideo.net",
        "domain:nflximg.net",
        "domain:nflxext.com",

        // ===== Другие заблокированные / нужные через VPN =====
        "domain:medium.com",
        "domain:notion.so",
        "domain:notion.site",
        "domain:linkedin.com",
        "domain:licdn.com",
        "domain:soundcloud.com",
        "domain:twitch.tv",
        "domain:jtvnw.net",
        "domain:twitchcdn.net",
        "domain:pinterest.com",
        "domain:pinimg.com",
        "domain:quora.com",
        "domain:reddit.com",
        "domain:redd.it",
        "domain:redditstatic.com",
        "domain:redditmedia.com",
    };

    /// <summary>Включить принудительный VPN для указанных доменов</summary>
    public bool EnableForceProxy { get; set; } = true;

    /// <summary>
    /// Список доменов для прямого подключения (bypass proxy)
    /// Поддерживает:
    /// - Полные домены: vk.com, telegram.org
    /// - Шаблоны с точкой в начале: .ru (все домены в зоне .ru)
    /// - Регулярные выражения: regexp:.*\.ru$
    /// </summary>
    public List<string> BypassDomains { get; set; } = new()
    {
        // ===== Российские TLD =====
        "domain:.ru",
        "domain:.su",
        "domain:.рф",
        "domain:.москва",
        "domain:.дети",
        "domain:.tatar",

        // ===== GeoIP: весь российский трафик напрямую =====
        "geoip:ru",

        // ===== Яндекс (все сервисы, включая .com домены) =====
        "domain:yandex.ru",
        "domain:yandex.com",
        "domain:yandex.net",
        "domain:yandex.by",
        "domain:yandex.kz",
        "domain:yandex.uz",
        "domain:ya.ru",
        "domain:yastatic.net",
        "domain:yandexcloud.net",
        "domain:yandex-team.ru",
        "domain:yx.tld",

        // ===== VK / Mail.ru Group =====
        "domain:vk.com",
        "domain:vk.me",
        "domain:vk.cc",
        "domain:vkontakte.ru",
        "domain:vkuser.net",
        "domain:vkuseraudio.net",
        "domain:vkuservideo.net",
        "domain:userapi.com",
        "domain:mail.ru",
        "domain:mymail.ru",
        "domain:mradx.net",
        "domain:imgsmail.ru",
        "domain:ok.ru",
        "domain:odkl.ru",
        "domain:okcdn.ru",
        "domain:mycdn.me",
        "domain:dzen.ru",

        // ===== Telegram =====
        "domain:telegram.org",
        "domain:t.me",
        "domain:telegram.me",
        "domain:telegra.ph",
        "domain:tdesktop.com",
        "domain:telesco.pe",

        // ===== Госсервисы =====
        "domain:gosuslugi.ru",
        "domain:mos.ru",
        "domain:government.ru",
        "domain:kremlin.ru",
        "domain:nalog.ru",
        "domain:pfr.gov.ru",
        "domain:esia.gosuslugi.ru",

        // ===== Банки =====
        "domain:sberbank.ru",
        "domain:sber.ru",
        "domain:online.sberbank.ru",
        "domain:tinkoff.ru",
        "domain:tbank.ru",
        "domain:vtb.ru",
        "domain:alfabank.ru",
        "domain:raiffeisen.ru",
        "domain:gazprombank.ru",
        "domain:open.ru",
        "domain:rshb.ru",
        "domain:psbank.ru",
        "domain:sovcombank.ru",
        "domain:unicreditbank.ru",
        "domain:rosbank.ru",
        "domain:mkb.ru",
        "domain:ozon.ru",

        // ===== Маркетплейсы и ритейл =====
        "domain:wildberries.ru",
        "domain:wb.ru",
        "domain:wbstatic.net",
        "domain:ozon.ru",
        "domain:ozoncdn.com",
        "domain:ozontech.ru",
        "domain:dns-shop.ru",
        "domain:mvideo.ru",
        "domain:eldorado.ru",
        "domain:citilink.ru",
        "domain:lamoda.ru",
        "domain:avito.ru",
        "domain:youla.ru",
        "domain:aliexpress.ru",

        // ===== Стриминг и медиа (РФ) =====
        "domain:kinopoisk.ru",
        "domain:ivi.ru",
        "domain:okko.tv",
        "domain:more.tv",
        "domain:wink.ru",
        "domain:premier.one",
        "domain:start.ru",
        "domain:amediateka.ru",
        "domain:rutube.ru",

        // ===== Доставка и такси =====
        "domain:delivery-club.ru",
        "domain:sbermarket.ru",
        "domain:samokat.ru",
        "domain:lavka.yandex.ru",

        // ===== Прочие российские сервисы =====
        "domain:2gis.ru",
        "domain:2gis.com",
        "domain:hh.ru",
        "domain:habr.com",
        "domain:pikabu.ru",
        "domain:rbc.ru",
        "domain:lenta.ru",
        "domain:ria.ru",
        "domain:tass.ru",
        "domain:kommersant.ru",
        "domain:vedomosti.ru",
        "domain:sports.ru",
        "domain:championat.com",

        // ===== Контент для взрослых =====
        "domain:bongacams.com",
        "domain:bongacams21.com",
        "regexp:.*\\.bongacams\\d+\\.com$",

        // ===== Локальные/приватные адреса =====
        "geoip:private"
    };

    /// <summary>Включить обход прокси для указанных доменов</summary>
    public bool EnableBypass { get; set; } = true;

    /// <summary>Шифровать DNS через Cloudflare (DoH 1.1.1.1)</summary>
    public bool UseCloudflareDns { get; set; } = true;

    // ===== TLS Fragment (Anti-DPI) =====

    /// <summary>Включить фрагментацию TLS ClientHello (обход DPI)</summary>
    public bool EnableTlsFragment { get; set; } = false;

    /// <summary>Диапазон длины фрагментов (например "100-200")</summary>
    public string TlsFragmentLength { get; set; } = "100-200";

    /// <summary>Диапазон интервала между фрагментами в мс (например "10-20")</summary>
    public string TlsFragmentInterval { get; set; } = "10-20";

    // ===== WARP (Cloudflare WireGuard) =====

    /// <summary>Включить цепочку через WARP (трафик идёт: VLESS сервер → WARP → Интернет)</summary>
    public bool EnableWarp { get; set; } = false;

    /// <summary>WireGuard private key для WARP</summary>
    public string WarpPrivateKey { get; set; } = string.Empty;

    /// <summary>WARP IPv4 адрес (например "172.16.0.2/32")</summary>
    public string WarpAddressV4 { get; set; } = "172.16.0.2/32";

    /// <summary>WARP IPv6 адрес</summary>
    public string WarpAddressV6 { get; set; } = string.Empty;

    /// <summary>WARP reserved bytes (3 числа через запятую, например "0,0,0")</summary>
    public string WarpReserved { get; set; } = string.Empty;

    /// <summary>WARP endpoint (по умолчанию: engage.cloudflareclient.com:2408)</summary>
    public string WarpEndpoint { get; set; } = "engage.cloudflareclient.com:2408";
}
