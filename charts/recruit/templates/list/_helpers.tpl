{{/* vim: set filetype=mustache: */}}

{{/*
Get the name of the secret containing list's OIDC client secret
*/}}
{{- define "recruit.list.oidc.secretName" -}}
{{- if .Values.list.auth.oidc.existingSecret.name -}}
    {{ .Values.list.auth.oidc.existingSecret.name | quote }}
{{- else -}}
    {{ printf "%s-%s" .Release.Name "list-oidc" | quote }}
{{- end -}}
{{- end -}}

{{/*
Get the key inside the secret containing list's OIDC client secret
*/}}
{{- define "recruit.list.oidc.secretKey" -}}
{{- if .Values.list.auth.oidc.existingSecret.name -}}
    {{ .Values.list.auth.oidc.existingSecret.key | quote }}
{{- else -}}
    {{ "oidc-client-secret" | quote }}
{{- end -}}
{{- end -}}

{{/*
Get the name of the secret containing list's SMTP password
*/}}
{{- define "recruit.list.mail.secretName" -}}
{{- if .Values.list.notifyMailer.smtp.existingSecret -}}
    {{ .Values.list.notifyMailer.smtp.existingSecret | quote }}
{{- else -}}
    {{ printf "%s-%s" .Release.Name "list-smtp" | quote }}
{{- end -}}
{{- end -}}

{{/*
True if list should use the included PostgreSQL database, i.e. `postgres.enabled=true` and no
explicit `list.database.connectionString` is set
*/}}
{{- define "recruit.list.database.useIncludedPostgres" -}}
{{- if and .Values.postgres.enabled (not .Values.list.database.connectionString) -}}
    {{ true }}
{{- end -}}
{{- end -}}

{{/*
Get list's database connection string
*/}}
{{- define "recruit.list.database.connectionString" -}}
{{- if (include "recruit.list.database.useIncludedPostgres" .) -}}
    {{ printf "Host=%s;Port=5432;Database=%s" (include "recruit.postgresql.fullname" .) .Values.postgres.auth.database | quote }}
{{- else if .Values.list.database.connectionString -}}
    {{ .Values.list.database.connectionString | quote }}
{{- else -}}
    {{- fail "list requires a database: either set `postgres.enabled=true` or `list.database.connectionString`" -}}
{{- end -}}
{{- end -}}

{{/*
Get the username used to connect to list's database
*/}}
{{- define "recruit.list.database.username" -}}
{{- if (include "recruit.list.database.useIncludedPostgres" .) -}}
    {{ "postgres" }}
{{- else -}}
    {{ .Values.list.database.username }}
{{- end -}}
{{- end -}}

{{/*
Get the name of the secret containing list's database password
*/}}
{{- define "recruit.list.database.secretName" -}}
{{- if (include "recruit.list.database.useIncludedPostgres" .) -}}
    {{- if .Values.postgres.auth.existingSecret -}}
        {{ .Values.postgres.auth.existingSecret | quote }}
    {{- else -}}
        {{ include "recruit.postgresql.fullname" . | quote }}
    {{- end -}}
{{- else if .Values.list.database.existingSecret.name -}}
    {{ .Values.list.database.existingSecret.name | quote }}
{{- else -}}
    {{ printf "%s-%s" .Release.Name "list-db" | quote }}
{{- end -}}
{{- end -}}

{{/*
Get the key inside the secret containing list's database password
*/}}
{{- define "recruit.list.database.secretKey" -}}
{{- if (include "recruit.list.database.useIncludedPostgres" .) -}}
    {{- if .Values.postgres.auth.existingSecret -}}
        {{ default "postgres-password" .Values.postgres.auth.secretKeys.adminPasswordKey | quote }}
    {{- else -}}
        {{ "postgres-password" | quote }}
    {{- end -}}
{{- else if .Values.list.database.existingSecret.name -}}
    {{ .Values.list.database.existingSecret.key | quote }}
{{- else -}}
    {{ "db-password" | quote }}
{{- end -}}
{{- end -}}

{{/*
Get the screening list link template used in list's notification emails
*/}}
{{- define "recruit.list.mail.screeningListLinkTemplate" -}}
{{- if .Values.list.ingress.enabled -}}
    {{- $host := (index .Values.list.ingress.hosts 0).host }}
    {{- $protocol := (empty .Values.list.ingress.tls) | ternary "http" "https" }}
    {{- printf "%s://%s/recommendations/{0}" $protocol $host -}}
{{- else -}}
    {{- .Values.list.notifyMailer.screeningListLinkTemplate | quote -}}
{{- end -}}
{{- end -}}
