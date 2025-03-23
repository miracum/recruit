#!/bin/bash

FHIR_SERVER_BASE_URL=${FHIR_SERVER_BASE_URL:-"http://localhost:8080/fhir"}

# The -r flag to sort makes sure the lowercase hospitalinformation and practitioner
# resources are sent first
find "./transaction/fhir/" -maxdepth 1 -name "*.put.json" -printf "%f\n" |
  sort -r |
  while read -r filename; do
    file="./transaction/fhir/$filename"
    if [[ -f "$file" ]]; then
      echo Sending $file
      curl --fail-with-body --retry-connrefused -X POST -H "Content-Type: application/fhir+json" --data @"$file" "$FHIR_SERVER_BASE_URL"
    fi
  done
