#!/bin/bash

for file in "./transaction/fhir/"*.json; do
  if [[ -f "$file" ]]; then
    # Synthea always generates Patient & Encounter resources, but we only care about Patients
    jq '.entry |= map(
      .request.method = "PUT" |
      .request.url = "\(.resource.resourceType)/\(.resource.id | sub("^urn:uuid:"; ""))"
    )' "$file" >"${file%.json}.put.json"
  fi
done
