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
using System.Net.Http;
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
                return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
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
    public async Task<List<VlessConfig>> FetchSubscription(string subscriptionUrl)
    {
        var configs = new List<VlessConfig>();
        
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            // Добавляем User-Agent чтобы сервер не блокировал запрос
            httpClient.DefaultRequestHeaders.Add("User-Agent", "VlessVPN/1.0");
            
            // Загружаем содержимое подписки
            var response = await httpClient.GetStringAsync(subscriptionUrl);
            
            if (string.IsNullOrWhiteSpace(response))
            {
                return configs;
            }

            // Пробуем декодировать base64
            string content = TryDecodeBase64(response.Trim());
            
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
            
        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
