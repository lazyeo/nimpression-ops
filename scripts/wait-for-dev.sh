#!/usr/bin/env bash
set -euo pipefail

# 等待前端 (4200) 与后端 (5080/health) 真实可访问后打印就绪横幅

FRONTEND_URL="http://localhost:4200"
BACKEND_HEALTH_URL="http://localhost:5080/health"
MAX_WAIT_SECONDS=120
SLEEP_INTERVAL=1

echo "[dev] Waiting for frontend ($FRONTEND_URL) and backend ($BACKEND_HEALTH_URL) to be ready..."
echo "[dev] (Angular initial build takes ~40-60s on cold start, please wait)"

elapsed=0
frontend_ready=0
backend_ready=0

while [ "$elapsed" -lt "$MAX_WAIT_SECONDS" ]; do
  # Check frontend
  if [ "$frontend_ready" -eq 0 ]; then
    fe_code=$(curl -s -o /dev/null -w "%{http_code}" "$FRONTEND_URL" 2>/dev/null || true)
    if [ "$fe_code" = "200" ]; then
      frontend_ready=1
    fi
  fi

  # Check backend
  if [ "$backend_ready" -eq 0 ]; then
    be_code=$(curl -s -o /dev/null -w "%{http_code}" "$BACKEND_HEALTH_URL" 2>/dev/null || true)
    if [ "$be_code" = "200" ]; then
      backend_ready=1
    fi
  fi

  # If both are ready, print the banner
  if [ "$frontend_ready" -eq 1 ] && [ "$backend_ready" -eq 1 ]; then
    echo ""
    echo "========================================================"
    echo "  🚀 Nimpression Ops is Ready! (in ${elapsed}s)"
    echo "========================================================"
    echo "  Frontend (Angular):    http://localhost:4200"
    echo "  Backend API (.NET):    http://localhost:5080"
    echo "  Mailpit Web UI:        http://localhost:8025"
    echo "  MinIO Console:         http://localhost:9001"
    echo "========================================================"
    echo ""
    exit 0
  fi

  sleep "$SLEEP_INTERVAL"
  elapsed=$((elapsed + SLEEP_INTERVAL))
done

echo ""
echo "========================================================"
echo "  [WARNING] Timed out waiting for full stack readiness after ${MAX_WAIT_SECONDS}s."
if [ "$frontend_ready" -eq 0 ]; then
  echo "  - Frontend ($FRONTEND_URL) did not respond 200."
fi
if [ "$backend_ready" -eq 0 ]; then
  echo "  - Backend ($BACKEND_HEALTH_URL) did not respond 200."
fi
echo "  Please check the build and server logs above for errors."
echo "========================================================"
echo ""
