#!/usr/bin/env bash
set -e

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export DOTNET_CLI_HOME="/tmp"

echo "=== Running SourceGit Automated Tests ==="
dotnet test "$DIR/tests/SourceGit.Tests/SourceGit.Tests.csproj" --verbosity normal
echo "=== All Tests Passed! ==="
