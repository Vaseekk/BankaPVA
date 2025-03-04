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
Program simuluje bankovní systém s podporou běžných, spořicích (včetně studentských) a úvěrových účtů. Umožňuje klientům provádět transakce, sledovat zůstatky a úroky, zatímco bankéři mají přístup k dohledovým funkcím a administrátoři spravují uživatelská oprávnění. Systém zahrnuje logování transakcí, automatizované výpočty úroků a bezpečné ukládání citlivých dat. Implementace je realizována v C# s využitím .NET a SQLite pro správu dat.  

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
- **REQ-011:** Logy lze ukládat do souboru (CSV) nebo databáze (SQLite) pomocí rozhraní `ILogger`.  

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
5. Historie transakcí
6. Odhlásit se
Vyberte možnost (1-6): _
```

#### Zobrazení účtu
```
------------------------------------
Moje účty
------------------------------------
Číslo účtu | Typ účtu       | Zůstatek   | Úroky
------------------------------------
1001       | Běžný účet     | 5,000 Kč   | 0,00 Kč
1002       | Spořicí účet   | 10,000 Kč  | 32,08 Kč
1003       | Studentský     | 2,000 Kč   | 15,20 Kč
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
3. Export dat
4. Odhlásit se
Vyberte možnost (1-4): _
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
- **REQ-013:** Klienti: Zobrazení vlastních účtů, transakcí, vkládání/výběry, převody.  
- **REQ-014:** Bankéři: Přístup ke všem účtům, přehled vkladů/úroků.  
- **REQ-015:** Administrátoři: Správa uživatelů, oprávnění, zálohování dat.  

## Řešení chybových stavů  
- **REQ-016:** Nedostatečný zůstatek: „Nedostatek prostředků na účtu. Aktuální zůstatek: [Balance] Kč.“  
- **REQ-017:** Překročení limitu: „Překročen denní/jednorázový limit výběru. Zkuste to znovu s nižší částkou.“  
- **REQ-018:** Neplatná transakce: „Neplatná částka. Zadejte kladné číslo.“  
- **REQ-019:** Chybné přihlášení: „Nesprávné uživatelské jméno nebo heslo. Zkuste to znovu.“  
- **REQ-020:** Chybové zprávy jsou logovány do `TransactionLog`.  

## Databáze a bezpečnost  
- **REQ-021:** Databáze (SQLite): Tabulky `Users`, `Accounts`, `Transactions` pro uchování dat.  
- **REQ-022:** Bezpečnost: Hesla hashována pomocí SHA-256 s solí, validace vstupů, omezení přístupu podle rolí.  

## Počítání úroků  
### Spořicí účet  
- **REQ-023:** Úrok = (vážený průměrný zůstatek * roční úroková sazba) / 12.  
- **REQ-024:** Příklad: Zůstatek 10 000 Kč (10 dní), 15 000 Kč (15 dní), 12 000 Kč (5 dní) → `(10000 * 10 + 15000 * 15 + 12000 * 5) / 30 = 12833,33 Kč`. Úrok = `12833,33 * 0,03 / 12 = 32,08 Kč`, zaokrouhlený podle bankovních pravidel.  

### Úvěrový účet  
- **REQ-025:** Úrok se počítá pouze po skončení bezúročného období (`GracePeriodEnd`), stejným způsobem jako u spořicího účtu, ale s záporným znaménkem.