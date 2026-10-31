# QuestLock Gate – Datové a komunikační kontrakty (Contracts Specification)

Tento dokument definuje přesné datové struktury, formáty konfiguračních a stavových souborů, komunikační protokoly IPC a REST API pro vzdálenou správu.

---

## 1. Datový model konfigurace (`config.json`)

**Umístění souboru:** `%ProgramData%\QuestLock\config.json`  
**Oprávnění:** Čtení/zápis pouze pro `NT AUTHORITY\SYSTEM` a skupinu `Administrators`. Běžní uživatelé mají právo pouze ke čtení (nebo vůbec žádné, čtou přes IPC).

```json
{
  "$schema": "./schemas/config.schema.json",
  "version": 1,
  "server": {
    "port": 5001,
    "admin_pin_hash": "a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3",
    "admin_pin_salt": "c29tZV9yYW5kb21fc2FsdA==",
    "quick_unlock_key": "mamka"
  },
  "rules": {
    "daily_gatekeeper_enabled": true,
    "auto_update_enabled": true,
    "github_repo_url": "https://github.com/tomas-kansky/quest-lock-gate"
  },
  "math": {
    "total_questions": 5,
    "max_number": 100,
    "allowed_operations": [
      "add",
      "subtract",
      "multiply",
      "divide"
    ],
    "allow_negative_results": false
  }
}
```

### Popis polí:
| Pole | Typ | Výchozí | Popis |
| :--- | :--- | :--- | :--- |
| `server.port` | integer | `5001` | TCP port lokálního webového serveru pro administraci po síti. |
| `server.admin_pin_hash` | string | - | SHA-256 hash administrátorského PINu se solí. |
| `rules.daily_gatekeeper_enabled` | boolean | `true` | Pokud je `true`, zámek se aktivuje maximálně 1× denně. |
| `math.total_questions` | integer | `5` | Počet správně vyřešených úloh požadovaných k odemčení. |
| `math.max_number` | integer | `10` | Horní mez pro základní operandy/faktory (u dělení může být dělenec až `max_number * max_number`). |
| `math.allowed_operations` | string[] | všechny 4 | Množina povolených operací (`"add"`, `"subtract"`, `"multiply"`, `"divide"`). |

---

## 2. Datový model stavu (`state.json`)

**Umístění souboru:** `%ProgramData%\QuestLock\state.json`  
**Oprávnění:** Spravováno výhradně službou `QuestLock.Guardian`.

```json
{
  "$schema": "./schemas/state.schema.json",
  "version": 1,
  "is_locked": true,
  "last_completed_date": "2026-10-27",
  "last_completed_at": "2026-10-27T15:30:00Z",
  "today_stats": {
    "attempts_count": 1,
    "correct_answers": 5,
    "total_questions": 5,
    "duration_seconds": 142
  },
  "manual_override": {
    "active": false,
    "unlocked_by": null,
    "unlocked_at": null
  }
}
```

---

## 3. IPC Protokol (Named Pipes: `\\.\pipe\QuestLockPipe`)

Komunikace mezi klientským Lockerem (`QuestLock.UI`) a systémovou službou (`QuestLock.Guardian`). Zprávy jsou kódovány v UTF-8 JSONu oddělené znakem nového řádku (`\n`).

### Zpráva 1: Žádost o stav a konfiguraci (Client -> Service)
```json
{
  "type": "GET_INIT_DATA"
}
```

**Odpověď (Service -> Client):**
```json
{
  "type": "INIT_DATA_RESPONSE",
  "payload": {
    "is_locked_required": true,
    "math_config": {
      "total_questions": 5,
      "max_number": 10,
      "allowed_operations": ["multiply", "divide"]
    },
    "pending_update_version": "1.1.0"
  }
}
```

### Zpráva 2: Odeslání výsledku / Odemčení (Client -> Service)
```json
{
  "type": "SUBMIT_COMPLETION",
  "payload": {
    "correct_answers": 5,
    "total_questions": 5,
    "duration_seconds": 120
  }
}
```

**Odpověď (Service -> Client):**
```json
{
  "type": "UNLOCK_GRANTED",
  "payload": {
    "unlocked_at": "2026-10-27T17:35:00Z"
  }
}
```

---

## 4. REST API – LAN Administrace (Port 5001)

Služba naslouchá na `http://0.0.0.0:5001`.

### Endpointy:

#### 1. `POST /api/auth/login`
Autentizace administrátora přes PIN kód.
- **Request Body:**
  ```json
  { "pin": "1234" }
  ```
- **Response (200 OK):**
  ```json
  { "token": "jwt_session_token", "expires_in": 3600 }
  ```

#### 2. `GET /api/status`
Získání aktuálního stavu zamykání (nevyžaduje autentizaci pro základní info, nebo vrací detail po přihlášení).
- **Response (200 OK):**
  ```json
  {
    "is_locked": false,
    "last_completed_date": "2026-10-27",
    "completed_today": true,
    "server_time": "2026-10-27T17:40:00Z"
  }
  ```

#### 3. `GET /api/config`
Načtení aktuální konfigurace (vyžaduje `Authorization: Bearer <token>`).
- **Response (200 OK):** vrací objekt `math` a `rules`.

#### 4. `PUT /api/config`
Aktualizace nastavení z webového rozhraní (vyžaduje `Authorization: Bearer <token>`).
- **Request Body:**
  ```json
  {
    "math": {
      "total_questions": 10,
      "max_number": 12,
      "allowed_operations": ["multiply", "divide"]
    }
  }
  ```
- **Response (200 OK):**
  ```json
  { "success": true, "message": "Konfigurace byla uložena." }
  ```

#### 5. `POST /api/action/unlock` a `GET /api/action/unlock`
Udělení **Dne volna** – manuální vzdálené odemčení (zrušení dnešních příkladů).
- **Response (200 OK):**
  ```json
  { "success": true, "message": "Den volna byl úspěšně aktivován a počítač odemčen!" }
  ```

#### 6. `POST /api/action/lock` a `GET /api/action/lock`
Aktivace **Okamžitého studia** – okamžitě vyvolá novou sadu příkladů na počítači i v případě, že bratr dnes již úkoly splnil.
- **Response (200 OK):**
  ```json
  { "success": true, "message": "Okamžité studium bylo aktivováno a příklady zobrazeny!" }
  ```

#### 7. Webové stránky a přímé URL akce
- **`GET /administration`**: Hlavní administrační stránka s přehledem stavu, tlačítky pro Den volna, Okamžité studium a odkazem na nastavení.
- **`GET /settings`**: Stránka pro editaci počtu otázek, `max_number` a povolených početních operací.
- **`GET /day-off` (nebo `/action/day-off`)**: Přímý URL odkaz pro udělení Dne volna – po otevření v prohlížeči rovnou uvolní počítač a zobrazí potvrzení.
- **`GET /study-now` (nebo `/action/study-now`)**: Přímý URL odkaz pro spuštění Okamžitého studia – po otevření v prohlížeči rovnou uzamkne počítač novými příklady a zobrazí potvrzení.
- **`GET /unlock` a `GET /`**: Automatické přesměrování na `/administration`.

#### 8. Auto-Updater API
- **`GET /api/update/status`**: Vrátí stav aktualizátoru, aktuální a nejnovější verzi na GitHubu, čas poslední kontroly a zda je dostupný update.
  - **Response (200 OK):**
    ```json
    {
      "current_version": "1.1.0",
      "latest_version": "1.2.0",
      "is_update_available": true,
      "auto_update_enabled": true,
      "github_repo": "https://github.com/tomas-kansky/quest-lock-gate",
      "last_check_time": "2026-10-31T18:15:00Z",
      "is_updating": false,
      "status_message": null,
      "release_notes": "Popis novinek verze v1.2.0..."
    }
    ```
- **`POST /api/update/check`**: Okamžitě provede online kontrolu GitHub Releases API.
  - **Response (200 OK):**
    ```json
    {
      "success": true,
      "current_version": "1.1.0",
      "latest_version": "1.2.0",
      "is_update_available": true,
      "error_message": null,
      "release_info": { ... }
    }
    ```
- **`POST /api/update/apply`**: Spustí bezobslužné stažení nového balíčku a aktivuje worker proces `update-worker.ps1` na pozadí.
  - **Response (200 OK):**
    ```json
    {
      "success": true,
      "message": "Aktualizace byla úspěšně zahájena na pozadí. Systémová služba a Kiosk se restartují."
    }
    ```

---

## 5. Algoritmický kontrakt pro generování úloh (Math Generator Contract)

Generátor je deterministicky bezpečný a ošetřuje hraniční stavy:

```typescript
interface MathProblem {
  id: string;
  operation: "add" | "subtract" | "multiply" | "divide";
  operandA: number;
  operandB: number;
  operatorSymbol: string;
  expectedResult: number;
  displayExpression: string; // např. "81 : 9 ="
}

function generateProblem(config: MathConfig): MathProblem {
  const op = pickRandom(config.allowed_operations);
  const max = config.max_number; // výchozí: 100

  switch (op) {
    case "add": {
      // Výsledek a + b je maximálně max (100)
      const sum = randomInt(2, max);
      const a = randomInt(1, sum - 1);
      const b = sum - a;
      return {
        operation: "add",
        operandA: a,
        operandB: b,
        operatorSymbol: "+",
        expectedResult: sum,
        displayExpression: `${a} + ${b} =`
      };
    }

    case "subtract": {
      // Menšenec a je do max (např. 100 - 81)
      const a = randomInt(2, max);
      const b = randomInt(1, a);
      return {
        operation: "subtract",
        operandA: a,
        operandB: b,
        operatorSymbol: "-",
        expectedResult: a - b,
        displayExpression: `${a} - ${b} =`
      };
    }

    case "multiply": {
      // Malá násobilka do 10 (výsledek do 100)
      const factorMax = Math.min(10, Math.floor(Math.sqrt(max * 10)));
      const a = randomInt(1, 10);
      const b = randomInt(1, 10);
      return {
        operation: "multiply",
        operandA: a,
        operandB: b,
        operatorSymbol: "×",
        expectedResult: a * b,
        displayExpression: `${a} × ${b} =`
      };
    }

    case "divide": {
      // Dělení z malé násobilky do 100 (např. 100 : 10 = 10, 81 : 9 = 9)
      const b = randomInt(2, 10); // dělitel 2 až 10
      const q = randomInt(1, 10); // podíl 1 až 10
      const a = b * q;           // dělenec do 100

      return {
        operation: "divide",
        operandA: a,
        operandB: b,
        operatorSymbol: ":",
        expectedResult: q,
        displayExpression: `${a} : ${b} =`
      };
    }
  }
}
```
