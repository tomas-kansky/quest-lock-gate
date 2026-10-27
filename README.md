# QuestLock Gate

> **Vzdělávací brána k hernímu počítači.** Aplikace pro Windows 10/11, která vyžaduje splnění denní dávky matematických úloh (tzv. *Daily Quest*) dříve, než odemkne přístup k ploše Windows a hrám.

---

## 🎯 Hlavní funkce

- 🔒 **Denní zámek (Once-a-day Gatekeeper):** Zámek se aktivuje 1× denně při prvním zapnutí nebo přihlášení. Po splnění může hráč počítač libovolně restartovat bez nutnosti opakovaného počítání.
- 🌙 **Ochrana při režimu spánku (Sleep / Resume):** Pokud počítač zůstane zapnutý přes noc v režimu spánku, při ranním probuzení do nového dne okamžitě automaticky aktivuje zámek.
- 🧮 **Offline matematický generátor pro 10leté žáky:**
  - 5 příkladů v sérii.
  - Sčítání do 100 ($a + b \le 100$).
  - Odčítání od čísel do 100 ($a - b \ge 0$).
  - Malá násobilka do 100 ($a, b \in [1, 10]$).
  - Celočíselné dělení z násobilky (např. $81 : 9 = 9$, $100 : 10 = 10$).
- ⌨️ **Plná podpora NumPadu:** Numerický blok na klávesnici funguje **vždy** – i když má uživatel vypnutý `NumLock` (podobně jako u Windows PIN přihlášení). K dispozici je i klikatelná klávesnice.
- 📱 **1-Click LAN odemčení pro rodiče (`/unlock`):**
  - Služba na portu 5001 poskytuje přehlednou mobilní webovou stránku pro mamku: `http://<IP_BRATROVA_PC>:5001/unlock?key=mamka`.
  - Kliknutím na velké tlačítko na mobilu se počítač na dálku okamžitě uvolní.
- 🖥️ **Multi-Monitor podpora:** Spolehlivě překryje primární obrazovku i všechny připojené sekundární monitory.

---

## 🚀 Rychlá instalace (1 příkaz)

Spusťte PowerShell jako **Správce (Run as Administrator)** v kořenovém adresáři projektu:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\install.ps1
```

Skript automaticky:
1. Sestaví komponenty v Release režimu.
2. Nainstaluje je do `C:\Program Files\QuestLock`.
3. Zaregistruje a spustí Windows Service `QuestLock.Guardian`.
4. Povolí port 5001 v bráně Windows Defender Firewall.
5. Nastaví autostart UI aplikace po přihlášení a ihned ji spustí.

Pro čistou odinstalaci spusťte jako Správce:
```powershell
powershell -ExecutionPolicy Bypass -File scripts\uninstall.ps1
```

---

## 🧪 Spuštění testů

```powershell
dotnet test
```

---

## 📂 Architektura a dokumentace

Podrobné analýzy, architektonické návrhy a datové modely naleznete ve složce `docs/`:

- 📐 **[ARCHITECTURE.md](docs/ARCHITECTURE.md):** Kompletní systémová specifikace (Two-Tier model, Windows Service, spolehlivý Kiosk, životní cyklus, noční režim spánku).
- 📜 **[CONTRACTS.md](docs/CONTRACTS.md):** Datové kontrakty (`config.json`, `state.json`), IPC komunikační protokol (Named Pipes), REST API pro port 5001 a algoritmus generování příkladů.
- 🛡️ **[RELEASE_WORKFLOW.md](docs/RELEASE_WORKFLOW.md):** Pravidla větvení, ochrana větve `main`, sémantické verzování a kontrolní seznam pro bezpečné vydávání nových verzí.

---

## 🏗️ Struktura projektu

```
quest-lock-gate/
├── src/
│   ├── QuestLock.Core/         # Sdílená logika: MathGenerator, ConfigManager, StateManager, IPC modely
│   ├── QuestLock.Guardian/     # Windows Service + Kestrel web server (port 5001) + Named Pipe server
│   └── QuestLock.UI/           # WPF Celoobrazovkový Kiosk, NumPad mapping, Sleep listener
├── tests/
│   └── QuestLock.Core.Tests/   # Unit testy matematického generátoru a úložiště
├── scripts/
│   ├── install.ps1             # 1-kliknutí instalátor (služba, firewall, autostart)
│   └── uninstall.ps1           # 1-kliknutí odinstalátor
├── docs/
│   ├── ARCHITECTURE.md
│   └── CONTRACTS.md
├── work-logs/                  # Záznamy o odpracovaném čase (Agentic Design Framework)
├── QuestLockGate.sln
└── README.md
```
