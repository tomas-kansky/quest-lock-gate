using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting.WindowsServices;
using QuestLock.Core.Models;
using QuestLock.Core.Storage;
using QuestLock.Guardian.Services;

var options = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default
};

var builder = WebApplication.CreateBuilder(options);

// Povolit beh jako Windows Service i beznou konzolovou aplikaci
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

// Přesměrování výchozích cest
app.MapGet("/", () => Results.Redirect("/administration"));
app.MapGet("/admin", () => Results.Redirect("/settings"));
app.MapGet("/unlock", () => Results.Redirect("/administration"));

// ==========================================
// 1. HLAVNÍ STRÁNKA ADMINISTRACE (/administration)
// ==========================================
app.MapGet("/administration", (ConfigManager cfg, StateManager state, HttpRequest request) =>
{
    var currentConfig = cfg.Load();
    var currentState = state.Load();
    var today = DateTime.Now.ToString("yyyy-MM-dd");
    var isCompleted = currentState.IsCompletedToday(today);
    var isLocked = currentState.IsLocked && !isCompleted;

    var host = request.Host.Value;
    var dayOffUrl = $"http://{host}/day-off";
    var studyNowUrl = $"http://{host}/study-now";

    var opsList = new List<string>();
    var ops = currentConfig.Math.AllowedOperations ?? new List<string>();
    if (ops.Contains("add")) opsList.Add("Sčítání");
    if (ops.Contains("subtract")) opsList.Add("Odčítání");
    if (ops.Contains("multiply")) opsList.Add("Násobení");
    if (ops.Contains("divide")) opsList.Add("Dělení");
    var opsFormatted = opsList.Count > 0 ? string.Join(", ", opsList) : "Žádné";

    var html = $@"<!DOCTYPE html>
<html lang=""cs"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no"">
    <title>QuestLock Gate – Administrace</title>
    <style>
        * {{ box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }}
        body {{ background: #0f172a; color: #f8fafc; min-height: 100vh; padding: 20px; display: flex; justify-content: center; }}
        .container {{ width: 100%; max-width: 500px; }}
        .header {{ text-align: center; margin-bottom: 24px; padding-top: 10px; }}
        .header h1 {{ font-size: 26px; font-weight: 800; color: #ffffff; margin-bottom: 4px; }}
        .header p {{ color: #94a3b8; font-size: 14px; }}
        
        .card {{ background: #1e293b; border-radius: 24px; padding: 24px; border: 1px solid #334155; margin-bottom: 20px; box-shadow: 0 10px 25px -5px rgba(0,0,0,0.4); }}
        .card h2 {{ font-size: 17px; font-weight: 700; color: #e2e8f0; margin-bottom: 16px; border-bottom: 1px solid #334155; padding-bottom: 8px; text-transform: uppercase; letter-spacing: 0.05em; }}
        
        .status-box {{ display: flex; justify-content: space-between; align-items: center; background: #0f172a; border-radius: 16px; padding: 16px; border: 1px solid #334155; }}
        .status-badge {{ padding: 8px 16px; border-radius: 9999px; font-size: 14px; font-weight: 800; text-transform: uppercase; letter-spacing: 0.05em; }}
        .status-badge.locked {{ background: #7f1d1d; color: #fecaca; border: 1px solid #dc2626; }}
        .status-badge.unlocked {{ background: #14532d; color: #bbf7d0; border: 1px solid #16a34a; }}
        
        .action-btn {{ display: flex; align-items: center; justify-content: center; width: 100%; padding: 18px 20px; border-radius: 18px; font-size: 18px; font-weight: 700; border: none; cursor: pointer; transition: all 0.2s ease; text-decoration: none; margin-bottom: 12px; }}
        .action-btn:active {{ transform: scale(0.98); }}
        .btn-dayoff {{ background: linear-gradient(135deg, #16a34a, #15803d); color: white; box-shadow: 0 10px 15px -3px rgba(22, 163, 74, 0.4); }}
        .btn-studynow {{ background: linear-gradient(135deg, #2563eb, #1d4ed8); color: white; box-shadow: 0 10px 15px -3px rgba(37, 99, 235, 0.4); }}
        .btn-settings {{ background: #334155; color: #f8fafc; font-size: 16px; padding: 15px; border: 1px solid #475569; }}
        .btn-settings:hover {{ background: #475569; }}

        .action-desc {{ font-size: 12px; color: #94a3b8; margin-bottom: 14px; line-height: 1.4; padding: 0 4px; }}
        
        .url-box {{ background: #0f172a; border-radius: 10px; padding: 8px 12px; font-size: 11px; color: #64748b; display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; border: 1px dashed #334155; }}
        .url-box a {{ color: #38bdf8; text-decoration: none; word-break: break-all; margin-right: 8px; }}
        .url-copy {{ background: #334155; color: #cbd5e1; border: none; padding: 4px 8px; border-radius: 6px; font-size: 11px; cursor: pointer; }}

        .rules-grid {{ display: grid; grid-template-columns: 1fr 1fr; gap: 10px; margin-bottom: 16px; }}
        .rule-item {{ background: #0f172a; padding: 12px; border-radius: 12px; border: 1px solid #334155; }}
        .rule-lbl {{ font-size: 11px; color: #94a3b8; text-transform: uppercase; margin-bottom: 4px; }}
        .rule-val {{ font-size: 15px; font-weight: 700; color: #f8fafc; }}

        .msg {{ padding: 14px; border-radius: 14px; font-size: 14px; font-weight: 600; text-align: center; display: none; margin-bottom: 16px; }}
        .msg.show {{ display: block; }}
        .msg.success {{ background: #064e3b; color: #6ee7b7; border: 1px solid #059669; }}
        .msg.info {{ background: #1e3a8a; color: #93c5fd; border: 1px solid #2563eb; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>QuestLock Gate</h1>
            <p>Centrální správa a dohled nad herním počítačem</p>
        </div>

        <div id=""feedbackMsg"" class=""msg""></div>

        <div class=""card"">
            <h2>Aktuální stav počítače</h2>
            <div class=""status-box"">
                <div>
                    <div style=""font-size: 12px; color: #94a3b8;"">Dnešní datum: {today}</div>
                    <div id=""statusText"" style=""font-size: 16px; font-weight: 700; margin-top: 4px;"">
                        {(isLocked ? "Čeká na splnění úloh" : "Počítač je volný k hraní")}
                    </div>
                </div>
                <div id=""statusBadge"" class=""status-badge {(isLocked ? "locked" : "unlocked")}"">
                    {(isLocked ? "🔒 Zamčeno" : "✅ Odemčeno")}
                </div>
            </div>
        </div>

        <div class=""card"">
            <h2>Rychlé akce zamykání</h2>

            <!-- Tlačítko Den volna -->
            <button class=""action-btn btn-dayoff"" onclick=""executeAction('unlock', 'Den volna byl úspěšně udělen! Počítač je okamžitě odemčen.')"">
                🏖️ Udělit den volna
            </button>
            <div class=""action-desc"">Zruší dnešní příklady na počítači a ihned uvolní přístup k hrám.</div>
            <div class=""url-box"">
                <a href=""{dayOffUrl}"" target=""_blank"">{dayOffUrl}</a>
                <button class=""url-copy"" onclick=""copyText('{dayOffUrl}')"">Kopírovat</button>
            </div>

            <!-- Tlačítko Okamžité studium -->
            <button class=""action-btn btn-studynow"" onclick=""executeAction('lock', 'Okamžité studium spuštěno! Na počítači se právě zobrazily příklady.')"">
                ⚡ Okamžité studium
            </button>
            <div class=""action-desc"">Vyvolá novou sadu příkladů a uzamkne obrazovku i v případě, že bratr dnes již úkoly splnil.</div>
            <div class=""url-box"">
                <a href=""{studyNowUrl}"" target=""_blank"">{studyNowUrl}</a>
                <button class=""url-copy"" onclick=""copyText('{studyNowUrl}')"">Kopírovat</button>
            </div>
        </div>

        <div class=""card"">
            <h2>Aktivní pravidla úloh</h2>
            <div class=""rules-grid"">
                <div class=""rule-item"">
                    <div class=""rule-lbl"">Příkladů na den</div>
                    <div class=""rule-val"">{currentConfig.Math.TotalQuestions} otázek</div>
                </div>
                <div class=""rule-item"">
                    <div class=""rule-lbl"">Max. číslo</div>
                    <div class=""rule-val"">do {currentConfig.Math.MaxNumber}</div>
                </div>
            </div>
            <div class=""rule-item"" style=""margin-bottom: 16px;"">
                <div class=""rule-lbl"">Povolené operace</div>
                <div class=""rule-val"">{opsFormatted}</div>
            </div>

            <a href=""/settings"" class=""action-btn btn-settings"">
                ⚙️ Upravit nastavení pravidel (/settings)
            </a>
        </div>
    </div>

    <script>
        async function executeAction(action, successMessage) {{
            const msg = document.getElementById('feedbackMsg');
            const badge = document.getElementById('statusBadge');
            const statusText = document.getElementById('statusText');

            try {{
                const resp = await fetch('/api/action/' + action, {{ method: 'POST' }});
                const data = await resp.json();

                if (resp.ok && data.success) {{
                    msg.className = 'msg show ' + (action === 'unlock' ? 'success' : 'info');
                    msg.innerText = '✅ ' + successMessage;

                    if (action === 'unlock') {{
                        badge.className = 'status-badge unlocked';
                        badge.innerText = '✅ Odemčeno';
                        statusText.innerText = 'Počítač je volný k hraní';
                    }} else {{
                        badge.className = 'status-badge locked';
                        badge.innerText = '🔒 Zamčeno';
                        statusText.innerText = 'Čeká na splnění úloh';
                    }}

                    setTimeout(() => {{ msg.className = 'msg'; }}, 5000);
                }} else {{
                    alert(data.message || 'Akci se nepodařilo provést.');
                }}
            }} catch (err) {{
                alert('Chyba spojení s počítačem: ' + err);
            }}
        }}

        function copyText(txt) {{
            navigator.clipboard.writeText(txt).then(() => {{
                alert('Odkaz byl zkopírován do schránky: ' + txt);
            }}).catch(() => {{
                prompt('Zkopírujte si odkaz:', txt);
            }});
        }}
    </script>
</body>
</html>";

    return Results.Content(html, "text/html; charset=utf-8");
});

// ==========================================
// 2. NASTAVENÍ PRAVIDEL (/settings)
// ==========================================
app.MapGet("/settings", (ConfigManager cfg, StateManager state) =>
{
    var currentConfig = cfg.Load();
    var ops = currentConfig.Math.AllowedOperations ?? new List<string>();
    bool hasAdd = ops.Contains("add");
    bool hasSub = ops.Contains("subtract");
    bool hasMul = ops.Contains("multiply");
    bool hasDiv = ops.Contains("divide");

    var html = $@"<!DOCTYPE html>
<html lang=""cs"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no"">
    <title>QuestLock Gate – Nastavení pravidel</title>
    <style>
        * {{ box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }}
        body {{ background: #0f172a; color: #f8fafc; min-height: 100vh; padding: 20px; display: flex; justify-content: center; }}
        .container {{ width: 100%; max-width: 500px; }}
        .header {{ display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; padding-top: 10px; }}
        .header h1 {{ font-size: 22px; font-weight: 800; color: #ffffff; }}
        .header a {{ color: #38bdf8; text-decoration: none; font-size: 14px; font-weight: 600; }}
        .card {{ background: #1e293b; border-radius: 24px; padding: 24px; border: 1px solid #334155; margin-bottom: 20px; box-shadow: 0 10px 25px -5px rgba(0,0,0,0.4); }}
        .card h2 {{ font-size: 17px; font-weight: 700; color: #e2e8f0; margin-bottom: 16px; border-bottom: 1px solid #334155; padding-bottom: 8px; text-transform: uppercase; letter-spacing: 0.05em; }}
        .form-group {{ margin-bottom: 20px; }}
        label {{ display: block; font-size: 14px; font-weight: 600; color: #cbd5e1; margin-bottom: 6px; }}
        .desc {{ font-size: 12px; color: #94a3b8; margin-bottom: 8px; }}
        input[type=""number""], input[type=""text""] {{ width: 100%; padding: 14px; border-radius: 14px; background: #0f172a; border: 1px solid #334155; color: #ffffff; font-size: 16px; font-weight: 600; outline: none; }}
        input[type=""number""]:focus, input[type=""text""]:focus {{ border-color: #3b82f6; }}
        .checkbox-grid {{ display: grid; grid-template-columns: 1fr 1fr; gap: 12px; margin-top: 8px; }}
        .checkbox-label {{ display: flex; align-items: center; background: #0f172a; padding: 12px; border-radius: 12px; border: 1px solid #334155; cursor: pointer; }}
        .checkbox-label input {{ margin-right: 10px; width: 18px; height: 18px; accent-color: #3b82f6; cursor: pointer; }}
        .btn-save {{ display: block; width: 100%; padding: 16px; border-radius: 16px; font-size: 18px; font-weight: 700; background: #2563eb; color: white; border: none; cursor: pointer; transition: background 0.2s; }}
        .btn-save:hover {{ background: #1d4ed8; }}
        .msg {{ margin-top: 16px; padding: 12px; border-radius: 12px; font-size: 14px; display: none; text-align: center; }}
        .msg.show {{ display: block; }}
        .msg.success {{ background: #064e3b; color: #6ee7b7; border: 1px solid #059669; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Správa pravidel</h1>
            <a href=""/administration"">← Zpět do administrace</a>
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
                <div class=""desc"">Zaškrtněte operace, které se budou v denní dávce náhodně střídat.</div>
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
                <label for=""quickKey"">Bezpečnostní klíč (volitelný)</label>
                <div class=""desc"">Pokud je prázdný, kdokoliv na domácí Wi-Fi může provádět akce bez hesla.</div>
                <input type=""text"" id=""quickKey"" placeholder=""Nechte prázdné pro volný přístup"" value=""{currentConfig.Server.QuickUnlockKey}"">
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
    </script>
</body>
</html>";

    return Results.Content(html, "text/html; charset=utf-8");
});

// ==========================================
// 3. PŘÍMÉ ODKAZY PRO URL KLIKNUTÍ (DAY-OFF & STUDY-NOW)
// ==========================================

// Přímý odkaz pro udělení Dne volna: GET /day-off nebo /action/day-off
var handleDayOffDirect = async (
    [FromQuery] string? key,
    ConfigManager cfg,
    StateManager state,
    IpcServerService ipc) =>
{
    var currentConfig = cfg.Load();
    if (!string.IsNullOrEmpty(currentConfig.Server.QuickUnlockKey) && currentConfig.Server.QuickUnlockKey != key)
    {
        return Results.Content("<h1>403 – Neplatný bezpečnostní klíč</h1>", "text/html; charset=utf-8");
    }

    state.MarkUnlockedByOverride("DEN_VOLNA_URL");
    await ipc.BroadcastUnlockAsync("DEN_VOLNA_URL");

    var html = @"<!DOCTYPE html>
<html lang=""cs"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Den volna aktivován</title>
    <style>
        * { box-sizing: border-box; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }
        body { background: #0f172a; color: white; display: flex; align-items: center; justify-content: center; min-height: 100vh; padding: 20px; }
        .card { background: #1e293b; border-radius: 24px; padding: 40px 24px; max-width: 420px; text-align: center; border: 1px solid #16a34a; box-shadow: 0 20px 25px -5px rgba(0,0,0,0.5); }
        .icon { font-size: 64px; margin-bottom: 16px; }
        h1 { font-size: 24px; font-weight: 800; margin-bottom: 8px; color: #4ade80; }
        p { color: #94a3b8; font-size: 15px; margin-bottom: 24px; line-height: 1.5; }
        .btn { display: inline-block; padding: 14px 28px; background: #334155; color: white; border-radius: 14px; text-decoration: none; font-weight: 700; font-size: 15px; }
    </style>
</head>
<body>
    <div class=""card"">
        <div class=""icon"">🏖️</div>
        <h1>Den volna aktivován!</h1>
        <p>Příklady na počítači byly zrušeny a zámek obrazovky se okamžitě stáhl. Počítač je volný k hraní.</p>
        <a href=""/administration"" class=""btn"">Přejít do administrace</a>
    </div>
</body>
</html>";

    return Results.Content(html, "text/html; charset=utf-8");
};

app.MapGet("/day-off", handleDayOffDirect);
app.MapGet("/action/day-off", handleDayOffDirect);

// Přímý odkaz pro Okamžité studium: GET /study-now nebo /action/study-now
var handleStudyNowDirect = async (
    [FromQuery] string? key,
    ConfigManager cfg,
    StateManager state,
    IpcServerService ipc) =>
{
    var currentConfig = cfg.Load();
    if (!string.IsNullOrEmpty(currentConfig.Server.QuickUnlockKey) && currentConfig.Server.QuickUnlockKey != key)
    {
        return Results.Content("<h1>403 – Neplatný bezpečnostní klíč</h1>", "text/html; charset=utf-8");
    }

    state.ForceStudy();
    await ipc.BroadcastForceLockAsync();

    var html = @"<!DOCTYPE html>
<html lang=""cs"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Okamžité studium spuštěno</title>
    <style>
        * { box-sizing: border-box; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }
        body { background: #0f172a; color: white; display: flex; align-items: center; justify-content: center; min-height: 100vh; padding: 20px; }
        .card { background: #1e293b; border-radius: 24px; padding: 40px 24px; max-width: 420px; text-align: center; border: 1px solid #2563eb; box-shadow: 0 20px 25px -5px rgba(0,0,0,0.5); }
        .icon { font-size: 64px; margin-bottom: 16px; }
        h1 { font-size: 24px; font-weight: 800; margin-bottom: 8px; color: #60a5fa; }
        p { color: #94a3b8; font-size: 15px; margin-bottom: 24px; line-height: 1.5; }
        .btn { display: inline-block; padding: 14px 28px; background: #334155; color: white; border-radius: 14px; text-decoration: none; font-weight: 700; font-size: 15px; }
    </style>
</head>
<body>
    <div class=""card"">
        <div class=""icon"">⚡</div>
        <h1>Okamžité studium spuštěno!</h1>
        <p>Na počítači byla právě vyvolána nová sada matematických příkladů a počítač byl uzamčen, dokud je bratr nespočítá.</p>
        <a href=""/administration"" class=""btn"">Přejít do administrace</a>
    </div>
</body>
</html>";

    return Results.Content(html, "text/html; charset=utf-8");
};

app.MapGet("/study-now", handleStudyNowDirect);
app.MapGet("/action/study-now", handleStudyNowDirect);

// ==========================================
// 4. REST API ENDPOINTY
// ==========================================

// Odemknout / Den volna
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
        return Results.Json(new { success = false, message = "Neplatný bezpečnostní klíč." }, statusCode: 403);
    }

    state.MarkUnlockedByOverride("DEN_VOLNA_API");
    await ipc.BroadcastUnlockAsync("DEN_VOLNA_API");

    return Results.Ok(new { success = true, message = "Den volna byl úspěšně aktivován a počítač odemčen!" });
};

app.MapPost("/api/action/unlock", unlockActionHandler);
app.MapGet("/api/action/unlock", unlockActionHandler);

// Zamknout / Okamžité studium
var lockActionHandler = async (
    [FromQuery] string? key,
    ConfigManager cfg,
    StateManager state,
    IpcServerService ipc) =>
{
    var currentConfig = cfg.Load();
    var requiredKey = currentConfig.Server.QuickUnlockKey;

    if (!string.IsNullOrEmpty(requiredKey) && requiredKey != key)
    {
        return Results.Json(new { success = false, message = "Neplatný bezpečnostní klíč." }, statusCode: 403);
    }

    state.ForceStudy();
    await ipc.BroadcastForceLockAsync();

    return Results.Ok(new { success = true, message = "Okamžité studium bylo aktivováno a příklady zobrazeny!" });
};

app.MapPost("/api/action/lock", lockActionHandler);
app.MapGet("/api/action/lock", lockActionHandler);

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
    if (newConfig.Math != null) current.Math = newConfig.Math;
    if (newConfig.Rules != null) current.Rules = newConfig.Rules;
    if (newConfig.Server != null)
    {
        current.Server.QuickUnlockKey = newConfig.Server.QuickUnlockKey;
    }

    cfg.Save(current);
    return Results.Ok(new { success = true, message = "Pravidla byla úspěšně uložena." });
});

app.Run();
