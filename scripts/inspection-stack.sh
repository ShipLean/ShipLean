#!/usr/bin/env bash
# Starts or stops the Inspection stack (collector, ClickHouse, Tempo, Loki) by docker compose, for real agent runs and manual
# checks. The tests use InspectionStack (Testcontainers) with the same images and configs.
#   scripts/inspection-stack.sh up     start and wait; prints the endpoints
#   scripts/inspection-stack.sh down   stop and remove the containers (and the data)
set -euo pipefail
compose=(docker compose -f "$(dirname "$0")/../src/ShipLean.LocalEnvironment/Inspection/docker-compose.yml")
case "${1:-}" in
  up)
    "${compose[@]}" up -d --wait
    for url in http://127.0.0.1:13200/ready http://127.0.0.1:13100/ready; do
      until curl -sf "$url" >/dev/null; do sleep 1; done
    done
    echo "collector OTLP/HTTP  http://127.0.0.1:14318   (OTEL_EXPORTER_OTLP_ENDPOINT)"
    echo "ClickHouse HTTP      http://127.0.0.1:18123   (user shiplean, password shiplean, database otel)"
    echo "Tempo                http://127.0.0.1:13200"
    echo "Loki                 http://127.0.0.1:13100"
    ;;
  down) "${compose[@]}" down -v ;;
  *) echo "usage: $0 up|down" >&2; exit 2 ;;
esac
