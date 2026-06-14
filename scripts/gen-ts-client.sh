#!/usr/bin/env bash
# Generates a typed TypeScript client from the CivicPulse OpenAPI spec.
# Requires Node.js >= 18. Run with the API server live at localhost:8080.
set -euo pipefail

SPEC_URL="http://localhost:8080/swagger/v1/swagger.json"
OUT_DIR="$(dirname "$0")/../clients/typescript"

echo "Fetching spec from $SPEC_URL..."
curl -sf "$SPEC_URL" -o /tmp/civicpulse-spec.json || {
  echo "ERROR: Could not reach the API. Make sure it is running at localhost:8080." >&2
  exit 1
}

mkdir -p "$OUT_DIR"

echo "Generating TypeScript client into $OUT_DIR..."
npx --yes openapi-typescript /tmp/civicpulse-spec.json -o "$OUT_DIR/api.d.ts"

echo "Done. Types written to $OUT_DIR/api.d.ts"
