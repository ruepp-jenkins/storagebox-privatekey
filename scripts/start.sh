#!/bin/bash
set -euo pipefail
echo "Starting build workflow"

scripts/docker_initialize.sh

# run build
DATESTAMP="${DATESTAMP:-$(date +%Y%m%d)}"
DOCKER_PLATFORMS="${DOCKER_PLATFORMS:-linux/amd64,linux/arm64}"
BUILDER_NAME="${BUILDER_NAME:-mybuilder}"

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

# cleanup
scripts/docker_cleanup.sh
