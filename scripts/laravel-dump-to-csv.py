#!/usr/bin/env python3
"""Extrait les tables d'un dump mysqldump (Wimo Laravel, MariaDB) en CSV, sans serveur MySQL.

    python -I scripts/laravel-dump-to-csv.py <dump.sql[.gz]> <dossier-sortie> [table ...]

Lit les `CREATE TABLE` (noms de colonnes) et les `INSERT INTO ... VALUES (...),(...);` (format étendu de
mysqldump). NULL devient une cellule vide marquée `\\N` ; les échappements MySQL (\\' \\" \\\\ \\n \\r \\t \\0 \\Z)
sont décodés. Rien n'est envoyé ailleurs : la sortie est un dossier local de CSV (données réelles, hors dépôt).
"""
import csv
import gzip
import os
import re
import sys

NULL = "\\N"
ESCAPES = {"0": "\0", "n": "\n", "r": "\r", "t": "\t", "Z": "\x1a", "b": "\b"}


def rows(values: str):
    """Les tuples d'une liste `(...),(...)` de mysqldump."""
    i, n = 0, len(values)
    while i < n:
        while i < n and values[i] != "(":
            i += 1
        if i >= n:
            return
        i += 1
        row, cell, quoted, raw = [], [], False, True
        while i < n:
            ch = values[i]
            if quoted:
                if ch == "\\":
                    nxt = values[i + 1]
                    cell.append(ESCAPES.get(nxt, nxt))
                    i += 2
                    continue
                if ch == "'":
                    if i + 1 < n and values[i + 1] == "'":
                        cell.append("'")
                        i += 2
                        continue
                    quoted = False
                    i += 1
                    continue
                cell.append(ch)
                i += 1
                continue
            if ch == "'":
                quoted, raw = True, False
                i += 1
                continue
            if ch in ",)":
                text = "".join(cell).strip() if raw else "".join(cell)
                row.append(NULL if raw and text.upper() == "NULL" else text)
                cell, raw = [], True
                i += 1
                if ch == ")":
                    yield row
                    break
                continue
            cell.append(ch)
            i += 1


def main() -> None:
    source, out = sys.argv[1], sys.argv[2]
    wanted = set(sys.argv[3:])
    os.makedirs(out, exist_ok=True)
    opener = gzip.open if source.endswith(".gz") else open
    columns: dict[str, list[str]] = {}
    current = None
    writers: dict[str, tuple] = {}
    counts: dict[str, int] = {}
    with opener(source, "rt", encoding="utf-8", errors="strict") as dump:
        for line in dump:
            m = re.match(r"CREATE TABLE `([^`]+)`", line)
            if m:
                current = m.group(1)
                columns[current] = []
                continue
            if current and line.startswith("  `"):
                columns[current].append(line.split("`")[1])
                continue
            if current and line.startswith(")"):
                current = None
                continue
            m = re.match(r"INSERT INTO `([^`]+)` (?:\(([^)]*)\) )?VALUES ", line)
            if not m:
                continue
            table = m.group(1)
            if wanted and table not in wanted:
                continue
            if table not in writers:
                handle = open(os.path.join(out, f"{table}.csv"), "w", encoding="utf-8", newline="")
                writer = csv.writer(handle)
                head = [c.strip(" `") for c in m.group(2).split(",")] if m.group(2) else columns[table]
                writer.writerow(head)
                writers[table] = (handle, writer, len(head))
                counts[table] = 0
            handle, writer, width = writers[table]
            for row in rows(line[m.end():].rstrip().rstrip(";")):
                if len(row) != width:
                    raise SystemExit(f"{table} : {len(row)} valeurs pour {width} colonnes")
                writer.writerow(row)
                counts[table] += 1
    for handle, _, _ in writers.values():
        handle.close()
    for table in sorted(columns):
        if not wanted or table in wanted:
            print(f"{table}\t{counts.get(table, 0)}")


if __name__ == "__main__":
    main()
