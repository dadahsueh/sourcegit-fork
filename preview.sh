#!/usr/bin/env bash
set -e

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export DOTNET_CLI_HOME="/tmp"

echo "Building and launching SourceGit Preview..."
dotnet run --project "$DIR/src/SourceGit.csproj" -c Debug -- "$DIR" "$@"
