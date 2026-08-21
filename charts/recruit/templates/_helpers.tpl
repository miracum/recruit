{{/* vim: set filetype=mustache: */}}
{{/*
Expand the name of the chart.
*/}}
{{- define "recruit.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{/*
Create a default fully qualified app name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
If release name contains chart name it will be used as a full name.
*/}}
{{- define "recruit.fullname" -}}
{{- if .Values.fullnameOverride -}}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- $name := default .Chart.Name .Values.nameOverride -}}
{{- if contains $name .Release.Name -}}
{{- .Release.Name | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" -}}
{{- end -}}
{{- end -}}
{{- end -}}

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "recruit.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{/*
Common labels
*/}}
{{- define "recruit.labels" -}}
helm.sh/chart: {{ include "recruit.chart" . }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{ include "recruit.matchLabels" . }}
{{- if .Values.extraLabels }}
    {{- toYaml .Values.extraLabels }}
{{- end }}
{{- end -}}

{{/*
Labels to use on deploy.spec.selector.matchLabels and svc.spec.selector
*/}}
{{- define "recruit.matchLabels" -}}
app.kubernetes.io/name: {{ include "recruit.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end -}}

{{/*
Extra labels to apply to every pod
*/}}
{{- define "recruit.podLabels" -}}
{{ include "recruit.matchLabels" . }}
{{- end -}}

{{/*
Create the name of the service account to use.
Expects a dict with "context" (the component's values, e.g. .Values.notify), "component" (e.g.
"notify") and "root" (the chart's root context, i.e. $) - root is needed because "recruit.fullname"
reads .Values/.Chart/.Release from the top level, which isn't reachable from "context" alone.
*/}}
{{- define "recruit.serviceAccountName" -}}
{{- if .context.serviceAccount.create }}
{{- default (printf "%s-%s" (include "recruit.fullname" .root) .component) .context.serviceAccount.name }}
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
Create a default fully qualified mailhog name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
*/}}
{{- define "recruit.mailhog.fullname" -}}
{{- $name := default "mailhog" .Values.mailhog.nameOverride -}}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{/*
Get the SMTP server credentials secret.
*/}}
{{- define "recruit.mail.server.secretName" -}}
{{- if .Values.notify.mail.server.existingSecret -}}
    {{- printf "%s" .Values.notify.mail.server.existingSecret -}}
{{- else -}}
    {{ printf "%s-%s" .Release.Name "smtp" }}
{{- end -}}
{{- end -}}

{{- define "recruit.omop.db.host" -}}
{{- if .Values.ohdsi.enabled -}}
    {{ include "ohdsi.database.host" .Subcharts.ohdsi }}
{{- else -}}
    {{ .Values.query.omop.host }}
{{- end -}}
{{- end -}}

{{- define "recruit.omop.db.port" -}}
{{- if .Values.ohdsi.enabled -}}
    {{ include "ohdsi.database.port" .Subcharts.ohdsi }}
{{- else -}}
    {{ .Values.query.omop.port }}
{{- end -}}
{{- end -}}

{{- define "recruit.omop.db.user" -}}
{{- if .Values.ohdsi.enabled -}}
    {{ include "ohdsi.database.user" .Subcharts.ohdsi }}
{{- else -}}
    {{ .Values.query.omop.username }}
{{- end -}}
{{- end -}}

{{- define "recruit.omop.db.name" -}}
{{- if .Values.ohdsi.enabled -}}
    {{ include "ohdsi.database.name" .Subcharts.ohdsi }}
{{- else -}}
    {{ .Values.query.omop.database }}
{{- end -}}
{{- end -}}

{{/*
Get the OMOP JDBC URL.
*/}}
{{- define "recruit.omop.jdbcUrl" -}}
{{- $host := include "recruit.omop.db.host" . }}
{{- $port := include "recruit.omop.db.port" . | int64 }}
{{- $database := include "recruit.omop.db.name" . }}
{{- $appName := printf "%s-query" (include "recruit.fullname" .) -}}
{{- (printf "jdbc:postgresql://%s:%d/%s?ApplicationName=%s" $host $port $database $appName) | quote -}}
{{- end -}}

{{/*
Get the OHDSI WebAPI URL.
*/}}
{{- define "recruit.omop.webApiUrl" -}}
{{- if .Values.ohdsi.enabled -}}
    {{- if .Values.ohdsi.atlas.webApiUrl -}}
        {{ .Values.ohdsi.atlas.webApiUrl }}
    {{- else -}}
        {{- printf "http://%s-%s:%d/WebAPI" .Release.Name "ohdsi-webapi" (int64 .Values.ohdsi.webApi.service.port) -}}
    {{- end -}}
{{- else -}}
    {{ .Values.query.webAPI.url }}
{{- end -}}
{{- end -}}

{{- define "recruit.utils.joinListWithComma" -}}
{{- $local := dict "first" true -}}
{{- range $k, $v := . -}}{{- if not $local.first -}},{{- end -}}{{- $v -}}{{- $_ := set $local "first" false -}}{{- end -}}
{{- end -}}

{{/*
Return the OMOP credentials secret name
*/}}
{{- define "recruit.omopSecretName" -}}
{{- if .Values.ohdsi.enabled -}}
    {{ include "ohdsi.webapi.db-secret-name" .Subcharts.ohdsi }}
{{- else -}}
    {{- if .Values.query.omop.existingSecret -}}
        {{ printf "%s" (tpl .Values.query.omop.existingSecret $) }}
    {{- else -}}
        {{ printf "%s-%s" (include "recruit.fullname" . ) "query-omop-secret" }}
    {{- end -}}
{{- end -}}
{{- end -}}

{{/*
Return the OMOP credentials secret key
*/}}
{{- define "recruit.omopSecretKey" -}}
{{- if .Values.ohdsi.enabled -}}
    {{ include "ohdsi.webapi.db-secret-key" .Subcharts.ohdsi }}
{{- else -}}
    {{ printf "%s" "omop-password" }}
{{- end -}}
{{- end -}}

{{/*
Return the OMOP DB password when not using an existing secret
*/}}
{{- define "recruit.omop.password" -}}
{{- if .Values.ohdsi.enabled -}}
    {{ .Values.ohdsi.webApi.db.password}}
{{- else -}}
    {{ .Values.query.omop.password }}
{{- end -}}
{{- end -}}

{{- define "recruit.webApiSecretName" -}}
{{- if .Values.query.webAPI.auth.existingSecret.name -}}
    {{ printf "%s" (tpl .Values.query.webAPI.auth.existingSecret.name $) }}
{{- else -}}
    {{ printf "%s-%s" .Release.Name "query-webapi-secret" }}
{{- end -}}
{{- end -}}

{{- define "recruit.webApiSecretKey" -}}
{{- if .Values.query.webAPI.auth.existingSecret.key -}}
    {{ printf "%s" (tpl .Values.query.webAPI.auth.existingSecret.key $) }}
{{- else -}}
    {{ printf "%s" "webApiAuthPassword" }}
{{- end -}}
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
kubectl image used by the wait-for-migration initContainer to poll the list-next migration Job's
status.
*/}}
{{- define "recruit.kubectl.image" -}}
{{- $registry := .Values.kubectl.image.registry -}}
{{- $repository := .Values.kubectl.image.repository -}}
{{- $tag := .Values.kubectl.image.tag -}}
{{ printf "%s/%s:%s" $registry $repository $tag}}
{{- end -}}

{{/*
Name of the list-next DB-migration Job, derived from the list-next image tag (not
.Release.Revision - unreliable under GitOps rendering via `helm template`, since GitOps
controllers don't track real Helm release revisions). Same tag -> same name -> idempotent
re-syncs; changed tag -> new name -> new Job, with the old one pruned by the GitOps controller (or
via ttlSecondsAfterFinished as a fallback). Reserves the trailing 17 characters ("-migrate-" + 8
hex chars) so the hash suffix is never truncated away. Job names are capped at 63 characters by
Kubernetes - they back the "job-name" pod label, whose value is limited by K8s label-value rules.
*/}}
{{- define "recruit.listNext.migrationJobName" -}}
{{- $prefix := printf "%s-list-next" (include "recruit.fullname" .) | trunc 46 | trimSuffix "-" -}}
{{- printf "%s-migrate-%s" $prefix (.Values.listNext.image.tag | sha256sum | trunc 8) -}}
{{- end -}}

{{/*
Create a default fully qualified postgresql name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
*/}}
{{- define "recruit.postgresql.fullname" -}}
{{- $name := default "postgres" .Values.postgres.nameOverride -}}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" -}}
{{- end -}}


{{/*
Return the Trino credentials secret name
*/}}
{{- define "recruit.trino.secret.name" -}}
{{- if .Values.querySqlOnFhir.trino.auth.existingSecret.name  -}}
    {{ printf "%s" (tpl .Values.querySqlOnFhir.trino.auth.existingSecret.name $) }}
{{- else -}}
    {{ printf "%s-%s" (include "recruit.fullname" . ) "query-sql-on-fhir-secret" }}
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
Return the Trino password
*/}}
{{- define "recruit.trino.password" -}}
    {{ printf "%s" (tpl .Values.querySqlOnFhir.trino.auth.password $) }}
{{- end -}}
