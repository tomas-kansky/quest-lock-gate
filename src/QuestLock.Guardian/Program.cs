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
        .links {{ margin-top: 28px; font-size: 13px; display: flex; justify-content: space-between; color: #64748b; }}
        .links a {{ color: #38bdf8; text-decoration: none; font-weight: 600; }}
        .links a:hover {{ text-decoration: underline; }}
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

        <div class=""links"">
            <span>Datum: {today}</span>
            <a href=""/admin"">⚙️ Správa pravidel</a>
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

// Administrační webový panel pro změnu pravidel (/admin)
app.MapGet("/admin", (ConfigManager cfg, StateManager state) =>
{
    var currentConfig = cfg.Load();
    var currentState = state.Load();
    var today = DateTime.Now.ToString("yyyy-MM-dd");
    var isCompleted = currentState.IsCompletedToday(today);
    var isLocked = currentState.IsLocked && !isCompleted;

    var ops = currentConfig.Math.AllowedOperations ?? new List<string>();
    bool hasAdd = ops.Contains("add");
    bool hasSub = ops.Contains("subtract");
    bool hasMul = ops.Contains("multiply");
    bool hasDiv = ops.Contains("divide");

    var html = $@"<!DOCTYPE html>
<html lang=""cs"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>QuestLock Gate – Správa pravidel</title>
    <style>
        * {{ box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }}
        body {{ background: #0f172a; color: #f8fafc; min-height: 100vh; padding: 24px; display: flex; justify-content: center; }}
        .container {{ width: 100%; max-width: 540px; }}
        .header {{ display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }}
        .header h1 {{ font-size: 24px; font-weight: 800; color: #ffffff; }}
        .header a {{ color: #38bdf8; text-decoration: none; font-size: 14px; font-weight: 600; }}
        .card {{ background: #1e293b; border-radius: 20px; padding: 24px; border: 1px solid #334155; margin-bottom: 20px; }}
        .card h2 {{ font-size: 18px; font-weight: 700; color: #e2e8f0; margin-bottom: 16px; border-bottom: 1px solid #334155; padding-bottom: 8px; }}
        .status-row {{ display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; font-size: 14px; color: #94a3b8; }}
        .status-val {{ font-weight: 700; color: #f8fafc; }}
        .badge {{ padding: 4px 12px; border-radius: 9999px; font-size: 13px; font-weight: 700; }}
        .badge.locked {{ background: #7f1d1d; color: #fecaca; }}
        .badge.unlocked {{ background: #14532d; color: #bbf7d0; }}
        .form-group {{ margin-bottom: 20px; }}
        label {{ display: block; font-size: 14px; font-weight: 600; color: #cbd5e1; margin-bottom: 6px; }}
        .desc {{ font-size: 12px; color: #94a3b8; margin-bottom: 8px; }}
        input[type=""number""], input[type=""text""] {{ width: 100%; padding: 12px 14px; border-radius: 12px; background: #0f172a; border: 1px solid #334155; color: #ffffff; font-size: 16px; font-weight: 600; outline: none; }}
        input[type=""number""]:focus, input[type=""text""]:focus {{ border-color: #3b82f6; }}
        .checkbox-grid {{ display: grid; grid-template-columns: 1fr 1fr; gap: 12px; margin-top: 8px; }}
        .checkbox-label {{ display: flex; align-items: center; background: #0f172a; padding: 12px; border-radius: 12px; border: 1px solid #334155; cursor: pointer; }}
        .checkbox-label input {{ margin-right: 10px; width: 18px; height: 18px; accent-color: #3b82f6; cursor: pointer; }}
        .btn-save {{ display: block; width: 100%; padding: 16px; border-radius: 14px; font-size: 17px; font-weight: 700; background: #2563eb; color: white; border: none; cursor: pointer; transition: background 0.2s; }}
        .btn-save:hover {{ background: #1d4ed8; }}
        .btn-unlock-quick {{ display: inline-block; padding: 10px 18px; border-radius: 10px; background: #16a34a; color: white; font-size: 14px; font-weight: 700; border: none; cursor: pointer; }}
        .msg {{ margin-top: 16px; padding: 12px; border-radius: 12px; font-size: 14px; display: none; text-align: center; }}
        .msg.show {{ display: block; }}
        .msg.success {{ background: #064e3b; color: #6ee7b7; border: 1px solid #059669; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>QuestLock Správa</h1>
            <a href=""/unlock"">← Odemknout z mobilu</a>
        </div>

        <div class=""card"">
            <h2>Aktuální stav počítače</h2>
            <div class=""status-row"">
                <span>Dnešní stav:</span>
                <span class=""badge {(isLocked ? "locked" : "unlocked")}"">{(isLocked ? "🔒 Zamčeno (čeká na úkoly)" : "✅ Odemčeno")}</span>
            </div>
            <div class=""status-row"">
                <span>Dnešní datum:</span>
                <span class=""status-val"">{today}</span>
            </div>
            <div style=""margin-top: 16px; text-align: right;"">
                <button class=""btn-unlock-quick"" onclick=""quickUnlock()"">🔓 Okamžitě odemknout teď</button>
            </div>
        </div>

        <div class=""card"">
            <h2>Nastavení matematických úloh</h2>

            <div class=""form-group"">
                <label for=""totalQuestions"">Počet příkladů na den</label>
                <div class=""desc"">Kolik správných odpovědí musí bratr spočítat k odemčení počítače.</div>
                <input type=""number"" id=""totalQuestions"" min=""1"" max=""50"" value=""{currentConfig.Math.TotalQuestions}"">
            </div>

            <div class=""form-group"">
                <label for=""maxNumber"">Maximální číslo (max_number)</label>
                <div class=""desc"">Horní mez pro sčítání (výsledek do max) a odčítání (až od max). Malá násobilka a dělení počítají do 100.</div>
                <input type=""number"" id=""maxNumber"" min=""10"" max=""1000"" value=""{currentConfig.Math.MaxNumber}"">
            </div>

            <div class=""form-group"">
                <label>Povolené početní operace</label>
                <div class=""desc"">Zaškrtněte operace, které se budou v denní dávce náhodně objevovat.</div>
                <div class=""checkbox-grid"">
                    <label class=""checkbox-label"">
                        <input type=""checkbox"" id=""opAdd"" {(hasAdd ? "checked" : "")}>
                        <span>Sčítání (+)</span>
                    </label>
                    <label class=""checkbox-label"">
                        <input type=""checkbox"" id=""opSub"" {(hasSub ? "checked" : "")}>
                        <span>Odčítání (-)</span>
                    </label>
                    <label class=""checkbox-label"">
                        <input type=""checkbox"" id=""opMul"" {(hasMul ? "checked" : "")}>
                        <span>Násobení (×)</span>
                    </label>
                    <label class=""checkbox-label"">
                        <input type=""checkbox"" id=""opDiv"" {(hasDiv ? "checked" : "")}>
                        <span>Dělení (:)</span>
                    </label>
                </div>
            </div>

            <div class=""form-group"">
                <label for=""quickKey"">Klíč pro rychlé odemčení (volitelný)</label>
                <div class=""desc"">Pokud je prázdný, kdokoliv na domácí Wi-Fi může otevřít /unlock bez hesla.</div>
                <input type=""text"" id=""quickKey"" placeholder=""Nechte prázdné pro odemčení bez hesla"" value=""{currentConfig.Server.QuickUnlockKey}"">
            </div>

            <button class=""btn-save"" onclick=""saveSettings()"">💾 Uložit nová pravidla</button>
            <div id=""saveMsg"" class=""msg""></div>
        </div>
    </div>

    <script>
        async function saveSettings() {{
            const msg = document.getElementById('saveMsg');
            const totalQuestions = parseInt(document.getElementById('totalQuestions').value) || 5;
            const maxNumber = parseInt(document.getElementById('maxNumber').value) || 100;
            const quickKey = document.getElementById('quickKey').value.trim();

            const ops = [];
            if (document.getElementById('opAdd').checked) ops.push('add');
            if (document.getElementById('opSub').checked) ops.push('subtract');
            if (document.getElementById('opMul').checked) ops.push('multiply');
            if (document.getElementById('opDiv').checked) ops.push('divide');

            if (ops.length === 0) {{
                alert('Musíte povolit alespoň jednu početní operaci!');
                return;
            }}

            const payload = {{
                version: 1,
                server: {{
                    port: 5001,
                    quick_unlock_key: quickKey
                }},
                rules: {{
                    daily_gatekeeper_enabled: true
                }},
                math: {{
                    total_questions: totalQuestions,
                    max_number: maxNumber,
                    allowed_operations: ops,
                    allow_negative_results: false
                }}
            }};

            try {{
                const resp = await fetch('/api/config', {{
                    method: 'PUT',
                    headers: {{ 'Content-Type': 'application/json' }},
                    body: JSON.stringify(payload)
                }});
                const data = await resp.json();

                if (resp.ok) {{
                    msg.className = 'msg show success';
                    msg.innerText = '✅ Pravidla byla úspěšně uložena a jsou ihned aktivní!';
                    setTimeout(() => {{ msg.className = 'msg'; }}, 4000);
                }} else {{
                    alert('Chyba při ukládání: ' + (data.message || 'Neznámá chyba'));
                }}
            }} catch (err) {{
                alert('Chyba spojení se službou: ' + err);
            }}
        }}

        async function quickUnlock() {{
            try {{
                const resp = await fetch('/api/action/unlock', {{ method: 'POST' }});
                const data = await resp.json();
                if (resp.ok) {{
                    alert('✅ ' + data.message);
                    location.reload();
                }} else {{
                    alert(data.message || 'Nepodařilo se odemknout.');
                }}
            }} catch (err) {{
                alert('Chyba: ' + err);
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
    var current = cfg.Load();
    // Zachováme neměnné věci jako admin_pin_hash pokud nebyly zaslány
    if (newConfig.Math != null) current.Math = newConfig.Math;
    if (newConfig.Rules != null) current.Rules = newConfig.Rules;
    if (newConfig.Server != null)
    {
        current.Server.QuickUnlockKey = newConfig.Server.QuickUnlockKey;
    }

    cfg.Save(current);
    return Results.Ok(new { success = true, message = "Konfigurace byla uložena." });
});

app.Run();
