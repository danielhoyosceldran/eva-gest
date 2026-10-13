# Guió de proves manuals de l'EvaGest

Prova de la interfície real, feta des de Claude Code amb control de l'ordinador
(Claude fa les captures i els clics; tu pots mirar i aturar-ho quan vulguis).
Complementa els tests automàtics, que ja comproven la lògica: aquí busquem el que
només es veu a la pantalla (botons tapats, textos que no s'entenen, el teclat, la
mida de la finestra, una cosa que fa un clic de més).

**Regla d'or: mai sobre la base de dades real.** Abans de començar, prepara una
base de dades de prova amb l'script (vegeu el pas 0). En acabar, desfés-ho.

Cada error trobat va a `bugs.csv` (descripció, passos per reproduir-lo, resolt o no).

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

---

## A. Primera vegada (base de dades nova) — `-Mode Fresh`

| # | Què fer | Què ha de passar | ✓ |
|---|---|---|---|
| A1 | Obrir l'EvaGest per primer cop | S'obre sense cap error i demana crear el PIN de propietària | |
| A2 | Escriure dos PIN diferents a "PIN nou" i "repetir" | Diu que no coincideixen, no tanca | |
| A3 | Tancar el diàleg del PIN sense crear-lo | L'app continua; Caixa, Vendes, Informes… no surten al menú | |
| A4 | Clicar "Mode propietària" i crear el PIN bé | Mostra el codi de recuperació **una sola vegada**; apunta'l | |
| A5 | Recórrer totes les pàgines: Inici, Agenda, Clients, Treballadores, Catàleg, Vendes, Caixa, Informes, Configuració | Cap pàgina peta; les buides conviden a fer alguna cosa en lloc de mostrar una taula en blanc | |
| A6 | Catàleg → mètodes de pagament | Hi ha Efectiu, Targeta i Bizum | |
| A7 | "+ Nova venda" sense cap servei creat: concepte lliure "Tall", 12,00, Efectiu, Cobrar | Es cobra; l'IVA proposat és el 21 % | |
| A8 | Crear un servei amb preu "15.000" i després "15,00" | El primer es rebutja amb un missatge que diu com escriure-ho | |
| A9 | Crear una treballadora sense horari | Avisa que cal com a mínim un dia; "copiar dilluns a la resta" funciona | |
| A10 | Tancar l'EvaGest i tornar-lo a obrir | Tot el que s'ha fet hi és; no torna a demanar el PIN | |

## B. Un dia normal

| # | Què fer | Què ha de passar | ✓ |
|---|---|---|---|
| B1 | Crear 2 clients, 2 serveis (Tall 15 €, Barba 8 €) i un producte (Cera 12 €) | Apareixen a les llistes | |
| B2 | Inici → "+ Nova cita": client registrat buscat escrivint un tros del nom (sense accents) | El troba; amb Enter el selecciona | |
| B3 | Triar servei | La durada es posa sola | |
| B4 | Fer 3 cites: una per a un client, una per a algú no registrat, una altra a les 12 h | Totes surten a Inici i a l'Agenda | |
| B5 | Cita 1 → marcar-la feta | S'obre "Nova venda (des de cita)" amb el client, el servei i la treballadora ja posats | |
| B6 | Afegir "Cera" des del cercador, pagar amb Targeta, Cobrar | So de confirmació; la cita passa a Realitzada | |
| B7 | Cita 2 → cobrar en efectiu | Ídem; surt l'avís de client no registrat | |
| B8 | Cita 3 → "No assistida" | Canvia d'estat, no cobra res | |
| B9 | Inici → "+ Sortida": 20,00, "Material" | Es guarda | |
| B10 | Caixa → Avui | Vendes 35,00 €, sortides 20,00 €, saldo 15,00 €; IVA desglossat | |
| B11 | Vendes → Avui | 2 vendes; el total del peu coincideix amb el de Caixa | |
| B12 | Informes | La treballadora i el client del mes surten bé; cap percentatge estrany | |

## C. Coses rares però correctes

| # | Què fer | Què ha de passar | ✓ |
|---|---|---|---|
| C1 | Venda a "Sense client" (venda de mostrador) | Es cobra; no apareix a cap rànquing de clients | |
| C2 | Venda a un nom no registrat → "Registrar-lo ara" a mitja venda | Obre la fitxa amb el nom i el telèfon ja posats; en tornar, la venda continua igual | |
| C3 | Escriure com a nom de client exactament `no_registered_client` | Es rebutja amb un missatge entenedor | |
| C4 | Mateix servei dues vegades en una venda | Pregunta si de debò en vols un altre; si dius sí, posa quantitat 2 | |
| C5 | Una venda amb productes al 10 % i serveis al 21 % | Caixa mostra dues files d'IVA i la suma quadra | |
| C6 | Configuració → marcar un dia tancat; fer-hi una cita fora d'horari i encavalcada amb una altra | Surten els avisos, pregunta per l'encavalcament, però deixa guardar | |
| C7 | Una cita a les 23:30 de 90 min | No fa res estrany a la graella (no "dona la volta" a mitjanit) | |
| C8 | Moure una cita a un altre dia (el client truca) | Desapareix d'avui i surt al nou dia amb la mateixa durada | |
| C9 | Una treballadora amb vendes: eliminar-la | Diu que s'ha marcat inactiva; ja no surt a vendes noves, però sí en les antigues i als informes | |
| C10 | Canviar el mode d'IVA a "sense IVA inclòs" (primer dir que no, després que sí) | Si dius no, el selector torna on era; les vendes ja fetes no canvien d'import | |
| C11 | Canviar l'idioma a castellà i reiniciar | Tot en castellà, sense textos barrejats ni retallats | |
| C12 | Client adormit que torna | La seva cita antiga el mostra; es pot despertar des de Clients | |

## D. Equivocacions i correccions

| # | Què fer | Què ha de passar | ✓ |
|---|---|---|---|
| D1 | Cobrar 51,00 en lloc de 15,00 → Vendes → Editar → corregir | La venda queda a 15,00, mateixa data i hora; Caixa quadra | |
| D2 | Cobrar la cita d'una altra persona per error | No es pot cancel·lar la cita cobrada (ho diu) | |
| D3 | Anul·lar la venda equivocada: primer PIN incorrecte, després el bo | Amb el PIN dolent no anul·la; amb el bo, sí | |
| D4 | Tornar la cita a pendent i cobrar la persona correcta | Funciona; la venda anul·lada segueix a la llista però no suma | |
| D5 | Venda amb tots els errors possibles, un per un (sense línies, sense client, sense mètode, sense descripció, preu "quinze", preu negatiu, quantitat 0, IVA 121) | Cada cop un missatge diferent que diu què cal fer; al final, una sola venda | |
| D6 | Sortida de caixa: buida, "-5", "0", "1.200", sense concepte | Cada cas es rebutja amb un missatge clar | |
| D7 | Entrar dues vegades el mateix lloguer i eliminar-ne un (amb PIN) | En queda un; el saldo quadra | |
| D8 | Crear un client amb un nom que ja existeix (en minúscules i sense accent) | Es rebutja i diu qui té el nom i el seu telèfon | |
| D9 | Cancel·lar una cita per error i tornar-la a pendent | Es pot cobrar normalment | |
| D10 | Eliminar des de l'Agenda una cita que ja té venda | No l'elimina i ho explica | |

## E. Oblits

| # | Què fer | Què ha de passar | ✓ |
|---|---|---|---|
| E1 | Una cita de fa més d'una hora sense cobrar | Surt l'avís de cites pendents a dalt, sigui quina sigui la pàgina | |
| E2 | Tancar l'avís | Desapareix una estona i torna si continua pendent | |
| E3 | Marcar-la feta i tancar el diàleg de venda sense cobrar | La cita continua pendent (no es perd) | |
| E4 | Començar una venda, escriure línies i prémer Esc | Pregunta si vols descartar els canvis; "seguir editant" no perd res | |
| E5 | Deixar l'app oberta més de 5 minuts en mode propietària | Es bloqueja sola; les pàgines privades desapareixen | |
| E6 | Deixar l'app oberta d'un dia per l'altre (o canviar la data del PC) i mirar Caixa → Avui | Mostra el dia nou, no el d'ahir | |

## F. Maneig i pantalla

| # | Què fer | Què ha de passar | ✓ |
|---|---|---|---|
| F1 | Finestra petita / portàtil 1366×768 al 125 % | Tots els diàlegs mostren els botons (Cobrar, Guardar) sense sortir de la pantalla | |
| F2 | Venda amb 10 línies | La llista fa scroll; el total i "Cobrar" no es mouen | |
| F3 | Només teclat: Tab, Enter, Esc, fletxes als cercadors | Tot es pot fer sense ratolí; Esc tanca el desplegable abans que el diàleg | |
| F4 | Doble clic ràpid a "Cobrar" | Una sola venda | |
| F5 | Noms molt llargs i amb accents/emoji en clients i serveis | No trenquen les taules ni les targetes de l'agenda | |
| F6 | Obrir l'EvaGest dues vegades | La segona mostra la que ja està oberta | |

## G. Còpies i exportació

| # | Què fer | Què ha de passar | ✓ |
|---|---|---|---|
| G1 | Configuració → "Fer còpia ara" | Diu on s'ha fet; surt a "última còpia" | |
| G2 | Fer una venda, després "Restaurar còpia" de l'anterior | La venda nova desapareix; l'app avisa que es reinicia | |
| G3 | Vendes → "Exportar període" del trimestre | Genera els CSV; s'obren bé a l'Excel amb accents correctes | |
| G4 | Exportar amb el fitxer de destí obert a l'Excel | Diu que el tanquis; no peta | |

---

## Com ho fem amb Claude Code

1. Activa el control de l'ordinador per a aquesta sessió a l'app de Claude.
2. Executa el pas 0 amb el mode que vulguis.
3. Digues a Claude quin bloc vols provar (per exemple "fes el bloc D"). Claude
   obre l'EvaGest, segueix el guió, fa captures dels punts dubtosos i, quan troba
   un error, l'apunta a `bugs.csv` i (si vols) l'arregla amb el seu test.
4. En acabar: `.\scripts\prepare-test-session.ps1 -Restore`.
