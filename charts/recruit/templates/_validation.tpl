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
{{- end -}}
