"""Éclate un fichier qui regroupe plusieurs cas d'usage en un fichier par cas d'usage.

    python scripts/split-usecases.py <fichier.cs> [...]

Un cas d'usage commence à un commentaire `/// <summary>` (ou à `public sealed
record`) suivi d'une déclaration `public sealed record <Nom>(Query|Command)` ;
il s'étend jusqu'au suivant. Chaque fichier reprend les `using` et le
`namespace` d'origine, et porte le nom de la requête sans suffixe.
"""
import os
import re
import sys

RECORD = re.compile(r"^public sealed record (\w+?)(Query|Command)\b")

for path in sys.argv[1:]:
    lines = open(path, encoding="utf8").read().split("\n")
    ns = next(i for i, l in enumerate(lines) if l.startswith("namespace "))
    header = lines[: ns + 1]
    body = lines[ns + 1 :]
    starts = []
    for i, line in enumerate(body):
        if RECORD.match(line):
            start = i
            while start > 0 and body[start - 1].startswith("///"):
                start -= 1
            starts.append((start, RECORD.match(line).group(1)))
    if len(starts) < 2:
        continue
    folder = os.path.dirname(path)
    preamble = body[: starts[0][0]]
    if any(l.strip() for l in preamble):
        raise SystemExit(f"{path}: des types précèdent le premier cas d'usage, déplacez-les dans leur propre fichier d'abord")
    for index, (start, name) in enumerate(starts):
        end = starts[index + 1][0] if index + 1 < len(starts) else len(body)
        chunk = body[start:end]
        while chunk and chunk[-1].strip() == "":
            chunk.pop()
        content = "\n".join(header + [""] + chunk) + "\n"
        open(os.path.join(folder, f"{name}.cs"), "w", encoding="utf8").write(content)
    if os.path.basename(path)[:-3] not in [n for _, n in starts]:
        os.remove(path)
    print(f"{path}: {len(starts)} cas d'usage")
