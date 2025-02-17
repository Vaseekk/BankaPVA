# Model Banky - Požadavky na software
* *SSPŠ*
* *Verze 1*
* *Václav Bohdanecký*
* *17.2.2025*

## Obsah
1. Historie Dokumentu
2. Úvod
3. Celková hrubá architektura
4. Popis programu
5. Účel aplikace
6. Funkční požadavky
7. Nefunkční požadavky
8. Počítání úroků

## Historie Dokumentu
### Verze 1
* **Autor:** Václav Bohdanecký
* **Komentář:** První verze dokumentu

## Úvod
* **Účel dokumentu** – Cílem dokumentu je popsání všech požadovaných funkcí programu a nefunkčních požadavků.
* **Kontakt:** bohdanecky.va.2022@skola.ssps.cz

## Celková hrubá architektura
* **Jazyk aplikace:** Angličtina
* **Spuštění:** Konzolová aplikace (C# a .NET)
* **Databáze:** SQL databáze (SQLite)

## Popis programu
Program by měl simulovat bankovní systém s podporou různých typů účtů. Uživatelé mohou spravovat své účty, provádět transakce a sledovat stav zůstatků. Bankovní zaměstnanci mají přístup k dohledovým funkcím, zatímco administrátoři mohou spravovat uživatelská oprávnění. Systém zajišťuje bezpečné přihlašování, logování událostí a automatizované výpočty úroků. Implementace je realizována v C# s využitím .NET a SQLite pro správu dat.

## Účel aplikace
Účelem aplikace je poskytnutí realistické simulace bankovního prostředí, kde si uživatelé mohou provádět běžné bankovní operace a zaměstnanci banky mají k dispozici dohledové nástroje.

## Funkční požadavky
### Typy účtů
1. **Běžný účet** (Priorita: Střední)
   - Umožňuje transakce (příjem a odesílání plateb).
   - Možnost převodu mezi běžným a spořicím účtem.
   - Není úročený.

2. **Spořicí účet** (Priorita: Vysoká)
   - Roční úroková sazba.
   - Omezený výběr při kladném zůstatku.
   - Studentská varianta: omezené výběrové limity.
   
3. **Úvěrový účet** (Priorita: Vysoká)
   - Čerpání prostředků do výše úvěrového rámce.
   - Roční úrok stanoven bankou.
   - Splácení nejprve úroků, poté jistiny.
   - Bezúročné období.

### Implementace
- **Struktura tříd s dědičností** (Priorita: Střední)
- **Simulace časových událostí (přepis úroků, splátky)** (Priorita: Vysoká)
- **Logování všech transakcí** (Priorita: Vysoká)

### Uživatelské role
1. **Klienti** – Správa vlastních účtů. (Priorita: Vysoká)
2. **Bankéři** – Přehled nad všemi účty. (Priorita: Střední)
3. **Administrátoři** – Správa uživatelů a oprávnění. (Priorita: Střední)

## Nefunkční požadavky
- **Bezpečnost – Hashování hesel** (Priorita: Vysoká)
- **Dostupnost – Online i offline provoz** (Priorita: Střední)
- **Rozšiřitelnost – Možnost přidání nových typů účtů** (Priorita: Nízká)

## Počítání úroků
### Spořicí účet (Priorita: Vysoká)
Úrok se počítá podle váženého průměru zůstatků během měsíce. Celkový průměrný zůstatek se vynásobí roční úrokovou sazbou a vydělí dvanácti, aby se získala měsíční výše úroku.

### Úvěrový účet (Priorita: Vysoká)
- Pokud je úvěr v bezúročném období, úrok se nepočítá.
- Po skončení bezúročného období se úrok počítá stejným způsobem jako u spořicího účtu, ale výsledná částka je záporná (znamená náklady pro klienta).

