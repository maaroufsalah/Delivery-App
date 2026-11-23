"""Porte schema.prisma (Speedy) en entités EF Core + configurations.

Usage : python scripts/prisma-to-ef.py <schema.prisma> <dossier api>

Mono-société : tout champ `companyId` et toute relation vers `Company` sont
retirés ; les index et clés qui les contenaient perdent cette colonne.
Les noms de tables (@@map) et de colonnes (noms Prisma) sont conservés.
Outil de portage ponctuel : la sortie est ensuite maintenue à la main.
"""
import os
import re
import sys

schema_path, api_dir = sys.argv[1], sys.argv[2]
text = open(schema_path, encoding="utf8").read()
text = re.sub(r"//[^\n]*", "", text)

blocks = re.findall(r"^(model|enum)\s+(\w+)\s*\{(.*?)^\}", text, re.S | re.M)
enums = {}
models = {}
for kind, name, body in blocks:
    lines = [l.strip() for l in body.split("\n") if l.strip()]
    if kind == "enum":
        enums[name] = [l.split()[0] for l in lines if not l.startswith("@@")]
    else:
        models[name] = lines

TENANT = "companyId"
SCALARS = {"String": "string", "Int": "int", "Boolean": "bool", "DateTime": "DateTime",
           "Decimal": "decimal", "Json": "string", "Float": "double", "BigInt": "long", "Bytes": "byte[]"}


def pascal(s):
    return s[0].upper() + s[1:]


def parse_list(s):
    return [x.strip() for x in s.split(",") if x.strip()]


class Field:
    pass


parsed = {}
for mname, lines in models.items():
    fields, block_attrs = [], []
    for l in lines:
        if l.startswith("@@"):
            block_attrs.append(l)
            continue
        m = re.match(r"(\w+)\s+(\w+)(\[\])?(\?)?\s*(.*)$", l)
        f = Field()
        f.name, f.type, f.list, f.opt, f.attrs = m.group(1), m.group(2), bool(m.group(3)), bool(m.group(4)), m.group(5)
        f.is_relation = f.type in models
        f.is_enum = f.type in enums
        rel = re.search(r"@relation\(([^)]*)\)", f.attrs)
        f.rel_name = None
        f.rel_fields = f.rel_refs = None
        f.on_delete = None
        if rel:
            r = rel.group(1)
            nm = re.match(r'\s*"([^"]+)"', r) or re.search(r'name:\s*"([^"]+)"', r)
            f.rel_name = nm.group(1) if nm else None
            fm = re.search(r"fields:\s*\[([^\]]*)\]", r)
            rm = re.search(r"references:\s*\[([^\]]*)\]", r)
            if fm:
                f.rel_fields, f.rel_refs = parse_list(fm.group(1)), parse_list(rm.group(1))
            od = re.search(r"onDelete:\s*(\w+)", r)
            f.on_delete = od.group(1) if od else None
        fields.append(f)
    parsed[mname] = (fields, block_attrs)

# --- retrait du tenant -------------------------------------------------------
for mname, (fields, battrs) in parsed.items():
    keep = []
    for f in fields:
        if f.name == TENANT and mname != "Company":
            continue
        if f.is_relation and f.type == "Company":
            continue
        if mname == "Company" and f.is_relation:
            continue
        if f.rel_fields and TENANT in f.rel_fields:
            pairs = [(a, b) for a, b in zip(f.rel_fields, f.rel_refs) if a != TENANT]
            f.rel_fields, f.rel_refs = [p[0] for p in pairs], [p[1] for p in pairs]
        keep.append(f)
    nb = []
    for a in battrs:
        m = re.match(r"@@(unique|index|id)\(\[([^\]]*)\](.*)\)", a)
        if m:
            cols = [c for c in parse_list(m.group(2)) if c.split("(")[0] != TENANT]
            if not cols:
                continue
            nb.append((m.group(1), cols, m.group(3)))
        else:
            mm = re.match(r'@@map\("([^"]+)"\)', a)
            if mm:
                nb.append(("map", mm.group(1), ""))
    parsed[mname] = (keep, nb)

# --- C# ---------------------------------------------------------------------
dom = os.path.join(api_dir, "src/Delivery.Domain")
inf = os.path.join(api_dir, "src/Delivery.Infrastructure/Persistence/Configurations")
app = os.path.join(api_dir, "src/Delivery.Application/Common/Interfaces")
for d in (os.path.join(dom, "Entities"), os.path.join(dom, "Enums"), inf, app):
    os.makedirs(d, exist_ok=True)

for ename, values in enums.items():
    body = ",\n".join(f"    {v}" for v in values)
    open(os.path.join(dom, "Enums", f"{ename}.cs"), "w", encoding="utf8").write(
        f"namespace Delivery.Domain.Enums;\n\npublic enum {ename}\n{{\n{body},\n}}\n")


def prop_name(mname, fname):
    p = pascal(fname)
    return p + "Value" if p == mname else p


def cs_type(f):
    if f.is_enum:
        t = f.type
    elif f.type in SCALARS:
        t = SCALARS[f.type]
    else:
        t = f.type
    if f.type == "DateTime" and "@db.Date" in f.attrs:
        t = "DateOnly"
    if f.list:
        return f"List<{t}>"
    return t + ("?" if f.opt else "")


def default_init(mname, f):
    d = re.search(r"@default\(((?:[^()]|\([^()]*\))*)\)", f.attrs)
    if f.list and not f.is_relation:
        return " = [];"
    if not d:
        if f.is_relation:
            return " = null!;" if not f.opt else ""
        if cs_type(f) == "string":
            return " = string.Empty;"
        return ""
    v = d.group(1).strip()
    if v.startswith("cuid"):
        return " = Cuid.New();"
    if v.startswith("now"):
        return ""
    if v.startswith('"'):
        return f" = {v};"
    if f.is_enum:
        return f" = {f.type}.{v};"
    if f.type == "Decimal":
        return f" = {v}m;"
    if f.type == "Float":
        return f" = {v};"
    return f" = {v};"


# relations : trouver la contre-partie
def counterpart(mname, f):
    other_fields = parsed[f.type][0]
    cands = [g for g in other_fields if g.is_relation and g.type == mname and g.rel_name == f.rel_name and g is not f]
    if mname == f.type:  # auto-relation : celle sans fields
        cands = [g for g in cands if not g.rel_fields]
    return cands[0] if cands else None


dbsets = []
for mname, (fields, battrs) in parsed.items():
    lines = []
    for f in fields:
        lines.append(f"    public {cs_type(f) if not (f.is_relation and f.list) else 'ICollection<' + f.type + '>'} {prop_name(mname, f.name)} {{ get; set; }}"
                     + (" = [];" if f.is_relation and f.list else default_init(mname, f)))
    uses = ""
    if any("cuid()" in f.attrs for f in fields):
        uses += "using Delivery.Domain.Common;\n"
    if any(f.is_enum for f in fields):
        uses += "using Delivery.Domain.Enums;\n"
    uses += "\n" if uses else ""
    src = f"{uses}namespace Delivery.Domain.Entities;\n\npublic partial class {mname}\n{{\n" + "\n".join(lines) + "\n}\n"
    open(os.path.join(dom, "Entities", f"{mname}.cs"), "w", encoding="utf8").write(src)

    table = next((b[1] for b in battrs if b[0] == "map"), mname)
    c = [f'        builder.ToTable("{table}");']
    ids = [f for f in fields if "@id" in f.attrs.split("@default")[0] or re.search(r"@id\b", f.attrs)]
    bid = next((b for b in battrs if b[0] == "id"), None)
    if bid:
        cols = ", ".join(f"e.{prop_name(mname, x)}" for x in bid[1])
        c.append(f"        builder.HasKey(e => new {{ {cols} }});")
    elif ids:
        c.append(f"        builder.HasKey(e => e.{prop_name(mname, ids[0].name)});")
    for f in fields:
        if f.is_relation:
            continue
        p = prop_name(mname, f.name)
        col = re.search(r'@map\("([^"]+)"\)', f.attrs)
        parts = [f'HasColumnName("{col.group(1) if col else f.name}")']
        if f.type == "String" and not f.list:
            vc = re.search(r"@db\.(VarChar|Char)\((\d+)\)", f.attrs)
            if vc:
                parts.append(f"HasMaxLength({vc.group(2)})")
                if vc.group(1) == "Char":
                    parts.append("IsFixedLength()")
        if f.type == "Decimal":
            dm = re.search(r"@db\.Decimal\((\d+),\s*(\d+)\)", f.attrs)
            parts.append(f"HasPrecision({dm.group(1)}, {dm.group(2)})" if dm else 'HasColumnType("numeric(65,30)")')
        if f.type == "DateTime" and "@db.Date" not in f.attrs:
            parts.append('HasColumnType("timestamptz(3)")' if "Timestamptz(3)" in f.attrs else 'HasColumnType("timestamp(3)")')
        if f.type == "Json":
            parts.append('HasColumnType("jsonb")')
        if f.is_enum:
            parts.append("HasConversion<string>()")
        if not f.opt and not f.list:
            parts.append("IsRequired()")
        if "@default(autoincrement())" in f.attrs:
            parts.append("ValueGeneratedOnAdd()")
        elif re.search(r"@id\b", f.attrs) or bid and f.name in bid[1]:
            parts.append("ValueGeneratedNever()")
        if "@default(now())" in f.attrs:
            parts.append('HasAnnotation(PersistenceAnnotations.DefaultNow, true)')
        if "@updatedAt" in f.attrs:
            parts.append('HasAnnotation(PersistenceAnnotations.UpdatedAt, true)')
        c.append(f"        builder.Property(e => e.{p}).{'.'.join(parts)};")
        if re.search(r"@unique\b", f.attrs):
            c.append(f"        builder.HasIndex(e => e.{p}).IsUnique();")
    for b in battrs:
        if b[0] in ("unique", "index"):
            cols = [x.split("(")[0] for x in b[1]]
            expr = f"e.{prop_name(mname, cols[0])}" if len(cols) == 1 else "new { " + ", ".join(f"e.{prop_name(mname, x)}" for x in cols) + " }"
            c.append(f"        builder.HasIndex(e => {expr}){'.IsUnique()' if b[0] == 'unique' else ''};")
    for f in fields:
        if not (f.is_relation and f.rel_fields):
            continue
        cp = counterpart(mname, f)
        nav = prop_name(mname, f.name)
        if cp is None:
            with_ = ".WithMany()"
        elif cp.list:
            with_ = f".WithMany(e => e.{prop_name(f.type, cp.name)})"
        else:
            with_ = f".WithOne(e => e.{prop_name(f.type, cp.name)})"
        fk_cols = [prop_name(mname, x) for x in f.rel_fields]
        fk = f"e.{fk_cols[0]}" if len(fk_cols) == 1 else "new { " + ", ".join(f"e.{x}" for x in fk_cols) + " }"
        generic = f"<{mname}>" if (cp is not None and not cp.list) else ""
        line = f"        builder.HasOne(e => e.{nav}){with_}.HasForeignKey{generic}(e => {fk})"
        target_fields, target_battrs = parsed[f.type]
        tid = [g.name for g in target_fields if re.search(r"@id\b", g.attrs)]
        tbid = next((b[1] for b in target_battrs if b[0] == "id"), None)
        pk = tbid or tid
        if list(f.rel_refs) != list(pk):
            refs = [prop_name(f.type, x) for x in f.rel_refs]
            pexpr = f"e.{refs[0]}" if len(refs) == 1 else "new { " + ", ".join(f"e.{x}" for x in refs) + " }"
            line += f".HasPrincipalKey{generic}(e => {pexpr})"
        required = not any(g.opt for g in fields if g.name in f.rel_fields) and not f.opt
        od = f.on_delete or ("Restrict" if required else "SetNull")
        line += f".OnDelete(DeleteBehavior.{ {'Cascade': 'Cascade', 'Restrict': 'Restrict', 'SetNull': 'SetNull', 'NoAction': 'NoAction', 'SetDefault': 'SetNull'}[od] })"
        if required:
            line += ".IsRequired()"
        c.append(line + ";")
    cfg = ("using Delivery.Domain.Entities;\nusing Microsoft.EntityFrameworkCore;\nusing Microsoft.EntityFrameworkCore.Metadata.Builders;\n\n"
           "namespace Delivery.Infrastructure.Persistence.Configurations;\n\n"
           f"internal sealed class {mname}Configuration : IEntityTypeConfiguration<{mname}>\n{{\n"
           f"    public void Configure(EntityTypeBuilder<{mname}> builder)\n    {{\n" + "\n".join(c) + "\n    }\n}\n")
    open(os.path.join(inf, f"{mname}Configuration.cs"), "w", encoding="utf8").write(cfg)
    plural = mname + ("es" if mname.endswith(("s", "x", "ch", "sh")) else "s")
    if mname.endswith("y") and mname[-2] not in "aeiou":
        plural = mname[:-1] + "ies"
    dbsets.append((mname, plural))

iface = ("using Delivery.Domain.Entities;\nusing Microsoft.EntityFrameworkCore;\nusing Microsoft.EntityFrameworkCore.Infrastructure;\n\n"
         "namespace Delivery.Application.Common.Interfaces;\n\npublic interface IAppDbContext\n{\n"
         + "\n".join(f"    DbSet<{m}> {p} {{ get; }}" for m, p in dbsets)
         + "\n\n    DatabaseFacade Database { get; }\n\n    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);\n}\n")
open(os.path.join(app, "IAppDbContext.cs"), "w", encoding="utf8").write(iface)
sets = "\n".join(f"    public DbSet<{m}> {p} => Set<{m}>();" for m, p in dbsets)
open(os.path.join(api_dir, "src/Delivery.Infrastructure/Persistence/AppDbContext.Sets.cs"), "w", encoding="utf8").write(
    "using Delivery.Domain.Entities;\nusing Microsoft.EntityFrameworkCore;\n\nnamespace Delivery.Infrastructure.Persistence;\n\n"
    f"public sealed partial class AppDbContext\n{{\n{sets}\n}}\n")
print(f"{len(enums)} enums, {len(parsed)} models")
