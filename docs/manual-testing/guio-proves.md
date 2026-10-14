# Guió de proves manuals de l'EvaGest

Prova de la interfície real, feta des de Claude Code amb control de l'ordinador
(Claude fa les captures i els clics; tu pots mirar i aturar-ho quan vulguis).
Complementa els tests automàtics, que ja comproven la lògica: aquí busquem el que
només es veu a la pantalla (botons tapats, textos que no s'entenen, el teclat, la
mida de la finestra, una cosa que fa un clic de més).

**Regla d'or: mai sobre la base de dades real.** Abans de començar, prepara una
base de dades de prova amb l'script (vegeu el pas 0). En acabar, desfés-ho.

Cada error trobat va a `bugs.csv` amb el número següent (`B-43`, `B-44`…), la
descripció, els passos per reproduir-lo i si està resolt (i amb quin commit).

La numeració de les files no es canvia mai: `bugs.csv` i els commits hi fan
referència. Les proves noves s'afegeixen al final del seu bloc.

---

## 0. Preparació

| Què vols provar | Ordre |
|---|---|
| Instal·lació nova (base de dades buida) | `.\scripts\prepare-test-session.ps1 -Mode Fresh` |
| Amb les teves dades (còpia, l'original només es llegeix) | `.\scripts\prepare-test-session.ps1 -Mode Copy` |
| Amb sis mesos de dades inventades | `.\scripts\prepare-test-session.ps1 -Mode Seeded` (i segueix les instruccions) |
| Tornar a la normalitat en acabar | `.\scripts\prepare-test-session.ps1 -Restore` |

L'EvaGest s'obre des de `EvaGest\bin\Debug\net9.0-windows\EvaGest.exe`. Ha d'estar
tancat quan s'executa l'script. Les dades de prova queden a
`%LOCALAPPDATA%\EvaGest-Proves`.

Per a cada bloc: anota la **mida de pantalla** (p. ex. 1366×768 al 125 %), perquè
molts errors visuals només surten en portàtils petits.

**Si l'EvaGest s'obre des de l'app de Claude**, Windows li dona una còpia privada de
`%LOCALAPPDATA%` (dins de `%LOCALAPPDATA%\Packages\Claude_…\LocalCache\Local\`): les
dades de prova, els registres i les còpies d'aquella sessió viuen allà, i les dades
reals no es toquen. Per mirar les dades reals des d'una sessió, llegeix-les per
`\\localhost\C$\Users\<usuari>\AppData\Local\EvaGest\` (només lectura).

---

## A. Primera vegada (base de dades nova) — `-Mode Fresh`

| # | Què fer | Què ha de passar | ✓ |
|---|---|---|---|
| A1 | Obrir l'EvaGest per primer cop | S'obre sense cap error i demana crear el PIN de propietària | |
| A2 | Escriure dos PIN diferents a "PIN nou" i "repetir" | Diu que no coincideixen, no tanca | |
| A3 | Tancar el diàleg del PIN sense crear-lo | L'app continua; Caixa, Vendes, Informes… no surten al menú | |
| A4 | Clicar "Mode propietària" i crear el PIN bé | Mostra el codi de recuperació **una sola vegada**; apunta'l | |
| A5 | Recórrer totes les pàgines: Inici, Agenda, Clients, Treballadores, Catàleg, Vendes, Caixa, Informes, Configuració, Ajuda | Cap pàgina peta; les buides conviden a fer alguna cosa en lloc de mostrar una taula en blanc (Clients, Vendes, cada taula del Catàleg, moviments de Caixa, rànquings d'Informes) | |
| A6 | Catàleg → mètodes de pagament | Hi ha Efectiu, Targeta i Bizum | |
| A7 | "+ Nova venda" sense cap servei creat: "+ Concepte lliure", "Tall", 12,00, Efectiu, Cobrar | Es cobra; l'IVA proposat és el 21 %. "Veure la llista" del cercador diu que encara no hi ha serveis ni productes i on crear-los | |
| A8 | Crear un servei amb preu "15.000" i després "15,00" | El primer es rebutja amb un missatge que diu com escriure-ho | |
| A9 | Crear una treballadora sense horari | Avisa que cal com a mínim un dia; "copiar dilluns a la resta" funciona | |
| A10 | Tancar l'EvaGest i tornar-lo a obrir | Tot el que s'ha fet hi és; no torna a demanar el PIN | |
| A11 | Inici en mode propietària i bloquejat | No mostra cap import en cap dels dos modes: només quantes cites i vendes hi ha avui. No hi ha el botó "Entrada" (sí "+ Sortida") | |
| A12 | Nou servei: mirar la casella del preu abans d'escriure-hi | És buida amb "0,00" en gris; escrivint s'hi posa el preu sense esborrar cap zero. Guardar-la buida demana el preu; "0" es reescriu "0,00" i "15,5" "15,50" en sortir | |
| A13 | Crear set treballadores, una rere l'altra, sense tocar el color | Cadascuna rep un color diferent dels deu (Verd blau, Magenta, Prunya, Oliva, Ocre, Grafit, Rosa, Violeta, Taronja, Marró); la setena és Rosa, no repeteix el primer | |
| A14 | Obrir el desplegable de color d'una treballadora | Hi ha els deu colors, cadascun amb el seu punt i el nom en l'idioma de la sessió | |

## B. Un dia normal

| # | Què fer | Què ha de passar | ✓ |
|---|---|---|---|
| B1 | Crear 2 clients, 2 serveis (Tall 15 €, Barba 8 €) i un producte (Cera 12 €) | Apareixen a les llistes | |
| B2 | Inici → "+ Nova cita": client registrat buscat escrivint un tros del nom (sense accents) | El troba; amb Enter el selecciona | |
| B3 | Triar servei | La durada es posa sola | |
| B4 | Fer 3 cites: una per a un client, una per a algú no registrat, una altra a les 12 h | Totes surten a Inici i a l'Agenda | |
| B5 | Cita 1 → marcar-la feta | S'obre "Nova venda (des de cita)" amb el client, el servei i la treballadora ja posats | |
| B6 | Afegir "Cera" des del cercador, pagar amb Targeta, Cobrar | So de confirmació; la cita passa a Realitzada | |
| B7 | Cita 2 → cobrar en efectiu | Ídem; surt l'avís de client no registrat, en una sola fila amb "Registrar-lo ara" al costat, i els mètodes de pagament es veuen sense fer scroll | |
| B8 | Cita 3 → "No assistida" | Canvia d'estat, no cobra res | |
| B9 | Inici → "+ Sortida": 20,00, "Material" | Es guarda | |
| B10 | Caixa → Avui | Vendes 35,00 €, sortides 20,00 €, saldo 15,00 €; IVA desglossat | |
| B11 | Vendes → Avui | 2 vendes; el total del peu coincideix amb el de Caixa | |
| B12 | Informes | La treballadora i el client del mes surten bé; cap percentatge estrany | |
| B13 | "+ Nova venda" amb almenys un servei venut | A dalt hi ha la fila de botons ràpids (els serveis més venuts, fins a sis); un clic afegeix la línia | |
| B14 | Al cercador de la venda escriure "cer", triar "Cera"; després escriure "Rentat especial" i triar-lo com a concepte lliure | Cada tria afegeix la línia de seguida i buida el cercador; el text que no és al catàleg s'ofereix com a concepte lliure amb aquella descripció | |
| B15 | En una línia: − i + de la quantitat; després el llapis | − / + canvien la quantitat i el total; el llapis obre descripció, preu i IVA sota la línia | |
| B16 | Mètode de pagament de la venda | Un botó per mètode, cadascun tan ample com el seu nom; només un de marcat | |
| B17 | Amb **una o dues** treballadores actives: obrir "+ Nova venda" | La treballadora es tria amb un botó per persona, sense cercador ni "Veure la llista". Clicar-ne una la marca i desmarca l'altra; tornar a clicar la marcada deixa la venda sense treballadora | |
| B18 | Ídem amb una treballadora de nom molt llarg ("Maria Dolors Fernández-Puigdollers") | El nom no queda tallat: passa a dues línies dins del botó, i els botons es posen un sota l'altre si no hi caben al costat | |
| B19 | Crear una tercera treballadora i tornar a obrir "+ Nova venda" | Torna el cercador amb "Veure la llista" | |
| B20 | Agenda amb cites de diverses treballadores | Cada cita porta el color de la seva treballadora (barra i fons), també els colors nous | |
| B21 | "+ Nova cita" per a avui (a les 14:41, per exemple) | Comença al quart d'hora següent (14:45), no a les 10:00; per a un altre dia, a l'hora d'obrir | |
| B22 | Escriure l'hora d'una cita: "9" i després "30"; provar també les fletxes | "9" passa sol als minuts (09:30); ":" i "." també hi passen; les fletxes sumen una hora o un quart. Escriure-ho tecla a tecla, no enganxat | |
| B23 | Escriure una data: "13", "10", sense any; enganxar "2026-10-13"; escriure "31/02" | La data sense any és d'aquest any; l'enganxada omple les tres caselles; 31/02 es posa en vermell i no s'accepta | |
| B24 | Una cita de 15 min (Barba) | A l'Agenda es llegeixen l'hora i el nom, no és una barra buida | |
| B25 | Agenda en català i en castellà, una setmana dins d'un mes que comenci per vocal (octubre, abril, agost) | Català: "14 – 16 d'octubre 2026" i "Dimecres 14 d'octubre"; castellà: "14 – 16 de octubre 2026" | |
| B26 | Informes → "Veure totes les transaccions" | Finestra gran amb vendes i moviments; els filtres (Avui, Aquesta setmana…, tipus, client, treballadora, mètode, categoria, text sense accents) funcionen; els totals de sota corresponen al que es veu | |
| B27 | A la finestra de transaccions: "Mostrar les anul·lades" i "Exportar la taula" | Les anul·lades surten en gris i no sumen; el CSV té exactament les files de la pantalla | |
| B28 | Treballadores → resum d'horari setmanal | Els dies surten en l'idioma de la sessió (Dl, Dt… / Lu, Ma…) | |
| B29 | Comptes d'un sol element: una venda, una transacció, un client del mes amb una visita | Singular: "1 venda activa", "1 transacció", "1 visita" | |

## C. Coses rares però correctes

| # | Què fer | Què ha de passar | ✓ |
|---|---|---|---|
| C1 | Venda a "Sense client" (venda de mostrador) | Es cobra; no apareix a cap rànquing de clients | |
| C2 | Venda a un nom no registrat → "Registrar-lo ara" a mitja venda | Obre la fitxa amb el nom i el telèfon ja posats; en tornar, la venda continua igual | |
| C3 | Escriure com a nom de client exactament `no_registered_client` | Es rebutja amb un missatge entenedor | |
| C4 | Mateix servei dues vegades en una venda | Pregunta "Aquest servei ja és a la venda" amb "Afegir-ne un altre" / "No afegir"; si l'afegeixes, posa quantitat 2. Un producte no pregunta, i el + de la línia tampoc | |
| C5 | Una venda amb productes al 10 % i serveis al 21 % | Caixa mostra dues files d'IVA i la suma quadra | |
| C6 | Configuració → marcar un dia tancat; fer-hi una cita fora d'horari i encavalcada amb una altra | Surten els avisos, pregunta per l'encavalcament, però deixa guardar. A la graella, "Aquesta cita" surt al costat de la que encavalca, no a sobre | |
| C7 | Una cita a les 23:30 de 90 min | No fa res estrany a la graella (no "dona la volta" a mitjanit); la graella s'allarga per ensenyar "Aquesta cita" | |
| C8 | Moure una cita a un altre dia (el client truca) | Desapareix d'avui i surt al nou dia amb la mateixa durada | |
| C9 | Una treballadora amb vendes: eliminar-la | Diu que s'ha marcat inactiva; ja no surt a vendes noves, però sí en les antigues i als informes | |
| C10 | Canviar el mode d'IVA a "sense IVA inclòs" (primer dir que no, després que sí) | Si dius no, el selector torna on era al moment; les vendes ja fetes no canvien d'import | |
| C11 | Canviar l'idioma a castellà i reiniciar | Tot en castellà, sense textos barrejats ni retallats; les preguntes també (botons de l'app, no "Sí / No" de Windows) | |
| C12 | Client adormit que torna | La seva cita antiga el mostra; es pot despertar des de Clients | |
| C13 | Editar una venda antiga de la treballadora inactiva (C9) quan en queden dues d'actives | Surt el cercador (són tres amb la inactiva) i la venda conserva la seva treballadora | |
| C14 | Informes → "Fa temps que no vénen" | Només hi surten clients que fa 30 dies o més que no vénen, mai "0 dies" | |
| C15 | Clients → "Mostrar clients adormits" sense cap adormit | Diu que no n'hi ha i per a què serveix adormir un client | |
| C16 | En una cita, escriure 13:00 dígit a dígit i tornar a 10:00 | La graella s'eixampla per ensenyar l'hora i torna a l'horari normal; no es queda des de la 01:00 | |

## D. Equivocacions i correccions

| # | Què fer | Què ha de passar | ✓ |
|---|---|---|---|
| D1 | Cobrar 51,00 en lloc de 15,00 → Vendes → Editar → corregir | El botó principal diu "Guardar la venda", no "Cobrar". La venda queda a 15,00, mateixa data i hora; Caixa quadra | |
| D2 | Cobrar la cita d'una altra persona per error | No es pot cancel·lar la cita cobrada (ho diu) | |
| D3 | Anul·lar la venda equivocada: primer PIN incorrecte, després el bo | Amb el PIN dolent no anul·la; amb el bo, sí | |
| D4 | Tornar la cita a pendent i cobrar la persona correcta | Funciona; la venda anul·lada segueix a la llista però no suma | |
| D5 | Venda amb tots els errors possibles, un per un (sense línies, sense client, sense mètode, sense descripció, preu buit, preu "quinze", preu negatiu, quantitat 0, IVA 121) | Cada cop un missatge diferent que diu què cal fer; el de sense client esmenta "Sense client"; al final, una sola venda | |
| D6 | Sortida de caixa: buida, "-5", "0", "1.200", sense concepte | Cada cas es rebutja amb un missatge clar | |
| D7 | Entrar dues vegades el mateix lloguer i eliminar-ne un (amb PIN) | En queda un; el saldo quadra | |
| D8 | Crear un client amb un nom que ja existeix (en minúscules i sense accent) | Es rebutja i diu qui té el nom i el seu telèfon | |
| D9 | Cancel·lar una cita per error i tornar-la a pendent | Es pot cobrar normalment | |
| D10 | Eliminar des de l'Agenda una cita que ja té venda | No l'elimina i ho diu de seguida, sense preguntar abans "Eliminar la cita?" | |
| D11 | Eliminar una cita sense venda (des de la cita i des de l'Agenda) | La pregunta mostra "Eliminar" en vermell i el focus a "Cancel·lar": Enter no l'elimina | |
| D12 | Treure la treballadora d'una venda ja triada (B17: tornar a clicar el botó marcat) i cobrar | Es cobra sense treballadora; Vendes no li'n posa cap | |

## E. Oblits

| # | Què fer | Què ha de passar | ✓ |
|---|---|---|---|
| E1 | Una cita de fa més d'una hora sense cobrar | Surt l'avís de cites pendents a dalt, sigui quina sigui la pàgina | |
| E2 | Tancar l'avís | Desapareix una estona i torna si continua pendent | |
| E3 | Marcar-la feta i tancar el diàleg de venda sense cobrar | La cita continua pendent (no es perd) | |
| E4 | Començar una venda, escriure línies i prémer Esc | Pregunta si vols descartar els canvis ("Descartar" en vermell, focus a "Continuar editant"); "Continuar editant" no perd res | |
| E5 | Deixar l'app oberta més de 5 minuts en mode propietària | Es bloqueja sola; les pàgines privades desapareixen | |
| E6 | Deixar l'app oberta d'un dia per l'altre (o canviar la data del PC) i mirar Caixa → Avui | Mostra el dia nou, no el d'ahir | |
| E7 | Nou client amb nom i mòbil escrits → Esc; després Cancel·lar; després la X | Les tres vegades pregunta abans de descartar. Obert des d'una venda amb el nom ja posat i sense tocar res més, tanca sense preguntar | |
| E8 | "+ Sortida" amb l'import escrit → Esc (i també amb "+ Entrada" de Caixa) | Pregunta abans de descartar; sense res escrit, tanca directament | |
| E9 | Cobrar una venda de mostrador (sense cita) i obrir el registre del dia (`Logs\log-<data>.txt`) | Hi ha una línia "Sale N created" amb el total, com les vendes des de cita | |

## F. Maneig i pantalla

| # | Què fer | Què ha de passar | ✓ |
|---|---|---|---|
| F1 | Finestra petita / portàtil 1366×768 al 125 % | Tots els diàlegs mostren els botons (Cobrar, Guardar) sense sortir de la pantalla | |
| F2 | Venda amb 10 línies | La llista fa scroll; el total i "Cobrar" no es mouen; la línia nova queda a la vista | |
| F3 | Només teclat: Tab, Enter, Esc, fletxes als cercadors | Tot es pot fer sense ratolí; Esc tanca el desplegable abans que el diàleg. Amb el desplegable tancat i el cursor al cercador, Esc tanca el diàleg i Enter prem el botó principal | |
| F4 | Doble clic ràpid a "Cobrar" | Una sola venda | |
| F5 | Noms molt llargs i amb accents/emoji en clients i serveis | No trenquen les taules, les targetes de l'agenda ni el panell del dia (s'escurcen amb "…" i surten sencers en passar el ratolí) | |
| F6 | Obrir l'EvaGest dues vegades | La segona mostra la que ja està oberta | |
| F7 | Finestra a 1280×800 i a l'amplada mínima: Agenda i Caixa | "+ Nova cita" i "+ Entrada" / "+ Sortida" passen a una segona fila en lloc de quedar tallats | |
| F8 | Treballadores amb la taula buida i després amb files; i horaris setmanals amb la finestra estreta | "Editar" i els altres botons de fila es veuen sencers; la tarda passa sota el matí en lloc de tallar-se | |
| F9 | Venda a 1920×1080 al 125 % (la pantalla de la botiga) amb quatre línies, client, treballadora i mètode | Tot a la vista sense scroll, també amb la treballadora en botons (B17) i amb dues treballadores una sota l'altra (B18) | |

## G. Còpies i exportació

| # | Què fer | Què ha de passar | ✓ |
|---|---|---|---|
| G1 | Configuració → "Fer còpia ara" | Diu quan s'ha fet i a quina carpeta; surt a "última còpia" | |
| G2 | Fer una venda, després "Restaurar còpia" de l'anterior | La pregunta mostra "Restaurar" en vermell; la venda nova desapareix; l'app avisa que es reinicia | |
| G3 | Vendes → "Exportar període" del trimestre | Genera els CSV; s'obren bé a l'Excel amb accents correctes | |
| G4 | Exportar amb el fitxer de destí obert a l'Excel | Diu que el tanquis; no peta | |
| G5 | Deixar un fitxer `*.partial` a la carpeta de còpies (com si el PC s'hagués apagat a mitja còpia), mirar "Restaurar còpia" i després "Fer còpia ara" | No surt a "Restaurar còpia" ni compta com a còpia del dia; la còpia següent l'esborra | |

G3 i G4 passen pel selector de carpetes de Windows, que el control de l'ordinador no
sempre pot fer servir: si no es pot, es deixen per a la persona i ho cobreixen els tests
automàtics d'exportació.

---

## Com ho fem amb Claude Code

1. Activa el control de l'ordinador per a aquesta sessió a l'app de Claude.
2. Executa el pas 0 amb el mode que vulguis.
3. Digues a Claude quin bloc vols provar (per exemple "fes el bloc D"). Claude
   obre l'EvaGest, segueix el guió, fa captures dels punts dubtosos i, quan troba
   un error, l'apunta a `bugs.csv` i (si vols) l'arregla amb el seu test.
4. En acabar: `.\scripts\prepare-test-session.ps1 -Restore`.

Trucs que ja s'han après:

- Les hores i dates s'han d'escriure **tecla a tecla**: enviat tot d'un cop, "0900"
  pot quedar "09:09".
- Després de qualsevol canvi de mida o de pàgina, fes una captura nova abans de
  clicar per coordenades: la disposició es mou.
- Les proves de la treballadora en botons (B17–B19) necessiten primer una o dues
  treballadores actives i després tres: amb `-Mode Fresh` és fàcil de muntar.
