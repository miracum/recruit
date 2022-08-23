#!/bin/bash
set -euox pipefail

SBOMS_DIR=${SBOMS_DIR:-"./sboms"}
RECRUIT_IMAGES=$(yq .services.*.image <docker-compose/docker-compose.yaml)

mkdir -p $SBOMS_DIR

for IMAGE_NAME in $RECRUIT_IMAGES; do
    SLUGIFIED_IMAGE_NAME=$(echo $IMAGE_NAME | iconv -t ascii//TRANSLIT | sed -r s/[^a-zA-Z0-9]+/-/g | sed -r s/^-+\|-+$//g | tr A-Z a-z)
    trivy image --format=cyclonedx --security-checks=vuln --output="${SBOMS_DIR}/${SLUGIFIED_IMAGE_NAME}.cdx.json" "${IMAGE_NAME}"
done
