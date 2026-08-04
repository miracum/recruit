# Observability

## Logs

All components log to stdout by default. You can use a log aggregator like [Grafana Loki](https://grafana.com/oss/loki/)
or the [ELK Stack](https://www.elastic.co/elastic-stack) to collect these logs and provide a centralized overview.

## Metrics

The query and notification module expose metrics in standard Prometheus format on the `:8080/actuator/prometheus`
endpoint. The list module (a Blazor Server app) does not currently expose a `/metrics` endpoint or support
OpenTelemetry tracing; its `list.metrics.serviceMonitor` Helm value has no effect until that's added.

See [Kubernetes/Metrics](./kubernetes.md#metrics) for how to setup monitoring on Kubernetes.

## Tracing

The `notify` and `query` modules support distributed tracing using OpenTelemetry.
See <https://github.com/opentracing-contrib/java-spring-jaeger> for their configuration.
