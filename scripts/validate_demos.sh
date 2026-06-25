#!/usr/bin/env bash
# validate_demos.sh — smoke-test every example so a compiler/runtime change cannot
# silently break a demo (a credibility risk). Run from the repo root after building
# bin/goclr and the runtime DLLs (the standard loop build). Exits non-zero if any
# demo crashes.
#
# Each demo is run with `goclr run`. Run-once demos must finish without a .NET
# exception. Server demos are probed with a browser-like request (many headers +
# Cookie — the shape that exposed the slice-capacity crash) and must answer without
# crashing. Demos with no `func main` (test/library targets) are skipped.
# Portable to bash 3.2 (macOS default): no associative arrays.
set -u
cd "$(dirname "$0")/.."
FAIL=0
TMP=$(mktemp -d)
EXC='exception|nullreference|nullpointer|panic:|Cannot print exception|Unhandled|System\.'

# server_for <demo> echoes "port path" for a server demo, empty for run-once.
server_for() {
  case "$1" in
    demo_fiber)   echo "3000 /" ;;
    demo_gin)     echo "8080 /ping" ;;
    demo_gin_sql) echo "8080 /notes" ;;
    demo_echo)    echo "8080 /" ;;
    *)            echo "" ;;
  esac
}
BROWSER=(-H 'User-Agent: Mozilla/5.0' -H 'Accept: text/html,*/*;q=0.8'
         -H 'Accept-Language: en-US,en;q=0.9' -H 'Accept-Encoding: gzip, deflate'
         -H 'Cookie: session=abc123; theme=dark' -H 'Connection: keep-alive')

for dir in examples/*/; do
  d=$(basename "$dir")
  [ -f "$dir/main.go" ] || continue
  grep -q "func main(" "$dir/main.go" || { echo "SKIP $d (no main)"; continue; }
  log="$TMP/$d.log"
  srv=$(server_for "$d")

  if [ -n "$srv" ]; then
    port=${srv%% *}; path=${srv##* }
    lsof -ti:"$port" 2>/dev/null | xargs kill -9 2>/dev/null
    bin/goclr run "$dir/main.go" >"$log" 2>&1 &
    pid=$!
    up=0
    for _ in $(seq 1 90); do
      grep -qiE "$EXC" "$log" && break
      kill -0 $pid 2>/dev/null || break
      if curl -s -m 2 "http://127.0.0.1:$port$path" -o /dev/null 2>/dev/null; then up=1; break; fi
      sleep 1
    done
    [ $up -eq 1 ] && curl -s -m 3 "http://127.0.0.1:$port$path" "${BROWSER[@]}" -o /dev/null 2>/dev/null
    sleep 1
    { kill -9 $pid; wait $pid; } 2>/dev/null; pkill -9 -f "$dir/main.go" 2>/dev/null
    lsof -ti:"$port" 2>/dev/null | xargs kill -9 2>/dev/null
    if grep -qiE "$EXC" "$log"; then
      echo "FAIL $d (exception)"; grep -iE "$EXC" "$log" | head -1; FAIL=1
    elif [ $up -ne 1 ]; then
      echo "FAIL $d (never served on :$port)"; FAIL=1
    else
      echo "PASS $d (served :$port$path)"
    fi
  else
    bin/goclr run "$dir/main.go" >"$log" 2>&1 &
    pid=$!
    for _ in $(seq 1 90); do
      grep -qiE "$EXC" "$log" && break
      kill -0 $pid 2>/dev/null || break
      sleep 1
    done
    { kill -9 $pid; wait $pid; } 2>/dev/null; pkill -9 -f "$dir/main.go" 2>/dev/null
    if grep -qiE "$EXC" "$log"; then
      echo "FAIL $d (exception)"; grep -iE "$EXC" "$log" | head -1; FAIL=1
    else
      echo "PASS $d"
    fi
  fi
done

rm -rf "$TMP"
[ $FAIL -eq 0 ] && echo "ALL DEMOS OK" || echo "SOME DEMOS FAILED"
exit $FAIL
