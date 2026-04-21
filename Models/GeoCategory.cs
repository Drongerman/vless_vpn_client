/*
 * GeoCategory.cs - Пресеты маршрутизации (Bypass / Force Proxy)
 *
 * Категория = набор правил geosite:/geoip:/domain:.
 * При клике по чипу в UI все правила категории добавляются в список (или удаляются).
 *
 * Имена geosite-категорий соответствуют Loyalsoldier/v2ray-rules-dat
 * (https://github.com/Loyalsoldier/v2ray-rules-dat) - эти файлы геоданных
 * уже лежат в xray-core\geosite.dat и xray-core\geoip.dat. Для отсутствующих
 * в dat категорий Xray выведет warning, а domain:-правила-фолбэки всё равно
 * поймают основной трафик.
 */

namespace VlessVPN.Models;

public sealed record GeoCategory(string Key, string DisplayName, string Emoji, string[] Rules);

public static class GeoCategories
{
    // === BYPASS: категории, которые идут напрямую (мимо VPN) ===
    public static readonly GeoCategory[] Bypass = new[]
    {
        new GeoCategory("ru-all", "Россия (сайты)", "🇷🇺", new[]
        {
            "geosite:category-ru",
            "domain:.ru", "domain:.su", "domain:.рф",
            "domain:.москва", "domain:.дети", "domain:.tatar",
        }),
        new GeoCategory("ru-ip", "Российские IP", "🌐", new[]
        {
            "geoip:ru",
        }),
        new GeoCategory("ru-gov", "Госуслуги / банки РФ", "🏛️", new[]
        {
            "geosite:category-gov-ru",
            "domain:gosuslugi.ru", "domain:mos.ru", "domain:nalog.ru",
            "domain:government.ru", "domain:kremlin.ru", "domain:esia.gosuslugi.ru",
            "domain:sber.ru", "domain:sberbank.ru", "domain:tbank.ru",
            "domain:tinkoff.ru", "domain:vtb.ru", "domain:alfabank.ru",
            "domain:raiffeisen.ru", "domain:gazprombank.ru",
        }),
        new GeoCategory("ru-media", "СМИ / стриминг РФ", "🎬", new[]
        {
            "geosite:category-media-ru",
            "domain:rbc.ru", "domain:ria.ru", "domain:tass.ru",
            "domain:kommersant.ru", "domain:lenta.ru", "domain:vedomosti.ru",
            "domain:kinopoisk.ru", "domain:ivi.ru", "domain:okko.tv",
            "domain:more.tv", "domain:wink.ru", "domain:premier.one",
            "domain:rutube.ru",
        }),
        new GeoCategory("ru-shops", "Маркетплейсы РФ", "🛒", new[]
        {
            "domain:ozon.ru", "domain:ozoncdn.com",
            "domain:wildberries.ru", "domain:wb.ru", "domain:wbstatic.net",
            "domain:avito.ru", "domain:lamoda.ru", "domain:aliexpress.ru",
            "domain:dns-shop.ru", "domain:mvideo.ru", "domain:citilink.ru",
        }),
        new GeoCategory("yandex", "Яндекс", "🔴", new[]
        {
            "geosite:yandex",
            "domain:yandex.ru", "domain:yandex.com", "domain:yandex.net",
            "domain:yandex.by", "domain:yandex.kz", "domain:yandex.uz",
            "domain:ya.ru", "domain:yastatic.net", "domain:yandexcloud.net",
        }),
        new GeoCategory("vk-mail", "VK / Mail.ru / OK", "🟦", new[]
        {
            "geosite:vk", "geosite:mail-ru",
            "domain:vk.com", "domain:vk.me", "domain:vk.cc",
            "domain:vkuser.net", "domain:vkuseraudio.net", "domain:vkuservideo.net",
            "domain:userapi.com",
            "domain:mail.ru", "domain:mymail.ru", "domain:imgsmail.ru",
            "domain:ok.ru", "domain:odkl.ru", "domain:mycdn.me",
            "domain:dzen.ru",
        }),
        new GeoCategory("telegram", "Telegram", "📨", new[]
        {
            "geosite:telegram",
            "domain:telegram.org", "domain:t.me", "domain:telegram.me",
            "domain:telegra.ph", "domain:tdesktop.com", "domain:telesco.pe",
        }),
        new GeoCategory("private", "Локальная сеть", "🏠", new[]
        {
            "geoip:private",
        }),
    };

    // === FORCE PROXY: категории, которые ВСЕГДА идут через VPN ===
    public static readonly GeoCategory[] ForceProxy = new[]
    {
        new GeoCategory("google", "Google", "🔍", new[]
        {
            "geosite:google",
            "domain:google.com", "domain:google.ru", "domain:gstatic.com",
            "domain:googleapis.com", "domain:googleusercontent.com",
            "domain:googleadservices.com", "domain:googlesyndication.com",
            "domain:googletagmanager.com", "domain:google-analytics.com",
            "domain:ggpht.com",
        }),
        new GeoCategory("youtube", "YouTube", "▶️", new[]
        {
            "geosite:youtube",
            "domain:youtube.com", "domain:youtube.ru", "domain:youtu.be",
            "domain:ytimg.com", "domain:googlevideo.com",
            "domain:youtube-nocookie.com",
        }),
        new GeoCategory("apple", "Apple", "🍎", new[]
        {
            "geosite:apple",
            "domain:apple.com", "domain:apple-cloudkit.com", "domain:icloud.com",
            "domain:mzstatic.com", "domain:cdn-apple.com", "domain:apps.apple.com",
        }),
        new GeoCategory("microsoft", "Microsoft", "🪟", new[]
        {
            "geosite:microsoft",
            "domain:microsoft.com", "domain:microsoftonline.com",
            "domain:live.com", "domain:office.com", "domain:office365.com",
            "domain:bing.com", "domain:msn.com", "domain:windows.com",
            "domain:onedrive.live.com", "domain:sharepoint.com",
            "domain:xbox.com", "domain:xboxlive.com",
        }),
        new GeoCategory("meta", "Meta / Facebook", "📘", new[]
        {
            "geosite:facebook",
            "domain:facebook.com", "domain:facebook.net", "domain:fbcdn.net",
            "domain:fb.com", "domain:fb.me", "domain:meta.com",
            "domain:threads.net", "domain:whatsapp.com", "domain:whatsapp.net",
        }),
        new GeoCategory("instagram", "Instagram", "📷", new[]
        {
            "geosite:instagram",
            "domain:instagram.com", "domain:cdninstagram.com",
        }),
        new GeoCategory("anthropic", "Anthropic / Claude", "🤖", new[]
        {
            "geosite:anthropic",
            "domain:anthropic.com", "domain:claude.ai", "domain:claude.com",
        }),
        new GeoCategory("openai", "OpenAI / ChatGPT", "💬", new[]
        {
            "geosite:openai",
            "domain:openai.com", "domain:chatgpt.com",
            "domain:oaiusercontent.com", "domain:oaistatic.com",
        }),
        new GeoCategory("twitter-x", "X (Twitter)", "🐦", new[]
        {
            "geosite:twitter",
            "domain:twitter.com", "domain:x.com", "domain:twimg.com", "domain:t.co",
        }),
        new GeoCategory("discord", "Discord", "🎮", new[]
        {
            "geosite:discord",
            "domain:discord.com", "domain:discord.gg",
            "domain:discordapp.com", "domain:discordapp.net", "domain:discord.media",
        }),
        new GeoCategory("github", "GitHub", "🐙", new[]
        {
            "geosite:github",
            "domain:github.com", "domain:github.io",
            "domain:githubusercontent.com", "domain:githubassets.com",
        }),
        new GeoCategory("netflix", "Netflix", "🎞️", new[]
        {
            "geosite:netflix",
            "domain:netflix.com", "domain:nflxvideo.net",
            "domain:nflximg.net", "domain:nflxext.com",
        }),
        new GeoCategory("spotify", "Spotify", "🎵", new[]
        {
            "geosite:spotify",
            "domain:spotify.com", "domain:spotifycdn.com", "domain:scdn.co",
        }),
    };
}
