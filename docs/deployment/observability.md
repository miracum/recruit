# Observability

## Logs

All components log to stdout by default. You can use a log aggregator like [Grafana Loki](https://grafana.com/oss/loki/)
or the [ELK Stack](https://www.elastic.co/elastic-stack) to collect these logs and provide a centralized overview.

## Metrics

All components expose metrics in standard Prometheus format. The query and notification module do so on the
`:8080/actuator/prometheus` endpoint and the list module on `:8080/metrics`.

See [Kubernetes/Metrics](./kubernetes.md#metrics) for how to setup monitoring on Kubernetes.

## Tracing

All modules support distributed tracing using OpenTelemetry.
See <https://github.com/opentracing-contrib/java-spring-jaeger> for the `notify` and `query` module configuration and <https://github.com/miracum/recruit/blob/master/src/list/server/config.js#L8>
and <https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/sdk-environment-variables.md>
for the list module setup.
