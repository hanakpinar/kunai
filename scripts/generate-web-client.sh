#!/usr/bin/env bash
# Regenerates the web app's typed API client from the committed OpenAPI contract.
# Run it after openapi/kunai.v1.json changes (see tests/Kunai.Api.IntegrationTests/Contract).
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet tool restore
dotnet kiota generate \
  --language CSharp \
  --openapi openapi/kunai.v1.json \
  --class-name KunaiApiClient \
  --namespace-name Kunai.Web.Client.Api \
  --output src/Kunai.Web.Client/Api \
  --exclude-backward-compatible \
  --clean-output
