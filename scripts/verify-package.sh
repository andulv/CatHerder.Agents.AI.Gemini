#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

configuration="${CONFIGURATION:-Release}"

dotnet build CatHerder.Agents.AI.Gemini.slnx --configuration "$configuration"
dotnet test tests/CatHerder.Agents.AI.Gemini.UnitTests/CatHerder.Agents.AI.Gemini.UnitTests.csproj --configuration "$configuration" --no-build
dotnet pack src/CatHerder.Agents.AI.Gemini/CatHerder.Agents.AI.Gemini.csproj --configuration "$configuration" --no-build
