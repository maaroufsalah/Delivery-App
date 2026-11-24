"""Éclate un fichier de cas d'usage selon la règle du projet.

    python scripts/split-module.py <fichier.cs> [...]

- `XQuery` / `XCommand` + `XValidator` + `XHandler` -> `Queries/X.cs` ou `Commands/X.cs` ;
- les records `*Dto` -> `Dtos/<Fichier>Dtos.cs` ;
- tout autre type (aide statique, base abstraite et son validateur générique) -> `<Type>.cs` à la racine du module.

Chaque fichier reprend les `using` et le `namespace` d'origine. Le fichier source est supprimé.
"""
import os
import re
import sys

DECL = re.compile(r"^(?:public|internal) (?:sealed |abstract |static |partial )*(?:record|class) (\w+)")


def blocks(body):
    """Les déclarations de premier niveau, avec leurs commentaires `///` et attributs."""
    starts = []
    for i, line in enumerate(body):
        match = DECL.match(line)
        if not match:
            continue
        start = i
        while start > 0 and (body[start - 1].startswith("///") or body[start - 1].startswith("[")):
            start -= 1
        starts.append((start, match.group(1)))
    result = []
    for index, (start, name) in enumerate(starts):
        end = starts[index + 1][0] if index + 1 < len(starts) else len(body)
        chunk = body[start:end]
        while chunk and chunk[-1].strip() == "":
            chunk.pop()
        result.append((name, chunk))
    return result, (starts[0][0] if starts else len(body))


for path in sys.argv[1:]:
    text = open(path, encoding="utf8").read()
    lines = text.split("\n")
    ns = next(i for i, l in enumerate(lines) if l.startswith("namespace "))
    header = lines[: ns + 1]
    body = lines[ns + 1:]
    found, first = blocks(body)
    if any(l.strip() for l in body[:first]):
        raise SystemExit(f"{path}: du texte précède la première déclaration")
    folder = os.path.dirname(path)
    source = os.path.splitext(os.path.basename(path))[0]
    usecases = {}
    for name, _ in found:
        for suffix, kind in (("Query", "Queries"), ("Command", "Commands")):
            if name.endswith(suffix) and name != suffix:
                usecases[name[: -len(suffix)]] = kind
    files = {}

    def add(target, chunk):
        files.setdefault(target, []).append(chunk)

    others = [name for name, _ in found if not name.endswith("Dto") and not any(name == k + s for k in usecases for s in ("Query", "Command"))]
    for name, chunk in found:
        key = None
        for suffix in ("Query", "Command", "Validator", "Handler"):
            if name.endswith(suffix) and name[: -len(suffix)] in usecases:
                key = name[: -len(suffix)]
                break
        if key is not None:
            add(os.path.join(folder, usecases[key], f"{key}.cs"), chunk)
        elif name.endswith("Dto"):
            add(os.path.join(folder, "Dtos", f"{source}Dtos.cs"), chunk)
        else:
            # Un validateur générique rejoint son type de base (`XValidator` -> `X…`).
            owner = name
            if name.endswith("Validator"):
                base = name[: -len("Validator")]
                owner = next((o for o in others if o != name and o.startswith(base)), name)
            add(os.path.join(folder, f"{owner}.cs"), chunk)

    for target, chunks in files.items():
        os.makedirs(os.path.dirname(target), exist_ok=True)
        if os.path.exists(target) and os.path.abspath(target) != os.path.abspath(path):
            raise SystemExit(f"{target} existe déjà")
        content = "\n".join(header + [""] + "\n\n".join("\n".join(c) for c in chunks).split("\n")) + "\n"
        open(target, "w", encoding="utf8").write(content)
    if os.path.abspath(path) not in {os.path.abspath(t) for t in files}:
        os.remove(path)
    print(path, "->", len(files), "fichiers")
