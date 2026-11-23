#!/usr/bin/env bash
# Affiche un fichier TS sans commentaires, lignes vides ni imports (lecture ciblée de Speedy).
for f in "$@"; do
  echo "=== $f"
  awk '
    inimp { if ($0 ~ /;[[:space:]]*$/) inimp=0; next }
    /^import[[:space:]]/ { if ($0 !~ /;[[:space:]]*$/) inimp=1; next }
    /^[[:space:]]*(\*|\/\*|\/\/)/ { next }
    /^[[:space:]]*$/ { next }
    { print }
  ' "$f"
done
