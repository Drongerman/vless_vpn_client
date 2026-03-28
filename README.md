# VlessVPN - Windows 11 VLESS Client

## 🔒 Безопасность и прозрачность

Этот документ объясняет, что делает приложение и какие изменения вносит в систему.

### ⚠️ Что приложение делает:

1. **Запускает внешний процесс xray.exe** - это официальный open-source проект [Xray-core](https://github.com/XTLS/Xray-core)
2. **Изменяет настройки прокси Windows** - только при включённой опции "System Proxy"
3. **Сохраняет настройки** в папку `%AppData%\VlessVPN\`

### ✅ Что приложение НЕ делает:

- ❌ Не отправляет данные на сторонние серверы (только на ваш VPN сервер)
- ❌ Не устанавливает драйверы или сертификаты
- ❌ Не модифицирует системные файлы
- ❌ Не запускается автоматически при старте Windows (если не включить вручную)
- ❌ Не собирает телеметрию

---

## 📁 Структура файлов

```
VlessVPN/
├── Models/                 # Модели данных
│   ├── VlessConfig.cs      # Структура конфигурации сервера
│   └── ConnectionStatus.cs # Статус подключения
├── Services/
│   ├── XrayService.cs      # Управление процессом Xray
│   └── ConfigurationService.cs # Сохранение/загрузка настроек
├── ViewModels/
│   └── MainViewModel.cs    # Логика UI (MVVM паттерн)
├── Views/
│   ├── MainWindow.xaml     # Интерфейс (разметка)
│   └── MainWindow.xaml.cs  # Код окна
├── Converters/
│   └── Converters.cs       # Конвертеры для UI
├── xray-core/              # ⚠️ НУЖНО СКАЧАТЬ ОТДЕЛЬНО
│   └── xray.exe            # Движок для VLESS протокола
└── App.xaml                # Стили и ресурсы приложения
```

---

## 🔍 Детальное объяснение кода

### 1. XrayService.cs - Главный сервис

Этот файл управляет процессом Xray. Вот что он делает:

```csharp
// Запускает xray.exe как отдельный процесс
_xrayProcess = new Process { ... };
_xrayProcess.Start();

// При отключении - останавливает процесс
_xrayProcess.Kill(true);
```

**Изменение прокси Windows (строки ~250-280):**
```csharp
// Открывает ключ реестра для настроек интернета
var key = Registry.CurrentUser.OpenSubKey(
    @"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true);

// Включает прокси и указывает адрес
key.SetValue("ProxyEnable", 1);
key.SetValue("ProxyServer", "127.0.0.1:10809");
```

⚠️ **Это стандартный способ установки прокси в Windows.** Браузеры Chrome, Firefox и другие программы используют эти настройки. При отключении VPN настройки возвращаются к исходным (`ProxyEnable = 0`).

### 2. ConfigurationService.cs - Сохранение настроек

Сохраняет ваши серверы в JSON файл:

```csharp
// Путь: %AppData%\VlessVPN\settings.json
var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
var configDir = Path.Combine(appData, "VlessVPN");
```

**Файл settings.json содержит:**
- Список серверов (адреса, порты, UUID)
- Настройки портов (SOCKS5, HTTP)
- Флаг системного прокси

### 3. Модели данных (Models/)

Простые классы для хранения данных:

```csharp
public class VlessConfig
{
    public string Address { get; set; }  // Адрес сервера
    public int Port { get; set; }        // Порт
    public string UserId { get; set; }   // UUID (идентификатор)
    // ... другие настройки протокола
}
```

---

## 🛡️ Как проверить безопасность

### Способ 1: Проверка сетевой активности

1. Откройте **Диспетчер задач** → Вкладка **Производительность** → **Открыть монитор ресурсов**
2. Перейдите на вкладку **Сеть**
3. Запустите VlessVPN
4. Убедитесь, что соединения идут только на ваш VPN сервер

### Способ 2: Проверка реестра

До подключения:
```powershell
Get-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings" | Select ProxyEnable, ProxyServer
```

После подключения с включённым System Proxy:
```
ProxyEnable: 1
ProxyServer: 127.0.0.1:10809
```

После отключения:
```
ProxyEnable: 0
```

### Способ 3: Проверка процессов

```powershell
Get-Process | Where-Object {$_.ProcessName -like "*xray*" -or $_.ProcessName -like "*VlessVPN*"}
```

---

## 📦 Установка

### Шаг 1: Скачать Xray-core

1. Перейдите на https://github.com/XTLS/Xray-core/releases
2. Скачайте `Xray-windows-64.zip` (или 32-bit версию)
3. Распакуйте в папку `VlessVPN/xray-core/`
4. Должен быть файл: `VlessVPN/xray-core/xray.exe`

### Шаг 2: Сборка приложения

```powershell
cd VlessVPN
dotnet restore
dotnet build
dotnet run
```

Или откройте `VlessVPN.sln` в Visual Studio / Rider.

### Шаг 3: Релизная сборка (один .exe, self-contained)

Нужен [.NET 8 SDK](https://dotnet.microsoft.com/download). В проекте задан профиль **`Properties/PublishProfiles/Win64-SingleFile.pubxml`**: self-contained, **single-file**, вывод в **`out\publish-singlefile\`**. Так можно снова запускать `publish`, пока открыт клиент из **`out\dist\`** (или другой копии): сборка не перезаписывает тот же `VlessVPN.exe`, из которого вы работаете.

```powershell
cd <корень-репозитория-vless_vpn_client>
dotnet publish -c Release -p:PublishProfile=Win64-SingleFile
```

Результат:

- **`out\publish-singlefile\VlessVPN.exe`** — переносимый запуск;
- **`out\publish-singlefile\xray-core\`** — копируется из корневой **`xray-core\`** при publish (нужен **`xray-core\xray.exe`** на диске у сборщика).

Чтобы обновить **`out\dist\`**, **закройте** приложение, запущенное из этой папки, и скопируйте содержимое `out\publish-singlefile\` в `out\dist\` (или `robocopy out\publish-singlefile out\dist /E`). Если публиковать **напрямую** в папку, откуда сейчас запущен `VlessVPN.exe`, снова будет ошибка `The process cannot access the file ... VlessVPN.exe`.

То же без профиля (явные свойства):

```powershell
dotnet publish -c Release -r win-x64 /p:PublishSingleFile=true /p:SelfContained=true /p:IncludeNativeLibrariesForSelfExtract=true -o out\publish-singlefile
```

При странной иконке в exe после смены `.ico`: `dotnet clean -c Release`, затем снова `publish`.

Обычная отладочная сборка без single-file:

```powershell
dotnet build -c Release
# артефакты: bin\Release\net8.0-windows\...
```

---

## 🔧 Требования

- Windows 10/11
- .NET 8.0 SDK
- Xray-core (скачивается отдельно)

---

## ❓ FAQ

**Q: Почему нужен xray.exe?**
A: VLESS - это сложный протокол. Xray-core - официальная реализация, которой доверяют миллионы пользователей.

**Q: Приложение меняет DNS?**
A: Нет, DNS остаётся без изменений. Но трафик через прокси будет использовать DNS вашего VPN сервера.

**Q: Как полностью удалить?**
A: 
1. Удалите папку с приложением
2. Удалите `%AppData%\VlessVPN`
3. Убедитесь что прокси отключён (Settings → Network → Proxy)

**Q: Безопасно ли хранить UUID в файле?**
A: UUID хранится локально в вашей папке AppData. Он не передаётся никуда кроме вашего VPN сервера.

---

## 📜 Лицензия

MIT License - используйте свободно, на свой страх и риск.

## 🔗 Ссылки

- [Xray-core GitHub](https://github.com/XTLS/Xray-core)
- [VLESS Protocol Documentation](https://xtls.github.io/en/config/outbounds/vless.html)






