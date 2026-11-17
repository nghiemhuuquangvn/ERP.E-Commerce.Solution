#!/usr/bin/env bash
# Runs the SQL Server migration Job, then the PostgreSQL one, and fails when either fails.
# Used by .github/workflows/db-migrations.yml; also works by hand:
#   KUBECONFIG=~/.kube/config SQL_IMAGE=ghcr.io/<owner>/erp-db-migrations:sha-xxxxxxx \
#   PG_IMAGE=ghcr.io/<owner>/erp-pg-migrations:sha-xxxxxxx bash deployment/k8s/jobs/run-migration-jobs.sh
# Env: KUBE_CONFIG (content, CI) or KUBECONFIG (path), SQL_IMAGE, PG_IMAGE, RUN_ID (default: time stamp).
set -euo pipefail

NS="${K8S_NAMESPACE:-erp}"
RUN="${RUN_ID:-${GITHUB_RUN_ID:-$(date +%s)}}-${GITHUB_RUN_ATTEMPT:-1}"
DIR="$(cd "$(dirname "$0")" && pwd)"

if [[ -n "${KUBE_CONFIG:-}" ]]; then
  export KUBECONFIG="${RUNNER_TEMP:-/tmp}/kubeconfig-migrate"
  printf '%s' "$KUBE_CONFIG" > "$KUBECONFIG"; chmod 600 "$KUBECONFIG"
  trap 'rm -f "$KUBECONFIG"' EXIT
fi
: "${SQL_IMAGE:?SQL_IMAGE is required}" "${PG_IMAGE:?PG_IMAGE is required}"

run_job() { # <manifest> <job name prefix> <image>
  local manifest="$1" name="$2-$RUN" image="$3"
  echo "::group::$name ($image)"
  sed -e "s#__IMAGE__#$image#" -e "s#__RUN__#$RUN#" "$DIR/$manifest" | kubectl apply -f -
  # Wait for whichever comes first: complete or failed.
  local status=""
  for _ in $(seq 1 360); do
    status="$(kubectl -n "$NS" get job "$name" -o jsonpath='{range .status.conditions[?(@.status=="True")]}{.type}{end}' 2>/dev/null || true)"
    [[ "$status" == *Complete* || "$status" == *Failed* ]] && break
    sleep 5
  done
  kubectl -n "$NS" logs "job/$name" --all-containers=true || true
  echo "::endgroup::"
  if [[ "$status" != *Complete* ]]; then
    echo "::error::$name did not complete (status: ${status:-timeout}) - nothing after it was run"
    exit 1
  fi
  echo "- \`$name\` complete (\`$image\`)" >> "${GITHUB_STEP_SUMMARY:-/dev/null}"
}

run_job db-migrate.yaml db-migrate "$SQL_IMAGE"   # 16 SQL Server databases
run_job pg-migrate.yaml pg-migrate "$PG_IMAGE"    # ManufacturingDB (PostgreSQL)
