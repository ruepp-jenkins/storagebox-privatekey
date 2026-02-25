#!/bin/bash
set -euo pipefail
echo "Initialize docker"

: "${DOCKER_USERNAME:?DOCKER_USERNAME is required}"
: "${DOCKER_API_PASSWORD:?DOCKER_API_PASSWORD is required}"
: "${BUILDER_NAME:?BUILDER_NAME is required}"

printf '%s' "${DOCKER_API_PASSWORD}" | docker login --username "${DOCKER_USERNAME}" --password-stdin

if docker buildx inspect "${BUILDER_NAME}" > /dev/null 2>&1; then
    echo "Using existing buildx builder: ${BUILDER_NAME}"
else
    echo "Creating buildx builder: ${BUILDER_NAME}"
    docker buildx create --name "${BUILDER_NAME}" --driver docker-container > /dev/null
fi

docker buildx inspect --bootstrap "${BUILDER_NAME}"
