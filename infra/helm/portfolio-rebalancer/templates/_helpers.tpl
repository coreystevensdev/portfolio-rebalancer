{{/*
Expand the name of the chart.
*/}}
{{- define "portfolio-rebalancer.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
*/}}
{{- define "portfolio-rebalancer.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{/*
Common labels.
*/}}
{{- define "portfolio-rebalancer.labels" -}}
helm.sh/chart: {{ include "portfolio-rebalancer.name" . }}-{{ .Chart.Version | replace "+" "_" }}
{{ include "portfolio-rebalancer.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
Selector labels.
*/}}
{{- define "portfolio-rebalancer.selectorLabels" -}}
app.kubernetes.io/name: {{ include "portfolio-rebalancer.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}
