/*
 * ConnectionStatus.cs - Модели статуса подключения
 * 
 * Простые классы для отслеживания состояния VPN соединения.
 * Не выполняют никаких действий, только хранят данные.
 */

namespace VlessVPN.Models;

/// <summary>
/// Возможные состояния VPN подключения
/// </summary>
public enum ConnectionState
{
    /// <summary>VPN отключён</summary>
    Disconnected,
    
    /// <summary>Идёт подключение</summary>
    Connecting,
    
    /// <summary>VPN подключён и работает</summary>
    Connected,
    
    /// <summary>Идёт отключение</summary>
    Disconnecting,
    
    /// <summary>Ошибка подключения</summary>
    Error
}

/// <summary>
/// Детальный статус подключения
/// </summary>
public class ConnectionStatus
{
    /// <summary>Текущее состояние</summary>
    public ConnectionState State { get; set; } = ConnectionState.Disconnected;
    
    /// <summary>Сообщение о статусе для отображения</summary>
    public string Message { get; set; } = "Not connected";
    
    /// <summary>Время начала подключения</summary>
    public DateTime? ConnectedSince { get; set; }
    
    /// <summary>Отправлено байт (для статистики)</summary>
    public long BytesSent { get; set; }
    
    /// <summary>Получено байт (для статистики)</summary>
    public long BytesReceived { get; set; }
    
    /// <summary>ID текущего сервера</summary>
    public string? CurrentServerId { get; set; }

    /// <summary>Длительность подключения</summary>
    public TimeSpan? Duration => ConnectedSince.HasValue 
        ? DateTime.Now - ConnectedSince.Value 
        : null;
}
