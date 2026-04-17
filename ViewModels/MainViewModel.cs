/*
 * MainViewModel.cs - Логика главного окна (MVVM паттерн)
 * 
 * БЕЗОПАСНОСТЬ:
 * - Этот файл содержит ТОЛЬКО логику интерфейса
 * - Не выполняет никаких системных операций напрямую
 * - Все действия делегируются сервисам (XrayService, ConfigurationService)
 * 
 * MVVM (Model-View-ViewModel):
 * - Model = VlessConfig, AppSettings (данные)
 * - View = MainWindow.xaml (интерфейс)
 * - ViewModel = MainViewModel (связь между ними)
 */

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VlessVPN.Models;
using VlessVPN;
using VlessVPN.Services;

namespace VlessVPN.ViewModels;

/// <summary>
/// ViewModel для главного окна
/// Использует CommunityToolkit.Mvvm для упрощения кода
/// </summary>
public partial class MainViewModel : ObservableObject
{
    // Сервисы для работы с VPN и настройками
    private readonly XrayService _xrayService;
    private readonly ConfigurationService _configService;
    
    // Таймер для обновления длительности подключения
    private readonly DispatcherTimer _timer;

    // ===== Свойства для привязки данных (Data Binding) =====
    // [ObservableProperty] автоматически генерирует уведомления об изменениях

    /// <summary>Список серверов</summary>
    [ObservableProperty]
    private ObservableCollection<VlessConfig> _servers = new();

    /// <summary>Выбранный сервер в списке</summary>
    [ObservableProperty]
    private VlessConfig? _selectedServer;

    /// <summary>Текущее состояние подключения</summary>
    [ObservableProperty]
    private ConnectionState _connectionState = ConnectionState.Disconnected;

    /// <summary>Текстовое сообщение о статусе</summary>
    [ObservableProperty]
    private string _statusMessage = "Not connected";

    /// <summary>Длительность подключения (HH:MM:SS)</summary>
    [ObservableProperty]
    private string _connectionDuration = "00:00:00";

    /// <summary>Текст логов для отображения</summary>
    [ObservableProperty]
    private string _logOutput = "";

    /// <summary>Флаг: подключено ли сейчас</summary>
    [ObservableProperty]
    private bool _isConnected;

    /// <summary>Флаг: идёт ли подключение</summary>
    [ObservableProperty]
    private bool _isConnecting;

    /// <summary>Порт SOCKS5 прокси</summary>
    [ObservableProperty]
    private int _localPort;

    /// <summary>Порт HTTP прокси</summary>
    [ObservableProperty]
    private int _httpPort;

    /// <summary>Использовать системный прокси</summary>
    [ObservableProperty]
    private bool _enableSystemProxy;

    /// <summary>Поле ввода для импорта URI</summary>
    [ObservableProperty]
    private string _importUri = "";

    /// <summary>Включить принудительный VPN для указанных доменов</summary>
    [ObservableProperty]
    private bool _enableForceProxy;

    /// <summary>Текст со списком доменов для принудительного VPN (по одному на строку)</summary>
    [ObservableProperty]
    private string _forceProxyDomainsText = "";

    /// <summary>Включить обход прокси для определённых доменов</summary>
    [ObservableProperty]
    private bool _enableBypass;

    /// <summary>Текст со списком доменов для обхода (по одному на строку)</summary>
    [ObservableProperty]
    private string _bypassDomainsText = "";

    /// <summary>Шифровать DNS через Cloudflare (DoH 1.1.1.1)</summary>
    [ObservableProperty]
    private bool _enableCloudflareDns;

    // ===== TLS Fragment (Anti-DPI) =====

    /// <summary>Включить фрагментацию TLS ClientHello</summary>
    [ObservableProperty]
    private bool _enableTlsFragment;

    /// <summary>Диапазон длины фрагментов</summary>
    [ObservableProperty]
    private string _tlsFragmentLength = "100-200";

    /// <summary>Диапазон интервала между фрагментами</summary>
    [ObservableProperty]
    private string _tlsFragmentInterval = "10-20";

    // ===== WARP (Cloudflare WireGuard) =====

    /// <summary>Включить WARP</summary>
    [ObservableProperty]
    private bool _enableWarp;

    /// <summary>WireGuard private key</summary>
    [ObservableProperty]
    private string _warpPrivateKey = "";

    /// <summary>WARP IPv4 адрес</summary>
    [ObservableProperty]
    private string _warpAddressV4 = "172.16.0.2/32";

    /// <summary>WARP IPv6 адрес</summary>
    [ObservableProperty]
    private string _warpAddressV6 = "";

    /// <summary>WARP reserved bytes</summary>
    [ObservableProperty]
    private string _warpReserved = "";

    /// <summary>WARP endpoint</summary>
    [ObservableProperty]
    private string _warpEndpoint = "engage.cloudflareclient.com:2408";

    // Время начала подключения для расчёта длительности
    private DateTime? _connectedSince;

    public MainViewModel()
    {
        // Инициализация сервисов (Xray — один экземпляр с App, иначе при выходе прокси не сбрасывается)
        _xrayService = DesignerProperties.GetIsInDesignMode(new DependencyObject())
            ? new XrayService()
            : App.Xray;
        _configService = new ConfigurationService();

        // Загрузка настроек из файла
        LocalPort = _configService.Settings.LocalPort;
        HttpPort = _configService.Settings.HttpPort;
        EnableSystemProxy = _configService.Settings.EnableSystemProxy;
        EnableBypass = _configService.Settings.EnableBypass;
        EnableCloudflareDns = _configService.Settings.UseCloudflareDns;
        EnableForceProxy = _configService.Settings.EnableForceProxy;
        ForceProxyDomainsText = string.Join("\n", _configService.Settings.ForceProxyDomains);
        EnableTlsFragment = _configService.Settings.EnableTlsFragment;
        TlsFragmentLength = _configService.Settings.TlsFragmentLength;
        TlsFragmentInterval = _configService.Settings.TlsFragmentInterval;
        EnableWarp = _configService.Settings.EnableWarp;
        WarpPrivateKey = _configService.Settings.WarpPrivateKey;
        WarpAddressV4 = _configService.Settings.WarpAddressV4;
        WarpAddressV6 = _configService.Settings.WarpAddressV6;
        WarpReserved = _configService.Settings.WarpReserved;
        WarpEndpoint = _configService.Settings.WarpEndpoint;
        BypassDomainsText = string.Join("\n", _configService.Settings.BypassDomains);
        ImportUri = _configService.Settings.LastSubscriptionUrl ?? "";

        // Загрузка списка серверов
        foreach (var server in _configService.Settings.Servers)
        {
            Servers.Add(server);
        }

        // Выбор последнего использованного сервера или первого в списке
        if (!string.IsNullOrEmpty(_configService.Settings.LastConnectedServerId))
        {
            SelectedServer = Servers.FirstOrDefault(s => s.Id == _configService.Settings.LastConnectedServerId);
        }
        SelectedServer ??= Servers.FirstOrDefault();

        // Подписка на события от XrayService
        
        // Получение логов от xray.
        // BeginInvoke, а не Invoke: при выходе App.OnExit ждёт StopXray на UI-потоке,
        // а StopXray синхронно поднимает это событие. Invoke() ждал бы UI-поток,
        // которого в этот момент нет → зависание процесса на выходе (процесс остаётся
        // висеть в Диспетчере задач). BeginInvoke безопасно игнорируется при завершении.
        _xrayService.OutputReceived += (s, msg) =>
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                return;
            dispatcher.BeginInvoke(() =>
            {
                LogOutput += $"[{DateTime.Now:HH:mm:ss}] {msg}\n";

                if (LogOutput.Length > 50000)
                {
                    LogOutput = LogOutput.Substring(LogOutput.Length - 40000);
                }
            });
        };

        // Изменение состояния подключения
        _xrayService.StateChanged += (s, state) =>
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                return;
            dispatcher.BeginInvoke(() =>
            {
                ConnectionState = state;
                UpdateStatusFromState(state);
            });
        };

        // Таймер для обновления длительности каждую секунду
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += (s, e) => UpdateDuration();
    }

    /// <summary>
    /// Обновляет UI в зависимости от состояния подключения
    /// </summary>
    private void UpdateStatusFromState(ConnectionState state)
    {
        IsConnecting = state == ConnectionState.Connecting;
        IsConnected = state == ConnectionState.Connected;

        // Текстовое сообщение для пользователя
        StatusMessage = state switch
        {
            ConnectionState.Disconnected => "Not connected",
            ConnectionState.Connecting => "Connecting...",
            ConnectionState.Connected => $"Connected to {SelectedServer?.Name ?? "server"}",
            ConnectionState.Disconnecting => "Disconnecting...",
            ConnectionState.Error => "Connection error",
            _ => "Unknown"
        };

        // Запуск/остановка таймера длительности
        if (state == ConnectionState.Connected)
        {
            _connectedSince = DateTime.Now;
            _timer.Start();
        }
        else
        {
            _timer.Stop();
            _connectedSince = null;
            ConnectionDuration = "00:00:00";
        }
    }

    /// <summary>
    /// Обновляет отображение длительности подключения
    /// </summary>
    private void UpdateDuration()
    {
        if (_connectedSince.HasValue)
        {
            var duration = DateTime.Now - _connectedSince.Value;
            ConnectionDuration = duration.ToString(@"hh\:mm\:ss");
        }
    }

    // ===== Команды (вызываются из UI) =====
    // [RelayCommand] автоматически создаёт ICommand для привязки к кнопкам

    /// <summary>
    /// Подключение к выбранному серверу
    /// </summary>
    [RelayCommand]
    private async Task Connect()
    {
        if (SelectedServer == null)
        {
            StatusMessage = "Please select a server";
            return;
        }

        // Сохраняем настройки перед подключением
        _configService.Settings.LocalPort = LocalPort;
        _configService.Settings.HttpPort = HttpPort;
        _configService.Settings.EnableSystemProxy = EnableSystemProxy;
        _configService.Settings.LastConnectedServerId = SelectedServer.Id;
        _configService.SaveSettings();

        // Запускаем подключение (делегируем XrayService)
        await _xrayService.StartXray(SelectedServer);
    }

    /// <summary>
    /// Отключение от VPN
    /// </summary>
    [RelayCommand]
    private async Task Disconnect()
    {
        await _xrayService.StopXrayAsync();
    }

    /// <summary>
    /// Переключение подключения (кнопка Connect/Disconnect)
    /// </summary>
    [RelayCommand]
    private async Task ToggleConnection()
    {
        if (IsConnected || IsConnecting)
        {
            await Disconnect();
        }
        else
        {
            await Connect();
        }
    }

    /// <summary>
    /// Добавление нового пустого сервера
    /// </summary>
    [RelayCommand]
    private void AddServer()
    {
        var server = new VlessConfig { Name = $"Server {Servers.Count + 1}" };
        Servers.Add(server);
        _configService.AddServer(server);
        SelectedServer = server;
    }

    /// <summary>
    /// Удаление выбранного сервера
    /// </summary>
    [RelayCommand]
    private void RemoveServer()
    {
        if (SelectedServer == null) return;

        // Нельзя удалить сервер к которому подключены
        if (IsConnected && _configService.Settings.LastConnectedServerId == SelectedServer.Id)
        {
            MessageBox.Show("Please disconnect before removing this server.", "Warning", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var server = SelectedServer;
        Servers.Remove(server);
        _configService.RemoveServer(server.Id);
        SelectedServer = Servers.FirstOrDefault();
    }

    /// <summary>
    /// Импорт серверов из буфера обмена
    /// Поддерживает:
    /// - Несколько VLESS ссылок (по одной на строку)
    /// - URL подписки (subscription)
    /// </summary>
    [RelayCommand]
    private async Task ImportFromClipboard()
    {
        try
        {
            var text = Clipboard.GetText();
            ImportUri = text;
            
            if (string.IsNullOrWhiteSpace(text))
            {
                LogOutput += "[INFO] Clipboard is empty\n";
                return;
            }

            var trimmedText = text.Trim();

            // Если это URL подписки - загружаем её
            if (_configService.IsSubscriptionUrl(trimmedText))
            {
                await ImportFromSubscription(trimmedText);
                return;
            }

            // Иначе парсим как список VLESS ссылок
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int imported = 0;

            foreach (var line in lines)
            {
                var config = _configService.ParseVlessUri(line.Trim());
                if (config != null)
                {
                    Servers.Add(config);
                    _configService.AddServer(config);
                    imported++;
                }
            }

            if (imported > 0)
            {
                LogOutput += $"[INFO] Imported {imported} server(s)\n";
                SelectedServer = Servers.LastOrDefault();
            }
            else
            {
                LogOutput += "[INFO] No valid VLESS links found in clipboard\n";
            }
        }
        catch (Exception ex)
        {
            LogOutput += $"[ERROR] Import failed: {ex.Message}\n";
        }
    }

    /// <summary>
    /// Импорт сервера из поля ввода URI
    /// Поддерживает:
    /// - vless:// ссылки
    /// - https:// подписки (subscription URLs)
    /// </summary>
    [RelayCommand]
    private async Task ImportFromUri()
    {
        if (string.IsNullOrWhiteSpace(ImportUri))
        {
            LogOutput += "[INFO] Please enter a VLESS URI or subscription URL\n";
            return;
        }

        var uri = ImportUri.Trim();

        // Проверяем, это подписка (http/https) или прямая ссылка (vless://)
        if (_configService.IsSubscriptionUrl(uri))
        {
            await ImportFromSubscription(uri);
        }
        else
        {
            // Обычная VLESS ссылка
            var config = _configService.ParseVlessUri(uri);
            if (config != null)
            {
                Servers.Add(config);
                _configService.AddServer(config);
                SelectedServer = config;
                ImportUri = "";
                LogOutput += $"[INFO] Imported: {config.Name}\n";
            }
            else
            {
                LogOutput += "[ERROR] Invalid VLESS URI format\n";
            }
        }
    }

    /// <summary>
    /// Импорт серверов из URL подписки (subscription). Синхронизирует записи с этой подпиской:
    /// удаляет старые серверы, ранее загруженные с того же URL, затем добавляет актуальный список.
    /// </summary>
    private async Task ImportFromSubscription(string subscriptionUrl)
    {
        try
        {
            subscriptionUrl = subscriptionUrl.Trim();
            LogOutput += $"[INFO] Fetching subscription from {subscriptionUrl}...\n";

            // Сохраняем URL сразу, до фетча. Иначе при ошибке сети (например,
            // недоступный сервер подписки) URL не попадёт в settings.json и
            // исчезнет из поля после рестарта приложения.
            if (!string.Equals(_configService.Settings.LastSubscriptionUrl, subscriptionUrl, StringComparison.Ordinal))
            {
                _configService.Settings.LastSubscriptionUrl = subscriptionUrl;
                _configService.SaveSettings();
            }
            ImportUri = subscriptionUrl;

            // Если VPN подключён — тянем подписку через наш же HTTP-прокси.
            // Это обходит блокировку сервера подписки провайдером (симптом: 502).
            var viaProxy = IsConnected ? $"http://127.0.0.1:{HttpPort}" : null;
            var configs = await _configService.FetchSubscription(subscriptionUrl, viaProxy);

            if (configs.Count == 0)
            {
                LogOutput += "[WARNING] No VLESS servers found in subscription\n";
                return;
            }

            var toRemove = Servers
                .Where(s => string.Equals(s.SourceSubscriptionUrl, subscriptionUrl, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var removedIds = new HashSet<string>(toRemove.Select(s => s.Id));
            foreach (var s in toRemove)
            {
                Servers.Remove(s);
                _configService.RemoveServer(s.Id);
            }

            var added = 0;
            foreach (var config in configs)
            {
                config.SourceSubscriptionUrl = subscriptionUrl;
                if (Servers.Any(x => x.Address == config.Address && x.Port == config.Port))
                    continue;
                Servers.Add(config);
                _configService.AddServer(config);
                added++;
            }

            _configService.Settings.LastSubscriptionUrl = subscriptionUrl;

            if (SelectedServer != null && removedIds.Contains(SelectedServer.Id))
                SelectedServer = Servers.FirstOrDefault();
            else
                SelectedServer ??= Servers.FirstOrDefault();

            if (!string.IsNullOrEmpty(_configService.Settings.LastConnectedServerId) &&
                !Servers.Any(s => s.Id == _configService.Settings.LastConnectedServerId))
            {
                _configService.Settings.LastConnectedServerId = SelectedServer?.Id;
            }

            _configService.SaveSettings();
            ImportUri = subscriptionUrl;

            LogOutput += $"[INFO] Subscription synced: {configs.Count} server(s) in feed, {toRemove.Count} removed, {added} added\n";
        }
        catch (Exception ex)
        {
            LogOutput += $"[ERROR] Failed to fetch subscription: {ex.Message}\n";
        }
    }

    /// <summary>
    /// Повторная загрузка списка с сохранённого URL подписки
    /// </summary>
    [RelayCommand]
    private async Task RefreshSubscription()
    {
        var url = (ImportUri ?? "").Trim();
        if (string.IsNullOrEmpty(url))
            url = (_configService.Settings.LastSubscriptionUrl ?? "").Trim();
        if (string.IsNullOrEmpty(url))
        {
            LogOutput += "[INFO] Укажите URL подписки в поле импорта или импортируйте подписку один раз\n";
            return;
        }

        if (!_configService.IsSubscriptionUrl(url))
        {
            LogOutput += "[INFO] Обновление доступно только для URL подписки (http/https)\n";
            return;
        }

        if (IsConnecting)
        {
            MessageBox.Show("Дождитесь окончания подключения, затем обновите подписку.", "VlessVPN",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // При включённом VPN обновление идёт через локальный HTTP-прокси (обход блокировок).
        // Активное подключение при этом не прерывается — меняется только список серверов.
        await ImportFromSubscription(url);
    }

    /// <summary>
    /// Копирование URI выбранного сервера в буфер обмена
    /// </summary>
    [RelayCommand]
    private void CopyServerUri()
    {
        if (SelectedServer == null) return;
        
        var uri = _configService.GenerateVlessUri(SelectedServer);
        Clipboard.SetText(uri);
        LogOutput += $"[INFO] Copied URI for {SelectedServer.Name}\n";
    }

    /// <summary>
    /// Сохранение изменений в настройках сервера
    /// </summary>
    [RelayCommand]
    private void SaveServerChanges()
    {
        if (SelectedServer == null) return;
        _configService.UpdateServer(SelectedServer);
        LogOutput += $"[INFO] Saved changes for {SelectedServer.Name}\n";
    }

    /// <summary>
    /// Очистка лога
    /// </summary>
    [RelayCommand]
    private void ClearLog()
    {
        LogOutput = "";
    }

    // ===== Обработчики изменения свойств =====
    // Автоматически сохраняют настройки при изменении

    partial void OnLocalPortChanged(int value)
    {
        if (value < 1 || value > 65535) return;
        if (value == HttpPort)
        {
            LogOutput += $"[{DateTime.Now:HH:mm:ss}] [WARNING] SOCKS5 and HTTP ports must be different\n";
            return;
        }
        _configService.Settings.LocalPort = value;
        _configService.SaveSettings();
    }

    partial void OnHttpPortChanged(int value)
    {
        if (value < 1 || value > 65535) return;
        if (value == LocalPort)
        {
            LogOutput += $"[{DateTime.Now:HH:mm:ss}] [WARNING] HTTP and SOCKS5 ports must be different\n";
            return;
        }
        _configService.Settings.HttpPort = value;
        _configService.SaveSettings();
    }

    partial void OnEnableSystemProxyChanged(bool value)
    {
        _configService.Settings.EnableSystemProxy = value;
        _configService.SaveSettings();
    }

    partial void OnEnableForceProxyChanged(bool value)
    {
        _configService.Settings.EnableForceProxy = value;
        _configService.SaveSettings();
    }

    partial void OnForceProxyDomainsTextChanged(string value)
    {
        var domains = value
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(d => d.Trim())
            .Where(d => !string.IsNullOrEmpty(d))
            .ToList();

        _configService.Settings.ForceProxyDomains = domains;
        _configService.SaveSettings();
    }

    partial void OnEnableBypassChanged(bool value)
    {
        _configService.Settings.EnableBypass = value;
        _configService.SaveSettings();
    }

    partial void OnEnableCloudflareDnsChanged(bool value)
    {
        _configService.Settings.UseCloudflareDns = value;
        _configService.SaveSettings();
        if (!value)
            LogOutput += $"[{DateTime.Now:HH:mm:ss}] [WARNING] DNS encryption disabled. DNS queries may be visible to your ISP.\n";
    }

    partial void OnBypassDomainsTextChanged(string value)
    {
        var domains = value
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(d => d.Trim())
            .Where(d => !string.IsNullOrEmpty(d))
            .ToList();

        _configService.Settings.BypassDomains = domains;
        _configService.SaveSettings();
    }

    // ===== TLS Fragment =====

    partial void OnEnableTlsFragmentChanged(bool value)
    {
        _configService.Settings.EnableTlsFragment = value;
        _configService.SaveSettings();
    }

    partial void OnTlsFragmentLengthChanged(string value)
    {
        _configService.Settings.TlsFragmentLength = value;
        _configService.SaveSettings();
    }

    partial void OnTlsFragmentIntervalChanged(string value)
    {
        _configService.Settings.TlsFragmentInterval = value;
        _configService.SaveSettings();
    }

    // ===== WARP =====

    partial void OnEnableWarpChanged(bool value)
    {
        _configService.Settings.EnableWarp = value;
        _configService.SaveSettings();
    }

    partial void OnWarpPrivateKeyChanged(string value)
    {
        _configService.Settings.WarpPrivateKey = value;
        _configService.SaveSettings();
    }

    partial void OnWarpAddressV4Changed(string value)
    {
        _configService.Settings.WarpAddressV4 = value;
        _configService.SaveSettings();
    }

    partial void OnWarpAddressV6Changed(string value)
    {
        _configService.Settings.WarpAddressV6 = value;
        _configService.SaveSettings();
    }

    partial void OnWarpReservedChanged(string value)
    {
        _configService.Settings.WarpReserved = value;
        _configService.SaveSettings();
    }

    partial void OnWarpEndpointChanged(string value)
    {
        _configService.Settings.WarpEndpoint = value;
        _configService.SaveSettings();
    }
}
