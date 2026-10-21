{{/* vim: set filetype=mustache: */}}
{{/*
Standard Kubernetes labels (see the common chart's "common.labels.standard") plus `extraLabels`.
*/}}
{{- define "recruit.labels" -}}
{{ include "common.labels.standard" . }}
{{- with .Values.extraLabels }}
{{ toYaml . }}
{{- end }}
{{- end -}}

{{/*
Create the name of the service account to use.
Expects a dict with "context" (the component's values, e.g. .Values.list), "component" (e.g.
"list") and "root" (the chart's root context, i.e. $) - root is needed because "common.names.fullname"
reads .Values/.Chart/.Release from the top level, which isn't reachable from "context" alone.
*/}}
{{- define "recruit.serviceAccountName" -}}
{{- if .context.serviceAccount.create }}
{{- default (printf "%s-%s" (include "common.names.fullname" .root) .component) .context.serviceAccount.name }}
{{- else }}
{{- default "default" .context.serviceAccount.name }}
{{- end }}
{{- end }}

{{/*
Get the FHIR server URL.
*/}}
{{- define "recruit.fhirserver.url" -}}
{{- if .Values.fhirserver.enabled }}
    {{- $fullname := include "hapi-fhir-jpaserver.fullname" (index .Subcharts "fhirserver") -}}
    {{ printf "http://%s:%d/fhir" $fullname 8080 }}
{{- else -}}
    {{ .Values.externalFhirServer.url | quote }}
{{- end -}}
{{- end -}}

{{/*
Fully qualified name of the included maildev chart. Only valid if `maildev.enabled=true`.
*/}}
{{- define "recruit.maildev.fullname" -}}
{{- include "maildev.fullname" (index .Subcharts "maildev") -}}
{{- end -}}

{{/*
Image used to for the PostgreSQL readiness init containers
*/}}
{{- define "recruit.waitforDB.image" -}}
{{- $registry := .Values.waitForPostgresInitContainer.image.registry -}}
{{- $repository := .Values.waitForPostgresInitContainer.image.repository -}}
{{- $tag := .Values.waitForPostgresInitContainer.image.tag -}}
{{ printf "%s/%s:%s" $registry $repository $tag}}
{{- end -}}

{{/*
curlimages/curl image used by various init containers and helper Jobs across the chart.
*/}}
{{- define "recruit.curl.image" -}}
{{- $registry := .Values.curl.image.registry -}}
{{- $repository := .Values.curl.image.repository -}}
{{- $tag := .Values.curl.image.tag -}}
{{ printf "%s/%s:%s" $registry $repository $tag}}
{{- end -}}

{{/*
kubectl image used by the wait-for-migration initContainer to poll the list migration Job's
status.
*/}}
{{- define "recruit.kubectl.image" -}}
{{- $registry := .Values.kubectl.image.registry -}}
{{- $repository := .Values.kubectl.image.repository -}}
{{- $tag := .Values.kubectl.image.tag -}}
{{ printf "%s/%s:%s" $registry $repository $tag}}
{{- end -}}

{{/*
Name of the list DB-migration Job, derived from the list image tag (not
.Release.Revision - unreliable under GitOps rendering via `helm template`, since GitOps
controllers don't track real Helm release revisions). Same tag -> same name -> idempotent
re-syncs; changed tag -> new name -> new Job, with the old one pruned by the GitOps controller (or
via ttlSecondsAfterFinished as a fallback). Reserves the trailing 17 characters ("-migrate-" + 8
hex chars) so the hash suffix is never truncated away. Job names are capped at 63 characters by
Kubernetes - they back the "job-name" pod label, whose value is limited by K8s label-value rules.
*/}}
{{- define "recruit.list.migrationJobName" -}}
{{- $prefix := printf "%s-list" (include "common.names.fullname" .) | trunc 46 | trimSuffix "-" -}}
{{- printf "%s-migrate-%s" $prefix (.Values.list.image.tag | sha256sum | trunc 8) -}}
{{- end -}}

{{/*
Fully qualified name of the included postgres chart. Only valid if `postgres.enabled=true`.
*/}}
{{- define "recruit.postgresql.fullname" -}}
{{- include "postgres.fullname" (index .Subcharts "postgres") -}}
{{- end -}}


{{/*
Return the Trino credentials secret name
*/}}
{{- define "recruit.trino.secret.name" -}}
{{- if .Values.querySqlOnFhir.trino.auth.existingSecret.name  -}}
    {{ printf "%s" (tpl .Values.querySqlOnFhir.trino.auth.existingSecret.name $) }}
{{- else -}}
    {{ printf "%s-%s" (include "common.names.fullname" . ) "query-sql-on-fhir-secret" }}
{{- end -}}
{{- end -}}

{{/*
Return the Trino credentials secret key
*/}}
{{- define "recruit.trino.secret.key" -}}
{{- if .Values.querySqlOnFhir.trino.auth.existingSecret.name  -}}
    {{ printf "%s" (tpl .Values.querySqlOnFhir.trino.auth.existingSecret.key $) }}
{{- else -}}
    {{ printf "%s" "trino-password" }}
{{- end -}}
{{- end -}}

{{/*
Return the Trino JDBC URL
*/}}
{{- define "recruit.trino.jdbcUrl" -}}
    {{ tpl .Values.querySqlOnFhir.trino.jdbcUrl $ }}
{{- end -}}

{{/*
Return the Trino password
*/}}
{{- define "recruit.trino.password" -}}
    {{ printf "%s" (tpl .Values.querySqlOnFhir.trino.auth.password $) }}
{{- end -}}
