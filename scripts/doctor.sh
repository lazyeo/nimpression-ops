#!/usr/bin/env bash
set -uo pipefail

# 🩺 Nimpression Ops — Environment Doctor
# 检查开发环境所需依赖：dotnet / node / pnpm / colima / docker / 关键端口

EXIT_CODE=0
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo "========================================================"
echo "  🩺 Nimpression Ops — Environment Doctor"
echo "========================================================"

# 1. Check .NET
if command -v dotnet >/dev/null 2>&1; then
  DOTNET_VER=$(dotnet --version 2>/dev/null || echo "unknown")
  echo -e "  [${GREEN}✓${NC}] dotnet:      $DOTNET_VER (.NET SDK)"
else
  echo -e "  [${RED}✗${NC}] dotnet:      not found in PATH"
  echo "      Fix command:"
  echo "        brew install --cask dotnet-sdk"
  EXIT_CODE=1
fi

# 2. Check Node.js
if command -v node >/dev/null 2>&1; then
  NODE_VER=$(node -v 2>/dev/null || echo "unknown")
  MAJOR_VER=$(echo "$NODE_VER" | sed -E 's/^v([0-9]+).*/\1/')
  if [ -n "$MAJOR_VER" ] && [ "$MAJOR_VER" -ge 22 ] 2>/dev/null; then
    echo -e "  [${GREEN}✓${NC}] node:        $NODE_VER (>= 22 required)"
  else
    echo -e "  [${RED}✗${NC}] node:        $NODE_VER (requires Node.js >= 22)"
    echo "      Fix command:"
    echo "        nvm install 24 && nvm use 24"
    echo "        # or: brew install node@24"
    EXIT_CODE=1
  fi
else
  echo -e "  [${RED}✗${NC}] node:        not found in PATH"
  echo "      Fix command:"
  echo "        brew install node@24"
  echo "        # or: nvm install 24 && nvm use 24"
  EXIT_CODE=1
fi

# 3. Check pnpm / corepack
PNPM_OK=0
if command -v corepack >/dev/null 2>&1; then
  if PNPM_VER=$(corepack pnpm --version 2>/dev/null); then
    echo -e "  [${GREEN}✓${NC}] pnpm:        $PNPM_VER (via corepack)"
    PNPM_OK=1
  fi
fi

if [ "$PNPM_OK" -eq 0 ]; then
  if command -v pnpm >/dev/null 2>&1; then
    PNPM_VER=$(pnpm --version 2>/dev/null || echo "unknown")
    echo -e "  [${GREEN}✓${NC}] pnpm:        $PNPM_VER (standalone binary)"
    PNPM_OK=1
  fi
fi

if [ "$PNPM_OK" -eq 0 ]; then
  echo -e "  [${RED}✗${NC}] pnpm:        pnpm not found or corepack shim not active"
  echo "      Fix command:"
  echo "        corepack enable"
  echo "        # or: npm install -g pnpm@11.20.0"
  EXIT_CODE=1
fi

# 4. Check Colima
if command -v colima >/dev/null 2>&1; then
  if colima status >/dev/null 2>&1; then
    COLIMA_STATUS=$(colima status 2>&1 | head -n 1)
    echo -e "  [${GREEN}✓${NC}] colima:      $COLIMA_STATUS"
  else
    echo -e "  [${YELLOW}!${NC}] colima:      installed but not running"
    echo "      Fix command:"
    echo "        colima start"
  fi
else
  echo -e "  [${RED}✗${NC}] colima:      not found in PATH"
  echo "      Fix command:"
  echo "        brew install colima"
  EXIT_CODE=1
fi

# 5. Check Docker
if command -v docker >/dev/null 2>&1; then
  if docker info >/dev/null 2>&1; then
    DOCKER_VER=$(docker --version 2>/dev/null | sed -E 's/Docker version ([^,]+).*/\1/' || echo "ready")
    echo -e "  [${GREEN}✓${NC}] docker:      $DOCKER_VER (daemon is responding)"
  else
    echo -e "  [${YELLOW}!${NC}] docker:      installed, but daemon is not responding"
    echo "      Fix command:"
    echo "        colima start"
  fi
else
  echo -e "  [${RED}✗${NC}] docker:      not found in PATH"
  echo "      Fix command:"
  echo "        brew install docker docker-compose"
  EXIT_CODE=1
fi

# 6. Check Port Status
echo "--------------------------------------------------------"
echo "  Port Status Check:"

for port_info in "5080:Backend API" "4200:Frontend Angular" "5432:PostgreSQL" "8025:Mailpit Web" "9001:MinIO Console"; do
  port="${port_info%%:*}"
  service="${port_info##*:}"

  lsof_out=$(lsof -nP -iTCP:"$port" -sTCP:LISTEN 2>/dev/null || true)
  if [ -z "$lsof_out" ]; then
    echo -e "  [${GREEN}✓${NC}] Port $port ($service): available"
  else
    pids=$(lsof -ti :"$port" 2>/dev/null | tr '\n' ' ' || true)
    proc_name=$(ps -p "$(echo "$pids" | awk '{print $1}')" -o comm= 2>/dev/null || echo "process")
    proc_basename=$(basename "$proc_name")
    
    # If it's colima/docker/ssh forwarding for containers
    if [ "$port" = "5432" ] || [ "$port" = "8025" ] || [ "$port" = "9001" ]; then
      echo -e "  [${BLUE}i${NC}] Port $port ($service): in use by $proc_basename (PID $pids) [Container service]"
    else
      echo -e "  [${YELLOW}!${NC}] Port $port ($service): in use by $proc_basename (PID $pids)"
      echo "      Fix command to release port $port:"
      echo "        kill -9 $pids"
      echo "        # or: lsof -ti :$port | xargs kill -9"
    fi
  fi
done

echo "========================================================"
if [ "$EXIT_CODE" -eq 0 ]; then
  echo -e "  ${GREEN}All core dependencies are healthy!${NC}"
else
  echo -e "  ${RED}Some dependencies need attention. Run the fix commands above.${NC}"
fi
echo "========================================================"

exit "$EXIT_CODE"
