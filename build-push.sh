#!/usr/bin/env bash
set -euo pipefail

# Build + push Docker image to registry.main.fpi using buildx.
#
# Why "prebuilt":
#   In some corporate networks, "dotnet restore" inside a Linux container fails with TLS errors (NU1301 PartialChain).
#   This script publishes on the host first, then builds a runtime image from ./publish (Dockerfile.prebuilt).
#
# Usage:
#   ./build-push.sh                 # pushes :latest
#   ./build-push.sh 1.0.0           # pushes :1.0.0
#   REGISTRY=registry.main.fpi ./build-push.sh 1.0.0
#   IMAGE=businesscalendar ./build-push.sh 1.0.0
#   PLATFORM=linux/amd64 ./build-push.sh 1.0.0
#
# Prereqs:
#   docker login registry.main.fpi

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$ROOT_DIR"

TAG="${1:-latest}"
REGISTRY="${REGISTRY:-registry.main.fpi}"
# full repository path inside registry (namespace/project)
IMAGE="${IMAGE:-dtd/businesscalendar}"
PLATFORM="${PLATFORM:-linux/amd64}"

FULL_IMAGE="${REGISTRY}/${IMAGE}:${TAG}"

if ! command -v docker >/dev/null 2>&1; then
  echo "docker not found in PATH" >&2
  exit 1
fi

if ! docker buildx version >/dev/null 2>&1; then
  echo "docker buildx is not available. Install/enable Buildx." >&2
  exit 1
fi

echo "==> Publishing .NET app to ./publish"
rm -rf publish
dotnet publish -c Release -o publish

echo "==> Building and pushing: ${FULL_IMAGE}"
docker buildx build \
  --platform "${PLATFORM}" \
  -f Dockerfile.prebuilt \
  -t "${FULL_IMAGE}" \
  --push \
  .

echo "==> Done: ${FULL_IMAGE}"


