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
- **Základní třída `Account`** (abstraktní): Obsahuje atributy jako `AccountId`, `Balance`, `OwnerId`, `CreationDate` a metody `Deposit`, `Withdraw`, `GetBalance`, `LogTransaction`.  
- **Třída `CheckingAccount`** (běžný účet): Dědí z `Account`, přidává `LinkedSavingsAccountId` a metodu `TransferToSavings`.  
- **Třída `SavingsAccount`** (spořicí účet): Dědí z `Account`, obsahuje `InterestRate`, `DailyWithdrawalLimit` a metodu `CalculateMonthlyInterest`.  
- **Třída `StudentSavingsAccount`** (studentský spořicí účet): Dědí z `SavingsAccount`, přidává `SingleWithdrawalLimit`.  
- **Třída `CreditAccount`** (úvěrový účet): Dědí z `Account`, obsahuje `CreditLimit`, `InterestRate`, `GracePeriodEnd` a metody `Borrow`, `Repay`, `CalculateMonthlyInterest`.  
- **Třída `User`**: Spravuje `UserId`, `Username`, `PasswordHash`, `Role` (enum: Client, Banker, Admin) a metody `Authenticate`, `GetAccounts`.  
- **Třída `BankSystem`**: Koordinuje `Accounts`, `Users`, `TransactionLog` a metody `SimulateTime`, `SaveLogs`.  

### Práce s časem  
- Systém využívá `DateTime.Now` pro reálný čas, ale implementuje `SimulateTime` pro testování (posun času pro výpočet úroků).  
- Úroky se počítají na konci měsíce (30 dní).  

### Transakce a logování  
- Transakce (vklady, výběry, převody, úroky) jsou logovány do `TransactionLog` s údaji o datu, typu, účtu, částce a novém zůstatku.  
- Logy lze ukládat do souboru (CSV) nebo databáze (SQLite) pomocí rozhraní `ILogger`.  

## Uživatelské rozhraní  
### Návrh rozhraní  
- **Konzolová aplikace**: Textové menu s číslovanými položkami (přihlášení, odhlášení, správa účtů, logování).  
- **WPF (alternativa)**: Formuláře s textovými poli, tlačítky („Potvrdit“, „Zrušit“), seznamy (ListBox, DataGrid) pro zobrazení účtů a transakcí.  

### Role uživatelů  
- **Klienti**: Zobrazení vlastních účtů, transakcí, vkládání/výběry, převody.  
- **Bankéři**: Přístup ke všem účtům, přehled vkladů/úroků.  
- **Administrátoři**: Správa uživatelů, oprávnění, zálohování dat.  

## Řešení chybových stavů  
- **Nedostatečný zůstatek**: „Nedostatek prostředků na účtu. Aktuální zůstatek: [Balance] Kč.“  
- **Překročení limitu**: „Překročen denní/jednorázový limit výběru. Zkuste to znovu s nižší částkou.“  
- **Neplatná transakce**: „Neplatná částka. Zadejte kladné číslo.“  
- **Chybné přihlášení**: „Nesprávné uživatelské jméno nebo heslo. Zkuste to znovu.“  
- Chybové zprávy jsou logovány do `TransactionLog`.  

## Databáze a bezpečnost  
- **Databáze (SQLite)**: Tabulky `Users`, `Accounts`, `Transactions` pro uchování dat.  
- **Bezpečnost**: Hesla hashována pomocí SHA-256 s solí, validace vstupů, omezení přístupu podle rolí.  

## Počítání úroků  
### Spořicí účet  
- Úrok = (vážený průměrný zůstatek * roční úroková sazba) / 12.  
- Příklad: Zůstatek 10 000 Kč (10 dní), 15 000 Kč (15 dní), 12 000 Kč (5 dní) → `(10000 * 10 + 15000 * 15 + 12000 * 5) / 30 = 12833,33 Kč`. Úrok = `12833,33 * 0,03 / 12 = 32,08 Kč`, zaokrouhlený podle bankovních pravidel.  

### Úvěrový účet  
- Úrok se počítá pouze po skončení bezúročného období (`GracePeriodEnd`), stejným způsobem jako u spořicího účtu, ale s záporným znaménkem.