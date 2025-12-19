# 🔐 Проверка безопасности кода VlessVPN

Этот документ поможет вам самостоятельно проверить, что код безопасен.

## ✅ Чек-лист безопасности

### 1. Проверка сетевых подключений

Приложение создаёт подключения ТОЛЬКО к:
- `127.0.0.1` (localhost) - для локального прокси
- Вашему VPN серверу (адрес который вы указали)

**Как проверить:**
```powershell
# Запустите приложение и посмотрите соединения
netstat -an | findstr "xray"
```

Вы должны увидеть:
- `127.0.0.1:10808` - SOCKS5 прокси (LISTENING)
- `127.0.0.1:10809` - HTTP прокси (LISTENING)
- Соединение с вашим VPN сервером (ESTABLISHED)

---

### 2. Проверка файлов

Приложение создаёт файлы ТОЛЬКО в двух местах:

| Расположение | Содержимое |
|-------------|-----------|
| `%AppData%\VlessVPN\settings.json` | Настройки и список серверов |
| `%AppData%\VlessVPN\xray-config.json` | Временная конфигурация Xray |

**Как проверить:**
```powershell
# Откройте папку с настройками
explorer $env:APPDATA\VlessVPN
```

---

### 3. Проверка реестра

Приложение изменяет ТОЛЬКО эти ключи реестра (при включённом System Proxy):

```
HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Internet Settings
  - ProxyEnable: 0 или 1
  - ProxyServer: 127.0.0.1:10809
```

**Как проверить:**
```powershell
# До подключения
Get-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings" | Select ProxyEnable, ProxyServer

# Подключитесь к VPN, затем снова проверьте

# После отключения ProxyEnable должен быть 0
```

---

### 4. Проверка процессов

При работе запущены только:
- `VlessVPN.exe` - само приложение
- `xray.exe` - движок VPN (официальный open-source)

**Как проверить:**
```powershell
Get-Process | Where-Object {$_.ProcessName -match "Vless|xray"}
```

---

### 5. Проверка исходного кода

Ключевые файлы для проверки:

| Файл | Что проверить |
|------|--------------|
| `XrayService.cs` | Как запускается xray.exe |
| `ConfigurationService.cs` | Куда сохраняются данные |
| `NativeMethods` в `XrayService.cs` | Какие Windows API вызываются |

#### Единственный нативный вызов:

```csharp
// wininet.dll - стандартная библиотека Windows
InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
```

Это **стандартный способ** обновить настройки прокси. Используется всеми VPN клиентами.

---

### 6. Xray-core

Приложение использует [Xray-core](https://github.com/XTLS/Xray-core) - это:
- ✅ Open-source проект с 20k+ звёзд на GitHub
- ✅ Регулярно аудитируется сообществом
- ✅ Скачивается вами напрямую с GitHub (не включён в приложение)

**Проверьте хэш файла:**
```powershell
Get-FileHash xray-core\xray.exe -Algorithm SHA256
```

Сравните с хэшем на странице [Releases](https://github.com/XTLS/Xray-core/releases).

---

## 🔍 Как проверить код самостоятельно

### Шаг 1: Читайте комментарии
Каждый файл содержит подробные комментарии на русском языке.

### Шаг 2: Ищите "опасные" паттерны
```powershell
# Поиск HTTP запросов (их не должно быть кроме xray)
Select-String -Path *.cs -Pattern "HttpClient|WebRequest" -Recurse

# Поиск записи файлов
Select-String -Path *.cs -Pattern "File\.Write|StreamWriter" -Recurse

# Поиск работы с реестром
Select-String -Path *.cs -Pattern "Registry" -Recurse
```

### Шаг 3: Соберите из исходников
```powershell
dotnet build
```

Вы точно знаете что запускаете код, который видите.

---

## ⚠️ Что может быть потенциально опасно

### 1. Xray-core
Вы скачиваете его отдельно. Убедитесь что скачиваете с официального GitHub.

### 2. VPN сервер
Весь ваш трафик идёт через сервер. Доверяйте только проверенным серверам.

### 3. Системный прокси
Когда включён - весь HTTP/HTTPS трафик идёт через VPN. При ошибке может быть утечка.

---

## 🛡️ Рекомендации

1. **Отключите System Proxy** если хотите использовать VPN только для отдельных приложений
2. **Проверяйте xray.exe** при каждом обновлении
3. **Используйте антивирус** - он предупредит о подозрительной активности
4. **Смотрите логи** в приложении - там видно что происходит

---

## 📞 Если нашли проблему

Код открытый - вы можете:
1. Исправить самостоятельно
2. Сообщить об уязвимости
3. Не использовать приложение

Безопасность - это ваша ответственность. Проверяйте всё сами!






