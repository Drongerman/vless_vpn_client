/*
 * XrayService.cs - Сервис управления Xray-core
 * 
 * БЕЗОПАСНОСТЬ:
 * - Запускает xray.exe как дочерний процесс (не как службу Windows)
 * - Процесс автоматически завершается при закрытии приложения
 * - Изменяет только настройки прокси в реестре (HKCU - только для текущего пользователя)
 * - Все изменения реестра откатываются при отключении VPN
 * 
 * ЧТО ДЕЛАЕТ:
 * 1. Генерирует конфигурацию JSON для Xray
 * 2. Запускает процесс xray.exe с этой конфигурацией
 * 3. Опционально устанавливает системный прокси Windows
 * 4. При отключении - останавливает процесс и очищает прокси
 */

using System.Diagnostics;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VlessVPN.Models;

namespace VlessVPN.Services;

public class XrayService
{
    // Процесс xray.exe - хранится для последующего завершения
    private Process? _xrayProcess;
    
    // Кэш последнего найденного xray.exe (для KillOrphan)
    private string? _lastResolvedXrayPath;
    
    // Путь к временному файлу конфигурации
    private readonly string _configPath;
    
    // Сервис настроек для получения портов и опций
    private readonly ConfigurationService _configService;

    /// <summary>Сериализация Start/Stop — иначе два параллельных Connect дают два xray на одном порту.</summary>
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

    // События для уведомления UI о статусе
    public event EventHandler<string>? OutputReceived;      // Логи от xray
    public event EventHandler<ConnectionState>? StateChanged; // Изменение состояния

    public ConnectionState CurrentState { get; private set; } = ConnectionState.Disconnected;

    public XrayService()
    {
        _configService = new ConfigurationService();
        
        // Конфигурация сохраняется в AppData (не требует прав администратора)
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configDir = Path.Combine(appData, "VlessVPN");
        Directory.CreateDirectory(configDir);
        _configPath = Path.Combine(configDir, "xray-config.json");
    }

    /// <summary>
    /// Каталог, где лежит xray-core рядом с приложением (для publish single-file — папка .exe, не temp).
    /// </summary>
    private static string GetAppContentDirectory()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        try
        {
            var proc = Environment.ProcessPath;
            if (string.IsNullOrEmpty(proc))
                return baseDir;

            var procDir = Path.GetDirectoryName(proc);
            if (string.IsNullOrEmpty(procDir))
                return baseDir;

            if (string.Equals(Path.GetFileName(proc), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
                return baseDir;

            return procDir;
        }
        catch
        {
            return baseDir;
        }
    }

    /// <summary>Возможные расположения xray.exe (первый существующий выбирается при подключении).</summary>
    private IEnumerable<string> EnumerateXrayCandidatePaths()
    {
        var appDir = GetAppContentDirectory();
        var baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VlessVPN", "xray-core", "xray.exe");

        yield return Path.Combine(appDir, "xray-core", "xray.exe");
        yield return Path.Combine(baseDir, "xray-core", "xray.exe");
        yield return appDataDir;
    }

    private string? ResolveXrayPath()
    {
        foreach (var p in EnumerateXrayCandidatePaths())
        {
            if (File.Exists(p))
                return p;
        }

        return null;
    }

    /// <summary>
    /// Запускает VPN подключение
    /// </summary>
    /// <param name="config">Конфигурация сервера VLESS</param>
    /// <returns>true если подключение успешно</returns>
    public async Task<bool> StartXray(VlessConfig config)
    {
        await _lifecycleLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Всегда освобождаем порты: «осиротевший» xray после сбоя, мёртвый Process без Dispose, гонка двух Connect
            await PrepareForFreshXrayStartAsync().ConfigureAwait(false);

            SetState(ConnectionState.Connecting);

            // ШАГ 1: Генерируем JSON конфигурацию для Xray
            var xrayConfig = GenerateXrayConfig(config, _configService.Settings);
            await File.WriteAllTextAsync(_configPath, xrayConfig).ConfigureAwait(false);

            // ШАГ 2: Проверяем наличие xray.exe (рядом с .exe, в BaseDirectory или в %AppData%\VlessVPN\xray-core\)
            var xrayPath = ResolveXrayPath();
            if (xrayPath == null)
            {
                OutputReceived?.Invoke(this, "Error: xray.exe not found. Checked paths:");
                foreach (var p in EnumerateXrayCandidatePaths())
                    OutputReceived?.Invoke(this, $"  - {p}");
                OutputReceived?.Invoke(this, "Download Windows x64 zip from: https://github.com/XTLS/Xray-core/releases");
                OutputReceived?.Invoke(this, "Extract xray.exe into .\\xray-core\\ next to VlessVPN.exe or into %AppData%\\VlessVPN\\xray-core\\");
                SetState(ConnectionState.Error);
                return false;
            }

            _lastResolvedXrayPath = xrayPath;

            // ШАГ 3: Запускаем процесс xray.exe
            _xrayProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = xrayPath,
                    Arguments = $"run -config \"{_configPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(xrayPath)
                },
                EnableRaisingEvents = true
            };

            _xrayProcess.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    OutputReceived?.Invoke(this, e.Data);
                    if (e.Data.Contains("started") || e.Data.Contains("listening"))
                        SetState(ConnectionState.Connected);
                }
            };

            _xrayProcess.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    OutputReceived?.Invoke(this, $"[ERROR] {e.Data}");
            };

            _xrayProcess.Exited += (s, e) =>
            {
                if (CurrentState != ConnectionState.Disconnecting)
                {
                    SetState(ConnectionState.Error);
                    OutputReceived?.Invoke(this, "Xray process terminated unexpectedly");
                }
            };

            _xrayProcess.Start();
            ChildProcessJob.TryAssign(_xrayProcess);
            _xrayProcess.BeginOutputReadLine();
            _xrayProcess.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            await DisposeXrayProcessAsync().ConfigureAwait(false);
            KillOrphanXrayByOurExecutable();
            OutputReceived?.Invoke(this, $"Error starting Xray: {ex.Message}");
            SetState(ConnectionState.Error);
            return false;
        }
        finally
        {
            _lifecycleLock.Release();
        }

        // Без удержания lock: иначе «Отключить» ждёт весь Delay; Stop может прервать ожидание и убить процесс
        await Task.Delay(2000).ConfigureAwait(false);

        await _lifecycleLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_xrayProcess == null || _xrayProcess.HasExited)
            {
                await DisposeXrayProcessAsync().ConfigureAwait(false);
                KillOrphanXrayByOurExecutable();
                SetState(ConnectionState.Error);
                return false;
            }

            if (_configService.Settings.EnableSystemProxy)
                SetSystemProxy(_configService.Settings.HttpPort);

            SetState(ConnectionState.Connected);
            OutputReceived?.Invoke(this, $"Connected to {config.Name} ({config.Address}:{config.Port})");
            return true;
        }
        catch (Exception ex)
        {
            await DisposeXrayProcessAsync().ConfigureAwait(false);
            KillOrphanXrayByOurExecutable();
            OutputReceived?.Invoke(this, $"Error starting Xray: {ex.Message}");
            SetState(ConnectionState.Error);
            return false;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    /// <summary>
    /// Останавливает VPN (синхронная версия для App.OnExit)
    /// </summary>
    public void StopXray()
    {
        StopXrayAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Останавливает VPN подключение
    /// ВАЖНО: Обязательно вызывается при закрытии приложения
    /// </summary>
    public async Task StopXrayAsync()
    {
        await _lifecycleLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopXrayCoreAsync(notifyDisconnect: true).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    /// <summary>Перед новым запуском: без сообщения «Disconnected» в логе (тихая зачистка).</summary>
    private async Task PrepareForFreshXrayStartAsync()
    {
        await StopXrayCoreAsync(notifyDisconnect: false).ConfigureAwait(false);
        // Сокет может отпустить порт не мгновенно после Kill
        await Task.Delay(150).ConfigureAwait(false);
    }

    /// <summary>Остановка процесса и сброс прокси. notifyDisconnect — обновление UI/лог при ручном отключении.</summary>
    private async Task StopXrayCoreAsync(bool notifyDisconnect)
    {
        if (notifyDisconnect)
            SetState(ConnectionState.Disconnecting);

        ClearSystemProxy();
        await DisposeXrayProcessAsync().ConfigureAwait(false);
        KillOrphanXrayByOurExecutable();

        if (notifyDisconnect)
        {
            SetState(ConnectionState.Disconnected);
            OutputReceived?.Invoke(this, "Disconnected");
        }
    }

    /// <summary>Синхронный WaitForExit: из UI-потока в OnExit async-продолжение с захватом Dispatcher давало взаимную блокировку и «зависший» xray.</summary>
    private Task DisposeXrayProcessAsync()
    {
        if (_xrayProcess == null)
            return Task.CompletedTask;

        var p = _xrayProcess;
        _xrayProcess = null;

        try
        {
            if (!p.HasExited)
            {
                try
                {
                    p.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    OutputReceived?.Invoke(this, $"Error stopping Xray: {ex.Message}");
                }
            }

            try
            {
                p.WaitForExit(15000);
            }
            catch (Exception ex)
            {
                OutputReceived?.Invoke(this, $"Error waiting for Xray exit: {ex.Message}");
            }
        }
        finally
        {
            p.Dispose();
        }

        return Task.CompletedTask;
    }

    /// <summary>Завершает любые xray.exe с тем же путём, что наш (например после «Снять задачу» или сбоя).</summary>
    private void KillOrphanXrayByOurExecutable()
    {
        var ourPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in EnumerateXrayCandidatePaths())
            ourPaths.Add(Path.GetFullPath(p));
        if (_lastResolvedXrayPath != null)
            ourPaths.Add(Path.GetFullPath(_lastResolvedXrayPath));

        try
        {
            foreach (var proc in Process.GetProcessesByName("xray"))
            {
                try
                {
                    string? path = null;
                    try
                    {
                        path = proc.MainModule?.FileName;
                    }
                    catch
                    {
                        continue;
                    }

                    if (path == null)
                        continue;
                    var full = Path.GetFullPath(path);
                    if (!ourPaths.Contains(full))
                        continue;

                    if (!proc.HasExited)
                    {
                        proc.Kill(entireProcessTree: true);
                        proc.WaitForExit(5000);
                    }
                }
                catch
                {
                    // ignore
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }
        catch
        {
            // ignore
        }
    }

    private void SetState(ConnectionState state)
    {
        CurrentState = state;
        try
        {
            StateChanged?.Invoke(this, state);
        }
        catch
        {
            // При выходе из приложения подписчики могут дергать уже закрывающийся Dispatcher
        }
    }

    /// <summary>
    /// Генерирует JSON конфигурацию для Xray-core
    /// Формат: https://xtls.github.io/en/config/
    /// </summary>
    private string GenerateXrayConfig(VlessConfig config, AppSettings settings)
    {
        var xrayConfig = new JObject
        {
            // Настройки логирования
            ["log"] = new JObject
            {
                ["loglevel"] = "warning"  // Уровень: debug, info, warning, error, none
            },
            
            // INBOUNDS - локальные точки входа (куда подключаются программы на вашем ПК)
            ["inbounds"] = new JArray
            {
                // SOCKS5 прокси (используется большинством программ)
                new JObject
                {
                    ["tag"] = "socks",
                    ["port"] = settings.LocalPort,    // По умолчанию 10808
                    ["listen"] = "127.0.0.1",         // ТОЛЬКО локальный доступ
                    ["protocol"] = "socks",
                    ["settings"] = new JObject
                    {
                        ["auth"] = "noauth",          // Без пароля (безопасно, т.к. только localhost)
                        ["udp"] = true                // Поддержка UDP (для игр, видео и т.д.)
                    },
                    ["sniffing"] = new JObject
                    {
                        ["enabled"] = true,
                        ["destOverride"] = new JArray { "http", "tls" }  // Определение типа трафика
                    }
                },
                // HTTP прокси (для браузеров и curl)
                new JObject
                {
                    ["tag"] = "http",
                    ["port"] = settings.HttpPort,     // По умолчанию 10809
                    ["listen"] = "127.0.0.1",         // ТОЛЬКО локальный доступ
                    ["protocol"] = "http",
                    ["settings"] = new JObject
                    {
                        ["allowTransparent"] = false
                    }
                }
            },
            
            // OUTBOUNDS - куда отправляется трафик
            ["outbounds"] = new JArray
            {
                CreateOutbound(config),               // Основной: через VPN сервер
                new JObject                           // Прямой: напрямую (для локальных адресов)
                {
                    ["tag"] = "direct",
                    ["protocol"] = "freedom"
                },
                new JObject                           // Блокировка (не используется по умолчанию)
                {
                    ["tag"] = "block",
                    ["protocol"] = "blackhole"
                }
            },
            
            // ROUTING - правила маршрутизации
            ["routing"] = new JObject
            {
                ["domainStrategy"] = "IPIfNonMatch",  // Резолвим домены если нет прямого совпадения
                ["rules"] = CreateRoutingRules(settings)
            }
        };

        // DNS over HTTPS (Cloudflare 1.1.1.1) — шифрование DNS-запросов
        if (settings.UseCloudflareDns)
        {
            xrayConfig["dns"] = new JObject
            {
                ["servers"] = new JArray
                {
                    "https://1.1.1.1/dns-query",
                    "https://1.0.0.1/dns-query"
                }
            };
        }

        return JsonConvert.SerializeObject(xrayConfig, Formatting.Indented);
    }

    /// <summary>
    /// Создаёт правила маршрутизации с учётом списка обхода прокси
    /// </summary>
    private JArray CreateRoutingRules(AppSettings settings)
    {
        var rules = new JArray();

        // Если включён обход прокси - добавляем правила для доменов
        if (settings.EnableBypass && settings.BypassDomains.Count > 0)
        {
            // Разделяем домены и IP-правила
            var domains = new JArray();
            var ips = new JArray();

            foreach (var rule in settings.BypassDomains)
            {
                if (rule.StartsWith("geoip:") || rule.StartsWith("geosite:"))
                {
                    // Специальные правила GeoIP/GeoSite
                    if (rule.StartsWith("geoip:"))
                        ips.Add(rule);
                    else
                        domains.Add(rule);
                }
                else if (rule.StartsWith("domain:"))
                {
                    // domain:example.com - точное совпадение домена и поддоменов
                    domains.Add(rule);
                }
                else if (rule.StartsWith("full:"))
                {
                    // full:example.com - только точное совпадение
                    domains.Add(rule);
                }
                else if (rule.StartsWith("regexp:"))
                {
                    // regexp:.*\.ru$ - регулярное выражение
                    domains.Add(rule);
                }
                else
                {
                    // Простой домен - добавляем как domain:
                    domains.Add($"domain:{rule}");
                }
            }

            // Правило для доменов
            if (domains.Count > 0)
            {
                rules.Add(new JObject
                {
                    ["type"] = "field",
                    ["domain"] = domains,
                    ["outboundTag"] = "direct"
                });
            }

            // Правило для IP-адресов
            if (ips.Count > 0)
            {
                rules.Add(new JObject
                {
                    ["type"] = "field",
                    ["ip"] = ips,
                    ["outboundTag"] = "direct"
                });
            }
        }
        else
        {
            // Если обход отключён - только локальные адреса напрямую
            rules.Add(new JObject
            {
                ["type"] = "field",
                ["ip"] = new JArray { "geoip:private" },
                ["outboundTag"] = "direct"
            });
        }

        return rules;
    }

    /// <summary>
    /// Создаёт конфигурацию исходящего подключения VLESS
    /// </summary>
    private JObject CreateOutbound(VlessConfig config)
    {
        var outbound = new JObject
        {
            ["tag"] = "proxy",
            ["protocol"] = "vless",
            ["settings"] = new JObject
            {
                ["vnext"] = new JArray
                {
                    new JObject
                    {
                        ["address"] = config.Address,     // Адрес VPN сервера
                        ["port"] = config.Port,           // Порт VPN сервера
                        ["users"] = new JArray
                        {
                            new JObject
                            {
                                ["id"] = config.UserId,   // UUID пользователя
                                ["encryption"] = config.Encryption,
                                ["flow"] = config.Flow    // Тип шифрования (xtls-rprx-vision и т.д.)
                            }
                        }
                    }
                }
            }
        };

        // Настройки транспорта
        var streamSettings = new JObject
        {
            ["network"] = config.Network  // tcp, ws, grpc, и т.д.
        };

        // Настройки безопасности (TLS или Reality)
        if (config.Security == "tls")
        {
            streamSettings["security"] = "tls";
            streamSettings["tlsSettings"] = new JObject
            {
                ["serverName"] = string.IsNullOrEmpty(config.Sni) ? config.Address : config.Sni,
                ["fingerprint"] = config.Fingerprint,     // Имитация браузера
                ["allowInsecure"] = config.AllowInsecure  // Разрешить невалидные сертификаты
            };
            
            if (!string.IsNullOrEmpty(config.Alpn))
            {
                streamSettings["tlsSettings"]!["alpn"] = new JArray(config.Alpn.Split(','));
            }
        }
        else if (config.Security == "reality")
        {
            // Reality - новый протокол маскировки
            streamSettings["security"] = "reality";
            streamSettings["realitySettings"] = new JObject
            {
                ["serverName"] = string.IsNullOrEmpty(config.Sni) ? config.Address : config.Sni,
                ["fingerprint"] = config.Fingerprint,
                ["publicKey"] = config.PublicKey,
                ["shortId"] = config.ShortId,
                ["spiderX"] = config.SpiderX
            };
        }

        // Настройки для разных типов транспорта
        switch (config.Network)
        {
            case "ws":  // WebSocket
                streamSettings["wsSettings"] = new JObject
                {
                    ["path"] = config.WsPath,
                    ["headers"] = new JObject
                    {
                        ["Host"] = string.IsNullOrEmpty(config.WsHost) ? config.Address : config.WsHost
                    }
                };
                break;
            case "grpc":  // gRPC
                streamSettings["grpcSettings"] = new JObject
                {
                    ["serviceName"] = config.GrpcServiceName,
                    ["multiMode"] = false
                };
                break;
            case "tcp":  // TCP
                streamSettings["tcpSettings"] = new JObject
                {
                    ["header"] = new JObject { ["type"] = "none" }
                };
                break;
        }

        outbound["streamSettings"] = streamSettings;
        return outbound;
    }

    /// <summary>
    /// Устанавливает системный HTTP прокси Windows
    /// 
    /// БЕЗОПАСНОСТЬ:
    /// - Изменяет только HKEY_CURRENT_USER (не требует прав администратора)
    /// - Это стандартный способ, используемый браузерами и другими приложениями
    /// - Изменения видны в: Настройки → Сеть и Интернет → Прокси
    /// </summary>
    private void SetSystemProxy(int port)
    {
        try
        {
            // Открываем ключ реестра для настроек интернета
            // HKCU = только текущий пользователь, не влияет на других
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true);
            
            if (key != null)
            {
                // ProxyEnable = 1 - включить прокси
                key.SetValue("ProxyEnable", 1);
                // ProxyServer - адрес прокси (только localhost!)
                key.SetValue("ProxyServer", $"127.0.0.1:{port}");
                key.Close();
                
                // Уведомляем Windows об изменении настроек
                // Это заставляет браузеры и другие программы перечитать настройки
                NativeMethods.InternetSetOption(IntPtr.Zero, 
                    NativeMethods.INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
                NativeMethods.InternetSetOption(IntPtr.Zero, 
                    NativeMethods.INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
                
                OutputReceived?.Invoke(this, $"System proxy set to 127.0.0.1:{port}");
            }
        }
        catch (Exception ex)
        {
            OutputReceived?.Invoke(this, $"Failed to set system proxy: {ex.Message}");
        }
    }

    /// <summary>
    /// Отключает системный прокси Windows
    /// ВАЖНО: Вызывается при отключении VPN для восстановления прямого соединения
    /// </summary>
    private void ClearSystemProxy()
    {
        try
        {
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true);
            
            if (key != null)
            {
                // ProxyEnable = 0 - отключить прокси
                key.SetValue("ProxyEnable", 0);
                key.Close();
                
                // Уведомляем Windows
                NativeMethods.InternetSetOption(IntPtr.Zero, 
                    NativeMethods.INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
                NativeMethods.InternetSetOption(IntPtr.Zero, 
                    NativeMethods.INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
                
                OutputReceived?.Invoke(this, "System proxy cleared");
            }
        }
        catch (Exception ex)
        {
            OutputReceived?.Invoke(this, $"Failed to clear system proxy: {ex.Message}");
        }
    }
}

/// <summary>
/// Нативные методы Windows для обновления настроек прокси
/// Это стандартный Windows API, не делает ничего опасного
/// </summary>
internal static class NativeMethods
{
    public const int INTERNET_OPTION_SETTINGS_CHANGED = 39;  // Настройки изменились
    public const int INTERNET_OPTION_REFRESH = 37;           // Обновить настройки

    // Функция из wininet.dll - стандартная библиотека Windows для интернета
    [System.Runtime.InteropServices.DllImport("wininet.dll")]
    public static extern bool InternetSetOption(IntPtr hInternet, int dwOption, 
        IntPtr lpBuffer, int dwBufferLength);
}
