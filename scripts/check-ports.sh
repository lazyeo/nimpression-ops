#!/usr/bin/env bash
set -euo pipefail

# 检查传入的一个或多个端口是否被占用
# 若被占用，打印占用进程的详细信息（PID、进程名、详细连接）与可直接复制的释放命令并退出 1

if [ "$#" -eq 0 ]; then
  echo "Usage: $0 <port> [port2 ...]"
  exit 1
fi

has_conflict=0

for port in "$@"; do
  lsof_output=$(lsof -nP -iTCP:"$port" -sTCP:LISTEN 2>/dev/null || true)
  if [ -n "$lsof_output" ]; then
    has_conflict=1
    echo "========================================================"
    echo "  [ERROR] Port $port is already in use!"
    echo "========================================================"
    echo "Process listening on port $port:"
    echo "$lsof_output"
    echo ""
    pids=$(lsof -ti :"$port" 2>/dev/null || true)
    if [ -n "$pids" ]; then
      echo "To release port $port, run:"
      for pid in $pids; do
        cmd=$(ps -p "$pid" -o comm= 2>/dev/null || true)
        echo "  kill -9 $pid  # ($cmd)"
      done
      echo "  # or: lsof -ti :$port | xargs kill -9"
    fi
    echo "========================================================"
  fi
done

if [ "$has_conflict" -ne 0 ]; then
  exit 1
fi
