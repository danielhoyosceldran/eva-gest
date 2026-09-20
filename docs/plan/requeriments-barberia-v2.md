# Requeriments — App gestió barberia (versió tancada)

## 1. Objectiu

Eina d'escriptori per a Windows per gestionar el dia a dia d'una barberia (agenda, clients, vendes, caixa). Ús **intern**, per a una o diverses treballadores, sense reserves per Internet.

---

## 2. Mòduls

1. Pantalla principal
2. Agenda
3. Clients
4. Treballadores
5. Serveis i productes
6. Vendes
7. Caixa (entrades/sortides)
8. Informes i anàlisi
9. Configuració
10. Ajuda amb FAQ
11. Còpies de seguretat
12. Exportació
13. Logging bàsic

---

## 3. Requisits funcionals

### RF-01. Pantalla principal
Mostra: cites del dia, vendes del dia, diners cobrats, entrades/sortides, balanç del dia.

Botons ràpids: `+ Nova cita`, `+ Nova venda`, `+ Nou client`, `+ Entrada`, `+ Sortida`.

---

### RF-02. Agenda

Crear, modificar i consultar cites (del dia o de qualsevol altre dia).

**Vista setmanal.** La pantalla mostra tota la setmana d'un cop, amb una columna per dia i les cites d'aquell dia dins. Es pot canviar de setmana (anterior/següent) i tornar directament a la setmana actual. Seleccionar un dia en mostra el detall (mateixa informació que abans, en format de llista).

**Estats d'una cita:**

| Estat | Significat | Genera venda? |
|---|---|---|
| Pendent | Cita programada | — |
| Realitzada | El client ha vingut i s'ha fet el servei | Sí |
| Cancel·lada | S'ha cancel·lat amb antelació | No |
| No assistida | El client no s'ha presentat sense avisar | No |

Les cites cancel·lades i no assistides no generen cap moviment econòmic; es comptabilitzen per separat.

**Dades d'una cita:** data, hora, durada, client, servei (opcional), treballadora assignada (opcional), observacions, estat.

**Durada:** ve del servei seleccionat. Si no hi ha servei o el servei no té durada definida, s'usa una durada per defecte configurable (p. ex. 30 min). Sempre es pot ajustar manualment.

**Avís de solapament:** en crear o modificar una cita, l'aplicació comprova la disponibilitat (vegeu RF-06, Treballadores) i avisa si no hi ha capacitat, sense bloquejar la creació.

**Horari de la barberia:** l'agenda només ofereix per defecte les hores dins de l'horari configurat (RF-23). Si es crea una cita fora d'horari, s'avisa però es permet.

**Servei de la cita:** és opcional. Si s'indica, es precarrega al formulari de venda quan la cita es marca com a realitzada (i sempre es pot modificar). Si no s'indica, s'introdueix directament al moment de registrar la venda.

**Client de la cita:** pot ser un client registrat, o bé un client convidat indicant només **nom** (i telèfon opcional). Vegeu RF-05bis.

**Vincle amb el client:** quan la cita té un client registrat, queda lligada a la seva fitxa. Des de la fitxa del client es veuen totes les seves cites passades i el seu estat; des de la cita s'accedeix directament a la fitxa del client.

**Accions directes des de la llista de cites del dia:**

| Acció | Resultat |
|---|---|
| Realitzada | Obre el formulari de venda amb client, servei i treballadora precarregats (els que la cita tingués). La cita passa a *Realitzada* quan es guarda la venda; si es cancel·la el cobrament, es queda *Pendent* |
| Cancel·lada | Marca la cita com a cancel·lada. No permet venda associada |
| No assistida | Marca la cita com a no assistida. No permet venda associada |

**Sense** reserves per Internet, portal públic ni app mòbil per a clients.

---

### RF-03. Clients

Crear, modificar, consultar i cercar clients. Cerca per nom o telèfon.

**Dades del client:** nom i mòbil (**obligatoris**); correu, observacions i data de naixement (**opcionals**). Totes les dades es podran ampliar i modificar més endavant.

**Nom únic:** dos clients no poden compartir nom (decisió 6.1). En crear o modificar un client amb un nom que ja existeix, l'aplicació ho refusa i diu amb quin client xoca, perquè la usuària hi pugui afegir el cognom.

**Aniversari:** si s'indica la data de naixement, l'aplicació mostra un avís discret a la pantalla principal el dia de l'aniversari d'un client.

### RF-04. Eliminar i adormir clients

Dues opcions diferenciades:

| Acció | Efecte | Reversible |
|---|---|---|
| Adormir | El client no apareix a les cerques ni com a opció en cites/vendes. Es manté l'historial i continua comptant a les estadístiques | Sí, es pot despertar |
| Eliminar | S'esborra la fitxa **i tot el seu historial** (cites i vendes vinculades) | No |

Totes dues accions requereixen confirmació explícita. L'eliminació ha d'advertir clarament que es perdrà l'historial.

### RF-05. Fitxa del client

Resum visual:

- Última visita
- Total de visites realitzades
- Cites cancel·lades
- Cites no assistides
- Total gastat
- Mitjana per visita
- Freqüència aproximada
- Aniversari (si s'ha indicat)
- Historial complet (cites amb estat, vendes, productes)

### RF-05bis. Clients convidats

Una cita o una venda es pot registrar amb un **client convidat**: només nom, i telèfon opcional. No es crea cap fitxa.

**Implicacions d'un client convidat:**

- Compta per a les **estadístiques globals** (total de vendes, balanç, IVA, ingressos del període)
- **No** genera historial individual, ni freqüència, ni mitjana per visita, ni apareix als rànquings

**Avís:** en registrar una cita o venda amb un client convidat, l'aplicació mostra un avís amigable oferint registrar-lo com a client. L'usuària pot registrar-lo al moment o continuar sense fer-ho. Aquest avís es pot desactivar des de la configuració.

**Regla important:** si un convidat es registra posteriorment com a client, l'historial anterior **no** es recupera.

---

### RF-06. Treballadores

Gestió de les persones que treballen a la barberia (pot ser-ne només una).

**Dades:** nom, horari laboral setmanal (dies i franges horàries), estat actiu/inactiu.

**Actiu/inactiu:** una treballadora inactiva (p. ex. de vacances) no compta com a disponible als càlculs de solapament de l'agenda, encara que el seu horari setmanal segueixi definit. Es reactiva amb un clic quan torna.

**Assignació:** cites i vendes poden assignar-se a una treballadora, però **no és obligatori**.

**Comprovació de solapament (RF-02):** quan es crea o modifica una cita:

- Si la cita **té treballadora assignada**, es comprova el solapament només contra les altres cites d'aquella treballadora.
- Si la cita **no té treballadora assignada**, es compta quantes treballadores actives tenen aquella franja dins del seu horari laboral. Si el nombre de cites ja existents en aquella franja iguala o supera el nombre de treballadores disponibles, es mostra un avís de possible solapament (sense bloquejar).

En un negoci d'una sola treballadora, això equival simplement a avisar quan ja hi ha una cita a la mateixa hora.

---

### RF-07. Serveis

Tractaments que es fan a la barberia (tall, barba, etc.). Nom, preu, **IVA propi**, durada (opcional), actiu/inactiu. S'ofereixen com a opció en crear una cita o una venda.

### RF-08. Productes

Articles físics que es venen (xampú, cera, etc.). Nom, preu, categoria, **IVA propi**, actiu/inactiu. Només s'ofereixen en crear una venda.

Els serveis/productes inactius no apareixen com a opció.

**IVA per element:** cada servei i cada producte té el seu propi percentatge d'IVA, que pot ser diferent del valor per defecte. La majoria compartiran el mateix tipus, però la possibilitat de definir-lo individualment és obligatòria (vegeu 6.4).

---

### RF-09. Vendes — dos fluxos

**1. Venda independent:** des del botó `+ Nova venda`, sense cap cita associada.

**2. Venda des d'una cita:** en marcar una cita com a "Realitzada", s'obre el mateix formulari amb client, servei i treballadora precarregats (els que la cita tingués). Tots els camps precarregats són modificables. Guardar la venda és el que passa la cita a *Realitzada*; cancel·lar el formulari no canvia res.

També es pot adjuntar manualment una cita existent (en estat Realitzada) a una venda creada de forma independent.

El formulari és el mateix component en tots dos casos; només canvien les dades per defecte segons d'on s'obre.

**Dades d'una venda:**

- Data i hora
- Client: registrat o convidat (nom + telèfon opcional)
- Treballadora (opcional)
- Servei/s i/o producte/s del catàleg
- Línies personalitzades (descripció lliure + preu a mà)
- Quantitat, preu unitari i IVA de cada línia
- Base imposable, quota d'IVA i total
- Mètode de pagament
- Cita associada (si escau)
- Estat (Activa / Anul·lada)
- Observacions

**Personalització sempre disponible:** qualsevol línia d'una venda és sempre modificable (descripció, preu, quantitat i IVA), encara que provingui del catàleg o d'una cita.

### RF-10. Edició i anul·lació de vendes

**Editar:** una venda ja registrada es pot editar en qualsevol moment (client, línies, mètode de pagament, treballadora, cita associada). L'edició queda registrada al log.

**Anul·lar:** una venda no es pot eliminar directament. S'anul·la, passant a l'estat "Anul·lada": deixa de comptar al balanç i a l'IVA del període, però continua visible a l'historial per mantenir la traçabilitat.

### RF-11. Venda ràpida
Client → serveis/productes/línia personalitzada → total → mètode de pagament → confirmar. Mínim de passos.

### RF-12. Mètodes de pagament
Llista **configurable** per l'usuària. No hi ha cap opció genèrica tipus "Altres": cada mètode que es faci servir s'ha de definir a la configuració (efectiu, targeta, Bizum, transferència...).

La moneda és sempre **euros** i no és configurable.

---

### RF-13. Entrades i sortides de caixa

Moviments que no són vendes. Dades: data, tipus (entrada/sortida), import, mètode de pagament, concepte, observacions, IVA (opcional).

**Mètode de pagament:** mateixa llista configurable que a les vendes (RF-12). Permet diferenciar, per exemple, una entrada en efectiu d'una entrada per targeta o transferència.

**Tractament de l'IVA en moviments:** configurable globalment.

- Desactivat (per defecte): l'import és el valor final, sense desglossament
- Activat: cada moviment pot indicar un percentatge d'IVA i es calcula base + quota, igual que a les vendes

### RF-14. Balanç de caixa
`Vendes + Entrades − Sortides = Balanç`. Consulta per avui, ahir, setmana, mes, mes anterior o període personalitzat.

Es mostra també el desglossament d'IVA del període: base imposable, quota d'IVA i total. Les vendes anul·lades s'exclouen d'aquest càlcul.

### RF-15. Historial de vendes
Filtrable per data, client, producte, servei, mètode de pagament, treballadora, estat (activa/anul·lada).

---

### RF-16. Informes i anàlisi

Pantalla amb tres blocs:

**A) Indicadors per client** (només clients registrats):

- Nombre de visites realitzades
- Cites cancel·lades i no assistides (comptadors separats)
- Import total gastat
- Mitjana per visita
- Primera i última visita
- Freqüència aproximada de visita

**B) Rànquings:**

- Clients amb més visites, més despesa, major mitjana, més temps sense venir
- Treballadores: vendes ateses i ingressos generats en el període (si n'hi ha més d'una)

**C) Visió general:**

- Gràfica d'evolució de vendes al llarg dels mesos
- **Client del mes**: el client amb més visites o més despesa del mes en curs, destacat automàticament

**D) Detall per treballadora:**

- Serveis realitzats: nombre de vegades que ha fet cada servei del catàleg, en el període
- Productes venuts: unitats venudes de cada producte, en el període
- Altres conceptes: import de les línies personalitzades (favors, preus especials), que no són ni servei ni producte del catàleg
- Activitat per dia de la setmana: vendes i ingressos agrupats per dia (Dl a Dv), per detectar patrons d'activitat desigual entre dies (p. ex. una treballadora amb molta menys feina els dilluns)
- **% de treball realitzat**: pes de la facturació d'aquesta treballadora sobre la facturació total del període
- **% de venda de productes**: pes dels productes sobre la facturació total d'aquesta treballadora

Aquests dos percentatges substitueixen el recompte manual diari: es calculen automàticament, sense que l'usuària hagi de fer cap operació.

Aquest detall és per treballadora individual, seleccionable des de la pantalla d'Informes; el rànquing del bloc B en dona la comparativa global.

### RF-17. Càlcul d'indicadors — regles

Només compten les visites **realitzades**. Les cites cancel·lades i no assistides s'exclouen de tots els càlculs econòmics i de freqüència, i es comptabilitzen per separat.

| Indicador | Fórmula |
|---|---|
| Visites | Nombre de cites en estat *Realitzada* |
| Total gastat | Suma dels totals de les vendes actives del client |
| Mitjana per visita | Total gastat ÷ nombre de visites realitzades |
| Freqüència aproximada | Mitjana de dies entre visites realitzades consecutives |
| Cites cancel·lades | Nombre de cites en estat *Cancel·lada* |
| Cites no assistides | Nombre de cites en estat *No assistida* |
| % de treball realitzat (treballadora) | Facturació de la treballadora ÷ Facturació total del període × 100 |
| % de venda de productes (treballadora) | Import en productes de la treballadora ÷ Facturació total d'aquella treballadora × 100 |

**Classificació de les línies de venda** (per als percentatges anteriors):

| Tipus | Condició | Compta com a |
|---|---|---|
| Servei | `servei_id` informat | Serveis |
| Producte | `producte_id` informat | Productes |
| Personalitzada | cap dels dos informat | **Altres** |

Les línies personalitzades no s'agrupen amb els serveis: fer-ho distorsionaria el % de venda de productes, que és precisament l'indicador que es vol vigilar. Per tant `% serveis + % productes + % altres = 100%`.

**Indicadors sense valor definit.** Quatre indicadors tenen el denominador a zero en casos reals. En tots aquests casos la interfície mostra un guionet (`—`), mai `0%`, per no confondre "sense dades" amb "zero real":

| Indicador | Quan no té valor |
|---|---|
| % de treball realitzat | El període no té cap venda |
| % de venda de productes | La treballadora no té cap venda al període |
| Mitjana per visita | El client no té cap visita realitzada |
| Freqüència aproximada | El client té **menys de 2** visites realitzades (calen 2 per tenir un interval) |

Les vendes anul·lades (RF-10) queden excloses de tots els totals, incloent-hi el detall per treballadora (bloc D). Els clients adormits continuen comptant a les estadístiques i als rànquings.

### RF-18. Historial del client
Cronologia de cites (amb estat), vendes i productes des de la fitxa.

---

### RF-19. Ajuda amb FAQ
Botó `?` visible sempre. Obre una llista de preguntes freqüents en llenguatge planer, amb resposta pas a pas.

### RF-20. Missatges d'error entenedors
Mai errors tècnics en pantalla. Sempre: què ha passat + com solucionar-ho.

### RF-21. Confirmació d'accions destructives
Eliminar client, anul·lar venda, restaurar còpia, etc. → confirmació abans d'executar. Els botons destructius han d'estar clarament diferenciats.

### RF-22. So de confirmació
So curt i discret en confirmar una venda ("cobrar"). Activable/desactivable des de la configuració.

---

### RF-23. Configuració

Tota la configuració viu a la **base de dades** i es pot modificar des d'un panell dins de l'aplicació:

- Dades de la barberia (nom, adreça, telèfon)
- Horari setmanal de la barberia i dies tancats puntuals (festius, vacances)
- Serveis (nom, preu, IVA, durada, actiu)
- Productes (nom, preu, categoria, IVA, actiu)
- Treballadores (nom, horari, actiu/inactiu)
- Mètodes de pagament (llista editable)
- Percentatge d'IVA per defecte i mode de càlcul (inclòs / no inclòs al preu)
- Aplicar IVA als moviments de caixa (sí/no)
- Durada per defecte d'una cita sense servei
- Hora de la còpia de seguretat automàtica i nombre de còpies a conservar
- Mostrar o no l'avís de client convidat
- Activar o desactivar el so de confirmació

**Avís en canviar el mode d'IVA:** canviar entre "preu amb IVA inclòs" i "preu sense IVA" reinterpreta tots els preus del catàleg (un servei de 15 € passaria a costar 18,15 €). L'aplicació ha d'advertir-ho de manera clara abans de confirmar el canvi. Les vendes ja registrades no es veuen afectades.

**Única excepció:** la ruta del fitxer de base de dades es guarda a `appsettings.json`, ja que és necessària abans de poder llegir la base de dades.

### RF-24. Còpia de seguretat automàtica
Còpia diària automàtica a l'hora configurada (per defecte 20:00 h). Es conserven les últimes N còpies (per defecte 15); les més antigues s'esborren automàticament.

### RF-25. Còpia de seguretat manual
Botó `Fer còpia ara` addicional a l'automàtica. Confirmació visual d'èxit.

### RF-26. Restauració de còpia
Triar entre les còpies disponibles (automàtiques o manuals) i restaurar-la, amb confirmació prèvia.

---

### RF-27. Exportació per a l'assessoria

Exportar a **CSV** les dades d'un període, disponible des de l'Historial de vendes o la Caixa. Es generen dos fitxers: un amb una fila per venda (base, IVA, total) i un altre amb el desglossament per tipus impositiu, que és el que cal per al model 303. El detall es descriu a 6.8.

Pensat per enviar les dades a una gestoria per a la declaració trimestral, sense integració directa amb cap sistema extern.

### RF-28. Logging bàsic
Fitxer de text amb data, error/excepció i moment (inici/tancament de l'app, operacions clau, edicions i anul·lacions de vendes). Sense necessitat de categoritzar per component.

Es conserven els **30 últims dies** de registres, valor fix al codi. No s'exposa a la configuració perquè no és una decisió que hagi de prendre la usuària.

---

## 4. Requisits no funcionals

| Àrea | Requisit |
|---|---|
| Plataforma | Windows 10/11, .NET 10, WPF (pur, sense llibreries d'estil externes), SQLite local |
| Funcionament | 100% local, sense connexió a Internet necessària |
| Usabilitat | Botons grans, llenguatge planer, poques passes, pensat per a usuària sense coneixements informàtics |
| Consistència | Mateix comportament sempre per als mateixos botons (`Guardar`, `Cancel·lar`, etc.) |
| Accessibilitat | Text llegible, bon contrast, no dependre només del color |
| Persistència | Dades es conserven entre sessions (SQLite) |
| Mantenibilitat | Separar UI / lògica / accés a dades / models (MVVM), sense sobre-enginyeria |
| Instal·lació | Còpia de carpeta o executable simple. Sense wizard elaborat |
| Actualitzacions | Migracions d'esquema amb EF Core Migrations, aplicades automàticament a l'inici sense perdre dades |
| Còpies de seguretat | Automàtiques (diàries, retenció configurable) + manuals sota demanda |
| Moneda | Euros, valor fix no configurable |

---

## 5. Fora d'abast (explícit)

- Reserves online, portal web o app mòbil per a clients
- Notificacions automàtiques a clients
- Pagaments online
- Integracions externes (calendaris, Veri*Factu, cloud)
- Internacionalització (només català)
- Multi-moneda
- Permisos o rols d'accés entre treballadores (totes veuen i fan tot)
- Fitxatge d'entrada/sortida laboral de treballadores
- Nòmines o gestió laboral
- Seguretat avançada de fitxers (ús d'un sol PC)

---

## 6. Decisions tècniques de baix nivell

### 6.1. Identificació de clients

| Camp | Tipus | Propòsit |
|---|---|---|
| `Id` | `INTEGER PRIMARY KEY AUTOINCREMENT` | Clau primària interna, estable, mai canvia. Usada per totes les FK |
| `ClientKey` | `TEXT` amb índex **únic** | Nom normalitzat: dos clients no poden dir-se igual |

**Càlcul de `ClientKey`:**

```
nom sencer normalitzat (sense accents, lowercase, sense espais)
```

**Regla:** el nom identifica el client. Dos clients no poden compartir nom, **ni tan sols amb telèfons diferents**; quan passa, la usuària hi afegeix el cognom o un segon nom per distingir-los. Dos clients amb noms diferents sí que poden compartir telèfon (una família amb un sol número).

**El telèfon no hi participa** (revisat). A la primera versió la clau era el nom de pila + els 9 últims dígits del telèfon, i feia dues feines alhora que no poden compartir una clau: detectar duplicats *probables*, que vol falsos positius, i alimentar un índex únic, que no en tolera cap. Guanyava l'índex — el botó «Guardar-lo igualment» arribava a `Create` amb una clau repetida i petava amb una `DbUpdateException` que la usuària veia com un error inesperat, perdent el client. Ara el xoc es refusa al diàleg, amb un missatge que diu què cal fer.

**Per què PK autoincremental i no el nom com a PK:** si es corregeix una errada al nom, la clau canviaria i trencaria totes les cites i vendes vinculades. La clau interna és estable; `ClientKey` només fa de restricció d'unicitat.

### 6.2. Estat dels clients

| Estat | A cerques i selectors | A estadístiques | Recuperable |
|---|---|---|---|
| Actiu | Sí | Sí | — |
| Adormit | No | Sí | Sí |
| Eliminat | No existeix | No | No |

L'estat "adormit" és un camp booleà. L'eliminació és física i esborra en cascada cites i vendes del client.

### 6.3. Representació de diners i percentatges

**Tots els imports es guarden com a `INTEGER` en cèntims.** Mai `decimal`, `float` ni `double`.

```
15,00 €  →  1500
 0,05 €  →     5
```

Motius:

1. Precisió exacta: cap error d'arrodoniment binari
2. EF Core mapeja `decimal` a `TEXT` a SQLite, cosa que obliga a resoldre `ORDER BY` i comparacions al client. Amb `INTEGER` s'ordenen dins de SQLite
3. Tota l'aritmètica de sumes i restes és exacta per construcció

**Tots els percentatges d'IVA es guarden com a `INTEGER` en punts base** (centèsimes de percentatge, base 10.000):

```
21,0 %  →  2100
10,0 %  →  1000
 4,0 %  →   400
 5,2 %  →   520   (recàrrec d'equivalència)
```

Guardar-los com a enter de percentatge (`21`) no permetria expressar el 5,2%. Amb punts base tot queda en enters.

---

### 6.4. Gestió de l'IVA

**Principi fonamental: l'IVA es congela a cada transacció.**

- Cada **servei** i cada **producte** té el seu propi percentatge d'IVA al catàleg
- Cada **línia de venda** guarda una **còpia** del percentatge d'IVA aplicat en aquell moment
- Si demà canvia l'IVA d'un producte al catàleg, les vendes passades **no es veuen afectades**: conserven el que realment es va cobrar

Això és imprescindible per al control fiscal: una declaració trimestral ja presentada no pot canviar de valor perquè s'hagi editat el catàleg després.

**Mode de càlcul** (configurable, global): per defecte el preu **inclou** l'IVA.

| Mode | Fórmula |
|---|---|
| IVA inclòs | `base = round(total × 10000 / (10000 + iva_bp))` · `quota = total − base` |
| IVA no inclòs | `quota = round(base × iva_bp / 10000)` · `total = base + quota` |

#### Algoritme de desglossament d'una venda

Calcular la base línia a línia i sumar **no** dona el mateix resultat que calcular-la sobre el total. Exemple real (3 línies de 10,00 € al 21% amb IVA inclòs):

| Mètode | Base | Quota |
|---|---|---|
| Per línia i sumat | 2478 | 522 |
| Sobre el total | 2479 | 521 |

Un cèntim de descuadre per venda faria que l'IVA trimestral no quadrés amb la suma de tiquets. Per evitar-ho, s'agrupa **per tipus d'IVA**:

```
1. Cada línia guarda el seu import final en cèntims (enter exacte, sense arrodonir)
       import_linia = preu_unitari × quantitat
2. Agrupar les línies per percentatge d'IVA (iva_bp)
3. Per cada grup:
       base_grup  = round(total_grup × 10000 / (10000 + iva_bp))
       quota_grup = total_grup − base_grup
4. La venda guarda: base = Σ base_grup · iva = Σ quota_grup · total = Σ total_grup
```

Aquest mètode garanteix sempre la invariant `base + iva == total`, i coincideix amb la forma en què es declara el model 303 (desglossat per tipus impositiu).

#### Regla canònica d'agregació

**Els totals d'un període es calculen SEMPRE sumant els valors ja guardats a cada venda. Mai recalculant a partir de les línies del període.**

Els dos mètodes no donen el mateix resultat, i la diferència **creix indefinidament amb el nombre de vendes**. Simulat amb vendes reals:

| Període | Vendes | Facturació | Divergència de la base |
|---|---|---|---|
| 1 any | 3.000 | 110.493 € | 1,12 € |
| 5 anys | 15.000 | 553.842 € | 5,65 € |
| 20 anys | 60.000 | 2.207.459 € | 21,40 € |

El total facturat coincideix sempre; el que divergeix és el repartiment entre base i quota. Com que cada venda ja té la seva base i la seva quota calculades i congelades, sumar-les és exacte, reprodueix el que es va veure el dia de la venda, i és el que quadra amb els tiquets.

Recalcular sobre el conjunt del període donaria una xifra lleugerament diferent cada cop que es consulta un rang distint, i faria que dos informes del mateix període presentat de maneres diferents no quadressin.

**Conseqüència pràctica:** `CaixaService.Resum` i `InformesService` han d'agregar `Vendes.base_cents` i `Vendes.iva_cents`. Només poden mirar `VendaLinies` per a comptar unitats i classificar serveis/productes, mai per a recalcular imports fiscals.

#### Mode d'arrodoniment

C# fa **arrodoniment bancari** per defecte, que no és el que espera la comptabilitat espanyola:

| Valor | Correcte | `Math.Round` per defecte |
|---|---|---|
| 0,5 | 1 | 0 ❌ |
| 2,5 | 3 | 2 ❌ |

Per tant cal indicar-ho sempre de manera explícita:

```csharp
// Spanish accounting requires away-from-zero rounding, not banker's rounding
Math.Round(value, MidpointRounding.AwayFromZero);
```

#### Moviments de caixa

Poden tenir IVA si l'opció està activada (RF-13). Guarden `base` i `iva` com a valors fixos, no derivats, pel mateix motiu que les vendes.

#### Avís en canviar el mode d'IVA

Canviar el mode reinterpreta tots els preus del catàleg: un servei guardat com a `1500` passa de significar "15 € IVA inclòs" a "15 € + IVA = 18,15 €". Les vendes passades queden segures, però el catàleg canvia de sentit. L'aplicació ha d'avisar-ho clarament abans de confirmar el canvi.

### 6.5. Migracions

**EF Core Migrations.** Aplicades automàticament a l'inici. Abans d'aplicar-ne cap, es fa una còpia de seguretat automàtica de la base de dades.

### 6.6. Anul·lació de vendes

Les vendes anul·lades no s'esborren: es marca `Estat = Anulada`. Totes les consultes de balanç, IVA i rànquings **filtren per `Estat = Activa`**. L'historial mostra totes, amb l'estat visible.

### 6.7. Càlcul de disponibilitat i solapament

Per a cada franja horària:

```
treballadores_disponibles = treballadores ACTIVES
    amb aquesta franja dins del seu horari setmanal

cites_existents = cites (Pendent o Realitzada) que se solapen amb la franja
    → si la nova cita té treballadora assignada, només es compten
      les d'aquella treballadora
    → si no, es compten totes les cites sense assignar o assignades
      a treballadores disponibles en aquell moment

si cites_existents >= treballadores_disponibles → avís de solapament (no bloqueja)
```

Amb una sola treballadora activa, la fórmula es redueix al cas simple: avisar si ja hi ha una cita a la mateixa hora.

### 6.8. Exportació

**Dos fitxers CSV**, tots dos UTF-8 amb separador `;` i decimals amb coma, per compatibilitat amb Excel en configuració espanyola.

**`vendes_<periode>.csv` — una fila per VENDA:**

```
data; hora; client; treballadora; conceptes; metode_pagament; base; iva; total
```

**`iva_<periode>.csv` — una fila per TIPUS IMPOSITIU:**

```
tipus_iva; base; quota; total
```

Aquest segon fitxer és el que la gestoria necessita per al model 303.

**Per què una fila per venda i no per línia.** La base imposable només existeix a nivell de venda i de tipus impositiu, no de línia. Repartir la base d'una venda entre les seves línies obliga a arrodonir, i les parts no sumen el total: en una venda de tres línies de 10,00 € al 21%, la base guardada és 2479 cèntims però la suma de bases per línia dona 2478. El CSV no quadraria amb el que mostra l'aplicació.

Si algun dia cal el detall de línies, s'exporta en un tercer fitxer **sense columnes de base ni IVA**, amb només descripció, quantitat, preu unitari, tipus d'IVA i import. Aquestes columnes sí que són exactes a nivell de línia.

### 6.9. Informes detallats per treballadora

No calen taules noves: el detall (RF-16-D) s'obté agregant `VendaLinies` (agrupat per `servei_id` / `producte_id`) i `Vendes` (agrupat per `treballadora_id` i pel dia de la setmana extret de `data`), filtrant sempre `Estat = Activa`.

Els dos percentatges (RF-17) es deriven de les mateixes agregacions, sense necessitat de guardar-los: es calculen al vol cada vegada que es consulta el període.

---

## 7. Estat de la documentació

- ~~Esquema de base de dades i diagrama ER~~ ✅ `esquema-bbdd.md` · `diagrama-er.mermaid`
- ~~Models de domini (entitats C#)~~ ✅ `models-domini.md`
- ~~Definició detallada de pantalles~~ ✅ `pantalles.md`
- ~~Diagrames de casos d'ús (Mermaid)~~ ✅ `casos-us.md`
- ~~Llistat de ViewModels / Serveis~~ ✅ `capa-mvvm.md`
- ~~Decisions de disseny UI~~ ✅ `disseny-ui.md`

**Documentació completa.** Següent pas: crear el projecte a Visual Studio i generar la migració inicial.
