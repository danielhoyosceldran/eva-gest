# Stack tecnològic i arquitectura

## App gestió barberia — Projecte personal

---

## 1. Stack tecnològic

| Capa | Tecnologia | Motiu |
|---|---|---|
| UI | **WPF** (.NET 10) | Interfície moderna, sense servidor, accés directe a dades |
| Estil UI | **WPF pur** (`Style` i `ControlTemplate` propis) | Control total sobre l'aspecte, sense dependències externes de disseny |
| Lampisteria MVVM | **CommunityToolkit.Mvvm** | Paquet oficial de Microsoft. Genera `INotifyPropertyChanged` i comandaments, estalvia centenars de línies repetitives |
| Calendari/Agenda | **WPF natiu** (graella setmanal amb `ItemsControl`, 7 columnes) | Vista setmanal completa i navegació entre setmanes, sense dependències externes |
| Base de dades | **SQLite** | Fitxer local, sense instal·lació de servidor, ideal per a un únic PC |
| Representació de diners | **`INTEGER` en cèntims** | Precisió exacta i ordenació dins de SQLite (vegeu 6.3 dels requeriments) |
| Accés a dades | **Entity Framework Core** | Evita escriure SQL a mà i aporta migracions d'esquema |
| Migracions | **EF Core Migrations** | Actualitzar l'esquema sense perdre dades |
| Logging | **Serilog** (fitxer de text pla) | Senzill, escriu logs sense configuració complexa |
| Proves | **xUnit v3** + **AwesomeAssertions** | Estàndard de .NET; AwesomeAssertions és Apache 2.0 (vegeu la nota) |
| BD a les proves | **SQLite en memòria** | Motor real: aplica `CHECK`, claus foranes i índexs únics |

**Per què no Angular/React + backend:** afegirien un servidor, una API i més capes de comunicació sense cap benefici real. L'aplicació l'opera sempre una sola persona alhora en un sol PC — el suport a diverses treballadores és una dada de negoci, no un accés concurrent que necessiti servidor.

---

## 2. Arquitectura de capes

```
┌─────────────────────────┐
│   UI (WPF + XAML)        │  ← Pantalles, controls i styles propis
├─────────────────────────┤
│   ViewModels (MVVM)      │  ← Lògica de presentació
├─────────────────────────┤
│   Serveis / Lògica       │  ← Càlculs (balanç, freqüència visites...)
├─────────────────────────┤
│   Accés a dades (EF Core)│  ← DbContext, consultes
├─────────────────────────┤
│   SQLite (fitxer .db)    │  ← Dades persistents
└─────────────────────────┘
```

### Per què MVVM

Separa la interfície (XAML) de la lògica (C#). Si un dia vols canviar com es veu una pantalla, no toques la lògica de negoci, i viceversa.

---

## 3. Estructura de carpetes proposada

```
BarberiaApp/
├── Models/            # Client, Cita, Venda, Servei, Producte, Treballadora...
├── ViewModels/         # Lògica de cada pantalla
├── Views/               # XAML de cada pantalla
├── Services/            # Lògica de negoci (balanç, freqüència, solapament, exportació...)
├── Data/                 # DbContext i migracions EF Core
├── Helpers/             # Utilitats (formatació, validacions...)
├── Logging/             # Configuració de Serilog
├── Backup/              # Còpia i restauració de la base de dades
└── appsettings.json     # Només la ruta de la base de dades (la resta de config viu a la BD)
```

I un projecte germà per a les proves:

```
BarberiaApp.Tests/
├── Infra/               # BaseDadesProva, constructors de dades
├── Calculs/             # Blocs A, B, C: IVA, agregació, diners
├── Serveis/             # Blocs D-J: un fitxer per servei
├── Escenaris/           # Bloc K: dies i períodes complets
└── Backup/              # Bloc L
```

---

## 4. Flux de dades (exemple: nova venda)

```
Usuari clica "Nova venda"
        ↓
View (XAML) mostra formulari
        ↓
ViewModel recull dades i valida
        ↓
Service calcula el total
        ↓
El servei guarda via DbContext a SQLite
        ↓
ViewModel actualitza la UI (confirmació visual)
```

---

## 5. Base de dades — taules principals

- `Clients` (id, client_key, nom, mobil, correu, data_naixement?, observacions, adormit)
- `Treballadores` (id, nom, actiu, color)
- `HorarisTreballadora` (id, treballadora_id, dia_setmana, hora_inici, hora_fi)
- `Cites` (id, data, hora, durada_min, client_id?, nom_convidat?, telefon_convidat?, servei_id?, treballadora_id?, estat, observacions)
- `Serveis` (id, nom, preu_cents, iva_bp, durada_min?, actiu)
- `Productes` (id, nom, preu_cents, categoria, iva_bp, actiu)
- `MetodesPagament` (id, nom, actiu)
- `Vendes` (id, data, hora, client_id?, nom_convidat?, telefon_convidat?, cita_id?, treballadora_id?, metode_pagament_id, base_cents, iva_cents, total_cents, iva_mode, estat, observacions)
- `VendaLinies` (id, venda_id, servei_id?, producte_id?, descripcio, quantitat, preu_unitari_cents, iva_bp, import_cents)
- `VendaDesglossaments` (id, venda_id, iva_bp, base_cents, iva_cents, total_cents)
- `MovimentsCaixa` (id, data, tipus, import_cents, base_cents?, iva_cents?, iva_bp?, metode_pagament_id, concepte, observacions)
- `HorariBarberia` (id, dia_setmana, hora_obertura, hora_tancament)
- `DiesTancats` (id, data, motiu?)
- `Configuracio` (clau, valor)

> El detall complet (tipus, claus, índexs i diagrama ER) es documentarà a l'esquema de base de dades.

**Informes per treballadora:** no calen taules addicionals. El desglossament per servei/producte i per dia de la setmana s'obté agregant `VendaLinies` i `Vendes` (filtrant `Estat = Activa`), sense necessitat de vistes materialitzades per al volum de dades esperat. Els percentatges de treball i de venda de productes es calculen al vol a partir de les mateixes agregacions, sense guardar-los.

---

## 6. Migracions

**EF Core Migrations**, aplicades automàticament a l'inici de l'aplicació. Abans d'aplicar-ne cap es fa una còpia de seguretat de la base de dades.

---

## 7. Backup i logging

**Backup:**

- Automàtic diari a l'hora configurada (per defecte 20:00 h)
- Manual sota demanda des de Configuració
- Retenció configurable (per defecte 15 còpies); les més antigues s'esborren automàticament
- Restauració seleccionant qualsevol còpia disponible, amb confirmació prèvia

**Logging:** fitxer de text (`log_YYYYMMDD.txt`) amb errors, excepcions i esdeveniments clau (inici app, operacions importants).

---

## 8. Resum de decisions

| Decisió | Alternativa descartada | Motiu |
|---|---|---|
| WPF | Angular/React + backend | Sense servidor, un sol operador de l'app, menys peces mòbils |
| SQLite | SQL Server / PostgreSQL | No cal servidor de BD per a un únic PC |
| Agenda amb WPF natiu | Syncfusion Community | Una graella setmanal amb `ItemsControl` cobreix la vista setmanal demanada sense arrossegar cites ni vista mensual. Menys dependències |
| EF Core Migrations | Sense migracions (`EnsureCreated`) | Permet evolucionar l'esquema sense perdre les dades existents |
| WPF pur (sense llibreries d'estil) | MaterialDesignInXaml | Queda forçat en WPF; amb `Style` propis s'obté millor resultat i control total |
| MVVM | Codi tot barrejat a la View | Facilita corregir errors i afegir funcions més endavant |
| AwesomeAssertions | FluentAssertions | FluentAssertions 8+ té llicència comercial que exclou el programari usat per entitats que facturen. La bifurcació comunitària manté Apache 2.0 amb la mateixa API |
| SQLite en memòria a les proves | Proveïdor InMemory d'EF Core | InMemory no aplica `CHECK`, claus foranes ni índexs únics: les proves de restriccions passarien encara que estiguessin mal escrites |
