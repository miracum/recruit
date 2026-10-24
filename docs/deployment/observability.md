# Observability

## Logs

All components log to stdout by default. You can use a log aggregator like [Grafana Loki](https://grafana.com/oss/loki/)
or the [ELK Stack](https://www.elastic.co/elastic-stack) to collect these logs and provide a centralized overview.

## Metrics

All components expose metrics in Prometheus format: the screening list (`list`) at `:8081/metrics`, and
`query-sql-on-fhir` at `/actuator/prometheus` on its management port (`:8081` in the Helm chart, the same port as the
app itself otherwise). Run as a one-shot job, `query-sql-on-fhir` can push them to a Pushgateway instead, see
[Metrics for one-shot runs](./kubernetes.md#metrics-for-one-shot-runs).

Besides the usual HTTP, runtime and database metrics, both report what recruIT itself is doing:

| Metric                                              | Module            | What it measures                                                                                |
| --------------------------------------------------- | ----------------- | ----------------------------------------------------------------------------------------------- |
| `recruit_notifications_recommendations_total`       | list              | Newly recommended patients the notification detector found, by `trial`                          |
| `recruit_notifications_detection_duration_seconds`  | list              | How long one detector run took, checking every screening list, by `outcome`                     |
| `recruit_notifications_detection_failures_total`    | list              | Screening lists the detector couldn't check, retried on its next run                            |
| `recruit_notifications_email_send_duration_seconds` | list              | How long sending one notification email took, by `outcome`                                      |
| `recruit_notifications_email_pending`               | list              | Notification emails not sent yet, waiting for their digest or for a retry                       |
| `recruit_notifications_email_dropped_total`         | list              | Notification emails given up on after failing to send for 30 days                               |
| `recruit_screening_status_changes_total`            | list              | Recruitment status changes saved by users, by the new `status`                                  |
| `recruit_query_poll_seconds`                        | query-sql-on-fhir | How long one poll took, evaluating every study, by `outcome`                                    |
| `recruit_query_study_seconds`                       | query-sql-on-fhir | How long evaluating one study's criteria and writing its results took, by `study` and `outcome` |
| `recruit_query_poll_last_success_timestamp_seconds` | query-sql-on-fhir | When the last poll without any failing study finished, as a Unix timestamp                      |
| `recruit_query_study_candidates`                    | query-sql-on-fhir | Patients on a study's screening list after its latest evaluation, by `study`                    |

The screening list also reports Blazor's own metrics, such as open sessions (`aspnetcore_components_circuit_*`) and
how long handling a user's click took (`aspnetcore_components_handle_event_duration_seconds`). Its requests to the
FHIR server are part of `http_client_request_duration_seconds`, and the query module's are
`fhir_client_seconds`.

The Helm chart includes a Grafana dashboard that brings all of this together, see
[Kubernetes/Metrics](./kubernetes.md#metrics). For other setups, import
`charts/recruit/files/dashboards/recruit.json` from the repository into Grafana.

## Tracing

All modules support distributed tracing using OpenTelemetry.
See <https://github.com/opentracing-contrib/java-spring-jaeger> for the `notify` and `query` module configuration and <https://github.com/miracum/recruit/blob/master/src/list/server/config.js#L8>
and <https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/configuration/sdk-environment-variables.md>
for the list module setup.
