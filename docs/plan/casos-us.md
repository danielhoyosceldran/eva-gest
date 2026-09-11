# Casos d'ús

Fluxos clau de l'aplicació. Serveixen per validar que la lògica és completa abans d'escriure codi, i com a referència en implementar cada servei.

---

## 1. Visió general d'actors i casos

Hi ha un únic actor humà (l'usuària), més dos processos automàtics del sistema.

```mermaid
graph LR
    U([Usuària])
    S([Sistema])

    U --> CU01[Crear cita]
    U --> CU02[Marcar estat de cita]
    U --> CU03[Registrar venda]
    U --> CU04[Editar o anul·lar venda]
    U --> CU05[Gestionar clients]
    U --> CU06[Registrar moviment de caixa]
    U --> CU07[Consultar informes]
    U --> CU08[Exportar per assessoria]
    U --> CU09[Còpia manual i restauració]
    U --> CU10[Configurar l'aplicació]

    S --> CU11[Còpia automàtica diària]
    S --> CU12[Migració d'esquema a l'inici]
```

---

## 2. CU-01 · Crear una cita

El punt delicat és que **cap avís bloqueja**: sempre es pot guardar. L'aplicació informa, no impedeix.

```mermaid
flowchart TD
    A[Prem + Nova cita] --> B[Omple client, data, hora]
    B --> C{Client registrat?}
    C -->|Sí| D[Selecciona de la llista]
    C -->|No| E[Escriu nom lliure + telèfon opcional]
    E --> F{Avís de convidat<br/>activat?}
    F -->|Sí| G[Mostra avís amb<br/>botó Registrar-lo ara]
    F -->|No| H
    G --> H
    D --> H[Tria servei i treballadora<br/>opcionals]

    H --> I[Calcula durada:<br/>del servei o per defecte]
    I --> J{Comprova<br/>disponibilitat}
    J -->|Sense capacitat| K[Avís de solapament en línia]
    J -->|Correcte| L
    K --> L{Dins d'horari<br/>i dia obert?}
    L -->|No| M[Avís fora d'horari<br/>o dia tancat]
    L -->|Sí| N
    M --> N[Prem Guardar]
    N --> O{Validació:<br/>client, data,<br/>hora, durada}
    O -->|Falta alguna cosa| P[Missatge concret<br/>del que falta]
    P --> B
    O -->|Correcte| Q[Guarda amb estat Pendent]
    Q --> R[Tanca diàleg<br/>i refresca agenda]
```

---

## 3. CU-01b · Càlcul del solapament

Detall de la comprovació de disponibilitat (decisió 6.7). És el càlcul menys evident de tota l'aplicació.

```mermaid
flowchart TD
    A[Nova cita: data, hora,<br/>durada, treballadora?] --> B{Té treballadora<br/>assignada?}

    B -->|Sí| C[Compta cites d'AQUELLA treballadora<br/>que es solapen en el temps]
    C --> D{Alguna?}
    D -->|Sí| E[AVÍS de solapament]
    D -->|No| F[Sense avís]

    B -->|No| G[Compta treballadores ACTIVES<br/>amb la franja dins del seu horari]
    G --> H[Compta cites existents<br/>que es solapen<br/>estats Pendent o Realitzada]
    H --> I{cites >= treballadores<br/>disponibles?}
    I -->|Sí| E
    I -->|No| F

    E --> J[Es pot guardar igualment]
    F --> J
```

**Casos límit verificats:**

| Situació | Treballadores disponibles | Cites existents | Resultat |
|---|---|---|---|
| Una treballadora, cap cita a les 10:00 | 1 | 0 | Sense avís |
| Una treballadora, ja hi ha cita a les 10:00 | 1 | 1 | Avís |
| Dues treballadores, una cita a les 10:00 | 2 | 1 | Sense avís |
| Dues treballadores, dues cites a les 10:00 | 2 | 2 | Avís |
| Una de vacances (inactiva), una cita | 1 | 1 | Avís |
| Cita cancel·lada a la mateixa hora | 1 | 0 | Sense avís (no compta) |

---

## 4. CU-02 · Marcar l'estat d'una cita

Aquí es connecten agenda i vendes: només *Realitzada* obre el cobrament.

```mermaid
flowchart TD
    A[Cita en estat Pendent] --> B{Quina acció?}

    B -->|Realitzada| C[Canvia estat a Realitzada]
    C --> D[Obre diàleg de venda<br/>amb dades precarregades]
    D --> E{L'usuària cobra?}
    E -->|Sí| F[Es crea la venda<br/>lligada a la cita]
    E -->|Cancel·la| G[La cita queda Realitzada<br/>sense venda associada]

    B -->|Cancel·lada| H[Canvia estat a Cancel·lada]
    B -->|No assistida| I[Canvia estat a No assistida]

    H --> J[Cap venda possible.<br/>Compta al comptador<br/>de cancel·lades]
    I --> K[Cap venda possible.<br/>Compta al comptador<br/>de no assistides]

    F --> L[Refresca agenda,<br/>caixa i indicadors]
    G --> L
    J --> L
    K --> L
```

**Nota sobre el camí `G`:** si l'usuària tanca el diàleg de venda sense cobrar, la cita es queda com a *Realitzada* però sense venda. És un estat vàlid (el client va venir però encara no s'ha cobrat) i es podrà cobrar més tard associant la cita a una venda nova.

---

## 5. CU-03 · Registrar una venda

```mermaid
flowchart TD
    A{Des d'on s'obre?}
    A -->|Botó + Nova venda| B[Formulari buit]
    A -->|Cita marcada Realitzada| C[Precarrega client,<br/>servei i treballadora]

    B --> D[Selecciona client<br/>registrat o convidat]
    C --> E[Afegeix o modifica línies]
    D --> E

    E --> F{Tipus de línia}
    F -->|Servei del catàleg| G[Copia nom, preu i IVA]
    F -->|Producte del catàleg| G
    F -->|Concepte lliure| H[Descripció i preu a mà]

    G --> I[Línia editable:<br/>concepte, quantitat,<br/>preu i IVA]
    H --> I

    I --> J[Recalcula en viu:<br/>agrupa per tipus d'IVA]
    J --> K[Mostra base, IVA i total]
    K --> L{Més línies?}
    L -->|Sí| E
    L -->|No| M[Tria mètode de pagament]

    M --> N[Prem Cobrar]
    N --> O{Validació:<br/>almenys una línia,<br/>mètode, imports >= 0}
    O -->|Error| P[Missatge concret]
    P --> E
    O -->|Correcte| Q[Congela a cada línia:<br/>preu i IVA aplicats]
    Q --> R[Guarda venda amb<br/>iva_mode vigent i estat Activa]
    R --> S[So de confirmació<br/>si està activat]
    S --> T[Refresca inici, caixa<br/>i informes]
```

---

## 6. CU-03b · Desglossament d'IVA

El càlcul que garanteix que l'IVA trimestral quadri amb la suma de tiquets.

```mermaid
flowchart TD
    A[Línies de la venda] --> B[Calcula import de cada línia:<br/>preu_unitari × quantitat<br/>valor exacte, sense arrodonir]
    B --> C[Agrupa les línies<br/>pel seu percentatge d'IVA]

    C --> D{Per cada grup}
    D --> E{Mode d'IVA?}

    E -->|Inclòs| F["base = arrodonir(total × 10000 / (10000 + iva_bp))<br/>quota = total − base"]
    E -->|No inclòs| G["quota = arrodonir(base × iva_bp / 10000)<br/>total = base + quota"]

    F --> H[Arrodoniment sempre<br/>AwayFromZero, mai bancari]
    G --> H

    H --> I[Suma bases, quotes<br/>i totals de tots els grups]
    I --> J[Invariant garantida:<br/>base + IVA = total]
```

**Per què agrupar i no calcular línia a línia:** amb 3 línies de 10,00 € al 21% inclòs, calcular per línia i sumar dona base 2478, mentre que calcular-ho sobre el total dona 2479. Un cèntim de descuadre per venda faria que la declaració trimestral no quadrés.

---

## 7. CU-04 · Editar o anul·lar una venda

```mermaid
flowchart TD
    A[Venda a l'historial] --> B{Quina acció?}

    B -->|Editar| C[Obre diàleg amb<br/>les dades actuals]
    C --> D[Modifica línies, client,<br/>mètode o treballadora]
    D --> E[Recalcula base, IVA i total]
    E --> F[Guarda canvis]
    F --> G[Registra l'edició al log]

    B -->|Anul·lar| H[Confirmació explícita]
    H --> I{Confirma?}
    I -->|No| J[Res no canvia]
    I -->|Sí| K[Estat passa a Anul·lada]
    K --> L[Registra l'anul·lació al log]

    L --> M[Queda visible a l'historial<br/>però exclosa de:<br/>balanç, IVA, rànquings<br/>i indicadors]
    G --> N[Refresca caixa i informes]
    M --> N
```

**Per què no s'esborra:** una venda esborrada desapareixeria sense deixar rastre i faria impossible auditar per què un període no quadra. Anul·lant-la, es veu què va passar.

---

## 8. CU-05 · Eliminar o adormir un client

Els dos camins són molt diferents i cal que l'usuària entengui la diferència abans de decidir.

```mermaid
flowchart TD
    A[Fitxa del client] --> B{Acció}

    B -->|Adormir| C[Marca adormit = 1]
    C --> D[Desapareix de cerques<br/>i selectors]
    D --> E[Conserva historial<br/>i segueix comptant<br/>a estadístiques]
    E --> F[Reversible: Despertar]

    B -->|Eliminar| G[Confirmació amb el recompte real:<br/>24 cites i 18 vendes]
    G --> H{Què tria?}
    H -->|Cancel·lar| I[Res no canvia]
    H -->|Adormir en lloc d'això| C
    H -->|Eliminar definitivament| J[Esborra el client]
    J --> K[Cascada: esborra<br/>cites i vendes]
    K --> L[Irreversible.<br/>Els totals històrics<br/>del període canvien]
```

**Decisió de disseny:** la confirmació d'eliminació ofereix *Adormir* com a tercera opció. Qui vol "treure'l de la llista" gairebé sempre vol adormir-lo, no destruir la comptabilitat.

---

## 9. CU-06 · Registrar un moviment de caixa

```mermaid
flowchart TD
    A[Prem + Entrada o + Sortida] --> B[Omple import, mètode<br/>de pagament i concepte]
    B --> C{aplicar_iva_caixa<br/>activat?}
    C -->|No| D[Guarda només l'import final]
    C -->|Sí| E{Vol desglossar<br/>aquest moviment?}
    E -->|No| D
    E -->|Sí| F[Indica percentatge d'IVA]
    F --> G[Calcula base i quota<br/>i les guarda com a<br/>valors fixos]
    D --> H[Guarda el moviment]
    G --> H
    H --> I[Refresca balanç de caixa]
```

**Per què es guarden base i quota, i no es deriven:** si es calculessin al vol i més tard es canviés la configuració d'IVA, els moviments antics canviarien de valor retroactivament.

---

## 10. CU-07 · Consultar informes per treballadora

```mermaid
flowchart TD
    A[Obre Informes] --> B[Tria període]
    B --> C[Tria treballadora]
    C --> D[Consulta vendes ACTIVES<br/>del període]

    D --> E[Agrupa VendaLinies<br/>per classificació]
    E --> F[Servei: té servei_id]
    E --> G[Producte: té producte_id]
    E --> H[Altres: cap dels dos]

    D --> I[Agrupa vendes<br/>per dia de la setmana]

    F --> J[Calcula percentatges]
    G --> J
    H --> J
    I --> K[Gràfica d'activitat<br/>per dia]

    J --> L{Denominador = 0?}
    L -->|Sí| M[Mostra un guionet<br/>mai 0 %]
    L -->|No| N[Mostra el percentatge]

    M --> O[Presenta el detall]
    N --> O
    K --> O
```

**Els quatre indicadors que poden no tenir valor:**

| Indicador | Sense valor quan |
|---|---|
| % de treball realitzat | El període no té cap venda |
| % de venda de productes | La treballadora no té vendes al període |
| Mitjana per visita | El client no té cap visita realitzada |
| Freqüència de visita | El client té menys de 2 visites realitzades |

---

## 11. CU-08 · Exportar per a l'assessoria

```mermaid
flowchart TD
    A[Prem Exportar període] --> B[Indica dates<br/>des de / fins a]
    B --> C[Consulta vendes ACTIVES<br/>del període]
    C --> D[Fitxer 1: una fila per VENDA<br/>amb base, IVA i total guardats]
    C --> E[Fitxer 2: agrupa VendaDesglossaments<br/>per tipus impositiu]
    D --> F[Converteix cèntims a euros<br/>amb coma decimal]
    E --> F
    F --> G[Genera els CSV<br/>UTF-8, separador punt i coma]
    G --> H[Diàleg de triar carpeta]
    H --> I[Confirmació amb la ruta<br/>i botó Obrir carpeta]
```

**`vendes_<periode>.csv`:** data · hora · client · treballadora · conceptes · mètode de pagament · base · IVA · total

**`iva_<periode>.csv`:** tipus d'IVA · base · quota · total

**Per què no una fila per línia:** la base només existeix per venda i per tipus impositiu. Repartir-la entre les línies obliga a arrodonir i les parts no sumen el total, així que el fitxer no quadraria amb el que mostra l'aplicació.

---

## 12. CU-09 i CU-11 · Còpies de seguretat

```mermaid
flowchart TD
    subgraph auto[CU-11 · Automàtica]
        A1[Arriba l'hora configurada] --> A2{L'app està oberta?}
        A2 -->|No| A3[Es farà en el<br/>següent arrencada]
        A2 -->|Sí| A4[Copia el fitxer .db]
        A3 --> A4
        A4 --> A5[Compta còpies existents]
        A5 --> A6{Supera el màxim<br/>configurat?}
        A6 -->|Sí| A7[Esborra les més antigues]
        A6 -->|No| A8[Fet]
        A7 --> A8
    end

    subgraph manual[CU-09 · Manual]
        B1[Prem Fer còpia ara] --> B2[Copia el fitxer .db]
        B2 --> B3[Confirmació visual<br/>amb data i hora]
    end

    subgraph rest[CU-09b · Restauració]
        C1[Prem Restaurar còpia] --> C2[Llista còpies<br/>disponibles]
        C2 --> C3[Tria una]
        C3 --> C4[Confirmació amb avís<br/>de pèrdua de dades]
        C4 --> C5{Confirma?}
        C5 -->|No| C6[Res no canvia]
        C5 -->|Sí| C7[Còpia de seguretat<br/>de l'estat ACTUAL]
        C7 --> C8[Substitueix la base de dades]
        C8 --> C9[Reinicia l'aplicació]
    end
```

**Detall important del pas `C7`:** abans de restaurar es fa una còpia de l'estat actual. Si algú restaura per error una còpia antiga, encara pot tornar enrere.

---

## 13. CU-10 · Canviar el mode d'IVA

Cas d'ús petit però amb conseqüències grans, per això es documenta a part.

```mermaid
flowchart TD
    A[Configuració → IVA] --> B[Canvia el mode<br/>inclòs / no inclòs]
    B --> C[Avís destacat:<br/>tots els preus del catàleg<br/>canvien de significat]
    C --> D{Confirma?}
    D -->|No| E[Res no canvia]
    D -->|Sí| F[Guarda el nou mode]
    F --> G[Les vendes ja registrades<br/>NO es veuen afectades:<br/>cada una guarda el seu<br/>iva_mode i els seus totals]
    G --> H[Les vendes futures<br/>usaran el mode nou]
```

---

## 14. CU-12 · Arrencada de l'aplicació

```mermaid
flowchart TD
    A[Obre l'aplicació] --> Z{Ja hi ha una<br/>instància oberta?}
    Z -->|Sí| Z1[Porta la finestra<br/>existent al davant]
    Z1 --> Z2[Tanca aquesta instància]
    Z -->|No| B[Llegeix ruta de la BD<br/>d'appsettings.json]

    B --> C{Es pot obrir la<br/>base de dades?}
    C -->|Error| X[Diàleg entenedor:<br/>què ha passat i<br/>oferir restaurar una còpia]
    X --> X1{Vol restaurar?}
    X1 -->|Sí| X2[Obre el diàleg<br/>de restauració]
    X1 -->|No| X3[Tanca l'aplicació]

    C -->|Correcte| C2{Existeix la<br/>base de dades?}
    C2 -->|No| D[Crea BD i aplica<br/>totes les migracions]
    D --> E[Seed: configuració<br/>per defecte i<br/>mètodes de pagament]
    C2 -->|Sí| F{Hi ha migracions<br/>pendents?}
    F -->|Sí| G[Còpia de seguretat<br/>preventiva]
    G --> H[Aplica migracions]
    F -->|No| I
    H --> I{Toca còpia automàtica?<br/>compara amb<br/>ultima_copia_automatica}
    E --> I
    I -->|Sí| J[Fa la còpia,<br/>neteja les antigues i<br/>desa la data d'avui]
    I -->|No| K
    J --> K[Carrega la pantalla d'Inici]
    K --> L[Comprova aniversaris d'avui]
    L --> M[Registra l'arrencada al log]
```

### Una sola instància alhora

Dues instàncies sobre el mateix fitxer SQLite poden xocar en escriure, i les dues intentarien fer la còpia automàtica. S'evita amb un `Mutex` amb nom:

```csharp
// A named mutex keeps a second instance from touching the same SQLite file.
// If one is already running, bring its window forward instead of opening another.
private static Mutex? _instancia;

private static bool EsPrimeraInstancia()
{
    _instancia = new Mutex(initiallyOwned: true, "BarberiaApp.UnicaInstancia", out bool nova);
    return nova;
}
```

### Base de dades inaccessible

Si el fitxer està corrupte, bloquejat per un altre procés o en una ruta que ja no existeix, l'aplicació **no ha de mostrar l'excepció**. Mostra un missatge en llenguatge planer i ofereix la sortida:

> **No s'han pogut obrir les dades.**
>
> El fitxer de dades no es pot llegir. Pot ser que estigui malmès o que s'hagi mogut de lloc.
>
> Tens còpies de seguretat disponibles i pots recuperar-ne una.
>
> `Tancar` | `Restaurar una còpia`

L'error tècnic complet es registra al log, no a la pantalla (RF-18).

### Tancar amb feina a mitges

Tots els formularis són **diàlegs modals**, així que mentre n'hi hagi un obert la finestra principal no es pot tancar. No cal cap comprovació addicional de dades sense desar: o bé s'ha premut `Guardar`, o bé no hi ha res guardat.

---

## 15. Documentació completa

Tots els documents de disseny estan fets. Vegeu `capa-mvvm.md`, `disseny-ui.md` i `setup-projecte.md`.
