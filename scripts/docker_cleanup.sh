#!/bin/bash
set -euo pipefail
echo "Cleanup docker"

if [ -z "${BUILDER_NAME:-}" ]; then
    echo "BUILDER_NAME not set. Skipping buildx cleanup to avoid shared cache pruning."
    exit 0
fi

if docker buildx inspect "${BUILDER_NAME}" > /dev/null 2>&1; then
    echo "Removing buildx builder: ${BUILDER_NAME}"
    if ! docker buildx rm --force "${BUILDER_NAME}"; then
        echo "Warning: could not remove builder '${BUILDER_NAME}'."
    fi
else
    echo "Buildx builder '${BUILDER_NAME}' not found. Nothing to cleanup."
fi
