{{/* vim: set filetype=mustache: */}}

{{/*
Get the name of the secret containing the FHIR Pseudonymizer API key
*/}}
{{- define "recruit.list.fhirPseudonymizer.apiKey.secretName" -}}
{{- if (index .Values "fhir-pseudonymizer" "enabled") -}}
    {{ include "fhir-pseudonymizer.fhirPseudonymizerApiKeySecretName" (index .Subcharts "fhir-pseudonymizer") }}
{{- else if .Values.list.dePseudonymization.existingApiKeySecret.name -}}
    {{ .Values.list.dePseudonymization.existingApiKeySecret.name | quote }}
{{- else -}}
    {{ include "recruit.fullname" . }}-list-fhir-pseudonymizer-api-key
{{- end -}}
{{- end -}}

{{/*
Get the name of the key inside the secret containing the FHIR Pseudonymizer API key
*/}}
{{- define "recruit.list.fhirPseudonymizer.apiKey.secretKey" -}}
{{- if (index .Values "fhir-pseudonymizer" "enabled") -}}
    {{ "APIKEY" }}
{{- else if .Values.list.dePseudonymization.existingApiKeySecret.name -}}
    {{ .Values.list.dePseudonymization.existingApiKeySecret.key | quote }}
{{- else -}}
    {{ "fhir-pseudonymizer-api-key" }}
{{- end -}}
{{- end -}}

{{/*
Get the FHIR pseudonymizer URL
*/}}
{{- define "recruit.list.fhirPseudonymizer.serviceUrl" -}}
{{- if (index .Values "fhir-pseudonymizer" "enabled") -}}
    {{- printf "http://%s:%d/fhir" (include "fhir-pseudonymizer.fullname" (index .Subcharts "fhir-pseudonymizer")) 8080 -}}
{{- else -}}
    {{ .Values.list.dePseudonymization.serviceUrl | quote }}
{{- end -}}
{{- end -}}

{{/*
True if the FHIR pseudonymizer has an API key configured
*/}}
{{- define "recruit.list.fhirPseudonymizer.isApiKeyAuthEnabled" -}}
{{- if (index .Values "fhir-pseudonymizer" "enabled") -}}
    {{- (include "fhir-pseudonymizer.isApiKeyAuthEnabled" (index .Subcharts "fhir-pseudonymizer")) -}}
{{- else -}}
    {{ true }}
{{- end -}}
{{- end -}}
