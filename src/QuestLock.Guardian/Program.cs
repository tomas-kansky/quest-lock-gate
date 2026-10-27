using Microsoft.AspNetCore.Mvc;
using QuestLock.Core.Models;
using QuestLock.Core.Storage;
using QuestLock.Guardian.Services;

var builder = WebApplication.CreateBuilder(args);

// Povolit běh jako Windows Service i běžnou konzolovou aplikaci
builder.Host.UseWindowsService();

// Konfigurace Kestrelu na port 5001 pro celou lokální síť
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5001);
});

// Registrace core služeb
var configManager = new ConfigManager();
var stateManager = new StateManager();

builder.Services.AddSingleton(configManager);
builder.Services.AddSingleton(stateManager);
builder.Services.AddSingleton<IpcServerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<IpcServerService>());

var app = builder.Build();

// Přesměrování z kořene na /unlock
app.MapGet("/", () => Results.Redirect("/unlock"));

// 1-Click mobilní webové rozhraní pro rodiče / mamku
app.MapGet("/unlock", (
    [FromQuery] string? key,
    [FromQuery] bool? auto,
    ConfigManager cfg,
    StateManager state,
    IpcServerService ipc) =>
{
    var currentConfig = cfg.Load();
    var currentState = state.Load();
    var today = DateTime.Now.ToString("yyyy-MM-dd");
    var isCompleted = currentState.IsCompletedToday(today);
    var isLocked = currentState.IsLocked && !isCompleted;

    // Pokud je zadán auto=true a klíč souhlasí (nebo není klíč vyžadován), rovnou odemknout
    if (auto == true && isLocked)
    {
        if (string.IsNullOrEmpty(currentConfig.Server.QuickUnlockKey) || currentConfig.Server.QuickUnlockKey == key)
        {
            state.MarkUnlockedByOverride("MAMKA_MOBILE_AUTO");
            _ = ipc.BroadcastUnlockAsync("MAMKA_MOBILE_AUTO");
            isLocked = false;
        }
    }

    var html = $@"<!DOCTYPE html>
<html lang=""cs"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no"">
    <title>QuestLock Gate – Odemčení</title>
    <style>
        * {{ box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }}
        body {{ background: #0f172a; color: #f8fafc; display: flex; align-items: center; justify-content: center; min-height: 100vh; padding: 20px; }}
        .card {{ background: #1e293b; border-radius: 24px; padding: 32px 24px; width: 100%; max-width: 420px; text-align: center; box-shadow: 0 20px 25px -5px rgba(0,0,0,0.5); border: 1px solid #334155; }}
        .badge {{ display: inline-block; padding: 8px 16px; border-radius: 9999px; font-weight: 700; font-size: 14px; margin-bottom: 24px; text-transform: uppercase; letter-spacing: 0.05em; }}
        .badge.locked {{ background: #7f1d1d; color: #fecaca; border: 1px solid #dc2626; }}
        .badge.unlocked {{ background: #14532d; color: #bbf7d0; border: 1px solid #16a34a; }}
        h1 {{ font-size: 26px; font-weight: 800; margin-bottom: 8px; color: #ffffff; }}
        p {{ color: #94a3b8; font-size: 15px; margin-bottom: 32px; line-height: 1.5; }}
        .btn {{ display: block; width: 100%; padding: 20px; border-radius: 18px; font-size: 20px; font-weight: 700; border: none; cursor: pointer; transition: all 0.2s ease; text-decoration: none; }}
        .btn-unlock {{ background: linear-gradient(135deg, #22c55e, #16a34a); color: white; box-shadow: 0 10px 15px -3px rgba(34, 197, 94, 0.4); }}
        .btn-unlock:active {{ transform: scale(0.97); }}
        .btn-locked {{ background: #334155; color: #64748b; cursor: default; }}
        .msg {{ margin-top: 24px; padding: 12px; border-radius: 12px; font-size: 14px; display: none; }}
        .msg.show {{ display: block; }}
        .msg.success {{ background: #064e3b; color: #6ee7b7; border: 1px solid #059669; }}
        .footer {{ margin-top: 28px; font-size: 12px; color: #64748b; }}
    </style>
</head>
<body>
    <div class=""card"">
        <div id=""statusBadge"" class=""badge {(isLocked ? "locked" : "unlocked")}"">
            {(isLocked ? "🔒 Počítač je zamčený" : "✅ Počítač je odemčený")}
        </div>
        <h1>QuestLock Gate</h1>
        <p id=""statusDesc"">
            {(isLocked ? "Denní výzva čeká na vyřešení. Kliknutím níže můžete počítač na dálku okamžitě odemknout." : "Dnešní úkoly jsou hotové nebo byl počítač uvolněn. Hraní je povoleno.")}
        </p>

        <button id=""unlockBtn"" class=""btn {(isLocked ? "btn-unlock" : "btn-locked")}"" {(isLocked ? "" : "disabled")} onclick=""triggerUnlock()"">
            {(isLocked ? "🔓 Odemknout počítač teď" : "Počítač je volný")}
        </button>

        <div id=""resultMsg"" class=""msg""></div>

        <div class=""footer"">
            Dnešní datum: {today} • QuestLock LAN Gate
        </div>
    </div>

    <script>
        async function triggerUnlock() {{
            const btn = document.getElementById('unlockBtn');
            const msg = document.getElementById('resultMsg');
            const badge = document.getElementById('statusBadge');
            const desc = document.getElementById('statusDesc');

            btn.disabled = true;
            btn.innerText = 'Odesílám signál...';

            try {{
                const urlParams = new URLSearchParams(window.location.search);
                const key = urlParams.get('key') || '';
                const resp = await fetch('/api/action/unlock?key=' + encodeURIComponent(key), {{ method: 'POST' }});
                const data = await resp.json();

                if (resp.ok && data.success) {{
                    badge.className = 'badge unlocked';
                    badge.innerText = '✅ Počítač je odemčený';
                    desc.innerText = 'Počítač byl úspěšně na dálku odemčen. Zámek na obrazovce se ihned stáhl.';
                    btn.className = 'btn btn-locked';
                    btn.innerText = 'Počítač je volný';

                    msg.className = 'msg show success';
                    msg.innerText = '✅ ' + data.message;
                }} else {{
                    btn.disabled = false;
                    btn.innerText = '🔓 Odemknout počítač teď';
                    alert(data.message || 'Nepodařilo se odemknout.');
                }}
            }} catch (err) {{
                btn.disabled = false;
                btn.innerText = '🔓 Zkusit znovu';
                alert('Chyba spojení s počítačem: ' + err);
            }}
        }}
    </script>
</body>
</html>";

    return Results.Content(html, "text/html; charset=utf-8");
});

// API Endpoint pro vzdálené odemčení (POST nebo GET)
var unlockActionHandler = async (
    [FromQuery] string? key,
    ConfigManager cfg,
    StateManager state,
    IpcServerService ipc) =>
{
    var currentConfig = cfg.Load();
    var requiredKey = currentConfig.Server.QuickUnlockKey;

    if (!string.IsNullOrEmpty(requiredKey) && requiredKey != key)
    {
        return Results.Json(new { success = false, message = "Neplatný autorizační klíč." }, statusCode: 403);
    }

    state.MarkUnlockedByOverride("MAMKA_WEB_ACTION");
    await ipc.BroadcastUnlockAsync("MAMKA_WEB_ACTION");

    return Results.Ok(new { success = true, message = "Počítač byl na dálku úspěšně odemčen!" });
};

app.MapPost("/api/action/unlock", unlockActionHandler);
app.MapGet("/api/action/unlock", unlockActionHandler);

// Stavový endpoint
app.MapGet("/api/status", (StateManager state, ConfigManager cfg) =>
{
    var s = state.Load();
    var c = cfg.Load();
    var today = DateTime.Now.ToString("yyyy-MM-dd");

    return Results.Ok(new
    {
        is_locked = s.IsLocked,
        last_completed_date = s.LastCompletedDate,
        completed_today = s.IsCompletedToday(today),
        server_time = DateTime.UtcNow,
        rules = c.Rules,
        math = c.Math
    });
});

// Konfigurace
app.MapGet("/api/config", (ConfigManager cfg) =>
{
    return Results.Ok(cfg.Load());
});

app.MapPut("/api/config", ([FromBody] AppConfig newConfig, ConfigManager cfg) =>
{
    cfg.Save(newConfig);
    return Results.Ok(new { success = true, message = "Konfigurace byla uložena." });
});

app.Run();
