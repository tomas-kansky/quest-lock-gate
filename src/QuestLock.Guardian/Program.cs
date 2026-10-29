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
builder.WebHost.ConfigureKestrel(kestrelOptions =>
{
    kestrelOptions.ListenAnyIP(5001);
});

// Registrace core služeb
var configManager = new ConfigManager();
var stateManager = new StateManager();
var logManager = new LogManager();

builder.Services.AddSingleton(configManager);
builder.Services.AddSingleton(stateManager);
builder.Services.AddSingleton(logManager);
builder.Services.AddSingleton<IpcServerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<IpcServerService>());

logManager.Info("Guardian", "STARTUP", "QuestLock Guardian Service nastartovala Kestrel web server na portu 5001.");

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
    var netInfo = QuestLock.Core.Network.NetworkHelper.GetActiveNetworkInfo();
    var tailscaleBannerHtml = netInfo.HasTailscale
        ? $@"<div style=""background: #0284c720; border: 1px solid #0284c7; border-radius: 12px; padding: 10px 14px; margin-bottom: 16px; font-size: 13px; display: flex; justify-content: space-between; align-items: center;"">
                <div>
                    <span style=""color: #38bdf8; font-weight: 700;"">🌐 Vzdálený přístup (Tailscale):</span>
                    <span style=""color: #f8fafc; font-family: monospace; margin-left: 6px;"">http://{netInfo.TailscaleIp}:5001</span>
                </div>
                <span style=""background: #0284c7; color: white; padding: 2px 8px; border-radius: 9999px; font-size: 11px; font-weight: 700;"">AKTIVNÍ</span>
           </div>"
        : @"<div style=""background: #1e293b; border: 1px dashed #334155; border-radius: 12px; padding: 10px 14px; margin-bottom: 16px; font-size: 12px; color: #94a3b8;"">
                ℹ️ Vzdálený přístup přes internet není nakonfigurován (lze kdykoliv aktivovat přes Tailscale).
           </div>";

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
        .header {{ text-align: center; margin-bottom: 20px; padding-top: 10px; }}
        .header h1 {{ font-size: 26px; font-weight: 800; color: #ffffff; margin-bottom: 4px; }}
        .header p {{ color: #94a3b8; font-size: 14px; }}
        
        .nav-links {{ display: flex; gap: 8px; justify-content: center; margin-bottom: 20px; }}
        .nav-link {{ background: #1e293b; color: #94a3b8; text-decoration: none; padding: 8px 14px; border-radius: 12px; font-size: 13px; font-weight: 600; border: 1px solid #334155; transition: all 0.2s; }}
        .nav-link:hover {{ background: #334155; color: #ffffff; }}

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

        <div class=""nav-links"">
            <a href=""/activity"" class=""nav-link"">📊 Přehled aktivity</a>
            <a href=""/logs"" class=""nav-link"">🤖 AI Diagnostika & Logy</a>
            <a href=""/settings"" class=""nav-link"">⚙️ Pravidla</a>
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
// 2. DIAGNOSTIKA & AI LOGY (/logs)
// ==========================================
const string LogsHtmlTemplate = @"<!DOCTYPE html>
<html lang=""cs"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>QuestLock Gate – Diagnostika & AI Logy</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, monospace; }
        body { background: #0b1120; color: #f8fafc; min-height: 100vh; padding: 20px; }
        .header { max-width: 1100px; margin: 0 auto 20px; display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 12px; }
        .header h1 { font-size: 22px; font-weight: 800; color: #38bdf8; display: flex; align-items: center; gap: 8px; }
        .nav-links { display: flex; gap: 10px; }
        .nav-link { background: #1e293b; color: #94a3b8; text-decoration: none; padding: 8px 14px; border-radius: 10px; font-size: 13px; font-weight: 600; border: 1px solid #334155; }
        .nav-link:hover { background: #334155; color: white; }

        .main-card { max-width: 1100px; margin: 0 auto 20px; background: #1e293b; border-radius: 20px; padding: 20px; border: 1px solid #334155; }
        .ai-banner { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 16px; background: #0f172a; padding: 18px 20px; border-radius: 16px; border: 1px solid #0284c7; margin-bottom: 20px; }
        .ai-banner h2 { font-size: 17px; font-weight: 700; color: #38bdf8; margin-bottom: 4px; }
        .ai-banner p { font-size: 13px; color: #94a3b8; }

        .btn-ai { background: linear-gradient(135deg, #0284c7, #0369a1); color: white; border: none; padding: 12px 24px; border-radius: 12px; font-size: 15px; font-weight: 700; cursor: pointer; display: flex; align-items: center; gap: 8px; transition: all 0.2s; }
        .btn-ai:active { transform: scale(0.97); }
        .btn-raw { background: #334155; color: #cbd5e1; border: 1px solid #475569; padding: 8px 14px; border-radius: 10px; text-decoration: none; font-size: 13px; font-weight: 600; }

        .controls { display: flex; justify-content: space-between; align-items: center; margin-bottom: 14px; flex-wrap: wrap; gap: 10px; }
        .filters { display: flex; gap: 6px; }
        .filter-btn { background: #0f172a; color: #94a3b8; border: 1px solid #334155; padding: 6px 12px; border-radius: 8px; font-size: 12px; cursor: pointer; }
        .filter-btn.active { background: #38bdf8; color: #0f172a; font-weight: 700; border-color: #38bdf8; }

        .table-wrap { overflow-x: auto; background: #0f172a; border-radius: 12px; border: 1px solid #334155; max-height: 600px; overflow-y: auto; }
        table { width: 100%; border-collapse: collapse; font-size: 12px; text-align: left; }
        th { background: #1e293b; color: #94a3b8; font-weight: 700; padding: 10px 14px; position: sticky; top: 0; z-index: 10; border-bottom: 1px solid #334155; }
        td { padding: 10px 14px; border-bottom: 1px solid #1e293b; vertical-align: top; font-family: 'Consolas', 'Courier New', monospace; }
        
        .badge { display: inline-block; padding: 2px 8px; border-radius: 6px; font-size: 10px; font-weight: 800; text-transform: uppercase; }
        .badge-info { background: #0369a1; color: #bae6fd; }
        .badge-warn { background: #854d0e; color: #fef08a; }
        .badge-error { background: #991b1b; color: #fecaca; }
        .badge-debug { background: #334155; color: #cbd5e1; }
        
        .col-time { color: #64748b; white-space: nowrap; width: 110px; }
        .col-level { width: 80px; text-align: center; }
        .col-source { color: #a78bfa; width: 90px; }
        .col-event { color: #f59e0b; width: 150px; font-weight: 600; }
        .col-msg { color: #e2e8f0; }
        .log-details { display: block; margin-top: 4px; color: #64748b; font-size: 11px; }

        .alert-toast { position: fixed; bottom: 20px; right: 20px; background: #15803d; color: white; padding: 14px 20px; border-radius: 12px; font-weight: 700; box-shadow: 0 10px 25px rgba(0,0,0,0.5); display: none; z-index: 100; }
        .alert-toast.show { display: block; animation: fadeIn 0.3s; }
        @keyframes fadeIn { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }
    </style>
</head>
<body>
    <div class=""header"">
        <h1>🤖 QuestLock Gate – AI Diagnostika & Logy</h1>
        <div class=""nav-links"">
            <a href=""/administration"" class=""nav-link"">← Administrace</a>
            <a href=""/activity"" class=""nav-link"">📊 Přehled aktivity</a>
            <a href=""/settings"" class=""nav-link"">⚙️ Pravidla</a>
        </div>
    </div>

    <div class=""main-card"">
        <div class=""ai-banner"">
            <div>
                <h2>Strukturovaný balíček pro analýzu umělou inteligencí</h2>
                <p>Zkopíruje kompletní kontext (verze, stav, konfiguraci a posledních 50 událostí) do Markdownu. Stačí vložit do promptu s dotazem.</p>
            </div>
            <button class=""btn-ai"" onclick=""copyAiDiagnostics()"">📋 Kopírovat pro AI</button>
        </div>

        <div class=""controls"">
            <div class=""filters"">
                <button class=""filter-btn active"" onclick=""filterLogs('ALL', this)"">Vše (<!--COUNT-->)</button>
                <button class=""filter-btn"" onclick=""filterLogs('ERROR', this)"">Jen Chyby</button>
                <button class=""filter-btn"" onclick=""filterLogs('WARN', this)"">Varování</button>
                <button class=""filter-btn"" onclick=""filterLogs('UI', this)"">Jen Kiosk (UI)</button>
                <button class=""filter-btn"" onclick=""filterLogs('GUARDIAN', this)"">Jen Služba</button>
            </div>
            <a href=""/api/logs/raw"" class=""btn-raw"" target=""_blank"" download=""questlock.log"">📥 Stáhnout kompletní .log</a>
        </div>

        <div class=""table-wrap"">
            <table>
                <thead>
                    <tr>
                        <th>Čas</th>
                        <th>Úroveň</th>
                        <th>Zdroj</th>
                        <th>Událost</th>
                        <th>Zpráva & Detaily</th>
                    </tr>
                </thead>
                <tbody id=""logsTableBody"">
                    <!--ROWS-->
                </tbody>
            </table>
        </div>
    </div>

    <div id=""toast"" class=""alert-toast"">✅ Diagnostický bundle byl zkopírován do schránky!</div>

    <script>
        async function copyAiDiagnostics() {
            try {
                const resp = await fetch('/api/diagnostics/bundle');
                const data = await resp.json();

                const logLines = (data.logs || []).map(function(l) {
                    var dt = new Date(l.timestamp).toISOString();
                    var details = '';
                    if (l.details) {
                        details = ' | ' + Object.keys(l.details).map(function(k) { return k + '=' + l.details[k]; }).join(', ');
                    }
                    return '[' + dt + '] [' + l.level + '] [' + l.source + '] [' + l.event + '] ' + l.message + details;
                });

                var md = '# QuestLock Gate – Diagnostický balíček pro AI\n' +
                    '> Generováno: ' + data.generated_at + '\n\n' +
                    '## 1. Systém a prostředí\n```json\n' +
                    JSON.stringify(data.system, null, 2) + '\n```\n\n' +
                    '## 2. Aktuální stav (state.json)\n```json\n' +
                    JSON.stringify(data.state, null, 2) + '\n```\n\n' +
                    '## 3. Aktuální pravidla (config.json)\n```json\n' +
                    JSON.stringify(data.config, null, 2) + '\n```\n\n' +
                    '## 4. Poslední události systému\n```log\n' +
                    logLines.join('\n') + '\n```\n';

                await navigator.clipboard.writeText(md);

                const toast = document.getElementById('toast');
                toast.className = 'alert-toast show';
                setTimeout(function() { toast.className = 'alert-toast'; }, 4000);
            } catch (err) {
                alert('Chyba při kopírování: ' + err);
            }
        }

        function filterLogs(filter, btn) {
            document.querySelectorAll('.filter-btn').forEach(function(b) { b.classList.remove('active'); });
            if (btn) btn.classList.add('active');

            const rows = document.querySelectorAll('.log-row');
            rows.forEach(function(r) {
                if (filter === 'ALL') {
                    r.style.display = '';
                } else if (filter === 'ERROR') {
                    r.style.display = r.classList.contains('level-error') ? '' : 'none';
                } else if (filter === 'WARN') {
                    r.style.display = r.classList.contains('level-warn') ? '' : 'none';
                } else if (filter === 'UI') {
                    r.style.display = r.classList.contains('source-ui') ? '' : 'none';
                } else if (filter === 'GUARDIAN') {
                    r.style.display = r.classList.contains('source-guardian') ? '' : 'none';
                }
            });
        }
    </script>
</body>
</html>";

app.MapGet("/logs", (LogManager logs, HttpRequest request) =>
{
    var recent = logs.GetRecentLogs(limit: 100);

    var rowsHtml = string.Join("\n", recent.Select(entry =>
    {
        var localTime = entry.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");
        var badgeClass = entry.Level switch
        {
            "ERROR" => "badge-error",
            "WARN" => "badge-warn",
            "DEBUG" => "badge-debug",
            _ => "badge-info"
        };
        var details = entry.Details != null && entry.Details.Count > 0
            ? "<span class='log-details'>" + string.Join(" ", entry.Details.Select(kv => $"{kv.Key}=\"{kv.Value}\"")) + "</span>"
            : "";

        return $@"<tr class='log-row level-{entry.Level.ToLowerInvariant()} source-{entry.Source.ToLowerInvariant()}'>
            <td class='col-time'>{localTime}</td>
            <td class='col-level'><span class='badge {badgeClass}'>{entry.Level}</span></td>
            <td class='col-source'>{entry.Source}</td>
            <td class='col-event'>{entry.Event}</td>
            <td class='col-msg'>{System.Net.WebUtility.HtmlEncode(entry.Message)} {details}</td>
        </tr>";
    }));

    var html = LogsHtmlTemplate
        .Replace("<!--COUNT-->", recent.Count.ToString())
        .Replace("<!--ROWS-->", rowsHtml);

    return Results.Content(html, "text/html; charset=utf-8");
});

// ==========================================
// 3. RODIČOVSKÝ PŘEHLED AKTIVITY (/activity)
// ==========================================
const string ActivityHtmlTemplate = @"<!DOCTYPE html>
<html lang=""cs"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no"">
    <title>QuestLock Gate – Přehled aktivity</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }
        body { background: #0f172a; color: #f8fafc; min-height: 100vh; padding: 20px; display: flex; justify-content: center; }
        .container { width: 100%; max-width: 500px; }
        .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; padding-top: 10px; }
        .header h1 { font-size: 22px; font-weight: 800; color: #ffffff; }
        .header a { color: #38bdf8; text-decoration: none; font-size: 14px; font-weight: 600; }

        .card { background: #1e293b; border-radius: 24px; padding: 24px; border: 1px solid #334155; margin-bottom: 20px; box-shadow: 0 10px 25px -5px rgba(0,0,0,0.4); }
        .card h2 { font-size: 16px; font-weight: 700; color: #e2e8f0; margin-bottom: 16px; border-bottom: 1px solid #334155; padding-bottom: 8px; text-transform: uppercase; letter-spacing: 0.05em; }

        .stats-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; margin-bottom: 16px; }
        .stat-box { background: #0f172a; padding: 14px; border-radius: 14px; border: 1px solid #334155; text-align: center; }
        .stat-lbl { font-size: 12px; color: #94a3b8; margin-bottom: 4px; }
        .stat-val { font-size: 20px; font-weight: 800; color: #38bdf8; }

        .event-item { display: flex; align-items: flex-start; gap: 12px; padding: 12px; background: #0f172a; border-radius: 12px; border: 1px solid #334155; margin-bottom: 8px; font-size: 13px; }
        .event-time { color: #64748b; font-size: 12px; font-family: monospace; padding-top: 2px; }
        .event-icon { font-size: 16px; }
        .event-msg { flex: 1; font-weight: 600; line-height: 1.4; }
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Přehled aktivity bratra</h1>
            <a href=""/administration"">← Administrace</a>
        </div>

        <div class=""card"">
            <h2>Dnešní statistika (<!--TODAY-->)</h2>
            <div class=""stats-grid"">
                <div class=""stat-box"">
                    <div class=""stat-lbl"">Stav dneška</div>
                    <div class=""stat-val"" style=""color: <!--STATUS_COLOR-->;"">
                        <!--STATUS_TEXT-->
                    </div>
                </div>
                <div class=""stat-box"">
                    <div class=""stat-lbl"">Doba řešení</div>
                    <div class=""stat-val"">
                        <!--DURATION-->
                    </div>
                </div>
            </div>
            <div class=""stats-grid"">
                <div class=""stat-box"">
                    <div class=""stat-lbl"">Správných odpovědí</div>
                    <div class=""stat-val""><!--CORRECT--> / <!--TOTAL--></div>
                </div>
                <div class=""stat-box"">
                    <div class=""stat-lbl"">Poslední splnění</div>
                    <div class=""stat-val"" style=""font-size: 15px; padding-top: 4px;"">
                        <!--LAST_TIME-->
                    </div>
                </div>
            </div>
        </div>

        <div class=""card"">
            <h2>Časová osa pokusů a událostí</h2>
            <div>
                <!--EVENTS-->
            </div>
        </div>
    </div>
</body>
</html>";

app.MapGet("/activity", (StateManager state, ConfigManager cfg, LogManager logs) =>
{
    var s = state.Load();
    var c = cfg.Load();
    var today = DateTime.Now.ToString("yyyy-MM-dd");
    var isCompleted = s.IsCompletedToday(today);

    var recentLogs = logs.GetRecentLogs(limit: 100);
    var mathAttempts = recentLogs
        .Where(l => l.Event is "ANSWER_CORRECT" or "ANSWER_INCORRECT" or "MATH_COMPLETED" or "REMOTE_UNLOCK_RECEIVED" or "FORCE_LOCK_RECEIVED" or "LOCK_ENGAGED")
        .ToList();

    var eventsHtml = string.Join("\n", mathAttempts.Select(e =>
    {
        var localTime = e.Timestamp.ToLocalTime().ToString("HH:mm:ss");
        var icon = e.Event switch
        {
            "ANSWER_CORRECT" => "✅",
            "ANSWER_INCORRECT" => "❌",
            "MATH_COMPLETED" => "🏆",
            "REMOTE_UNLOCK_RECEIVED" => "🏖️",
            "FORCE_LOCK_RECEIVED" => "⚡",
            _ => "ℹ️"
        };
        var color = e.Event switch
        {
            "ANSWER_CORRECT" => "#4ade80",
            "ANSWER_INCORRECT" => "#f87171",
            "MATH_COMPLETED" => "#38bdf8",
            _ => "#cbd5e1"
        };

        return $@"<div class=""event-item"">
            <div class=""event-time"">{localTime}</div>
            <div class=""event-icon"">{icon}</div>
            <div class=""event-msg"" style=""color: {color};"">{System.Net.WebUtility.HtmlEncode(e.Message)}</div>
        </div>";
    }));

    if (string.IsNullOrWhiteSpace(eventsHtml))
    {
        eventsHtml = "<div style='color: #64748b; text-align: center; padding: 20px;'>Zatím dnes nebyla zaznamenána žádná aktivita bratra.</div>";
    }

    var html = ActivityHtmlTemplate
        .Replace("<!--TODAY-->", today)
        .Replace("<!--STATUS_COLOR-->", isCompleted ? "#4ade80" : "#f87171")
        .Replace("<!--STATUS_TEXT-->", isCompleted ? "Hotovo" : "Nesplněno")
        .Replace("<!--DURATION-->", s.TodayStats.DurationSeconds > 0 ? $"{s.TodayStats.DurationSeconds} s" : "—")
        .Replace("<!--CORRECT-->", s.TodayStats.CorrectAnswers.ToString())
        .Replace("<!--TOTAL-->", c.Math.TotalQuestions.ToString())
        .Replace("<!--LAST_TIME-->", s.LastCompletedAt.HasValue ? s.LastCompletedAt.Value.ToLocalTime().ToString("HH:mm") : "Zatím ne")
        .Replace("<!--EVENTS-->", eventsHtml);

    return Results.Content(html, "text/html; charset=utf-8");
});

// ==========================================
// 4. NASTAVENÍ PRAVIDEL (/settings)
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
// 5. PŘÍMÉ ODKAZY PRO URL KLIKNUTÍ (DAY-OFF & STUDY-NOW)
// ==========================================

// Přímý odkaz pro udělení Dne volna: GET /day-off nebo /action/day-off
var handleDayOffDirect = async (
    [FromQuery] string? key,
    ConfigManager cfg,
    StateManager state,
    IpcServerService ipc,
    LogManager logs) =>
{
    var currentConfig = cfg.Load();
    if (!string.IsNullOrEmpty(currentConfig.Server.QuickUnlockKey) && currentConfig.Server.QuickUnlockKey != key)
    {
        logs.Warn("Web", "UNAUTHORIZED_ACCESS", "Pokus o přístup k /day-off s neplatným bezpečnostním klíčem.");
        return Results.Content("<h1>403 – Neplatný bezpečnostní klíč</h1>", "text/html; charset=utf-8");
    }

    state.MarkUnlockedByOverride("DEN_VOLNA_URL");
    logs.Info("Web", "DAY_OFF_DIRECT", "Udělen den volna přímo přes URL odkaz (/day-off).");
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
    IpcServerService ipc,
    LogManager logs) =>
{
    var currentConfig = cfg.Load();
    if (!string.IsNullOrEmpty(currentConfig.Server.QuickUnlockKey) && currentConfig.Server.QuickUnlockKey != key)
    {
        logs.Warn("Web", "UNAUTHORIZED_ACCESS", "Pokus o přístup k /study-now s neplatným bezpečnostním klíčem.");
        return Results.Content("<h1>403 – Neplatný bezpečnostní klíč</h1>", "text/html; charset=utf-8");
    }

    state.ForceStudy();
    logs.Info("Web", "STUDY_NOW_DIRECT", "Aktivováno okamžité studium přímo přes URL odkaz (/study-now).");
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
// 6. REST API ENDPOINTY
// ==========================================

// Odemknout / Den volna
var unlockActionHandler = async (
    [FromQuery] string? key,
    ConfigManager cfg,
    StateManager state,
    IpcServerService ipc,
    LogManager logs) =>
{
    var currentConfig = cfg.Load();
    var requiredKey = currentConfig.Server.QuickUnlockKey;

    if (!string.IsNullOrEmpty(requiredKey) && requiredKey != key)
    {
        logs.Warn("Web", "API_AUTH_FAILED", "Chyba autentizace pro /api/action/unlock.");
        return Results.Json(new { success = false, message = "Neplatný bezpečnostní klíč." }, statusCode: 403);
    }

    state.MarkUnlockedByOverride("DEN_VOLNA_API");
    logs.Info("Web", "ACTION_UNLOCK", "Aktivován Den volna přes API tlačítko.");
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
    IpcServerService ipc,
    LogManager logs) =>
{
    var currentConfig = cfg.Load();
    var requiredKey = currentConfig.Server.QuickUnlockKey;

    if (!string.IsNullOrEmpty(requiredKey) && requiredKey != key)
    {
        logs.Warn("Web", "API_AUTH_FAILED", "Chyba autentizace pro /api/action/lock.");
        return Results.Json(new { success = false, message = "Neplatný bezpečnostní klíč." }, statusCode: 403);
    }

    state.ForceStudy();
    logs.Info("Web", "ACTION_LOCK", "Aktivováno Okamžité studium přes API tlačítko.");
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

app.MapPut("/api/config", ([FromBody] AppConfig newConfig, ConfigManager cfg, LogManager logs) =>
{
    var current = cfg.Load();
    if (newConfig.Math != null) current.Math = newConfig.Math;
    if (newConfig.Rules != null) current.Rules = newConfig.Rules;
    if (newConfig.Server != null)
    {
        current.Server.QuickUnlockKey = newConfig.Server.QuickUnlockKey;
    }

    cfg.Save(current);
    logs.Info("Web", "CONFIG_UPDATED", "Nová pravidla byla uložena.", new Dictionary<string, string>
    {
        { "total_questions", current.Math.TotalQuestions.ToString() },
        { "max_number", current.Math.MaxNumber.ToString() }
    });
    return Results.Ok(new { success = true, message = "Pravidla byla úspěšně uložena." });
});

// ==========================================
// 7. DIAGNOSTIKA & LOGY API ENDPOINTY
// ==========================================

// JSON seznam posledních logů
app.MapGet("/api/logs", (LogManager logs, [FromQuery] int? limit, [FromQuery] string? level) =>
{
    var max = Math.Clamp(limit ?? 100, 1, 500);
    return Results.Ok(logs.GetRecentLogs(max, level));
});

// Kompletní raw .log soubor pro stažení
app.MapGet("/api/logs/raw", (LogManager logs) =>
{
    var content = logs.GetRawLogContent();
    return Results.Content(content, "text/plain; charset=utf-8");
});

// Strukturovaný balíček pro AI analýzu
app.MapGet("/api/diagnostics/bundle", (LogManager logs, ConfigManager cfg, StateManager state) =>
{
    return Results.Ok(logs.GenerateDiagnosticsBundle(cfg, state));
});

app.Run();
