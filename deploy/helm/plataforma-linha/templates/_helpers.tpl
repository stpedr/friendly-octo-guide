{{- /*
Pod template compartilhado entre Deployment (padrão) e Rollout (canary): a forma
do pod é a mesma, só a estratégia de rollout muda. Recebe (dict "name" .. "svc" .. "root" ..).
*/ -}}
{{- define "plataforma-linha.podTemplate" -}}
metadata:
  labels: { app: {{ .name }} }
  {{- if .root.Values.mesh.enabled }}
  annotations:
    linkerd.io/inject: enabled
  {{- end }}
spec:
  containers:
    - name: {{ .name }}
      image: "{{ .root.Values.image.registry }}/{{ .name }}:{{ .root.Values.image.tag }}"
      {{- if .svc.port }}
      ports: [{ containerPort: {{ .svc.port }} }]
      readinessProbe:
        httpGet: { path: /healthz, port: {{ .svc.port }} }
      {{- end }}
      env:
        - name: OTEL_EXPORTER_OTLP_ENDPOINT
          value: {{ .root.Values.otelEndpoint | quote }}
        {{- range $key, $value := .svc.env }}
        - name: {{ $key }}
          value: {{ $value | quote }}
        {{- end }}
      resources:
        {{- toYaml .svc.resources | nindent 8 }}
{{- end -}}
