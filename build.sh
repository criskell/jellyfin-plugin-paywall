#!/usr/bin/env bash
# Publica o plugin e monta a pasta pronta para copiar em <jellyfin-data>/plugins/.
set -euo pipefail

root="$(cd "$(dirname "$0")" && pwd)"
staging="$root/artifacts/Paywall_1.0.0.0"

rm -rf "$staging"
mkdir -p "$staging"

dotnet publish "$root/src/Jellyfin.Plugin.Paywall/Jellyfin.Plugin.Paywall.csproj" \
  -c Release -o "$staging" --nologo

find "$staging" -maxdepth 1 \( -name '*.pdb' -o -name '*.deps.json' \) -delete

cp "$root/src/Jellyfin.Plugin.Paywall/meta.json" "$staging/"

echo "Pronto: $staging"
