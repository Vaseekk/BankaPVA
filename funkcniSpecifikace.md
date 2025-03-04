# Funkční specifikace – Objektově orientované programování a Projekt Banka (Fáze 2)
* *SSPŠ*
* *Verze 1*
* *Václav Bohdanecký*
* *4.3.2025*

## Obsah
1. Historie Dokumentu  
2. Úvod  
3. Celková hrubá architektura  
4. Popis programu  
5. Účel aplikace  
6. Technická realizace požadavků  
7. Uživatelské rozhraní  
8. Řešení chybových stavů  
9. Databáze a bezpečnost  
10. Počítání úroků  

## Historie Dokumentu
### Verze 1  
* **Autor:** Václav Bohdanecký  
* **Komentář:** První verze funkční specifikace pro projekt Banka, popisující technickou realizaci požadavků.  

## Úvod  
* **Účel dokumentu** – Dokument popisuje technickou realizaci požadavků na projekt „Banka“, včetně návrhu struktury, uživatelského rozhraní, řešení chyb a dalších aspektů implementace.  
* **Kontakt:** bohdanecky.va.2022@skola.ssps.cz  

## Celková hrubá architektura  
* **Jazyk aplikace:** Angličtina 
* **Spuštění:** Konzolová aplikace (C# a .NET)
* **Databáze:** SQL databáze (SQLite)  

## Popis programu  
Program simuluje bankovní systém s podporou běžných, spořicích (včetně studentských) a úvěrových účtů. Umožňuje klientům provádět transakce, sledovat zůstatky a úroky, zatímco bankéři mají přístup k dohledovým funkcím a administrátoři spravují uživatelská oprávnění. Systém zahrnuje logování transakcí do textového souboru `logs` ve formátu plain text, automatizované výpočty úroků a bezpečné ukládání citlivých dat. Implementace je realizována v C# s využitím .NET a SQLite pro správu dat.  

## Účel aplikace  
Účelem aplikace je vytvořit realistickou simulaci bankovního systému, která umožní studentům procvičit principy objektově orientovaného programování a získat praktické zkušenosti s vývojem komplexního systému.  

## Technická realizace požadavků  
### Struktura tříd  
- **REQ-001:** Základní třída `Account` (abstraktní): Obsahuje atributy jako `AccountId`, `Balance`, `OwnerId`, `CreationDate` a metody `Deposit`, `Withdraw`, `GetBalance`, `LogTransaction`.  
- **REQ-002:** Třída `CheckingAccount` (běžný účet): Dědí z `Account`, přidává `LinkedSavingsAccountId` a metodu `TransferToSavings`.  
- **REQ-003:** Třída `SavingsAccount` (spořicí účet): Dědí z `Account`, obsahuje `InterestRate`, `DailyWithdrawalLimit` a metodu `CalculateMonthlyInterest`.  
- **REQ-004:** Třída `StudentSavingsAccount` (studentský spořicí účet): Dědí z `SavingsAccount`, přidává `SingleWithdrawalLimit`.  
- **REQ-005:** Třída `CreditAccount` (úvěrový účet): Dědí z `Account`, obsahuje `CreditLimit`, `InterestRate`, `GracePeriodEnd` a metody `Borrow`, `Repay`, `CalculateMonthlyInterest`.  
- **REQ-006:** Třída `User`: Spravuje `UserId`, `Username`, `PasswordHash`, `Role` (enum: Client, Banker, Admin) a metody `Authenticate`, `GetAccounts`.  
- **REQ-007:** Třída `BankSystem`: Koordinuje `Accounts`, `Users`, `TransactionLog` a metody `SimulateTime`, `SaveLogs`.  

### Práce s časem  
- **REQ-008:** Systém využívá `DateTime.Now` pro reálný čas, ale implementuje `SimulateTime` pro testování (posun času pro výpočet úroků).  
- **REQ-009:** Úroky se počítají na konci měsíce (30 dní).  

### Transakce a logování  
- **REQ-010:** Transakce (vklady, výběry, převody, úroky) jsou logovány do `TransactionLog` s údaji o datu, typu, účtu, částce a novém zůstatku.  
- **REQ-011:** Logy jsou ukládány do textového souboru `logs` ve formátu plain text (např. „[Datum] [Typ]: [Účet] [Částka] [Nový zůstatek]“).  

## Uživatelské rozhraní  
### Návrh rozhraní  
- **REQ-012:** Konzolová aplikace: Textové menu s číslovanými položkami (přihlášení, odhlášení, správa účtů, logování).  

#### Uvítací menu (hlavní menu)
```
------------------------------------
Bankovní systém SSPŠ
------------------------------------
1. Přihlásit se
2. Ukončit
Vyberte možnost (1-2): _
```

#### Přihlášení
```
------------------------------------
Přihlášení do bankovního systému
------------------------------------
Zadejte uživatelské jméno: _
Zadejte heslo: _
1. Přihlásit
2. Zpět
Vyberte možnost (1-2): _
```

#### Hlavní menu pro klienty
```
------------------------------------
Můj účet - [Uživatelské jméno]
------------------------------------
1. Zobrazit moje účty
2. Vložit peníze
3. Vybrat peníze
4. Převod mezi účty
5. Požádat o půjčku
6. Splátka půjčky
7. Historie transakcí
8. Odhlásit se
Vyberte možnost (1-8): _
```

#### Zobrazení účtu
```
------------------------------------
Moje účty
------------------------------------
Číslo účtu | Typ účtu       | Zůstatek   | Úroky      | Dluh (při úvěru)
------------------------------------
1001       | Běžný účet     | 5,000 Kč   | 0,00 Kč    | 0,00 Kč
1002       | Spořicí účet   | 10,000 Kč  | 32,08 Kč   | 0,00 Kč
1003       | Úvěrový účet   | -2,000 Kč  | -50,00 Kč  | 2,000 Kč
------------------------------------
1. Zpět
Vyberte možnost (1): _
```

#### Vklad
```
------------------------------------
Vložit peníze
------------------------------------
Vyberte účet (zadejte číslo účtu): _
Zadejte částku k vložení: _
1. Potvrdit
2. Zrušit
Vyberte možnost (1-2): _
```

#### Menu pro bankéře
```
------------------------------------
Správa účtů - [Uživatelské jméno]
------------------------------------
1. Zobrazit všechny účty
2. Celkový přehled vkladů a úroků
3. Provést půjčku pro klienta
4. Export dat
5. Odhlásit se
Vyberte možnost (1-5): _
```

#### Admin menu
```
------------------------------------
Správa uživatelů - [Uživatelské jméno]
------------------------------------
1. Zobrazit uživatele
2. Přidat uživatele
3. Upravit uživatele
4. Smazat uživatele
5. Změnit roli
6. Odhlásit se
Vyberte možnost (1-6): _
```

### Role uživatelů  
- **REQ-013:** Klienti: Zobrazení vlastních účtů, transakcí, vkládání/výběry, převody, žádosti o půjčky a splátky půjček.  
- **REQ-014:** Bankéři: Přístup ke všem účtům, přehled vkladů/úroků, možnost provést půjčku pro klienta.  
- **REQ-015:** Administrátoři: Správa uživatelů, oprávnění, zálohování dat.  

## Řešení chybových stavů  
- **REQ-016:** Nedostatečný zůstatek: „Nedostatek prostředků na účtu. Aktuální zůstatek: [Balance] Kč.“  
- **REQ-017:** Překročení limitu: „Překročen denní/jednorázový limit výběru. Zkuste to znovu s nižší částkou.“  
- **REQ-018:** Neplatná transakce: „Neplatná částka. Zadejte kladné číslo.“  
- **REQ-019:** Chybné přihlášení: „Nesprávné uživatelské jméno nebo heslo. Zkuste to znovu.“  
- **REQ-020:** Chybové zprávy jsou logovány do `TransactionLog`.  

## Databáze a bezpečnost  
- **REQ-021:** Databáze (SQLite): Tabulky `Users`, `Accounts`, `Transactions` pro uchování dat.  
  - **Tabulka `Users`**:
    - `UserId` (INTEGER PRIMARY KEY AUTOINCREMENT): Jedinečné ID uživatele.
    - `Username` (TEXT NOT NULL UNIQUE): Uživatelské jméno.
    - `PasswordHash` (TEXT NOT NULL): Hashované heslo (SHA-256).
    - `Salt` (TEXT NOT NULL): Náhodná sůl pro hashování.
    - `Role` (TEXT NOT NULL): Role uživatele (Client, Banker, Admin).
    - `CreatedAt` (DATETIME DEFAULT CURRENT_TIMESTAMP): Datum vytvoření účtu.
  - **Tabulka `Accounts`**:
    - `AccountId` (INTEGER PRIMARY KEY AUTOINCREMENT): Jedinečné ID účtu.
    - `OwnerId` (INTEGER NOT NULL, FOREIGN KEY (UserId)): ID vlastníka účtu.
    - `Type` (TEXT NOT NULL): Typ účtu (Checking, Savings, StudentSavings, Credit).
    - `Balance` (DECIMAL NOT NULL): Aktuální zůstatek.
    - `CreationDate` (DATETIME DEFAULT CURRENT_TIMESTAMP): Datum vytvoření účtu.
    - `InterestRate` (DECIMAL): Roční úroková sazba (pro spořicí/úvěrové účty, NULL pro běžné).
    - `DailyWithdrawalLimit` (DECIMAL): Denní limit výběru (pro spořicí účty, NULL pro jiné).
    - `SingleWithdrawalLimit` (DECIMAL): Jednorázový limit výběru (pro studentské účty, NULL pro jiné).
    - `CreditLimit` (DECIMAL): Úvěrový rámec (pro úvěrové účty, NULL pro jiné).
    - `GracePeriodEnd` (DATETIME): Konec bezúročného období (pro úvěrové účty, NULL pro jiné).
  - **Tabulka `Transactions`**:
    - `TransactionId` (INTEGER PRIMARY KEY AUTOINCREMENT): Jedinečné ID transakce.
    - `AccountId` (INTEGER NOT NULL, FOREIGN KEY (AccountId)): ID účtu.
    - `DateTime` (DATETIME DEFAULT CURRENT_TIMESTAMP): Datum a čas transakce.
    - `Type` (TEXT NOT NULL): Typ transakce (Deposit, Withdraw, Transfer, Interest, Borrow, Repay).
    - `Amount` (DECIMAL NOT NULL): Částka transakce.
    - `NewBalance` (DECIMAL NOT NULL): Nový zůstatek po transakci.
- **REQ-022:** Bezpečnost: Hesla hashována pomocí SHA-256 s solí, validace vstupů, omezení přístupu podle rolí.  
  - **Knihovny pro hashování**: Použijeme `System.Security.Cryptography` pro hashování hesel pomocí SHA-256. Příklad implementace:
    ```csharp
    using System.Security.Cryptography;
    using System.Text;

    public string HashPassword(string password, string salt)
    {
        using (var sha256 = SHA256.Create())
        {
            string saltedPassword = password + salt;
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(saltedPassword));
            return Convert.ToBase64String(hashedBytes);
        }
    }

    public string GenerateSalt()
    {
        byte[] randomBytes = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        return Convert.ToBase64String(randomBytes);
    }
    ```
  - Hesla budou uložena v `PasswordHash` spolu s náhodnou solí (uloženou v samostatném sloupci `Salt` v tabulce `Users`), což zajistí bezpečnost proti útokům jako rainbow table.

## Počítání úroků  
### Spořicí účet  
- **REQ-023:** Úrok = (vážený průměrný zůstatek * roční úroková sazba) / 12.  
- **REQ-024:** Příklad: Zůstatek 10 000 Kč (10 dní), 15 000 Kč (15 dní), 12 000 Kč (5 dní) → `(10000 * 10 + 15000 * 15 + 12000 * 5) / 30 = 12833,33 Kč`. Úrok = `12833,33 * 0,03 / 12 = 32,08 Kč`, zaokrouhlený podle bankovních pravidel.  

### Úvěrový účet  
- **REQ-025:** Úrok se počítá pouze po skončení bezúročného období (`GracePeriodEnd`), stejným způsobem jako u spořicího účtu, ale s záporným znaménkem.