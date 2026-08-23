#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

echo "==> Applying database migrations..."
dotnet ef database update --project "${REPO_ROOT}/src/server/Nimpression.Infrastructure" --startup-project "${REPO_ROOT}/src/server/Nimpression.Infrastructure"

echo "==> Running deterministic database seeder..."
dotnet run --project "${REPO_ROOT}/src/server/Nimpression.Api" -- seed

echo "==> Database seeding complete."
