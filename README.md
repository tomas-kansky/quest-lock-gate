# QuestLock Gate

> **Vzdělávací brána k hernímu počítači.** Aplikace pro Windows 10/11, která vyžaduje splnění denní dávky matematických úloh (tzv. *Daily Quest*) dříve, než odemkne přístup k ploše Windows a hrám.

---

## 🚀 Rychlá instalace na cílový počítač (1 příkaz)

Instalace je plně automatizovaná. Cílový počítač **nemusí mít nainstalovaný .NET Runtime ani žádné vývojářské nástroje** (instalační balíček je kompletně Self-Contained win-x64).

### Postup instalace:
1. Na počítači otevřete **Windows PowerShell jako Správce** (*Run as Administrator*).
2. Zkopírujte a spusťte tento příkaz:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
irm https://github.com/tomas-kansky/quest-lock-gate/releases/download/v1.0.0/QuestLock-Setup.zip -OutFile "$env:TEMP\QuestLock-Setup.zip"
Expand-Archive "$env:TEMP\QuestLock-Setup.zip" -DestinationPath "$env:TEMP\QuestLock-Setup" -Force
& "$env:TEMP\QuestLock-Setup\install.ps1"
```

### Co instalační skript automaticky provede:
1. **Ověří administrátorská práva** a bezpečně ukončí případné dříve běžící instance.
2. **Nainstaluje aplikační binárky** do systémové složky `C:\Program Files\QuestLock\` (`Guardian` a `UI`).
3. **Inicializuje systémová data a konfiguraci** v `C:\ProgramData\QuestLock\` s výchozími pravidly.
4. **Nakonfiguruje Windows Defender Firewall** a povolí příchozí TCP port `5001` pro lokální síť (LAN).
5. **Zaregistruje a spustí Windows Service** `QuestLock.Guardian` s automatickým spouštěním po startu Windows (`NT AUTHORITY\SYSTEM`).
6. **Nastaví autostart Kiosku** do registru `HKLM:\Software\Microsoft\Windows\CurrentVersion\Run` (platí pro **všechny uživatele** na daném PC) a zámek okamžitě spustí pro aktuální relaci.
7. **Detekuje lokální IP adresu** a v terminálu vypíše přímé webové odkazy pro rodiče.

---

## 📱 Webová administrace a dálková správa (Port 5001)

Služba `QuestLock.Guardian` běží na pozadí pod systémovým účtem a na lokální síti zpřístupňuje moderní webové rozhraní pro rodiče přístupné z mobilu či počítače:

### 1. Hlavní administrace (`http://<IP_POCITACE>:5001/administration`)
- **Živý stav počítače:** Zobrazuje, zda bratr dnes již úkoly splnil, nebo zda je počítač zamčený.
- **Rychlé akce:** Jedním klepnutím na tlačítko lze počítač odemknout nebo naopak uzamknout do režimu učení.
- **Přehled aktivních pravidel:** Kolik úloh je nastaveno, do jakého maxima a které početní operace jsou zapnuty.

### 2. Přímé URL akce (vhodné pro uložení do záložek na mobilu)
- 🏖️ **Den volna (`http://<IP_POCITACE>:5001/day-off`):**
  - Otevření této URL okamžitě zruší dnešní příklady a stáhne zámek obrazovky.
  - Skvělé např. o víkendu nebo o prázdninách.
- ⚡ **Okamžité studium (`http://<IP_POCITACE>:5001/study-now`):**
  - Otevření této URL vyvolá novou sadu úloh a uzamkne obrazovku i v případě, že bratr dnes již úkoly splnil.

### 3. Správa pravidel (`http://<IP_POCITACE>:5001/settings`)
- Nastavení počtu příkladů na den (1–50).
- Nastavení horní meze čísel (`max_number`, výchozí 100).
- Volba povolených početních operací: sčítání (+), odčítání (-), násobení (×), dělení (:).
- Volitelný bezpečnostní PIN klíč pro přístup.

---

## 🎯 Klíčové vlastnosti aplikace

- 🔒 **Denní zámek (Once-a-day Gatekeeper):** Zámek se aktivuje 1× denně při prvním zapnutí nebo přihlášení. Po splnění může hráč počítač libovolně restartovat bez nutnosti opakovaného počítání.
- 🌙 **Ochrana při režimu spánku (Sleep / Resume):** Pokud počítač zůstane zapnutý přes noc v režimu spánku, při ranním probuzení do nového dne okamžitě automaticky aktivuje zámek (`SystemEvents.PowerModeChanged`).
- 🧮 **Procedurální matematický generátor:**
  - Pokaždé unikátní náhodné příklady dle nastavených pravidel.
  - Vždy kladné celočíselné výsledky bez zlomků.
  - Dělení generované z malé násobilky beze zbytku.
- ⌨️ **Plná podpora NumPadu:** Numerický blok na klávesnici funguje **vždy** – i když má uživatel vypnutý `NumLock` (využívá low-level keyboard hook `WH_KEYBOARD_LL`). K dispozici je i klikatelná klávesnice přímo na obrazovce.
- 🖥️ **Multi-Monitor podpora:** Spolehlivě překryje primární monitor i všechny připojené sekundární obrazovky černým pozadím bez možnosti prokliknutí.
- 🛡️ **Two-Tier architektura:** Stav a pravidla spravuje systémová služba `QuestLock.Guardian`, UI běží jako oddělený bezpečný Kiosk komunikující přes Named Pipe (`QuestLockPipe`) s nakonfigurovaným ACL.

---

## 🧹 Odinstalace

Pro úplné a čisté odstranění aplikace spusťte v PowerShellu jako Správce:

```powershell
powershell -ExecutionPolicy Bypass -File "$env:ProgramFiles\QuestLock\uninstall.ps1"
```
Skript korektně zastaví procesy, odregistruje a smaže službu `QuestLock.Guardian`, odstraní záznam z registru HKLM Run, vymaže firewallové pravidlo a smaže programové soubory.

---

## 🧪 Vývoj a spuštění testů

Projekt využívá .NET 6 a WPF:

```powershell
# Spuštění sady unit testů
dotnet test

# Kompilace a sestavení v Release režimu
dotnet build -c Release
```

---

## 📂 Architektura a dokumentace

Podrobné specifikace a architektonické podklady naleznete ve složce `docs/`:

- 📐 **[ARCHITECTURE.md](docs/ARCHITECTURE.md):** Kompletní systémová specifikace (Two-Tier model, Windows Service, Kiosk, noční režim spánku).
- 📜 **[CONTRACTS.md](docs/CONTRACTS.md):** Datové modely (`config.json`, `state.json`), IPC komunikační protokol (Named Pipes) a specifikace REST API.
- 🛡️ **[RELEASE_WORKFLOW.md](docs/RELEASE_WORKFLOW.md):** Pravidla větvení, ochrana větve `main`, sémantické verzování a kontrolní checklist pro releasy.
