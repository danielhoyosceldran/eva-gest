#!/usr/bin/env python3
"""
Seed EvaGest DB with 6 months of barbershop data.

Clears ALL tables, then populates with realistic data:
  - 2 workers with different schedules
  - 100 clients with varying visit patterns
  - Appointments + sales (service lines, optional product lines)
  - Walk-in sales (no appointment)
  - Monthly salary payments + tax consultory cash movements

Usage:
    python scripts/seed_db.py
    python scripts/seed_db.py --db-path "C:/custom/path/barberia.db"

DB default: %%LOCALAPPDATA%%\\EvaGest\\barberia.db
"""

import sqlite3
import os
import math
import random
import argparse
import unicodedata
from datetime import date, timedelta

# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------
DEFAULT_DB = os.path.expandvars(r"%LOCALAPPDATA%\EvaGest\barberia.db")
START_DATE  = date(2026, 3, 20)
TODAY       = date(2026, 9, 20)   # past/future boundary
END_DATE    = date(2026, 9, 19)   # last day with completed data
FUTURE_END  = date(2026, 10, 20)  # 1 month of future appointments
SEED = 42

# ---------------------------------------------------------------------------
# VAT helpers  (VatMode = Included; MidpointRounding.AwayFromZero)
# ---------------------------------------------------------------------------
def _round_away(value: float) -> int:
    return int(math.floor(value + 0.5)) if value >= 0 else int(math.ceil(value - 0.5))

def vat_included(total_cents: int, vat_bp: int) -> tuple[int, int, int]:
    """Returns (base_cents, vat_cents, total_cents) for a VAT-included price."""
    base = _round_away(total_cents * 10000 / (10000 + vat_bp))
    return base, total_cents - base, total_cents

def compute_sale_totals(lines: list[tuple]) -> tuple[int, int, int, list[tuple]]:
    """
    lines: [(service_id, product_id, description, qty, unit_price_cents, vat_bp), ...]
    Returns (base_cents, vat_cents, total_cents, breakdown_rows)
    breakdown_rows: [(vat_bp, base_cents, vat_cents, total_cents), ...]
    Groups by vat_bp before rounding (canonical rule).
    """
    by_rate: dict[int, int] = {}
    for (_, _, _, qty, uprice, vbp) in lines:
        by_rate[vbp] = by_rate.get(vbp, 0) + uprice * qty

    total_base = total_vat = total_total = 0
    breakdowns = []
    for vbp, sum_amt in sorted(by_rate.items()):
        base, vat, tot = vat_included(sum_amt, vbp)
        total_base  += base
        total_vat   += vat
        total_total += tot
        breakdowns.append((vbp, base, vat, tot))

    return total_base, total_vat, total_total, breakdowns

# ---------------------------------------------------------------------------
# Catalogue
# ---------------------------------------------------------------------------
# (name, price_cents, vat_bp, duration_min)
SERVICES = [
    ("Tall de cabell", 1500, 2100, 30),
    ("Barba",          1000, 2100, 20),
    ("Rapar",          1200, 2100, 20),
    ("Tint",           2500, 2100, 60),
    ("Afeitar",         800, 2100, 15),
    ("Tall + Barba",   2000, 2100, 45),
]
# weights for random pick (must match order above)
SVC_WEIGHTS = [40, 20, 15, 5, 10, 30]

# (name, price_cents, vat_bp, category)
PRODUCTS = [
    ("Cera cabell",   1200, 2100, "Estilisme"),
    ("Cera forta",    1400, 2100, "Estilisme"),
    ("Shampoo",        900, 2100, "Cabell"),
    ("Condicionador",  800, 2100, "Cabell"),
    ("Oli de barba",  1500, 2100, "Barba"),
    ("Crema d'afeitar", 950, 2100, "Barba"),
    ("Tònic cabell",  1100, 2100, "Cabell"),
]

# ---------------------------------------------------------------------------
# Schedule
# ---------------------------------------------------------------------------
# Weekday indices: 0=Mon … 6=Sun
MARC_DAYS = {0, 1, 2, 3, 4, 5}   # Mon-Sat
JOAN_DAYS = {0, 1, 3, 4}          # Mon-Tue, Thu-Fri (no Wed, no Sat)

WEEKDAY_TAG = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"]

def _day_blocks(wd: int) -> list[tuple[int, int, int, int]]:
    """Returns (start_h, start_m, end_h, end_m) blocks for a given weekday."""
    if wd in {2, 5}:          # Wed, Sat: morning only
        return [(9, 0, 13, 0)]
    return [(9, 0, 13, 0), (16, 0, 20, 0)]

def _open_slots(d: date, worker_days: set) -> list[tuple[int, int]]:
    """15-min time slots the worker is available on date d."""
    wd = d.weekday()
    if wd not in worker_days:
        return []
    slots = []
    for (sh, sm, eh, em) in _day_blocks(wd):
        t = sh * 60 + sm
        end = eh * 60 + em
        while t + 15 <= end:
            slots.append((t // 60, t % 60))
            t += 15
    return slots

# ---------------------------------------------------------------------------
# Client name pools
# ---------------------------------------------------------------------------
FIRST_NAMES = [
    "Marc", "Joan", "Pere", "Josep", "Antoni", "Miquel", "Jordi", "Pau",
    "Arnau", "Oriol", "Sergi", "Albert", "David", "Carlos", "Alejandro",
    "Manuel", "Francisco", "Antonio", "Juan", "Pablo", "Lluís", "Ferran",
    "Xavier", "Ricard", "Gerard", "Roger", "Bernat", "Guillem", "Martí",
    "Andreu", "Carles", "Enric", "Quim", "Santi", "Manel", "Vicenç",
    "Bruno", "Dani", "Edu", "Fran", "Iker", "Javi", "Leo", "Mario",
    "Nacho", "Tomàs", "Ramon", "Isidre", "Benet", "Climent",
]
LAST_NAMES = [
    "García", "Martínez", "López", "Sánchez", "Pérez", "González",
    "Fernández", "Rodríguez", "Torres", "Ramírez", "Puig", "Soler",
    "Vila", "Font", "Mas", "Roca", "Vidal", "Bosch", "Serra", "Costa",
    "Ferrer", "Prat", "Coll", "Marin", "Navarro", "Jiménez", "Moreno",
    "Díaz", "Molina", "Ortega", "Ruiz", "Serrano", "Blanco", "Castro",
    "Romero", "Alonso", "Suárez", "Gutiérrez", "Martín", "Herrera",
    "Medina", "Reyes", "Vargas", "Cruz", "Mora", "Silva", "Ramos",
    "Flores", "Aguilar", "Domínguez",
]

def _make_phone(rng: random.Random) -> str:
    return f"6{rng.randint(10, 99)}{rng.randint(100000, 999999)}"

def _make_client_key(name: str, phone: str) -> str:
    norm = ''.join(
        c for c in unicodedata.normalize('NFD', name.upper())
        if unicodedata.category(c) != 'Mn'
    ).replace(' ', '')
    digits = ''.join(filter(str.isdigit, phone))
    return f"{norm}|{digits[-9:] if len(digits) >= 9 else digits}"

# ---------------------------------------------------------------------------
# Weighted random choice
# ---------------------------------------------------------------------------
def _wchoice(rng: random.Random, items: list, weights: list):
    total = sum(weights)
    r = rng.random() * total
    for item, w in zip(items, weights):
        r -= w
        if r <= 0:
            return item
    return items[-1]

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
def seed(db_path: str) -> None:
    if not os.path.exists(db_path):
        print(f"ERROR: DB not found at {db_path}")
        print("Start the app once to create the database, then run this script.")
        return

    rng = random.Random(SEED)
    conn = sqlite3.connect(db_path)
    conn.execute("PRAGMA foreign_keys = OFF")  # skip FK checks during bulk insert
    cur = conn.cursor()

    # ------------------------------------------------------------------
    # 1. Clear all tables (order matters: children before parents)
    # ------------------------------------------------------------------
    print("Clearing tables …")
    for tbl in [
        "sale_breakdowns", "sale_lines", "sales", "appointments",
        "cash_movements", "worker_schedules", "shop_schedule",
        "clients", "workers", "services", "products",
        "payment_methods", "expense_categories", "closed_days", "settings",
    ]:
        cur.execute(f"DELETE FROM {tbl}")
    # Reset autoincrement counters so IDs start at 1 on each run
    cur.execute("DELETE FROM sqlite_sequence")
    conn.commit()

    # ------------------------------------------------------------------
    # 2. Settings
    # ------------------------------------------------------------------
    cur.executemany("INSERT INTO settings (key, value) VALUES (?,?)", [
        ("shop_name",                      "Barberia El Racó"),
        ("shop_address",                   "Carrer Major, 12"),
        ("shop_phone",                     "931234567"),
        ("default_vat_bp",                 "2100"),
        ("vat_mode",                       "Included"),
        ("apply_vat_to_till",              "0"),
        ("default_appointment_duration_min", "30"),
        ("backup_time",                    "20:00"),
        ("backups_to_keep",                "15"),
        ("last_automatic_backup",          ""),
        ("show_guest_notice",              "1"),
        ("confirmation_sound",             "1"),
        ("language",                       "Catalan"),
        ("agenda_slot_minutes",            "15"),
    ])

    # ------------------------------------------------------------------
    # 3. Payment methods
    # ------------------------------------------------------------------
    cur.execute("INSERT INTO payment_methods (name, active) VALUES ('Efectiu', 1)")
    pm_cash = cur.lastrowid
    cur.execute("INSERT INTO payment_methods (name, active) VALUES ('Targeta', 1)")
    pm_card = cur.lastrowid
    cur.execute("INSERT INTO payment_methods (name, active) VALUES ('Bizum', 1)")
    pm_bizum = cur.lastrowid
    pm_list   = [pm_cash, pm_card, pm_bizum]
    pm_weights = [50, 35, 15]

    # ------------------------------------------------------------------
    # 4. Expense categories
    # ------------------------------------------------------------------
    cur.execute("INSERT INTO expense_categories (name, active) VALUES ('Salaris', 1)")
    cat_salary = cur.lastrowid
    cur.execute("INSERT INTO expense_categories (name, active) VALUES ('Assessoria', 1)")
    cat_assess = cur.lastrowid
    cur.execute("INSERT INTO expense_categories (name, active) VALUES ('Material', 1)")
    cat_material = cur.lastrowid
    cur.execute("INSERT INTO expense_categories (name, active) VALUES ('Subministraments', 1)")
    _cat_subs = cur.lastrowid  # noqa: unused but inserted for completeness

    # ------------------------------------------------------------------
    # 5. Workers
    # ------------------------------------------------------------------
    cur.execute("INSERT INTO workers (name, active, color) VALUES ('Marc', 1, '#0F766E')")
    marc_id = cur.lastrowid
    cur.execute("INSERT INTO workers (name, active, color) VALUES ('Joan', 1, '#7C3AED')")
    joan_id = cur.lastrowid

    # ------------------------------------------------------------------
    # 6. Shop schedule (overall range per open day; lunch break visible via worker schedules)
    # ------------------------------------------------------------------
    cur.executemany(
        "INSERT INTO shop_schedule (weekday, opening_time, closing_time) VALUES (?,?,?)",
        [
            ("Mon", "09:00:00", "20:00:00"),
            ("Tue", "09:00:00", "20:00:00"),
            ("Wed", "09:00:00", "13:00:00"),
            ("Thu", "09:00:00", "20:00:00"),
            ("Fri", "09:00:00", "20:00:00"),
            ("Sat", "09:00:00", "13:00:00"),
        ],
    )

    # ------------------------------------------------------------------
    # 7. Worker schedules  (split rows = lunch break)
    # ------------------------------------------------------------------
    ws_rows = []
    for wd_idx in MARC_DAYS:
        for (sh, sm, eh, em) in _day_blocks(wd_idx):
            ws_rows.append((marc_id, WEEKDAY_TAG[wd_idx],
                            f"{sh:02d}:{sm:02d}:00", f"{eh:02d}:{em:02d}:00"))
    for wd_idx in JOAN_DAYS:
        for (sh, sm, eh, em) in _day_blocks(wd_idx):
            ws_rows.append((joan_id, WEEKDAY_TAG[wd_idx],
                            f"{sh:02d}:{sm:02d}:00", f"{eh:02d}:{em:02d}:00"))
    cur.executemany(
        "INSERT INTO worker_schedules (worker_id, weekday, start_time, end_time) VALUES (?,?,?,?)",
        ws_rows,
    )

    # ------------------------------------------------------------------
    # 8. Services & products
    # ------------------------------------------------------------------
    svc_ids: dict[str, int] = {}
    for (name, price, vat_bp, dur) in SERVICES:
        cur.execute(
            "INSERT INTO services (name, price_cents, vat_bp, duration_min, active) VALUES (?,?,?,?,1)",
            (name, price, vat_bp, dur),
        )
        svc_ids[name] = cur.lastrowid

    prod_ids: dict[str, int] = {}
    for (name, price, vat_bp, cat) in PRODUCTS:
        cur.execute(
            "INSERT INTO products (name, price_cents, vat_bp, category, active) VALUES (?,?,?,?,1)",
            (name, price, vat_bp, cat),
        )
        prod_ids[name] = cur.lastrowid

    conn.commit()

    # ------------------------------------------------------------------
    # 9. Clients  (100 with different visit patterns)
    # ------------------------------------------------------------------
    # pattern: int weeks between visits, or None = sporadic
    patterns = ([2] * 20 + [3] * 30 + [4] * 20 + [None] * 30)
    rng.shuffle(patterns)

    fn_pool = FIRST_NAMES * 3;  rng.shuffle(fn_pool)
    ln_pool = LAST_NAMES * 3;   rng.shuffle(ln_pool)

    client_ids: list[int]    = []
    client_patterns: list    = []
    used_combos: set[str]    = set()

    for i in range(100):
        fn = fn_pool[i % len(fn_pool)]
        ln = ln_pool[i % len(ln_pool)]
        combo = fn + ln
        if combo in used_combos:
            ln = ln_pool[(i + 51) % len(ln_pool)]
            combo = fn + ln
        used_combos.add(combo)

        name  = f"{fn} {ln}"
        phone = _make_phone(rng)
        key   = _make_client_key(name, phone)
        asleep = 1 if rng.random() < 0.04 else 0
        cur.execute(
            "INSERT INTO clients (client_key, name, mobile, asleep) VALUES (?,?,?,?)",
            (key, name, phone, asleep),
        )
        client_ids.append(cur.lastrowid)
        client_patterns.append(patterns[i])

    conn.commit()

    # ------------------------------------------------------------------
    # Helpers
    # ------------------------------------------------------------------
    svc_by_name = {n: (p, v, d) for (n, p, v, d) in SERVICES}
    prod_by_name = {n: (p, v, c) for (n, p, v, c) in PRODUCTS}
    prod_names = list(prod_ids.keys())

    def _pick_svc() -> str:
        return _wchoice(rng, [s[0] for s in SERVICES], SVC_WEIGHTS)

    def _pick_pm() -> int:
        return _wchoice(rng, pm_list, pm_weights)

    def _visit_dates(pattern, start: date, end: date) -> list[date]:
        out = []
        if pattern is None:
            n = rng.randint(1, 3)
            for _ in range(n):
                delta = rng.randint(0, (end - start).days)
                d = start + timedelta(days=delta)
                # advance to an open day
                while d.weekday() > 5:
                    d += timedelta(days=1)
                if d <= end:
                    out.append(d)
        else:
            d = start + timedelta(days=rng.randint(0, pattern * 7 - 1))
            while d.weekday() > 5:
                d += timedelta(days=1)
            while d <= end:
                out.append(d)
                jitter = rng.randint(-2, 2)
                d = d + timedelta(weeks=pattern, days=jitter)
                while d.weekday() > 5:
                    d += timedelta(days=1)
        return sorted(out)

    # Slot occupancy: (date, worker_id, hour, minute) → True
    used: set[tuple] = set()

    def _find_slot(d: date, duration_min: int, wid: int) -> tuple[int, int] | None:
        wdays = MARC_DAYS if wid == marc_id else JOAN_DAYS
        slots = _open_slots(d, wdays)
        if not slots:
            return None
        slots_set = set(slots)
        need = math.ceil(duration_min / 15)
        rng.shuffle(slots)
        for (h, m) in slots:
            t0 = h * 60 + m
            ok = True
            for b in range(need):
                bh = (t0 + b * 15) // 60
                bm = (t0 + b * 15) % 60
                if (d, wid, bh, bm) in used or (bh, bm) not in slots_set:
                    ok = False
                    break
            if ok:
                return (h, m)
        return None

    def _mark(d: date, wid: int, h: int, m: int, duration_min: int) -> None:
        t0 = h * 60 + m
        for b in range(math.ceil(duration_min / 15)):
            bh = (t0 + b * 15) // 60
            bm = (t0 + b * 15) % 60
            used.add((d, wid, bh, bm))

    # Batch accumulators
    appt_rows:  list[tuple] = []
    sale_rows:  list[tuple] = []
    line_rows:  list[tuple] = []
    bdown_rows: list[tuple] = []

    appt_id = 0
    sale_id = 0

    def _make_sale(d: date, h: int, m: int, wid: int,
                   cli_id: int | None, guest_name: str | None,
                   guest_phone: str | None, a_id: int | None) -> None:
        nonlocal sale_id
        sale_id += 1
        sid = sale_id
        d_str = d.isoformat()
        t_str = f"{h:02d}:{m:02d}:00"
        pm = _pick_pm()

        svc_name = _pick_svc()
        svc_price, svc_vat, _ = svc_by_name[svc_name]
        lines: list[tuple] = [
            (svc_ids.get(svc_name), None, svc_name, 1, svc_price, svc_vat),
        ]
        # ~28% chance add a product
        if rng.random() < 0.28:
            pn = rng.choice(prod_names)
            pp, pv, _ = prod_by_name[pn]
            lines.append((None, prod_ids[pn], pn, 1, pp, pv))

        base, vat, tot, bds = compute_sale_totals(lines)

        sale_rows.append((
            sid, d_str, t_str,
            cli_id, guest_name, guest_phone,
            a_id, wid, pm,
            base, vat, tot, "Included", "Active", None,
        ))
        for (s_id, p_id, desc, qty, uprice, vbp) in lines:
            line_rows.append((sid, s_id, p_id, desc, qty, uprice, vbp, uprice * qty))
        for (vbp, b, v, tt) in bds:
            bdown_rows.append((sid, vbp, b, v, tt))

    # ------------------------------------------------------------------
    # 10. Appointments (from client visit patterns)
    # ------------------------------------------------------------------
    print("Generating appointments …")

    for idx, cid in enumerate(client_ids):
        # Past visits up to END_DATE + future bookings up to FUTURE_END
        for vd in _visit_dates(client_patterns[idx], START_DATE, FUTURE_END):
            wd = vd.weekday()
            marc_ok = wd in MARC_DAYS
            joan_ok = wd in JOAN_DAYS

            if marc_ok and joan_ok:
                wid = rng.choices([marc_id, joan_id], weights=[65, 35])[0]
            elif marc_ok:
                wid = marc_id
            elif joan_ok:
                wid = joan_id
            else:
                continue

            svc_name = _pick_svc()
            _, _, dur = svc_by_name[svc_name]
            slot = _find_slot(vd, dur, wid)
            if slot is None:
                # try the other worker
                alt = joan_id if wid == marc_id else marc_id
                alt_ok = wd in (MARC_DAYS if alt == marc_id else JOAN_DAYS)
                if alt_ok:
                    slot = _find_slot(vd, dur, alt)
                    if slot:
                        wid = alt
            if slot is None:
                continue

            _mark(vd, wid, slot[0], slot[1], dur)

            appt_id += 1
            aid = appt_id
            t_str = f"{slot[0]:02d}:{slot[1]:02d}:00"
            d_str = vd.isoformat()

            is_future = vd >= TODAY

            if is_future:
                status = "Pending"
            else:
                r = rng.random()
                status = "Completed" if r < 0.85 else ("Cancelled" if r < 0.95 else "NoShow")

            appt_rows.append((aid, d_str, t_str, dur, cid, None, None,
                               svc_ids.get(svc_name), wid, status, None))

            if status == "Completed":
                sale_id += 1
                sid = sale_id
                pm = _pick_pm()
                svc_price, svc_vat, _ = svc_by_name[svc_name]
                lines: list[tuple] = [
                    (svc_ids[svc_name], None, svc_name, 1, svc_price, svc_vat),
                ]
                if rng.random() < 0.28:
                    pn = rng.choice(prod_names)
                    pp, pv, _ = prod_by_name[pn]
                    lines.append((None, prod_ids[pn], pn, 1, pp, pv))

                base, vat, tot, bds = compute_sale_totals(lines)
                sale_rows.append((
                    sid, d_str, t_str,
                    cid, None, None,
                    aid, wid, pm,
                    base, vat, tot, "Included", "Active", None,
                ))
                for (s_id, p_id, desc, qty, uprice, vbp) in lines:
                    line_rows.append((sid, s_id, p_id, desc, qty, uprice, vbp, uprice * qty))
                for (vbp, b, v, tt) in bds:
                    bdown_rows.append((sid, vbp, b, v, tt))

    # ------------------------------------------------------------------
    # 11. Walk-ins / fillers — enforces daily minimums and busy-day targets
    # ------------------------------------------------------------------
    print("Generating walk-ins and enforcing daily minimums …")

    # Pre-select 3 busy days per week (Mon-Sat range, within START_DATE..FUTURE_END).
    busy_days: set[date] = set()
    week_mon = START_DATE - timedelta(days=START_DATE.weekday())
    while week_mon <= FUTURE_END:
        week_open = sorted(
            week_mon + timedelta(days=i)
            for i in range(6)
            if START_DATE <= week_mon + timedelta(days=i) <= FUTURE_END
            and (week_mon + timedelta(days=i)).weekday() <= 5
        )
        if week_open:
            for bd in rng.sample(week_open, min(3, len(week_open))):
                busy_days.add(bd)
        week_mon += timedelta(weeks=1)

    # Count active/pending clients already booked per day (non-cancelled appointments).
    day_count: dict[str, int] = {}
    for row in appt_rows:
        # row: (aid, d_str, t_str, dur, cid, gname, gph, svc_id, wid, status, notes)
        if row[9] != "Cancelled":
            day_count[row[1]] = day_count.get(row[1], 0) + 1

    def _add_walkin_sale(d: date, wid: int, slot: tuple[int, int], svc_name: str) -> None:
        nonlocal sale_id
        svc_price, svc_vat, _ = svc_by_name[svc_name]
        if rng.random() < 0.40:
            cli_id = rng.choice(client_ids); gname = None; gphone = None
        else:
            cli_id = None
            gname  = rng.choice(FIRST_NAMES) + " " + rng.choice(LAST_NAMES)
            gphone = _make_phone(rng)
        lines: list[tuple] = [(svc_ids[svc_name], None, svc_name, 1, svc_price, svc_vat)]
        if rng.random() < 0.25:
            pn = rng.choice(prod_names); pp, pv, _ = prod_by_name[pn]
            lines.append((None, prod_ids[pn], pn, 1, pp, pv))
        sale_id += 1; sid = sale_id
        d_str = d.isoformat(); t_str = f"{slot[0]:02d}:{slot[1]:02d}:00"
        base, vat, tot, bds = compute_sale_totals(lines)
        sale_rows.append((sid, d_str, t_str, cli_id, gname, gphone,
                          None, wid, _pick_pm(),
                          base, vat, tot, "Included", "Active", None))
        for (s_id, p_id, desc, qty, uprice, vbp) in lines:
            line_rows.append((sid, s_id, p_id, desc, qty, uprice, vbp, uprice * qty))
        for (vbp, b, v, tt) in bds:
            bdown_rows.append((sid, vbp, b, v, tt))

    def _add_pending_appt(d: date, wid: int, slot: tuple[int, int],
                          dur: int, svc_name: str) -> None:
        nonlocal appt_id
        appt_id += 1
        d_str = d.isoformat(); t_str = f"{slot[0]:02d}:{slot[1]:02d}:00"
        if rng.random() < 0.60:
            cid = rng.choice(client_ids)
            appt_rows.append((appt_id, d_str, t_str, dur, cid, None, None,
                               svc_ids.get(svc_name), wid, "Pending", None))
        else:
            gname  = rng.choice(FIRST_NAMES) + " " + rng.choice(LAST_NAMES)
            gphone = _make_phone(rng)
            appt_rows.append((appt_id, d_str, t_str, dur, None, gname, gphone,
                               svc_ids.get(svc_name), wid, "Pending", None))

    d = START_DATE
    while d <= FUTURE_END:
        wd = d.weekday()
        if wd > 5:
            d += timedelta(days=1)
            continue

        is_future      = d >= TODAY
        morning_only   = wd in {2, 5}
        marc_on        = wd in MARC_DAYS
        joan_on        = wd in JOAN_DAYS
        n_workers      = int(marc_on) + int(joan_on)
        d_str = d.isoformat()

        # Targets based on realistic capacity (avg service ~31 min):
        #   full day per worker  = 480 min / 31 ≈ 15 clients
        #   morning per worker   = 240 min / 31 ≈  7 clients
        # Busy days: ~80 % of capacity.  Normal days: ~55 % — still busy, not empty.
        if morning_only:
            target = rng.randint(5, 7) if d in busy_days else rng.randint(3, 5)
        else:
            # n_workers: 2 on Mon/Tue/Thu/Fri, so capacity ~30
            if d in busy_days:
                target = rng.randint(14, 18) * n_workers // 2
            else:
                target = rng.randint(8, 12)  * n_workers // 2

        current = day_count.get(d_str, 0)
        to_add  = max(0, target - current)

        for _ in range(to_add):
            if marc_on and joan_on:
                wid = rng.choices([marc_id, joan_id], weights=[65, 35])[0]
            elif marc_on:
                wid = marc_id
            else:
                wid = joan_id

            svc_name = _pick_svc()
            _, _, dur = svc_by_name[svc_name]
            slot = _find_slot(d, dur, wid)
            if slot is None:
                alt    = joan_id if wid == marc_id else marc_id
                alt_ok = wd in (MARC_DAYS if alt == marc_id else JOAN_DAYS)
                if alt_ok:
                    slot = _find_slot(d, dur, alt)
                    if slot:
                        wid = alt
            if slot is None:
                break   # day is full

            _mark(d, wid, slot[0], slot[1], dur)
            day_count[d_str] = day_count.get(d_str, 0) + 1

            if is_future:
                _add_pending_appt(d, wid, slot, dur, svc_name)
            else:
                _add_walkin_sale(d, wid, slot, svc_name)

        d += timedelta(days=1)

    # ------------------------------------------------------------------
    # 12. Bulk insert appointments + sales
    # ------------------------------------------------------------------
    print(f"Inserting {len(appt_rows)} appointments …")
    cur.executemany(
        """INSERT INTO appointments
           (id, date, time, duration_min, client_id, guest_name, guest_phone,
            service_id, worker_id, status, notes)
           VALUES (?,?,?,?,?,?,?,?,?,?,?)""",
        appt_rows,
    )

    print(f"Inserting {len(sale_rows)} sales, {len(line_rows)} lines …")
    cur.executemany(
        """INSERT INTO sales
           (id, date, time, client_id, guest_name, guest_phone,
            appointment_id, worker_id, payment_method_id,
            base_cents, vat_cents, total_cents, vat_mode, status, notes)
           VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)""",
        sale_rows,
    )
    cur.executemany(
        """INSERT INTO sale_lines
           (sale_id, service_id, product_id, description, quantity,
            unit_price_cents, vat_bp, amount_cents)
           VALUES (?,?,?,?,?,?,?,?)""",
        line_rows,
    )
    cur.executemany(
        """INSERT INTO sale_breakdowns (sale_id, vat_bp, base_cents, vat_cents, total_cents)
           VALUES (?,?,?,?,?)""",
        bdown_rows,
    )
    conn.commit()

    # ------------------------------------------------------------------
    # 13. Cash movements: salaries + assessoria + occasional material
    # ------------------------------------------------------------------
    print("Generating cash movements …")
    mov_rows: list[tuple] = []

    # Monthly fixed costs: paid on first open day of each month
    months_done: set[tuple[int, int]] = set()
    d = START_DATE
    while d <= END_DATE:
        ym = (d.year, d.month)
        if ym not in months_done and d.weekday() <= 5:
            months_done.add(ym)
            ds = d.isoformat()
            # Marc 1 800 €/month = 180 000 cents
            mov_rows.append((ds, "Out", 180_000, None, None, None,
                              pm_cash, cat_salary, marc_id, "Salari Marc"))
            # Joan 1 500 €/month = 150 000 cents
            mov_rows.append((ds, "Out", 150_000, None, None, None,
                              pm_card, cat_salary, joan_id, "Salari Joan"))
            # Tax consultory 120 €/month = 12 000 cents
            mov_rows.append((ds, "Out", 12_000, None, None, None,
                              pm_card, cat_assess, None, "Assessoria fiscal"))
        d += timedelta(days=1)

    # Occasional material purchases (~2/month)
    material_concepts = [
        "Tints cabell", "Productes barba", "Tovalloles desechables",
        "Material neteja", "Consumibles barberia", "Cremes i gels",
    ]
    d = START_DATE
    while d <= END_DATE:
        if d.weekday() <= 5 and rng.random() < 0.033:
            amount = rng.randint(2_000, 9_000)  # 20-90 €
            mov_rows.append((
                d.isoformat(), "Out", amount, None, None, None,
                _pick_pm(), cat_material, None, rng.choice(material_concepts),
            ))
        d += timedelta(days=1)

    cur.executemany(
        """INSERT INTO cash_movements
           (date, type, amount_cents, base_cents, vat_cents, vat_bp,
            payment_method_id, category_id, worker_id, concept)
           VALUES (?,?,?,?,?,?,?,?,?,?)""",
        mov_rows,
    )
    conn.commit()
    conn.execute("PRAGMA foreign_keys = ON")
    conn.close()

    # ------------------------------------------------------------------
    # Summary
    # ------------------------------------------------------------------
    rev = sum(r[11] for r in sale_rows if r[13] == "Active")
    print()
    print("Done!")
    print(f"  Appointments : {len(appt_rows):>5}")
    print(f"  Sales        : {len(sale_rows):>5}  (revenue {rev/100:,.2f} €)")
    print(f"  Sale lines   : {len(line_rows):>5}")
    print(f"  Cash movements: {len(mov_rows):>4}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Seed EvaGest DB with demo data")
    parser.add_argument("--db-path", default=DEFAULT_DB, help="Path to barberia.db")
    args = parser.parse_args()
    seed(args.db_path)
