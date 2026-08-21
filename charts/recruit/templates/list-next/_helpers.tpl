{{/* vim: set filetype=mustache: */}}

{{/*
Get the name of the secret containing list-next's OIDC client secret
*/}}
{{- define "recruit.listNext.oidc.secretName" -}}
{{- if .Values.listNext.auth.oidc.existingSecret.name -}}
    {{ .Values.listNext.auth.oidc.existingSecret.name | quote }}
{{- else -}}
    {{ printf "%s-%s" .Release.Name "list-next-oidc" | quote }}
{{- end -}}
{{- end -}}

{{/*
Get the key inside the secret containing list-next's OIDC client secret
*/}}
{{- define "recruit.listNext.oidc.secretKey" -}}
{{- if .Values.listNext.auth.oidc.existingSecret.name -}}
    {{ .Values.listNext.auth.oidc.existingSecret.key | quote }}
{{- else -}}
    {{ "oidc-client-secret" | quote }}
{{- end -}}
{{- end -}}

{{/*
Get the name of the secret containing list-next's SMTP password
*/}}
{{- define "recruit.listNext.mail.secretName" -}}
{{- if .Values.listNext.notifyMailer.smtp.existingSecret -}}
    {{ .Values.listNext.notifyMailer.smtp.existingSecret | quote }}
{{- else -}}
    {{ printf "%s-%s" .Release.Name "list-next-smtp" | quote }}
{{- end -}}
{{- end -}}

{{/*
Get the screening list link template used in list-next's notification emails
*/}}
{{- define "recruit.listNext.mail.screeningListLinkTemplate" -}}
{{- if .Values.listNext.ingress.enabled -}}
    {{- $host := (index .Values.listNext.ingress.hosts 0).host }}
    {{- $protocol := (empty .Values.listNext.ingress.tls) | ternary "http" "https" }}
    {{- printf "%s://%s/recommendations/{0}" $protocol $host -}}
{{- else -}}
    {{- .Values.listNext.notifyMailer.screeningListLinkTemplate | quote -}}
{{- end -}}
{{- end -}}
