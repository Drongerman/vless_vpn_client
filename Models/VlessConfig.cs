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
    /// Список доменов для прямого подключения (bypass proxy)
    /// Поддерживает:
    /// - Полные домены: vk.com, telegram.org
    /// - Шаблоны с точкой в начале: .ru (все домены в зоне .ru)
    /// - Регулярные выражения: regexp:.*\.ru$
    /// </summary>
    public List<string> BypassDomains { get; set; } = new()
    {
        // Российские домены
        "domain:.ru",
        "domain:.su", 
        "domain:.рф",
        "domain:.москва",
        "domain:.дети",
        
        // Популярные российские сервисы
        "domain:vk.com",
        "domain:vkontakte.ru",
        "domain:mail.ru",
        "domain:yandex.ru",
        "domain:yandex.com",
        "domain:ya.ru",
        "domain:ok.ru",
        "domain:sberbank.ru",
        "domain:gosuslugi.ru",
        
        // Telegram (если нужен прямой доступ)
        "domain:telegram.org",
        "domain:t.me",
        
        "domain:bongacams.com",
        // CDN/зеркала в других зонах (ru13.bongacams21.com — не поддомен bongacams.com)
        "domain:bongacams21.com",
        "regexp:.*\\.bongacams\\d+\\.com$",
        
        // Локальные адреса
        "geoip:private"
    };

    /// <summary>Включить обход прокси для указанных доменов</summary>
    public bool EnableBypass { get; set; } = true;

    /// <summary>Шифровать DNS через Cloudflare (DoH 1.1.1.1)</summary>
    public bool UseCloudflareDns { get; set; } = false;
}
