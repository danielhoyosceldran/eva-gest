# Changelog

Installer / app releases. The version is `<Version>` in `EvaGest/EvaGest.csproj`;
the installer file is `installer/Output/EvaGest-Setup-<version>.exe`.

Version 1 is closed at 1.3.0. The releases first numbered 2.0.0 and 3.0.0 were
fixes within version 1 and were renumbered 1.2.0 and 1.3.0. The next version is 2.0.0.

## 2.0.0

- The Home screen never shows money any more, not even in owner mode: it looks the
  same locked or unlocked, with only the number of appointments and sales today. The
  day's takings, cash-ins, cash-outs and balance are on Caixa and Informes, so they
  cannot be seen by accident on the screen left open at the counter.
- A sale can be charged to nobody. Next to "Client existent" and "Client nou", the
  sale window has a third option, "Sense client", which saves the sale without any
  client; lists and exports show it as "Sense client". It has to be chosen: an empty
  client box is still refused, so a forgotten client is not saved by accident. Only
  sales offer it, never appointments. In the sale window the client column is now
  wider than the worker's, so the three options fit in one row.
- The sale window is split into its three parts (who, what is sold, how it is paid)
  by thin lines, with a vertical line between client and worker and between payment
  method and notes. The worker's "Veure la llista" button now sits under its search
  box, so the box keeps a usable width in the narrower column.
- The sale window always opens as tall as the screen. The list of services and
  products takes the free height and, when the sale has more lines than fit, scrolls
  on its own: the client, the search box, the payment method and Cobrar stay in view.
  A line just added is scrolled into view.
- Adding a service the sale already has asks first: "Aquest servei ja és a la venda",
  with "Afegir-ne un altre" or "No afegir". A client rarely has two haircuts, so a
  second one is usually a double click. Products are not asked about, and the + on a
  line still adds one more without asking.
- Informes has a new button, "Veure totes les transaccions", that opens a large window
  with every sale and till movement in one table. It filters by period (with Avui,
  Aquesta setmana, Aquest mes and Aquest any shortcuts), type (sales, cash-ins,
  cash-outs), client, worker, payment method, category (services, products, other
  concepts, each product category and each expense category) and free text, which
  ignores accents. Voided sales and movements can be shown, greyed out, and never
  count. Underneath are the totals of what is on screen: income, outgoings, net, and
  the base and VAT of the sales. The table is read-only; "Exportar la taula" saves
  exactly the rows on screen as a CSV.
- The sale window adds lines in one step. A row of quick buttons offers the six most
  sold services (by units on active sales; a new shop sees its services by name), one
  click each. Below it, a single search box covers services and products together;
  picking a match adds the line at once and empties the box for the next one, so the
  "+ Servei" and "+ Producte" buttons are gone. Text that is not in the catalogue is
  offered as a custom concept with that description. Adding the same item again raises
  its quantity instead of adding a second line, unless that line's price was changed
  by hand.
- Each sale line now shows only its concept, its quantity with − / + buttons and its
  amount. Description, price and VAT open under the line with the pencil button, by
  themselves for a custom concept, and on any line Cobrar refuses. With no lines yet,
  the window says how to add one instead of showing empty column headers.
- The sale window is laid out to fit an everyday sale (four lines) without scrolling on
  a 1920x1080 screen at 125 %: client and worker share a row, so do payment method and
  notes, and the totals sit next to Cobrar, always in view however long the sale grows.
- In the sale window the payment method is chosen with a row of buttons, one per
  method and each as wide as its name, instead of a drop-down list. The chosen one is
  filled in; only one can be chosen at a time.
- The drop-down lists that grow with the shop are now search boxes, like the client box
  of a new appointment: type to see the matches, or press "Veure la llista" ("…" where
  space is short) to scroll every option in its own window. This covers the worker,
  service, product and category boxes of the appointment, sale and till-movement
  windows; the client, service, product and worker filters of Vendes; the client,
  worker and category filters of the transactions window; and the worker filter of the
  Agenda. An empty filter box still means "all". Text typed but not picked is settled
  on leaving the box: it picks the one option it names, or is cleared.
- Hours are typed in two boxes, hours and minutes, with the ":" already in place: the
  appointment's hour and every opening-hours row (the shop's and each worker's). Two
  digits in the hours move on to the minutes by themselves (so does a single digit that
  can only be one hour, such as 9), ":" or "." also move on, and the arrow keys step an
  hour or a quarter of an hour. The drop-down list of quarter hours is gone.
- Dates are typed the same way, in three boxes (day / month / year) with the "/"
  already in place: every date in the app, from the appointment's day to the From / To
  filters. A complete day or month moves on by itself, "/", "-" or "." also move on, the
  arrow keys step a day, a month or a year, and a pasted "13/10/2026" or "2026-10-13"
  fills all three. Leaving out the year means this year, and "26" is 2026. A date that
  does not exist, such as 31/02, turns the box red and is not taken until it is put
  right. The calendar button is still there to pick a day with the mouse.
- Inici no longer has the "Entrada" quick button, which was hardly used. A cash-in is
  still recorded from Caixa, with "+ Entrada".
- The price and amount boxes (new service, new product, a custom line of a sale, a
  till movement) now start empty, with "0,00" shown in grey as a hint: a price is
  typed straight away, without first deleting a zero. Leaving the box empty is not
  read as zero any more: saving asks for the price ("Cal indicar el preu per guardar,
  encara que sigui 0") or the amount. Leaving a box rewrites what was typed with two
  decimals, so "0" reads "0,00" and "15,5" reads "15,50".
- The buttons at the end of a table row (Editar, Marcar inactiva, Eliminar…) are no
  longer cut off. On Treballadores, a table first shown empty kept that column almost
  zero wide once rows arrived, hiding Editar; it now widens to its buttons whenever
  the rows change, on every table.
- The weekly schedule overview on Treballadores names the days in the session's
  language (Dl, Dt, Dc… / Lu, Ma, Mi…), as the schedule column already did; it read
  Mon, Tue, Wed… in both languages.
- Counts of one read in the singular: "1 venda activa" under the sales list,
  "1 transacció" in the transactions window and "1 visita" for the client of the
  month, instead of "1 vendes actives", "1 transaccions" and "1 visites".
- Clients with nothing to list no longer shows bare table headers. It invites to add
  the first client with "+ Nou client", points to "Mostrar clients adormits" when every
  client is asleep, and says "Cap client coincideix amb «…»" when a search finds
  nobody.
- Vendes with no sale at all says so and offers "+ Nova venda", instead of "Cap venda
  coincideix amb aquests filtres" and "Netejar filtres" when no filter was set. That
  message is kept for a table emptied by the filters.
- "Veure la llista" on an empty list no longer says "Res no coincideix amb la cerca"
  when nothing was searched. In the sale window it says the catalogue has no services
  or products yet, where to create them, and that "+ Concepte lliure" works meanwhile;
  other lists say they are still empty.
- The other empty tables say what to do instead of showing only their headers:
  services, products, payment methods and expense categories on Catàleg, the
  movements on Caixa, and the worker ranking on Informes. The client rankings on
  Informes say why they are empty instead of showing blank cards.
- The "client not registered" notice in the sale window is one row, with "Registrar-lo
  ara" beside the text instead of under it. Charging an appointment for someone not
  registered pushed the payment methods under the buttons on the shop's screen, and
  the form had to be scrolled to choose how to pay.
- "Fa temps que no vénen" on Informes lists only clients who have not come for at least
  30 days. It used to list every client by last visit, so with few clients it showed
  someone who had come that same day ("0 dies").
- A new appointment for today starts at the next quarter hour from now (14:41 opens at
  14:45) instead of always at 10:00, from Inici and from the Agenda's "+ Nova cita";
  another day still starts at its opening time.
- In the new-appointment window's grid, "Aquesta cita" sits beside an appointment it
  overlaps instead of on top of it, so both can be read and the clash is plain. While
  editing, the appointment is shown only as "Aquesta cita", not twice.
- A checkbox label wraps when its space is short instead of running off: "Mostrar les
  anul·lades" in the transactions window is no longer cut on a 1280x800 laptop, and in
  Spanish "Mañana" no longer spills over the time beside it in the weekly schedules.
- In the Agenda's day panel, a long client, service or worker name shortens with "…"
  and shows in full on hover, instead of running off the edge of the panel.
- On a narrow window the toolbars of Agenda and Caixa no longer lose their buttons off
  the right edge: "+ Nova cita", and "+ Entrada" / "+ Sortida", move to a second row
  when they do not fit beside the rest. On a 1280x800 laptop Caixa cut "+ Sortida",
  and at the window's minimum width the Agenda cut "+ Nova cita".
- Every question the app asks (add the same service again, discard what you typed,
  save over another appointment, change the VAT mode, restore a copy, delete an
  appointment) is its own window with concrete answers in the app's language, such as
  "Afegir-ne un altre" / "No afegir" or "Descartar" / "Seguir editant". They were
  Windows message boxes with "Yes" / "No" in the language of Windows.
- Answering "Cancel·lar" to the VAT mode change puts the picker back on the mode in
  force at once; it used to keep showing the mode just turned down until the page was
  left.
- The asleep clients list on Clients, opened with nobody asleep, says so and what
  putting a client to sleep is for, instead of showing an empty table.
- In the appointment window, a time outside the hours the grid shows (23:30 on a grid
  that ends at 20:00) widens the grid so "Aquesta cita" is in view; it used to sit below
  the last row, out of sight.
- In the weekly schedules (the shop's opening hours and each worker's), a window too
  narrow for a whole row moves the afternoon under the morning instead of cutting off
  its last time.
- "Fer còpia ara" now says which folder the new copy is in, not only when it was made.
- A backup cut off while being written (the PC switched off while the app was taking its
  closing copy) no longer stays in the backups folder looking like a good copy. It used
  to be offered for restore and to count as the day's automatic copy, so no other was
  taken that day.
- With the cursor in a search box (client, worker, catalogue), Esc closes the window and
  Enter presses its main button again once the box's list is closed; the box used to keep
  both keys for itself.
- Typing a time in the appointment window no longer leaves the picker's hours stretched
  from 01:00: the first digit of 13:00 is 01:00, and the grid widened to show it and
  never narrowed back to the working day.
- A question whose answer loses something (delete an appointment, discard changes,
  restore a backup) shows that answer in red and starts with the focus on the way back,
  so Enter pressed out of habit no longer deletes or discards.
- Deleting an appointment that was already charged says straight away that it cannot be
  deleted, instead of first asking "Eliminar la cita?" and refusing after the yes.

## 1.3.0 (closes version 1)

- Caixa now follows the date when EvaGest stays open overnight. "Avui", "Aquesta
  setmana" and the other periods used to stay on the day the app was opened, and a
  movement entered the next morning was dated the day before.
- The filters on the Vendes page now list clients, services, products, workers and
  payment methods created since the page was first opened, and sleeping clients too.
  They used to need a restart.
- Deleting a worker who was paid through the till (a salary or a commission) now
  deactivates her instead. Deleting her used to remove her name from those payments.
- The workers report counts a service sold twice on one line as two services.
- Clients born on 29 February get their birthday notice on 28 February in years without one.
- Searching for a client with "_" or "%" in the text now finds only names containing it,
  instead of every client.
- Without opening hours set, the Agenda and the slot picker in the appointment window
  no longer end where the last appointment ends. They show the usual day (9:00 to
  20:00), extended if an appointment falls outside it.

## 1.2.0

- Restoring a backup made by an older version (1.0) now finishes and closes the app as
  it should. It used to report an error after the data had already been restored and
  leave the app open on it.
- If EvaGest cannot start because its settings file or data folder is missing or
  unusable, it now says so and closes. It used to stay running invisibly, and every
  later attempt to open it said it was already open.
- If the appointment window cannot load its data, it now says so and does not let the
  appointment be saved. It used to open half empty without a word, and saving an
  edited appointment then removed its service and worker.
- A backup that fails while it is being written (disk full, data file busy) no longer
  leaves an empty file behind. That file used to count as the day's copy, so the
  automatic backup was not tried again, and it showed up in the list to restore.
- A backup that was taken correctly is no longer reported as failed when tidying up
  the old copies afterwards runs into a problem.
- A payment method or expense category can no longer be given a name that already
  exists (a deactivated one included). The window now says so and keeps what was typed,
  instead of closing on an unexpected error.
- The sale window now closes as soon as the sale is saved. If something failed just
  after saving, it could stay open and a second Cobrar charged the same sale twice.
- When "Fer còpia ara" or a restore cannot be done (disk full, data file busy), the
  page or the restore window now says so and what to try, instead of a generic error.
- The windows for services, products, payment methods, categories, clients, till
  movements and workers now save before they close. If saving fails they stay open,
  say so, and keep what was typed; before, it was lost behind a generic error.
- An error that keeps repeating now shows its message once (at most once a minute)
  instead of one window after another, and an error with no main window open closes
  the app instead of leaving it running invisibly.
- If the Agenda could not load the workers the first time it was opened, it now loads
  them the next time; the worker filter used to stay without them until a restart.
- When moving the Agenda to other days fails, the date range at the top now stays on
  the days still shown instead of naming days that were never loaded.
- If Configuració cannot read the settings, it now says so at the top of the page and
  does not save the switches over the stored values until it has read them.
- In the Agenda, appointments now cover at most two thirds of the day column. The
  right third stays free, so another appointment at the same time can be added by
  clicking there.
- The mouse wheel now scrolls the page when the pointer is over a table (Clients and
  every page with a long list). Before, the page only moved with the pointer beside it.

## 1.1.0

- Deleting an appointment no longer asks for the owner's PIN, only "are you sure?".
  Deleting clients, sales, till movements and catalogue items still asks for it.
- The action buttons at the end of each table (Editar, Anul·lar, Eliminar…) always show in full:
  that column takes the width its buttons need and the other columns share the rest.
  The Inici appointments table now drops its least important columns on a narrow screen.
- The message shown when a dialog can't be saved (missing or invalid fields) is now a
  bold red block instead of thin red text, so it is hard to miss.
- Works on laptop screens (down to 1366x768 at 125 % scaling):
  - Dialogs never grow past the screen. When the form is taller than the screen it
    scrolls, and the buttons (Guardar, Cobrar, Cancel·lar) and the error message stay
    visible underneath it. A sale with many lines no longer hides Cobrar.
  - The appointment dialog's week picker narrows to fit the screen.
  - The navigation scrolls when it doesn't fit, so Bloquejar is always reachable.
  - Summary cards (Inici, Caixa, Informes) wrap onto a second row instead of cutting
    off amounts.
  - The sales list hides base, VAT, worker and payment columns on a narrow screen
    (the totals stay in the footer) instead of hiding the client and concept.
  - The Agenda's date range moves under the buttons when there is no room beside
    them, and the day panel narrows so the week stays readable.
  - The sales chart in Informes keeps every month in view; long names wrap or show
    in full in a tooltip.
- Closing the app a second time on the same day no longer fails with an error.
- In the sale dialog, "+ Servei" and "+ Producte" stay greyed out until something is
  picked in the box next to them, instead of failing with an error. After adding, the
  box empties again.
- With the language set to Spanish, every page now shows in Spanish. Only the
  navigation did before; the pages themselves stayed in Catalan (or in the Windows
  language).

## 1.0.0

First installer. Self-contained win-x64 exe, Inno Setup, per-user install,
replaces itself on upgrade (fixed `AppId`).
