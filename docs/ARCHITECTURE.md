# QuestLock Gate – Technická architektura (RFC / System Specification)

Tento dokument představuje ucelený systémový návrh a specifikaci architektury aplikace **QuestLock Gate** – nástroje pro řízení přístupu k hernímu počítači formou denních vzdělávacích výzev (tzv. "Daily Quests").

---

## 1. Vize a cíle systému

Hlavním cílem aplikace je zajistit, aby uživatel (hráč) musel **nejprve splnit definovanou sadu úkolů** (prozatím zaměřeno na základní matematické operace), než získá přístup k běžnému prostředí Windows (plocha, spouštěče her jako Steam, Discord apod.).

### Klíčové principy:
1. **Jednou za den (Daily Gatekeeper):** Program se aktivuje pouze při prvním spuštění/přihlášení daného dne. Jakmile uživatel úkoly splní, může počítač během dne libovolně restartovat bez nutnosti opakovaného počítání.
2. **Neprůstřelnost (Anti-Tamper & Kiosk):** Uživatel nemůže okno obejít klávesovými zkratkami (`Alt+Tab`, `Alt+F4`, klávesa `Windows`), spuštěním Správce úloh ani restartem počítače.
3. **Plně autonomní provoz (Offline-First):** Úlohy se generují procedurálně přímo na lokálním stroji. Systém funguje 100% bez připojení k internetu.
4. **Vzdálená konfigurace po LAN (Port 5001):** Správce na stejné lokální síti může přes běžný webový prohlížeč (z mobilu nebo PC) nakonfigurovat pravidla (povolené operace, počet příkladů, maximální číslo, manuální odemčení).
5. **Spravované aktualizace (Managed Auto-Update via Velopack):** Automatické stahování delta aktualizací z GitHub Releases. Uživatel je informován dialogem s jedinou možností „Beru na vědomí“. Update se aplikuje až po vyřešení úkolu nebo při restartu, aby nebyl zneužit jako výmluva k odložení učení.

---

## 2. Bezpečnostní model a uživatelský kontext

Aplikace je navržena pro reálný rodinný scénář s 10letým uživatelem (hráčem):
- **Cílový uživatel (Bratr):** 10 let, standardní herní používání, jediný uživatelský účet na PC s právy správce (`Administrator`). Nemá znalosti PowerShellu, systémových hooků ani snahu hackovat systémové procesy.
- **Filozofie zabezpečení:** 
  - **Pragmatická a stabilní:** Prioritou je **maximální spolehlivost Windows** a předvídatelné chování (žádné experimentální nahrazování systémového shellu `Winlogon\Shell`, které by hrozilo černou obrazovkou).
  - **Kiosk překrytí:** Zámek běží jako `Topmost` celoobrazovkové okno bez okenních dekorací přes všechny připojené monitory, s blokací běžných zkratek (`Alt+Tab`, `Alt+F4`, klávesa `Windows`).
  - **Účet správce:** Vzhledem k tomu, že bratrův účet je jediný na PC, zámek se standardně aktivuje pro tento účet. Vzdálené i lokální nouzové odemčení je zajištěno přes webový odkaz pro rodiče (např. mamku na mobilu) nebo administrátorský PIN.

---

## 3. Komponentní architektura (Two-Tier Model)

Vzhledem k principu *Session 0 Isolation* ve Windows (služby běžící na pozadí nemohou zobrazovat GUI do uživatelské relace) a požadavku na maximální stabilitu bez experimentální výměny systémového shellu je systém rozdělen na dvě spolehlivé spolupracující části:

```
+-----------------------------------------------------------------------+
|                            WINDOWS SYSTEM                             |
|                                                                       |
|  [QuestLock.Guardian (Windows Service)]                               |
|  - Běží jako NT AUTHORITY\SYSTEM (spouští se ihned s bootem PC)       |
|  - Udržuje state.json a config.json (%ProgramData%\QuestLock)         |
|  - Integruje Embedded Kestrel Web Server na LAN portu 5001            |
|  - Poskytuje jednoduchý 1-click odemykací odkaz pro mamku (/unlock)    |
|  - Poskytuje IPC Named Pipe server pro obousměrnou komunikaci s UI   |
|  - Spravuje Windows Firewall pravidlo pro port 5001                   |
+-----------------------------------+-----------------------------------+
                                    |
                    IPC: Named Pipe (QuestLockPipe)
                                    |
+-----------------------------------v-----------------------------------+
|                     USER SESSION (Přihlášený hráč)                   |
|                                                                       |
|  [QuestLock.UI (WPF .NET 8 Celoobrazovkový Kiosk)]                    |
|  - Spouští se automaticky po přihlášení (Startup / Task Scheduler)    |
|  - Režim Fullscreen / Topmost překrytí přes všechny monitory          |
|  - Explorer.exe běží standardně na pozadí (žádný nestabilní shell)    |
|  - Keyboard hook (blokace Alt+Tab, Alt+F4, Win)                       |
|  - Podpora NumPadu i při vypnutém NumLocku (jako Windows PIN login)   |
|  - Generuje a vyhodnocuje matematické úlohy 100% offline              |
|  - Detekce probuzení ze spánku (PowerModeChanged / New Day check)     |
|  - Po odemčení skryje okno a zůstává aktivní pro noční spánek/probuzení|
+-----------------------------------------------------------------------+
```

---

## 4. Životní cyklus a workflow (Execution Lifecycle)

### Fáze 1: Start počítače a přihlášení
1. Počítač se zapne. Služba `QuestLock.Guardian` nastartuje ještě před zobrazením přihlašovací obrazovky Windows a otevře webový server na portu 5001.
2. Bratr se přihlásí do svého účtu. Běžný Windows Shell (`explorer.exe`) nastartuje standardním způsobem.
3. Automaticky startuje `QuestLock.UI`:
   - Zkontroluje přes IPC stav ve službě.
   - **Pokud je dnes již splněno (`last_completed_date == Today`):** UI se vůbec nezobrazí a zůstává tiše na pozadí sledovat události napájení.
   - **Pokud je nový den / nesplněno:** UI okamžitě překryje celou obrazovku na všech monitorech v režimu `Topmost` a aktivuje klávesový hook.

### Fáze 2: Zamčená obrazovka a řešení úloh
1. Hráč vidí matematické rozhraní s příkladem (např. `8 × 7 = ?`).
2. Podpora klávesnice:
   - Funguje standardní číselná řada i numerický blok (NumPad).
   - **NumPad funguje vždy**, i když má uživatel vypnutý `NumLock` (mapování kódů `Insert`, `End`, `Down`, `PageDown` atd. na číslice 0–9).
   - K dispozici je také klikatelná klávesnice na obrazovce.
3. Vyhodnocení:
   - Pokud je výsledek správný: přičte se bod a zobrazí se další příklad.
   - Pokud je výsledek chybný: zobrazí se zřetelná zpráva *„Výsledek není správný, zkus to znovu!“*, pole se vyčistí a hráč pokračuje bez penalizace.
4. Uživatel řeší úlohy do dosažení definovaného počtu (např. 5 správných odpovědí).

### Fáze 3: Splnění úkolů a odemčení
1. Po dosažení cílového počtu správných odpovědí UI odešle zprávu `SUBMIT_COMPLETION` do služby.
2. Služba uloží do `state.json`: `last_completed_date = Today`, `is_locked = false`.
3. UI deaktivuje klávesový hook, skryje celoobrazovková okna a přejde do úsporného stavu na pozadí (systém je plně přístupný).

### Fáze 4: Režim spánku a noční probuzení (Sleep / Resume Lifecycle)
1. **Scénář:** Bratr večer počítač nevypne, ale pouze uspí (režim spánku / zaklapnutí notebooku).
2. Následující den ráno počítač probudí:
   - `QuestLock.UI` zachytí systémovou událost probuzení (`SystemEvents.PowerModeChanged -> PowerModes.Resume`).
   - Služba i UI zkontrolují datum. Zjistí, že nastal nový den (`DateTime.Today != last_completed_date`).
   - UI okamžitě aktivuje celoobrazovkový zámek a klávesový hook **dříve, než může hráč spustit hru či prohlížeč**.
3. Obdobně periodický časovač (kontrola o půlnoci) zajistí uzamčení i v případě, že by počítač zůstal zapnutý celou noc.

### Fáze 5: Vzdálené odemčení pro mamku (1-Click URL)
1. Rodič (mamka) otevře na mobilu odkaz připojený k domácí Wi-Fi:
   `http://<IP_PC>:5001/unlock` (nebo `http://questlock.local:5001/unlock`).
2. Webový server zobrazí přehlednou responzivní stránku s velkým tlačítkem **„🔓 Odemknout počítač“**.
3. Po kliknutí (nebo přímo načtením URL s parametrem) služba `QuestLock.Guardian`:
   - Nastaví `is_locked = false` a `manual_override = true`.
   - Odešle přes IPC zprávu `UNLOCK_GRANTED` klientskému UI.
   - UI okamžitě stáhne zámek z obrazovky.
   - Mobilní prohlížeč zobrazí potvrzení: *„✅ Počítač byl úspěšně odemčen.“*

---

## 5. Matematický procedurální generátor (Quest Generator)

Generátor běží přímo v aplikaci a nepotřebuje připojení k síti.

### Pravidla pro generování početních operací:
- **Parametry z konfigurace:**
  - `allowed_operations`: pole obsahující povolené operace (`"add"`, `"subtract"`, `"multiply"`, `"divide"`). Výchozí: všechny 4.
  - `total_questions`: celkový počet příkladů k vyřešení (výchozí: 5).
  - `max_number`: horní hranice pro počítání (výchozí: 100).
- **Specifická pravidla jednotlivých operací pro 10letého žáka:**
  1. **Sčítání ($a + b = ?$):**
     - Výsledek je maximálně `max_number` ($a + b \le 100$).
     - Např. $34 + 48 = 82$, $15 + 23 = 38$.
  2. **Odčítání ($a - b = ?$):**
     - Menšenec $a \in [2, \text{max\_number}]$ (odčítáme až od 100).
     - Menšitel $b \in [1, a]$ (výsledek je vždy $\ge 0$).
     - Např. $100 - 81 = 19$, $74 - 28 = 46$.
  3. **Násobení ($a \times b = ?$):**
     - Klasická malá násobilka: $a, b \in [1, 10]$ (výsledek je v rozsahu 1 až 100).
     - Např. $7 \times 8 = 56$, $9 \times 6 = 54$.
  4. **Dělení ($a : b = ?$):**
     - Celočíselné dělení odpovídající malé násobilce s dělencem do 100:
       dělitel $b \in [2, 10]$, podíl $q \in [1, 10]$, dělenec $a = b \times q \le 100$.
     - Např. $81 : 9 = 9$, $72 : 8 = 9$, $100 : 10 = 10$.
     - Dělení nulou je vyloučeno. Výsledek je vždy celé číslo.

---

## 6. LAN Remote Configuration (Port 5001)

Služba `QuestLock.Guardian` hostuje odlehčený webový server (ASP.NET Core Kestrel) běžící na portu `5001`.

### Schéma komunikace a webové rozhraní:
- **Hlavní rozcestník:** `http://<IP_BRATROVA_PC>:5001/administration`
- **Přímé akce:**
  - **`http://<IP_BRATROVA_PC>:5001/day-off`**: Udělení Dne volna (zruší příklady a odemkne PC).
  - **`http://<IP_BRATROVA_PC>:5001/study-now`**: Okamžité studium (vyvolá novou sadu příkladů na PC).
- **Správa pravidel:** `http://<IP_BRATROVA_PC>:5001/settings`
  - Responzivní webová stránka (HTML5/CSS/JS) přímo servírovaná službou.
  - Možnost konfigurace:
    - Přepínače operací: `[x] Sčítání`, `[x] Odčítání`, `[x] Násobení`, `[x] Dělení`.
    - Vstup pro `max_number` (např. 100, 200).
    - Vstup pro `total_questions` (počet otázek).
    - Zobrazení aktuálního stavu: Dnes splněno / Čeká na splnění / Čas dokončení.

---

## 7. Distribuční a aktualizační mechanismus (Velopack)

1. **Vydání nové verze:**
   - Vývojář zvýší verzi a spustí `vpk pack`.
   - Balíčky (`releases.win.json`, delta `.nupkg`, full `.nupkg`) se nahrají na GitHub Releases.
2. **Kontrola aktualizací na klientovi:**
   - Služba provede dotaz na GitHub Releases API.
   - Při nalezení nové verze stáhne malý delta balíček do mezipaměti.
3. **Informační dialog:**
   - Zobrazí se oznámení: *"Byla připravena nová verze. Aplikuje se po dokončení úloh."*
   - Tlačítko: `[ Beru na vědomí ]` (okno je modal a nelze jej obejít).
4. **Odložená instalace:**
   - Update neprobíhá před vyřešením úloh, aby uživatel nemohl využít pád nebo restart jako způsob obcházení.
   - Po úspěšném odemčení počítače služba zavolá aplikaci updatu na pozadí.

---

## 8. Backlog a budoucí rozšíření

### Auto-Updater & 1-Click Remote Update (Priorita v1.2.0)
- **1-Click Update v administraci:**
  - Zobrazení informace o dostupné verzi na /administration a /settings.
  - Tlačítko pro okamžitou aktualizaci – systémová služba stáhne release ZIP z GitHubu, bezpečně vymění programové soubory a restartuje procesy.
- **Tichá noční kontrola:**
  - Automatická kontrola nových verzí na GitHub Releases API v nočních hodinách (např. ve 4:00 ráno).
- **Notifikace pro správce:**
  - Možnost odesílání webhooků na Discord/Telegram o stavu zamykání a provedených aktualizacích.