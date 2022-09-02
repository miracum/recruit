{{/* vim: set filetype=mustache: */}}

{{/*
Get the notification module's endpoint for the webhook
*/}}
{{- define "recruit.notify.webhookEndpointUrl" -}}
{{- if .Values.notify.ingress.enabled -}}
    {{- $host := (index .Values.notify.ingress.hosts 0).host }}
    {{- $protocol := (empty .Values.notify.ingress.tls) | ternary "http" "https" }}
    {{- printf "%s://%s/on-list-change" $protocol $host -}}
{{- else -}}
    {{- $host := (printf "%s-%s" (include "recruit.fullname" .) "notify") -}}
    {{- printf "http://%s:%d/on-list-change" $host (.Values.notify.service.port | int64) -}}
{{- end -}}
{{- end -}}

{{/*
Get the screening list link template
*/}}
{{- define "recruit.notify.mail.screeningListLinkTemplate" -}}
{{- if .Values.list.ingress.enabled -}}
    {{- $host := (index .Values.list.ingress.hosts 0).host }}
    {{- $protocol := (empty .Values.list.ingress.tls) | ternary "http" "https" }}
    {{- printf "%s://%s/recommendations/[list_id]" $protocol $host -}}
{{- else -}}
    {{- .Values.notify.mail.screeningListLinkTemplate | quote -}}
{{- end -}}
{{- end -}}

{{- define "recruit.notify.database.host" -}}
{{- if .Values.postgresql.enabled -}}
    {{ printf "%s-%s" .Release.Name .Values.postgresql.nameOverride }}
{{- else -}}
    {{ .Values.notify.ha.database.host }}
{{- end -}}
{{- end -}}

{{- define "recruit.notify.database.port" -}}
{{- ternary "5432" .Values.notify.ha.database.port .Values.postgresql.enabled -}}
{{- end -}}

{{- define "recruit.notify.database.username" -}}
{{- if .Values.postgresql.enabled -}}
    {{- if .Values.postgresql.auth.username -}}
        {{ .Values.postgresql.auth.username | quote }}
    {{- else -}}
        {{ "postgres" }}
    {{- end -}}
{{- else -}}
    {{ .Values.notify.ha.database.username }}
{{- end -}}
{{- end -}}

{{- define "recruit.notify.database.name" -}}
{{- ternary .Values.postgresql.auth.database .Values.notify.ha.database.name .Values.postgresql.enabled -}}
{{- end -}}

{{/*
Create the JDBC URL from the host, port and database name.
*/}}
{{- define "recruit.notify.database.jdbcUrl" -}}
{{- $host := (include "recruit.notify.database.host" .) -}}
{{- $port := (include "recruit.notify.database.port" .) -}}
{{- $name := (include "recruit.notify.database.name" .) -}}
{{- $appName := printf "%s-notify" (include "recruit.fullname" .) -}}
{{ printf "jdbc:postgresql://%s:%d/%s?ApplicationName=%s" $host (int $port) $name $appName}}
{{- end -}}

{{/*
Get the name of the secret containing the DB password
*/}}
{{- define "recruit.notify.database.secretName" -}}
{{- if .Values.postgresql.enabled -}}
    {{- if .Values.postgresql.auth.existingSecret -}}
        {{ .Values.postgresql.auth.existingSecret | quote }}
    {{- else -}}
        {{ ( include "recruit.postgresql.fullname" . ) }}
    {{- end -}}
{{- else if .Values.notify.ha.database.existingSecret.name -}}
    {{ .Values.notify.ha.database.existingSecret.name | quote }}
{{- else -}}
    {{- $fullname := ( include "recruit.fullname" . ) -}}
    {{ printf "%s-%s" $fullname "notify-ha-db" }}
{{- end -}}
{{- end -}}

{{/*
Get the key inside the secret containing the DB user's password
*/}}
{{- define "recruit.notify.database.secretKey" -}}
{{- if .Values.postgresql.enabled -}}
    {{- if (or .Values.postgresql.auth.username .Values.postgresql.auth.existingSecret ) -}}
        {{ "password" }}
    {{- else -}}
        {{ "postgres-password" }}
    {{- end -}}
{{- else if .Values.notify.ha.database.existingSecret.key -}}
    {{ .Values.notify.ha.database.existingSecret.key | quote }}
{{- else -}}
    {{ "postgres-password" }}
{{- end -}}
{{- end -}}
