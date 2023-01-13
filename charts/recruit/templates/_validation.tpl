{{/*
Validate values
*/}}
{{- define "recruit.validateValues" -}}
{{- if (empty .Values.query.webAPI.url) -}}
query: query.webAPI.url
    External OHDSI OMOP WebAPI URL needs to be set
{{- end -}}
{{- if (empty .Values.query.omop.host) -}}
\n
query: query.omop.host
    External OHDSI OMOP DB host needs to be set
{{- end -}}

{{- if or (empty .Values.list.auth.keycloak.url) -}}
list: list.auth.keycloak.url
    ⚠️ The URL of an external Keycloak instance should be specified. You won't be able to access the screening list.
{{- end -}}
{{- if (empty .Values.list.auth.keycloak.clientId) -}}
\n
list: list.auth.keycloak.clientId
    ⚠️ The client id in Keycloak should be specified. You won't be able to access the screening list.
{{- end -}}
{{- end -}}
