# Esquema de base de dades

## 1. Convencions generals

| Convenció | Decisió |
|---|---|
| Motor | SQLite (fitxer local) |
| Claus primàries | `INTEGER PRIMARY KEY AUTOINCREMENT`, excepte `Configuracio` (`clau TEXT PRIMARY KEY`) |
| **Imports monetaris** | `INTEGER` en **cèntims**. Mai `decimal`, `float` ni `double` (decisió 6.3) |
| **Percentatges d'IVA** | `INTEGER` en **punts base** (base 10.000): `2100` = 21,00% · `520` = 5,20% |
| Quantitats | `INTEGER` (no té sentit una quantitat fraccionada) |
| Dates | `TEXT` en format ISO `YYYY-MM-DD` |
| Hores | `TEXT` en format `HH:MM` |
| Dies de la setmana | `TEXT` curt: `Dl`, `Dt`, `Dc`, `Dj`, `Dv`, `Ds`, `Dg` |
| Booleans | `INTEGER` (0/1) — SQLite no té tipus booleà natiu |
| Noms de taula | Plural, `PascalCase` |
| Noms de camp | `snake_case` en minúscules; sufix `_cents` per a imports i `_bp` per a percentatges |

**Convenció de noms explícita.** Tot camp monetari acaba en `_cents` i tot percentatge en `_bp`. Això fa impossible confondre unitats en llegir el codi: `total_cents` mai s'interpretarà com a euros.

---

## 2. Taules

### 2.1. `Clients`

| Camp | Tipus | Obligatori | Notes |
|---|---|---|---|
| `id` | INTEGER PK | Sí | Autoincremental |
| `client_key` | TEXT | Sí | **Índex únic.** Nom sencer normalitzat (decisió 6.1) |
| `nom` | TEXT | Sí | |
| `mobil` | TEXT | Sí | |
| `correu` | TEXT | No | |
| `data_naixement` | TEXT (data) | No | Per a l'avís d'aniversari (RF-03) |
| `observacions` | TEXT | No | |
| `adormit` | INTEGER | Sí | Per defecte `0` (decisió 6.2) |

**Índexs:** `client_key` (únic), `nom`, `mobil` (per a la cerca de RF-03).

---

### 2.2. `Treballadores`

| Camp | Tipus | Obligatori | Notes |
|---|---|---|---|
| `id` | INTEGER PK | Sí | |
| `nom` | TEXT | Sí | |
| `actiu` | INTEGER | Sí | Per defecte `1`. `0` = de vacances (RF-06) |
| `color` | TEXT | Sí | Codi hexadecimal, per diferenciar-la a la UI |

---

### 2.3. `HorarisTreballadora`

Una fila per franja horària treballada (permet horaris partits, p. ex. matí i tarda).

| Camp | Tipus | Obligatori | Notes |
|---|---|---|---|
| `id` | INTEGER PK | Sí | |
| `treballadora_id` | INTEGER FK → `Treballadores.id` | Sí | `ON DELETE CASCADE` |
| `dia_setmana` | TEXT | Sí | `Dl`…`Dg` |
| `hora_inici` | TEXT | Sí | |
| `hora_fi` | TEXT | Sí | |

**Índex:** `treballadora_id`.

---

### 2.4. `Serveis`

| Camp | Tipus | Obligatori | Notes |
|---|---|---|---|
| `id` | INTEGER PK | Sí | |
| `nom` | TEXT | Sí | |
| `preu_cents` | INTEGER | Sí | |
| `iva_bp` | INTEGER | Sí | **IVA propi** d'aquest servei (6.4) |
| `durada_min` | INTEGER | No | Si és `NULL`, s'usa la durada per defecte (RF-23) |
| `actiu` | INTEGER | Sí | Per defecte `1` |

---

### 2.5. `Productes`

| Camp | Tipus | Obligatori | Notes |
|---|---|---|---|
| `id` | INTEGER PK | Sí | |
| `nom` | TEXT | Sí | |
| `preu_cents` | INTEGER | Sí | |
| `categoria` | TEXT | No | |
| `iva_bp` | INTEGER | Sí | **IVA propi** d'aquest producte (6.4) |
| `actiu` | INTEGER | Sí | Per defecte `1` |

---

### 2.6. `MetodesPagament`

| Camp | Tipus | Obligatori | Notes |
|---|---|---|---|
| `id` | INTEGER PK | Sí | |
| `nom` | TEXT | Sí | **Índex únic** |
| `actiu` | INTEGER | Sí | Per defecte `1` |

---

### 2.7. `Cites`

| Camp | Tipus | Obligatori | Notes |
|---|---|---|---|
| `id` | INTEGER PK | Sí | |
| `data` | TEXT (data) | Sí | |
| `hora` | TEXT | Sí | |
| `durada_min` | INTEGER | Sí | Calculada del servei o del valor per defecte (RF-02) |
| `client_id` | INTEGER FK → `Clients.id` | No* | `ON DELETE CASCADE` |
| `nom_convidat` | TEXT | No* | Client no registrat (RF-05bis) |
| `telefon_convidat` | TEXT | No | Opcional fins i tot per a convidats |
| `servei_id` | INTEGER FK → `Serveis.id` | No | `ON DELETE SET NULL`. Opcional (RF-02) |
| `treballadora_id` | INTEGER FK → `Treballadores.id` | No | `ON DELETE SET NULL`. Opcional |
| `estat` | TEXT | Sí | `Pendent` / `Realitzada` / `Cancellada` / `NoAssistida` (noms dels membres de l'enum). Per defecte `Pendent` |
| `observacions` | TEXT | No | |

\* **Regla (CHECK):** exactament un dels dos ha d'estar informat — `client_id` (client registrat) **o** `nom_convidat` (convidat), mai els dos ni cap.

**Índexs:** `(data, hora)` compost (agenda), `client_id`, `treballadora_id`, `estat`.

---

### 2.8. `Vendes`

| Camp | Tipus | Obligatori | Notes |
|---|---|---|---|
| `id` | INTEGER PK | Sí | |
| `data` | TEXT (data) | Sí | |
| `hora` | TEXT | Sí | |
| `client_id` | INTEGER FK → `Clients.id` | No* | `ON DELETE CASCADE` |
| `nom_convidat` | TEXT | No* | |
| `telefon_convidat` | TEXT | No | |
| `cita_id` | INTEGER FK → `Cites.id` | No | `ON DELETE SET NULL`. **Índex únic** (una cita genera com a màxim una venda) |
| `treballadora_id` | INTEGER FK → `Treballadores.id` | No | `ON DELETE SET NULL` |
| `metode_pagament_id` | INTEGER FK → `MetodesPagament.id` | Sí | |
| `base_cents` | INTEGER | Sí | Σ de les bases per grup d'IVA (6.4) |
| `iva_cents` | INTEGER | Sí | Σ de les quotes per grup d'IVA |
| `total_cents` | INTEGER | Sí | Σ dels imports de línia |
| `iva_mode` | TEXT | Sí | `Inclos` / `NoInclos`. Còpia del mode vigent en registrar la venda |
| `estat` | TEXT | Sí | `Activa` / `Anullada` (RF-10). Per defecte `Activa` |
| `observacions` | TEXT | No | |

\* Mateixa regla CHECK que a `Cites`: `client_id` o `nom_convidat`, mai els dos ni cap.

**Invariant:** `base_cents + iva_cents = total_cents`, sempre, per construcció de l'algoritme 6.4.

**Per què `iva_mode` es copia a cada venda:** si l'usuària canvia el mode global a la configuració, les vendes ja registrades han de continuar sent auditables amb el criteri que es va aplicar en aquell moment.

**Índexs:** `data`, `client_id`, `treballadora_id`, `estat` (per filtrar `Activa` a totes les agregacions).

---

### 2.9. `VendaLinies`

Cada línia guarda una **còpia congelada** de descripció, preu i IVA en el moment de la venda. Si demà es canvia el preu o l'IVA d'un producte al catàleg, aquesta línia no es veu afectada (6.4).

| Camp | Tipus | Obligatori | Notes |
|---|---|---|---|
| `id` | INTEGER PK | Sí | |
| `venda_id` | INTEGER FK → `Vendes.id` | Sí | `ON DELETE CASCADE` |
| `servei_id` | INTEGER FK → `Serveis.id` | No | `ON DELETE SET NULL`. Només per a agregacions (RF-16-D), **no** és la font del preu |
| `producte_id` | INTEGER FK → `Productes.id` | No | `ON DELETE SET NULL`. Idem |
| `descripcio` | TEXT | Sí | Còpia lliure; permet línies 100% personalitzades |
| `quantitat` | INTEGER | Sí | |
| `preu_unitari_cents` | INTEGER | Sí | Còpia del preu en el moment de la venda |
| `iva_bp` | INTEGER | Sí | **Còpia del percentatge d'IVA aplicat** en aquell moment |
| `import_cents` | INTEGER | Sí | `preu_unitari_cents × quantitat`. Valor exacte, sense arrodonir |

**Classificació de la línia** (per a RF-16-D i RF-17):

| Cas | Compta com a |
|---|---|
| `servei_id` informat | Servei |
| `producte_id` informat | Producte |
| Cap dels dos | **Altres** (línia personalitzada) |

**CHECK:** `servei_id` i `producte_id` no poden estar informats alhora.

**Índexs:** `venda_id`, `servei_id`, `producte_id` (per al desglossament RF-16-D).

---

### 2.10. `VendaDesglossaments`

Una fila per cada tipus d'IVA present a la venda. **Aquesta taula és la font de veritat fiscal.**

| Camp | Tipus | Obligatori | Notes |
|---|---|---|---|
| `id` | INTEGER PK | Sí | |
| `venda_id` | INTEGER FK → `Vendes.id` | Sí | `ON DELETE CASCADE` |
| `iva_bp` | INTEGER | Sí | Tipus impositiu d'aquest grup |
| `base_cents` | INTEGER | Sí | Base imposable del grup |
| `iva_cents` | INTEGER | Sí | Quota del grup |
| `total_cents` | INTEGER | Sí | Suma dels imports de línia d'aquest tipus |

**Per què cal aquesta taula.** El model 303 exigeix declarar la base i la quota **per tipus impositiu**. Si una venda barreja tipus (per exemple un servei al 21% i un producte al 10%), els camps agregats de `Vendes` no permeten separar-los després.

Recalcular-ho a partir de `VendaLinies` en el moment de consultar donaria una xifra lleugerament diferent de la que es va veure el dia de la venda, i la divergència creix amb el temps (vegeu decisió 6.4). Guardant el desglossament congelat, la suma d'un període és exacta i reproduïble.

**Índexs:** `venda_id`, `iva_bp`.

**Invariants:**

```
Vendes.base_cents  = Σ VendaDesglossaments.base_cents  d'aquella venda
Vendes.iva_cents   = Σ VendaDesglossaments.iva_cents   d'aquella venda
Vendes.total_cents = Σ VendaDesglossaments.total_cents d'aquella venda
```

Els camps de `Vendes` són redundants a propòsit: eviten haver de fer una unió per mostrar el total d'una venda a una llista.

---

### 2.11. `MovimentsCaixa`

| Camp | Tipus | Obligatori | Notes |
|---|---|---|---|
| `id` | INTEGER PK | Sí | |
| `data` | TEXT (data) | Sí | |
| `tipus` | TEXT | Sí | `Entrada` / `Sortida` |
| `import_cents` | INTEGER | Sí | Import final (equivalent al `total_cents` de `Vendes`) |
| `base_cents` | INTEGER | No | Només si `aplicar_iva_caixa` està activat |
| `iva_cents` | INTEGER | No | Quota d'IVA. Només si `aplicar_iva_caixa` està activat |
| `iva_bp` | INTEGER | No | Percentatge aplicat, si escau |
| `metode_pagament_id` | INTEGER FK → `MetodesPagament.id` | Sí | |
| `concepte` | TEXT | Sí | |
| `observacions` | TEXT | No | |

**Per què `base_cents` explícit i no derivat (`import − iva`):** igual que a `Vendes`, es guarda com a valor fix. Si es derivés sobre la marxa i més tard es canviés la configuració d'IVA, els moviments antics canviarien de valor retroactivament, trencant comptabilitat ja tancada.

**Índex:** `data`.

---

### 2.12. `HorariBarberia`

Una fila per franja d'obertura (permet horari partit).

| Camp | Tipus | Obligatori | Notes |
|---|---|---|---|
| `id` | INTEGER PK | Sí | |
| `dia_setmana` | TEXT | Sí | `Dl`…`Dg` |
| `hora_obertura` | TEXT | Sí | |
| `hora_tancament` | TEXT | Sí | |

---

### 2.13. `DiesTancats`

| Camp | Tipus | Obligatori | Notes |
|---|---|---|---|
| `id` | INTEGER PK | Sí | |
| `data` | TEXT (data) | Sí | **Índex únic** |
| `motiu` | TEXT | No | p. ex. "Nadal" |

---

### 2.14. `Configuracio`

Parella clau-valor per a totes les preferències editables (RF-23).

| Camp | Tipus | Obligatori |
|---|---|---|
| `clau` | TEXT PK | Sí |
| `valor` | TEXT | Sí |

**Claus previstes i valors per defecte (seed inicial):**

| Clau | Valor per defecte | Unitat |
|---|---|---|
| `barberia_nom` | *(buit)* | text |
| `barberia_adreca` | *(buit)* | text |
| `barberia_telefon` | *(buit)* | text |
| `iva_bp_defecte` | `2100` | punts base |
| `iva_mode` | `Inclos` | `Inclos` / `NoInclos` |
| `aplicar_iva_caixa` | `0` | booleà |
| `durada_defecte_cita_min` | `30` | minuts |
| `hora_backup` | `20:00` | `HH:MM` |
| `backups_a_conservar` | `15` | nombre |
| `ultima_copia_automatica` | *(buit)* | data ISO. Buit fins a la primera còpia |
| `mostrar_avis_convidat` | `1` | booleà |
| `so_confirmacio` | `1` | booleà |

---

## 3. Resum d'índexs

| Taula | Índexs |
|---|---|
| `Clients` | `client_key` (únic), `nom`, `mobil` |
| `HorarisTreballadora` | `treballadora_id` |
| `Cites` | `(data, hora)`, `client_id`, `treballadora_id`, `estat` |
| `Vendes` | `data`, `client_id`, `treballadora_id`, `estat`, `cita_id` (únic) |
| `VendaLinies` | `venda_id`, `servei_id`, `producte_id` |
| `VendaDesglossaments` | `venda_id`, `iva_bp` |
| `MovimentsCaixa` | `data` |
| `MetodesPagament` | `nom` (únic) |
| `DiesTancats` | `data` (únic) |

---

## 4. Comportament `ON DELETE` — resum

| Relació | Comportament | Motiu |
|---|---|---|
| `Clients` → `Cites`/`Vendes` | `CASCADE` | Eliminar un client esborra el seu historial (RF-04) |
| `Treballadores` → `Cites`/`Vendes` | `SET NULL` | Les treballadores no s'eliminen des de l'app (només s'inactiven), però es protegeix per si de cas |
| `Treballadores` → `HorarisTreballadora` | `CASCADE` | L'horari no té sentit sense la treballadora |
| `Serveis`/`Productes` → `Cites`/`VendaLinies` | `SET NULL` | El catàleg tampoc s'elimina (només s'inactiva); les línies ja guarden còpia pròpia de preu i IVA |
| `Cites` → `Vendes` | `SET NULL` | Si la cita desaparegués, la venda no s'ha de perdre |
| `Vendes` → `VendaLinies` / `VendaDesglossaments` | `CASCADE` | No existeixen sense la venda |

---

## 5. Validacions a nivell de base de dades (CHECK)

```sql
-- Cites i Vendes: client registrat XOR client convidat
CHECK (
  (client_id IS NOT NULL AND nom_convidat IS NULL) OR
  (client_id IS NULL AND nom_convidat IS NOT NULL)
)

-- VendaLinies: una linia no pot ser servei i producte alhora
CHECK (NOT (servei_id IS NOT NULL AND producte_id IS NOT NULL))

-- Imports i percentatges mai negatius
CHECK (import_cents >= 0)
CHECK (iva_bp >= 0)
CHECK (quantitat > 0)

-- Estats i modes tancats.
-- IMPORTANT: aquests valors han de coincidir EXACTAMENT amb els noms dels membres
-- dels enums de C#, perquè EF Core els guarda amb HasConversion<string>().
-- Per això son sense accents ni espais: 'Cancellada', no 'Cancel·lada'.
-- Les etiquetes amb accent que veu l'usuaria les posa la capa d'interficie.
CHECK (estat IN ('Pendent', 'Realitzada', 'Cancellada', 'NoAssistida'))  -- Cites
CHECK (estat IN ('Activa', 'Anullada'))                                  -- Vendes
CHECK (iva_mode IN ('Inclos', 'NoInclos'))                               -- Vendes
CHECK (tipus IN ('Entrada', 'Sortida'))                                  -- MovimentsCaixa
```

---

## 6. Regles que NO queden garantides per la base de dades

Regles de negoci que cal validar a la capa de servei (C#), no a SQLite:

- **Invariant `base_cents + iva_cents = total_cents`**: es garanteix per construcció de l'algoritme (6.4), però SQLite no la imposa. Val la pena una prova unitària que la verifiqui.
- **Avís de solapament d'agenda** (6.7): càlcul dinàmic sobre l'horari de treballadores actives; no es pot expressar amb un `CHECK`.
- **Només cites `Realitzada` poden tenir venda associada** (RF-02): SQLite no admet `CHECK` amb subconsultes a altres taules de forma fiable. Es valida a l'aplicació.
- **Recàlcul de `client_key`** si es corregeix el nom (6.1): camp derivat sense trigger, cal fer-ho al servei. El telèfon ja no hi intervé.

---

## 7. Notes tècniques

### 7.1. Per què cèntims i no `decimal`

El proveïdor EF Core per SQLite mapeja `decimal` a `TEXT`. Això no només és menys eficient, sinó que fa que els `ORDER BY` i les comparacions (`>`, `<`) es resolguin **al client**, fora de SQLite. Amb `INTEGER` tot passa dins del motor i amb precisió exacta.

### 7.2. Formatació per a la interfície i l'exportació

Els cèntims només es converteixen a euros en el moment de mostrar-los o exportar-los:

```csharp
// Cents are the storage unit; euros exist only for display
decimal euros = cents / 100m;
string text = euros.ToString("N2", new CultureInfo("ca-ES"));  // "15,00"
```

Per a l'exportació CSV (6.8) s'usa separador `;` i decimals amb coma, per compatibilitat amb Excel en configuració espanyola.

---

## 8. Implementació

Els passos per materialitzar aquest esquema (migració `InitialCreate`, seed de `Configuracio`, restriccions `CHECK`) estan a `setup-projecte.md`, seccions 9 i 10.

El diagrama entitat-relació és al fitxer `diagrama-er.mermaid`.
