#!/usr/bin/env bash
set -euo pipefail

runtime="${1:-linux-x64}"
case "$runtime" in
  win-x64|linux-x64) ;;
  *) echo "Usage: $0 [win-x64|linux-x64]" >&2; exit 2 ;;
esac

dotnet publish "$(dirname "$0")/../WhatKey/WhatKey.csproj" -c Release -r "$runtime"
