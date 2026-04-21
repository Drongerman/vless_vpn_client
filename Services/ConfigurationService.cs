/*
 * ConfigurationService.cs - Сервис сохранения и загрузки настроек
 * 
 * БЕЗОПАСНОСТЬ:
 * - Сохраняет данные ТОЛЬКО в папку пользователя (%AppData%\VlessVPN\)
 * - Не требует прав администратора
 * - Не отправляет данные в интернет
 * - Файл settings.json можно открыть и проверить вручную
 * 
 * ФОРМАТ ХРАНЕНИЯ:
 * - JSON файл с настройками и списком серверов
 * - UUID серверов хранятся в открытом виде (шифрование не требуется, 
 *   т.к. UUID бесполезен без доступа к серверу)
 */

using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using VlessVPN.Models;

namespace VlessVPN.Services;

public class ConfigurationService
{
    // Путь к файлу настроек: %AppData%\VlessVPN\settings.json
    private readonly string _configPath;
    
    // Текущие настройки (загружаются при старте)
    private AppSettings _settings;

    public ConfigurationService()
    {
        // Получаем путь к папке AppData пользователя
        // Например: C:\Users\ИмяПользователя\AppData\Roaming\VlessVPN
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configDir = Path.Combine(appData, "VlessVPN");
        
        // Создаём папку если её нет
        Directory.CreateDirectory(configDir);
        
        _configPath = Path.Combine(configDir, "settings.json");
        
        // Загружаем существующие настройки или создаём новые
        _settings = LoadSettings();
    }

    /// <summary>
    /// Текущие настройки приложения
    /// </summary>
    public AppSettings Settings => _settings;

    /// <summary>
    /// Загружает настройки из JSON файла
    /// </summary>
    private AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var loaded = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                EnsureDefaultBypassRules(loaded);
                EnsureDefaultForceProxyRules(loaded);
                return loaded;
            }
        }
        catch (Exception ex)
        {
            // Если файл повреждён - создаём новые настройки
            System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
        }
        return new AppSettings();
    }

    /// <summary>
    /// Добавляет новые правила обхода по умолчанию в уже сохранённые настройки (однократно после обновления приложения).
    /// </summary>
    private void EnsureDefaultBypassRules(AppSettings settings)
    {
        // Дополняем список правилами, которые появились в новых версиях.
        // Не заменяем пользовательские правила — только добавляем отсутствующие.
        var defaults = new[]
        {
            // Geosite-категории RU (Loyalsoldier)
            "geosite:category-ru",
            "geosite:category-gov-ru",
            "geosite:category-media-ru",
            "geosite:yandex",
            "geosite:vk",
            "geosite:mail-ru",
            "geosite:telegram",
            // GeoIP — весь российский трафик напрямую
            "geoip:ru",
            // Яндекс (.com/.net домены, которые не ловятся через domain:.ru)
            "domain:yandex.com", "domain:yandex.net", "domain:yandex.by",
            "domain:yandex.kz", "domain:yandex.uz", "domain:yastatic.net",
            "domain:yandexcloud.net", "domain:yx.tld",
            // VK / Mail.ru (.com/.net/.me домены)
            "domain:vk.com", "domain:vk.me", "domain:vk.cc",
            "domain:vkuser.net", "domain:vkuseraudio.net", "domain:vkuservideo.net",
            "domain:userapi.com", "domain:mymail.ru", "domain:mradx.net",
            "domain:imgsmail.ru", "domain:odkl.ru", "domain:okcdn.ru", "domain:mycdn.me",
            "domain:dzen.ru",
            // Telegram
            "domain:telegram.me", "domain:telegra.ph", "domain:tdesktop.com", "domain:telesco.pe",
            // Банки (новые .ru уже ловятся, но для надёжности)
            "domain:sber.ru", "domain:tbank.ru",
            // Маркетплейсы (.com/.net домены)
            "domain:wbstatic.net", "domain:ozoncdn.com", "domain:ozontech.ru",
            // 2GIS
            "domain:2gis.com",
            // Хабр, Championat
            "domain:habr.com", "domain:championat.com",
            // Контент для взрослых
            "domain:bongacams.com", "domain:bongacams21.com",
            "regexp:.*\\.bongacams\\d+\\.com$",
            // Стриминг
            "domain:okko.tv", "domain:premier.one",
            // Прочие .tatar
            "domain:.tatar"
        };
        var added = false;
        foreach (var rule in defaults)
        {
            if (settings.BypassDomains.Any(d => string.Equals(d.Trim(), rule, StringComparison.OrdinalIgnoreCase)))
                continue;
            settings.BypassDomains.Add(rule);
            added = true;
        }

        if (!added)
            return;

        try
        {
            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to migrate bypass rules: {ex.Message}");
        }
    }

    /// <summary>
    /// Добавляет force-proxy правила для существующих пользователей (Google, YouTube, Instagram, Claude и т.д.).
    /// Без этого domain:.ru ловит google.ru, а geoip:ru ловит Google CDN с российскими IP.
    /// </summary>
    private void EnsureDefaultForceProxyRules(AppSettings settings)
    {
        var defaults = new[]
        {
            // Geosite-категории (Loyalsoldier) — западные сервисы целиком
            "geosite:google", "geosite:youtube", "geosite:apple", "geosite:microsoft",
            "geosite:facebook", "geosite:instagram", "geosite:anthropic",
            "geosite:openai", "geosite:twitter", "geosite:discord",
            "geosite:github", "geosite:netflix", "geosite:spotify",
            // Google / YouTube
            "domain:google.com", "domain:google.ru", "domain:googleapis.com",
            "domain:googlevideo.com", "domain:googleusercontent.com", "domain:gstatic.com",
            "domain:ggpht.com", "domain:youtube.com", "domain:youtube.ru",
            "domain:youtu.be", "domain:ytimg.com",
            // Google AI / Gemini
            "domain:gemini.google.com", "domain:aistudio.google.com",
            "domain:generativelanguage.googleapis.com", "domain:ai.google.dev",
            // Claude / Anthropic
            "domain:anthropic.com", "domain:claude.ai",
            // OpenAI
            "domain:openai.com", "domain:chatgpt.com",
            // Meta / Instagram
            "domain:instagram.com", "domain:cdninstagram.com",
            "domain:facebook.com", "domain:fbcdn.net",
            // Discord
            "domain:discord.com", "domain:discordapp.com",
            // GitHub
            "domain:github.com", "domain:githubusercontent.com",
        };

        var added = false;
        foreach (var rule in defaults)
        {
            if (settings.ForceProxyDomains.Any(d => string.Equals(d.Trim(), rule, StringComparison.OrdinalIgnoreCase)))
                continue;
            settings.ForceProxyDomains.Add(rule);
            added = true;
        }

        if (!added)
            return;

        try
        {
            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to migrate force-proxy rules: {ex.Message}");
        }
    }

    /// <summary>
    /// Сохраняет настройки в JSON файл
    /// </summary>
    public void SaveSettings()
    {
        try
        {
            var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Добавляет новый сервер в список
    /// </summary>
    public void AddServer(VlessConfig server)
    {
        _settings.Servers.Add(server);
        SaveSettings();
    }

    /// <summary>
    /// Обновляет существующий сервер
    /// </summary>
    public void UpdateServer(VlessConfig server)
    {
        var index = _settings.Servers.FindIndex(s => s.Id == server.Id);
        if (index >= 0)
        {
            _settings.Servers[index] = server;
            SaveSettings();
        }
    }

    /// <summary>
    /// Удаляет сервер из списка
    /// </summary>
    public void RemoveServer(string serverId)
    {
        _settings.Servers.RemoveAll(s => s.Id == serverId);
        SaveSettings();
    }

    /// <summary>
    /// Получает сервер по ID
    /// </summary>
    public VlessConfig? GetServer(string serverId)
    {
        return _settings.Servers.FirstOrDefault(s => s.Id == serverId);
    }

    /// <summary>
    /// Парсит VLESS URI и создаёт конфигурацию сервера
    /// 
    /// Формат URI: vless://uuid@address:port?type=tcp&security=tls&sni=example.com#ServerName
    /// 
    /// Это стандартный формат, используемый всеми VLESS клиентами:
    /// - V2rayN, V2rayNG, Nekoray, Qv2ray и т.д.
    /// </summary>
    public VlessConfig? ParseVlessUri(string uri)
    {
        try
        {
            // Проверяем что это VLESS ссылка
            if (!uri.StartsWith("vless://", StringComparison.OrdinalIgnoreCase))
                return null;

            var config = new VlessConfig();
            
            // Убираем схему vless://
            var withoutScheme = uri.Substring(8);
            
            // Извлекаем имя сервера (после #)
            // Пример: ...#My%20Server -> "My Server"
            var nameIndex = withoutScheme.LastIndexOf('#');
            if (nameIndex > 0)
            {
                config.Name = Uri.UnescapeDataString(withoutScheme.Substring(nameIndex + 1));
                withoutScheme = withoutScheme.Substring(0, nameIndex);
            }

            // Извлекаем параметры (после ?)
            // Пример: ...?type=tcp&security=tls
            var paramsIndex = withoutScheme.IndexOf('?');
            var queryParams = new Dictionary<string, string>();
            if (paramsIndex > 0)
            {
                var paramsStr = withoutScheme.Substring(paramsIndex + 1);
                withoutScheme = withoutScheme.Substring(0, paramsIndex);
                
                // Парсим каждый параметр key=value
                foreach (var param in paramsStr.Split('&'))
                {
                    var parts = param.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        queryParams[parts[0].ToLower()] = Uri.UnescapeDataString(parts[1]);
                    }
                }
            }

            // Парсим uuid@address:port
            var atIndex = withoutScheme.IndexOf('@');
            if (atIndex < 0) return null;
            
            // UUID - идентификатор пользователя на сервере
            config.UserId = withoutScheme.Substring(0, atIndex);
            var hostPart = withoutScheme.Substring(atIndex + 1);
            
            // Обработка IPv6 адресов (в квадратных скобках)
            // Пример: [2001:db8::1]:443
            if (hostPart.StartsWith("["))
            {
                var closeBracket = hostPart.IndexOf(']');
                config.Address = hostPart.Substring(1, closeBracket - 1);
                var portPart = hostPart.Substring(closeBracket + 2);
                config.Port = int.Parse(portPart);
            }
            else
            {
                // IPv4 или доменное имя
                var colonIndex = hostPart.LastIndexOf(':');
                config.Address = hostPart.Substring(0, colonIndex);
                config.Port = int.Parse(hostPart.Substring(colonIndex + 1));
            }

            // Применяем параметры из URI
            if (queryParams.TryGetValue("type", out var network))
                config.Network = network;           // tcp, ws, grpc
            if (queryParams.TryGetValue("security", out var security))
                config.Security = security;         // tls, reality, none
            if (queryParams.TryGetValue("sni", out var sni))
                config.Sni = sni;                   // Server Name Indication
            if (queryParams.TryGetValue("flow", out var flow))
                config.Flow = flow;                 // xtls-rprx-vision и т.д.
            if (queryParams.TryGetValue("fp", out var fp))
                config.Fingerprint = fp;            // chrome, firefox, safari
            if (queryParams.TryGetValue("alpn", out var alpn))
                config.Alpn = alpn;                 // h2, http/1.1
            if (queryParams.TryGetValue("path", out var path))
                config.WsPath = path;               // WebSocket путь
            if (queryParams.TryGetValue("host", out var host))
                config.WsHost = host;               // WebSocket хост
            if (queryParams.TryGetValue("servicename", out var serviceName))
                config.GrpcServiceName = serviceName; // gRPC сервис
            if (queryParams.TryGetValue("pbk", out var pbk))
                config.PublicKey = pbk;             // Reality публичный ключ
            if (queryParams.TryGetValue("sid", out var sid))
                config.ShortId = sid;               // Reality short ID
            if (queryParams.TryGetValue("spx", out var spx))
                config.SpiderX = spx;               // Reality spiderX
            if (queryParams.TryGetValue("encryption", out var encryption))
                config.Encryption = encryption;     // Шифрование (обычно none)

            return config;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to parse VLESS URI: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Генерирует VLESS URI из конфигурации сервера
    /// Используется для экспорта/копирования настроек
    /// </summary>
    public string GenerateVlessUri(VlessConfig config)
    {
        // Базовая часть: vless://uuid@address:port
        var uri = $"vless://{config.UserId}@{config.Address}:{config.Port}";
        
        // Собираем параметры
        var queryParams = new List<string>
        {
            $"type={config.Network}",
            $"security={config.Security}"
        };

        // Добавляем опциональные параметры
        if (!string.IsNullOrEmpty(config.Sni))
            queryParams.Add($"sni={Uri.EscapeDataString(config.Sni)}");
        if (!string.IsNullOrEmpty(config.Flow))
            queryParams.Add($"flow={config.Flow}");
        if (!string.IsNullOrEmpty(config.Fingerprint))
            queryParams.Add($"fp={config.Fingerprint}");
        if (!string.IsNullOrEmpty(config.Alpn))
            queryParams.Add($"alpn={Uri.EscapeDataString(config.Alpn)}");

        // Параметры для WebSocket
        if (config.Network == "ws")
        {
            queryParams.Add($"path={Uri.EscapeDataString(config.WsPath)}");
            if (!string.IsNullOrEmpty(config.WsHost))
                queryParams.Add($"host={Uri.EscapeDataString(config.WsHost)}");
        }

        // Параметры для gRPC
        if (config.Network == "grpc" && !string.IsNullOrEmpty(config.GrpcServiceName))
            queryParams.Add($"serviceName={Uri.EscapeDataString(config.GrpcServiceName)}");

        // Параметры для Reality
        if (config.Security == "reality")
        {
            if (!string.IsNullOrEmpty(config.PublicKey))
                queryParams.Add($"pbk={Uri.EscapeDataString(config.PublicKey)}");
            if (!string.IsNullOrEmpty(config.ShortId))
                queryParams.Add($"sid={config.ShortId}");
            if (!string.IsNullOrEmpty(config.SpiderX))
                queryParams.Add($"spx={Uri.EscapeDataString(config.SpiderX)}");
        }

        // Собираем URI
        uri += "?" + string.Join("&", queryParams);
        uri += "#" + Uri.EscapeDataString(config.Name);

        return uri;
    }

    /// <summary>
    /// Загружает серверы из URL подписки (subscription)
    /// 
    /// Подписка - это URL который возвращает список VPN серверов.
    /// Формат ответа: base64-закодированный текст с ссылками (по одной на строку)
    /// Или обычный текст с ссылками.
    /// 
    /// Поддерживаемые протоколы в подписке:
    /// - vless:// - VLESS
    /// - vmess:// - VMess (пока не поддерживается)
    /// - trojan:// - Trojan (пока не поддерживается)
    /// </summary>
    /// <param name="subscriptionUrl">URL подписки (например: https://example.com/sub/abc123)</param>
    /// <returns>Список распарсенных конфигураций</returns>
    public async Task<List<VlessConfig>> FetchSubscription(string subscriptionUrl, string? viaHttpProxy = null)
    {
        var configs = new List<VlessConfig>();

        try
        {
            if (!TryCreateSafeSubscriptionUri(subscriptionUrl, out var safeUri, out var validationError))
                throw new InvalidOperationException(validationError);

            // Игнорируем системный HTTP_PROXY / WinINET: иначе при настроенной в Windows
            // переменной HTTP_PROXY=127.0.0.1:10809 (это наш же локальный прокси) запрос
            // подписки уйдёт в выключенный xray и упадёт с "Подключение не установлено".
            // Если передан viaHttpProxy (VPN включён) — ходим через него, чтобы обойти
            // блокировку сервера подписки провайдером (симптом: 502 Bad Gateway напрямую).
            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseProxy = !string.IsNullOrWhiteSpace(viaHttpProxy),
                Proxy = string.IsNullOrWhiteSpace(viaHttpProxy) ? null : new WebProxy(viaHttpProxy)
            };

            using var httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            // UA специально "VlessVPN/1.0", без Accept — такое сочетание пропускают все
            // типовые панели подписок (Marzban, 3x-ui, x-ui, v2board). Chrome-UA без
            // остальных Sec-Fetch/Accept-Language заголовков некоторые CDN/WAF помечают
            // как "bot" и отвечают 502 Bad Gateway.
            httpClient.DefaultRequestHeaders.UserAgent.Clear();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("VlessVPN/1.0");

            using var response = await httpClient.GetAsync(
                safeUri,
                HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.Moved or HttpStatusCode.Redirect or HttpStatusCode.RedirectMethod or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
                throw new InvalidOperationException("Subscription URL redirect is not allowed.");

            response.EnsureSuccessStatusCode();

            var responseText = await ReadResponseAsStringWithLimitAsync(
                response,
                maxBytes: 2 * 1024 * 1024).ConfigureAwait(false);
            
            if (string.IsNullOrWhiteSpace(responseText))
            {
                return configs;
            }

            // Пробуем декодировать base64
            string content = TryDecodeBase64(responseText.Trim());
            
            // Разбиваем на строки и парсим каждую ссылку
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Пропускаем пустые строки и комментарии
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                    continue;

                // Парсим VLESS ссылки
                if (trimmedLine.StartsWith("vless://", StringComparison.OrdinalIgnoreCase))
                {
                    var config = ParseVlessUri(trimmedLine);
                    if (config != null)
                    {
                        configs.Add(config);
                    }
                }
                // TODO: Добавить поддержку vmess://, trojan://, ss:// при необходимости
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to fetch subscription: {ex.Message}");
            throw; // Пробрасываем ошибку для отображения пользователю
        }

        return configs;
    }

    private static async Task<string> ReadResponseAsStringWithLimitAsync(HttpResponseMessage response, int maxBytes)
    {
        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength.HasValue && contentLength.Value > maxBytes)
            throw new InvalidOperationException($"Subscription response too large ({contentLength.Value} bytes).");

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var ms = new MemoryStream();

        var buffer = new byte[16 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            if (read <= 0)
                break;

            if (ms.Length + read > maxBytes)
                throw new InvalidOperationException($"Subscription response too large (>{maxBytes} bytes).");

            ms.Write(buffer, 0, read);
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static bool TryCreateSafeSubscriptionUri(string rawUrl, out Uri uri, out string error)
    {
        uri = null!;
        error = "";

        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            error = "Subscription URL is empty.";
            return false;
        }

        rawUrl = rawUrl.Trim();

        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var parsed))
        {
            error = "Invalid subscription URL.";
            return false;
        }

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            error = "Only https:// subscription URLs are allowed.";
            return false;
        }

        if (!string.IsNullOrEmpty(parsed.UserInfo))
        {
            error = "Subscription URL must not include credentials.";
            return false;
        }

        var host = parsed.Host;
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            error = "Subscription URL host 'localhost' is not allowed.";
            return false;
        }

        // Блокируем прямые IP на loopback/private/link-local. Это снижает SSRF на локальную сеть.
        if (IPAddress.TryParse(host, out var ip))
        {
            if (IsUnsafeIp(ip))
            {
                error = "Subscription URL must not point to local/private IP addresses.";
                return false;
            }
        }

        uri = parsed;
        return true;
    }

    private static bool IsUnsafeIp(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
            return true;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            // 10.0.0.0/8
            if (b[0] == 10) return true;
            // 127.0.0.0/8
            if (b[0] == 127) return true;
            // 172.16.0.0/12
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
            // 192.168.0.0/16
            if (b[0] == 192 && b[1] == 168) return true;
            // 169.254.0.0/16 (link-local)
            if (b[0] == 169 && b[1] == 254) return true;
            // 100.64.0.0/10 (carrier-grade NAT)
            if (b[0] == 100 && (b[1] >= 64 && b[1] <= 127)) return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal)
                return true;
            // fc00::/7 (ULA)
            var b = ip.GetAddressBytes();
            if ((b[0] & 0xFE) == 0xFC)
                return true;
            // ::1
            if (ip.Equals(IPAddress.IPv6Loopback))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Пытается декодировать строку из base64
    /// Если не получается - возвращает исходную строку
    /// </summary>
    private string TryDecodeBase64(string input)
    {
        try
        {
            // Исправляем padding если нужно
            var base64 = input.Trim();
            
            // Заменяем URL-safe символы на стандартные base64
            base64 = base64.Replace('-', '+').Replace('_', '/');
            
            // Добавляем padding если нужно
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }

            var bytes = Convert.FromBase64String(base64);
            var decoded = Encoding.UTF8.GetString(bytes);
            
            // Проверяем что результат похож на список ссылок
            if (decoded.Contains("://"))
            {
                return decoded;
            }
            
            // Если не похоже на ссылки - возвращаем исходный текст
            return input;
        }
        catch
        {
            // Не base64 - возвращаем как есть
            return input;
        }
    }

    /// <summary>
    /// Проверяет, является ли URL подпиской (subscription URL)
    /// </summary>
    public bool IsSubscriptionUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
            
        return url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
