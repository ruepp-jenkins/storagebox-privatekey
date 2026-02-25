#!/bin/bash
set -euo pipefail
echo "Starting build workflow"

DATESTAMP="${DATESTAMP:-$(date +%Y%m%d)}"
DOCKER_PLATFORMS="${DOCKER_PLATFORMS:-linux/amd64,linux/arm64}"

if [ -z "${BUILDER_NAME:-}" ]; then
    builder_scope="${BUILD_TAG:-${JOB_NAME:-local}-${BUILD_NUMBER:-$(date +%s)}}"
    builder_scope="$(printf '%s' "${builder_scope}" | tr '/:@ ' '----' | tr -cd '[:alnum:]_.-')"

    if [ -z "${builder_scope}" ]; then
        builder_scope="$(date +%s)"
    fi

    BUILDER_NAME="mybuilder-${builder_scope}"
fi

export BUILDER_NAME

cleanup() {
    scripts/docker_cleanup.sh
}

trap cleanup EXIT

scripts/docker_initialize.sh

# run build
BRANCH_NAME="${BRANCH_NAME:-local}"

echo "Buildx builder: ${BUILDER_NAME}"
echo "Build platforms: ${DOCKER_PLATFORMS}"
echo "[${BRANCH_NAME}] Building images: ${IMAGE_FULLNAME}"
if [ "$BRANCH_NAME" = "master" ] || [ "$BRANCH_NAME" = "main" ]
then
    docker buildx build \
        --builder "${BUILDER_NAME}" \
        --platform "${DOCKER_PLATFORMS}" \
        -t ${IMAGE_FULLNAME}:${DATESTAMP} \
        -t ${IMAGE_FULLNAME}:latest \
        --pull \
        --push .
else
    docker buildx build \
        --builder "${BUILDER_NAME}" \
        --platform "${DOCKER_PLATFORMS}" \
        -t ${IMAGE_FULLNAME}-test:${BRANCH_NAME}-${DATESTAMP} \
        --pull \
        --push .
fi
