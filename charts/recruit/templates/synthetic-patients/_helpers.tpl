{{/* vim: set filetype=mustache: */}}

{{/*
Image used to generate the synthetic patients and load them into the FHIR server
*/}}
{{- define "recruit.syntheticPatients.image" -}}
{{- $registry := .Values.syntheticPatients.image.registry -}}
{{- $repository := .Values.syntheticPatients.image.repository -}}
{{- $tag := .Values.syntheticPatients.image.tag -}}
{{ printf "%s/%s:%s" $registry $repository $tag}}
{{- end -}}

{{/*
The container generating the synthetic patients into the `synthetic-patients` volume's `fhir/` directory. Shared by
the Job loading them into the FHIR server and the query module's pod, so both always generate the exact same patients
with the same resource ids - only the exporter differs. Expects a dict with "root" (the chart's root context) and
"exporterArgs" (additional Synthea arguments selecting the exporter).
*/}}
{{- define "recruit.syntheticPatients.generateContainer" -}}
{{- $values := .root.Values.syntheticPatients -}}
- name: generate-synthetic-patients
  image: {{ include "recruit.syntheticPatients.image" .root }}
  imagePullPolicy: IfNotPresent
  command: ["/bin/bash", "-c"]
  workingDir: /data
  env:
    - name: HOME
      value: /tmp
    # the JVM defaults to a max heap of only 25% of the container's memory limit. Keeps the image's own file
    # encoding setting, which this replaces.
    - name: JAVA_TOOL_OPTIONS
      value: "-Dfile.encoding=UTF-8 -XX:MaxRAMPercentage=75"
    - name: SYNTHEA_SEED
      value: {{ $values.seed | quote }}
    - name: SYNTHEA_REFERENCE_DATE
      value: {{ $values.referenceDate | quote }}
    - name: SYNTHEA_POPULATION
      value: {{ $values.population | quote }}
  args:
    - |
      set -euo pipefail
      java -jar /usr/local/bin/synthea.jar -s "${SYNTHEA_SEED}" -cs "${SYNTHEA_SEED}" -r "${SYNTHEA_REFERENCE_DATE}" \
        -p "${SYNTHEA_POPULATION}" -c /config/synthea.properties --exporter.baseDirectory=/data/ {{ .exporterArgs }}
  volumeMounts:
    - name: synthetic-patients
      mountPath: /data
    - name: synthetic-patients-tmp
      mountPath: /tmp
    - name: synthetic-patients-config
      mountPath: /config
      readOnly: true
  {{- with .root.Values.restrictedContainerSecurityContext }}
  securityContext:
    {{- toYaml . | nindent 4 }}
  {{- end }}
  {{- with $values.resources }}
  resources:
    {{- toYaml . | nindent 4 }}
  {{- end }}
{{- end -}}

{{/*
The volumes used by the "recruit.syntheticPatients.generateContainer"
*/}}
{{- define "recruit.syntheticPatients.volumes" -}}
- name: synthetic-patients
  emptyDir: {}
- name: synthetic-patients-tmp
  emptyDir: {}
- name: synthetic-patients-config
  configMap:
    name: {{ include "common.names.fullname" . }}-synthetic-patients
{{- end -}}
