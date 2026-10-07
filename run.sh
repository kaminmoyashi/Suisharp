#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
if ! command -v dotnet >/dev/null 2>&1; then
  echo ".NET 10 SDK was not found. Install it and make the dotnet command available on PATH." >&2
  exit 1
fi

project="$script_dir/samples/Suisharp.Demo/Suisharp.Demo.csproj"
demo="$script_dir/samples/Suisharp.Demo"
config="$script_dir/NuGet.Config"

dotnet restore "$project" --configfile "$config" --disable-parallel
dotnet run --no-restore --project "$demo" -- --urls http://127.0.0.1:5080
