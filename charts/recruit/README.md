# recruIT Helm Chart

![Type: application](https://img.shields.io/badge/Type-application-informational?style=flat-square)

This chart deploys the recruIT clinical trial recruitment support system on a [Kubernetes](http://kubernetes.io) cluster using the [Helm](https://helm.sh) package manager.

## Prerequisites

- Kubernetes v1.21+
- Helm v3.8+

## Upgrades & Breaking Changes

See [UPGRADING.md](./docs/UPGRADING.md) for information on breaking changes introduced by major version bumps and instructions on how to update.

## Sample usage

```sh
helm install recruit oci://ghcr.io/miracum/recruit/charts/recruit -n recruit
```

## Values

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| broadseaAtlasdb.enabled | bool | `false` | whether to deploy the OHDSI Broadsea Atlasdb (<https://github.com/OHDSI/Broadsea-Atlasdb>) currently only used by internal integration tests. See [./values-integrationtest.yaml](values-integrationtest.yaml) |
| createSearchParameters.backoffLimit | int | `3` | number of retries for the Job before it is considered failed |
| createSearchParameters.enabled | bool | `true` | whether to install/update the SearchParameter resources on every `helm upgrade`. Only takes effect if `fhirserver.enabled=true` or `externalFhirServer.url` is set. |
| createSearchParameters.podAnnotations | object | `{}` | annotations to add to the Job's pod |
| createSearchParameters.reindex | bool | `false` | whether to also trigger a FHIR `$reindex` operation on the server after installing/updating the SearchParameter resources, so that resources already stored on the server get indexed using the new search parameters. This can be a slow, resource-intensive operation depending on the amount of data already stored, so it is disabled by default. |
| createSearchParameters.resources | object | `{}` | configure the job pod's resource requests and limits |
| createSearchParameters.resourcesPreset | string | `"nano"` | set container resources according to one common preset (allowed values: none, nano, micro, small, medium, large, xlarge, 2xlarge). This is ignored if `resources` is set. More information: <https://github.com/miracum/charts/blob/master/charts/common/templates/_resources.tpl#L1> |
| createSearchParameters.ttlSecondsAfterFinished | int | `600` | automatically delete the Job this many seconds after it finished. Set to `null` to disable. |
| deploymentAnnotations | object | `{}` | annotations to set on all Deployment resources |
| externalFhirServer.url | string | `""` | URL of an external FHIR Server - only used when `fhirserver.enabled` is set to `false` |
| extraLabels | object | `{}` | additionally labels to apply to all deployments |
| fhir-pseudonymizer | object | `{"enabled":false,"pseudonymizationService":"Vfps","vfps":{"enabled":true}}` | configuration for the optional fhir-pseudonymizer dependency |
| fhir-pseudonymizer.enabled | bool | `false` | install the included fhir-pseudonymizer chart. If set to `true`, the list module is auto-configured to use this service. |
| fhir-pseudonymizer.pseudonymizationService | string | `"Vfps"` | default to using Vfps as a pseudonym service. It is included as a dependency of the fhir-pseudonymizer. |
| fhir-pseudonymizer.vfps.enabled | bool | `true` | enable the included Vfps service. |
| fhirserver.enabled | bool | `true` | Whether the included HAPI FHIR server should be used See <https://github.com/hapifhir/hapi-fhir-jpaserver-starter/tree/master/charts/hapi-fhir-jpaserver#values> for options. |
| fhirserver.extraEnv | list | `[{"name":"HAPI_FHIR_SUBSCRIPTION_RESTHOOK_ENABLED","value":"true"},{"name":"SPRING_FLYWAY_BASELINE_ON_MIGRATE","value":"true"}]` | extra environment variables set for the HAPI FHIR server |
| fhirserver.postgres.nameOverride | string | `"fhir-server-postgres"` | overrides the chart's postgres server name to avoid conflicts with the included ohdsi chart |
| fullnameOverride | string | `""` | fully override the release name |
| imagePullSecrets | list | `[]` | image pull secrets used by all pods |
| kubectl.image.registry | string | `"docker.io"` |  |
| kubectl.image.repository | string | `"rancher/kubectl"` |  |
| kubectl.image.tag | string | `"v1.34.9@sha256:caffc74e47cf962edef931bc3c5eff73032a19d0487253b261c3c75cb3cf8e9e"` |  |
| list.affinity | object | `{}` | affinity for pod assignment see: <https://kubernetes.io/docs/concepts/configuration/assign-pod-node/#affinity-and-anti-affinity> |
| list.auth.enabled | bool | `false` | if enabled, requires authentication before accessing the screening list |
| list.auth.keycloak.clientId | string | `"uc1-screeninglist"` | the Keycloak client id |
| list.auth.keycloak.realm | string | `"MIRACUM"` | the Keycloak realm |
| list.auth.keycloak.url | string | `"http://localhost:8083"` | the Keycloak auth URL. For Keycloak 17+, this is the base URL without a `/auth` suffix (unless the server has `KC_HTTP_RELATIVE_PATH=/auth` configured for backwards compatibility). |
| list.dePseudonymization.apiKey | string | `""` | the API key to invoke the FHIR pseudonymizer |
| list.dePseudonymization.enabled | bool | `false` | if enabled, all FHIR resource will first be processed by the fhir-pseudonymizer for de-pseudonymization before being displayed. |
| list.dePseudonymization.existingApiKeySecret | object | `{"key":"fhir-pseudonymizer-api-key","name":""}` | use an existing Secret resource that contains the API key |
| list.dePseudonymization.existingApiKeySecret.key | string | `"fhir-pseudonymizer-api-key"` | key of the Kubernetes Secret that contains the API key |
| list.dePseudonymization.existingApiKeySecret.name | string | `""` | name of an existing Kubernetes Secret that contains the API key. Leave empty to use `dePseudonymization.apiKey` instead. |
| list.dePseudonymization.serviceUrl | string | `"http://fhir-pseudonymizer:8080/fhir"` | API base URL of the FHIR pseudonymizer to use. If `fhir-pseudonymizer.enabled` is set to `true`, this URL will be auto-configured. |
| list.enabled | bool | `true` | Whether the list module should be enabled |
| list.extraEnv | list | `[]` | specify extra environment vars on the container |
| list.extraPodLabels | object | `{}` | specify additional labels to apply to the list pod |
| list.ingress.annotations | object | `{}` | additional annotations for the Ingress resource |
| list.ingress.enabled | bool | `false` | if enabled, create an ingress resource to access the screening list |
| list.ingress.hosts[0].host | string | `"list.127.0.0.1.nip.io"` |  |
| list.ingress.hosts[0].paths[0] | string | `"/"` |  |
| list.ingress.ingressClassName | string | `""` | name of the IngressClass resource to use for this ingress |
| list.ingress.tls | list | `[]` | TLS configuration |
| list.metrics.serviceMonitor.additionalLabels | object | `{}` | additional labels |
| list.metrics.serviceMonitor.enabled | bool | `false` | if enabled, creates a ServiceMonitor instance for Prometheus Operator-based monitoring |
| list.nodeSelector | object | `{}` | node labels for pod assignment see: <https://kubernetes.io/docs/concepts/scheduling-eviction/assign-pod-node/> |
| list.podDisruptionBudget.enabled | bool | `false` | create a PodDisruptionBudget resource |
| list.podDisruptionBudget.maxUnavailable | string | `""` | Maximum unavailable instances; ignored if there is no PodDisruptionBudget |
| list.podDisruptionBudget.minAvailable | int | `1` | Minimum available instances; ignored if there is no PodDisruptionBudget |
| list.podSecurityContext | object | `{}` | security context for the pod |
| list.replicaCount | int | `1` | Number of replicas for the list module. A fault-tolerant number of replicas is recommended. |
| list.resources | object | `{}` | resource requests and limits for the container |
| list.resourcesPreset | string | `"micro"` | set container resources according to one common preset (allowed values: none, nano, micro, small, medium, large, xlarge, 2xlarge). This is ignored if primary.resources is set (primary.resources is recommended for production). More information: <https://github.com/miracum/charts/blob/master/charts/common/templates/_resources.tpl#L1> |
| list.revisionHistoryLimit | int | `5` | specify how many old ReplicaSets for this Deployment you want to retain. |
| list.service | object | `{"port":8080,"type":"ClusterIP"}` | the service used to expose the list module web port |
| list.service.port | int | `8080` | service port |
| list.service.type | string | `"ClusterIP"` | service type |
| list.serviceAccount.annotations | object | `{}` | Annotations to add to the service account |
| list.serviceAccount.automountServiceAccountToken | bool | `false` | whether to automount the SA token. |
| list.serviceAccount.create | bool | `false` | Specifies whether a service account should be created. |
| list.serviceAccount.name | string | `""` | The name of the service account to use. If not set and create is true, a name is generated using the fullname template |
| list.tolerations | list | `[]` | tolerations for pod assignment see: <https://kubernetes.io/docs/concepts/configuration/taint-and-toleration/> |
| list.topologySpreadConstraints | list | `[]` | pod topology spread configuration see: <https://kubernetes.io/docs/concepts/workloads/pods/pod-topology-spread-constraints/#api> |
| listNext.affinity | object | `{}` | affinity for pod assignment see: <https://kubernetes.io/docs/concepts/configuration/assign-pod-node/#affinity-and-anti-affinity> |
| listNext.auth.enabled | bool | `false` | if enabled, requires OIDC authentication before accessing list-next. If disabled, every visitor is treated as an authenticated admin - only safe for local development/testing. |
| listNext.auth.oidc.authority | string | `""` | the OIDC authority/issuer URL |
| listNext.auth.oidc.clientId | string | `""` | the OIDC client id |
| listNext.auth.oidc.clientSecret | string | `""` | the OIDC client secret. Ignored if `existingSecret.name` is set |
| listNext.auth.oidc.existingSecret.key | string | `"oidc-client-secret"` | key inside the existing secret |
| listNext.auth.oidc.existingSecret.name | string | `""` | name of an existing Kubernetes Secret containing the OIDC client secret |
| listNext.auth.oidc.roleClaimType | string | `"role"` | the claim type used to map roles from the ID token, e.g. `role` or `realm_access.roles` |
| listNext.database.connectionString | string | `""` | Npgsql connection string for list-next's own data (trial access grants, notification poll cursors, and the Hangfire job store backing its notification poller), e.g. `Host=postgres;Port=5432;Database=list_next`. Omit credentials here and set them via `username`/`password` below instead - they are injected as `PGUSER`/`PGPASSWORD` env vars, which Npgsql falls back to for any credentials missing from the connection string. |
| listNext.database.existingSecret.key | string | `"db-password"` | the key inside the existing secret containing the password |
| listNext.database.existingSecret.name | string | `""` | name of an existing Kubernetes secret that contains the database password |
| listNext.database.password | string | `""` | the password used to connect to the database. Injected as the `PGPASSWORD` env var. Ignored if `existingSecret.name` is set. |
| listNext.database.username | string | `""` | the username to connect to the database as. Injected as the `PGUSER` env var, in both the Deployment and the migration Job. Leave empty to omit `PGUSER`/`PGPASSWORD` entirely. |
| listNext.enabled | bool | `false` | Whether the list-next module should be enabled. list-next is the .NET-based successor to the list module; it is a separate deployment with its own database and is not a drop-in replacement yet, so both can run side-by-side during the migration. |
| listNext.extraEnv | list | `[]` | specify extra environment vars on the container |
| listNext.extraPodLabels | object | `{}` | specify additional labels to apply to the list-next pod |
| listNext.extraVolumeMounts | list | `[]` | extra volumes to mount inside the container |
| listNext.extraVolumes | list | `[]` | extra volumes |
| listNext.ingress.annotations | object | `{}` | additional annotations for the Ingress resource |
| listNext.ingress.enabled | bool | `false` | if enabled, create an ingress resource to access list-next |
| listNext.ingress.hosts[0].host | string | `"list-next.127.0.0.1.nip.io"` |  |
| listNext.ingress.hosts[0].paths[0] | string | `"/"` |  |
| listNext.ingress.ingressClassName | string | `""` | name of the IngressClass resource to use for this ingress |
| listNext.ingress.tls | list | `[]` | TLS configuration |
| listNext.metrics.serviceMonitor.additionalLabels | object | `{}` | additional labels |
| listNext.metrics.serviceMonitor.enabled | bool | `false` | if enabled, creates a ServiceMonitor instance for Prometheus Operator-based monitoring |
| listNext.migrationJob | object | `{"backoffLimit":3,"podAnnotations":{},"resources":{},"resourcesPreset":"small","ttlSecondsAfterFinished":600,"waitMaxAttempts":60,"waitPollIntervalSeconds":10}` | settings for the Job that runs list-next's DB migrations once per image tag, ahead of the Deployment's pods (see the wait-for-migration initContainer below). |
| listNext.migrationJob.backoffLimit | int | `3` | number of retries for the migration Job before it is considered failed |
| listNext.migrationJob.podAnnotations | object | `{}` | annotations to add to the migration Job's pod |
| listNext.migrationJob.resources | object | `{}` | resource requests and limits for the migration Job container |
| listNext.migrationJob.resourcesPreset | string | `"small"` | set the migration Job container's resources according to one common preset (allowed values: none, nano, micro, small, medium, large, xlarge, 2xlarge). Ignored if `resources` is set. |
| listNext.migrationJob.ttlSecondsAfterFinished | int | `600` | delete the migration Job this many seconds after it finishes; set to `null` to disable. Acts as a safety net if the GitOps controller doesn't prune superseded Jobs - a new image tag always produces a differently-named Job, so old ones would otherwise accumulate. |
| listNext.migrationJob.waitMaxAttempts | int | `60` | number of attempts (waitPollIntervalSeconds apart) the wait-for-migration initContainer polls the migration Job before giving up and failing the pod. The default (60 * 10s = 10 min) mirrors the grace period the old startupProbe gave in-process migrations. |
| listNext.migrationJob.waitPollIntervalSeconds | int | `10` | seconds between wait-for-migration polls of the migration Job's status |
| listNext.nodeSelector | object | `{}` | node labels for pod assignment see: <https://kubernetes.io/docs/concepts/scheduling-eviction/assign-pod-node/> |
| listNext.notifications.newSuggestionWindowDays | int | `7` | a screening recommendation is highlighted as new if it first appeared within this many days |
| listNext.notifications.scanIntervalSeconds | int | `60` | how often, in seconds, list-next scans for screening list changes to notify about |
| listNext.notifications.stalledLeadWindowDays | int | `14` | a pending recommendation is highlighted as stalled if untouched for this many days |
| listNext.notifyMailer.from | string | `"recruit@example.com"` | the sender email address for notification mails |
| listNext.notifyMailer.screeningListLinkTemplate | string | `"http://localhost:8080/recommendations/{0}"` | Used to link back to list-next from a notification email. If `listNext.ingress.enabled` is `true`, the ingress host is used instead. A composite format string (see .NET's String.Format) with a single `{0}` placeholder for the list id. |
| listNext.notifyMailer.smtp.existingSecret | string | `""` | name of an existing secret containing an `smtp-password` key to use instead of the password above |
| listNext.notifyMailer.smtp.host | string | `""` | hostname of the external SMTP mail server. Only used if `mailhog.enabled` is `false` |
| listNext.notifyMailer.smtp.password | string | `""` | mailserver password |
| listNext.notifyMailer.smtp.port | int | `25` | mailserver port |
| listNext.notifyMailer.smtp.username | string | `""` | mailserver username |
| listNext.notifyMailer.subjectTemplate | string | `"MIRACUM Rekrutierungsunterstützung: neue Vorschläge für die Studie {0}"` | subject line template, a composite format string (see .NET's String.Format) with a single `{0}` placeholder for the study acronym |
| listNext.podDisruptionBudget.enabled | bool | `false` | create a PodDisruptionBudget resource |
| listNext.podDisruptionBudget.maxUnavailable | string | `""` | Maximum unavailable instances; ignored if there is no PodDisruptionBudget |
| listNext.podDisruptionBudget.minAvailable | int | `1` | Minimum available instances; ignored if there is no PodDisruptionBudget |
| listNext.podSecurityContext | object | `{}` | security context for the pod |
| listNext.replicaCount | int | `1` | Number of replicas for the list-next module. A fault-tolerant number of replicas is recommended. |
| listNext.resources | object | `{}` | resource requests and limits for the container |
| listNext.resourcesPreset | string | `"small"` | set container resources according to one common preset (allowed values: none, nano, micro, small, medium, large, xlarge, 2xlarge). This is ignored if primary.resources is set (primary.resources is recommended for production). More information: <https://github.com/miracum/charts/blob/master/charts/common/templates/_resources.tpl#L1> |
| listNext.revisionHistoryLimit | int | `5` | specify how many old ReplicaSets for this Deployment you want to retain. |
| listNext.service | object | `{"metricsPort":8081,"port":8080,"type":"ClusterIP"}` | the service used to expose the list-next module |
| listNext.service.metricsPort | int | `8081` | service port for the Prometheus metrics endpoint |
| listNext.service.port | int | `8080` | service port for the HTTP endpoint |
| listNext.service.type | string | `"ClusterIP"` | service type |
| listNext.serviceAccount.annotations | object | `{}` | Annotations to add to the service account |
| listNext.serviceAccount.automountServiceAccountToken | bool | `false` | whether to automount the SA token. |
| listNext.serviceAccount.create | bool | `false` | Specifies whether a service account should be created. |
| listNext.serviceAccount.name | string | `""` | The name of the service account to use. If not set and create is true, a name is generated using the fullname template |
| listNext.tolerations | list | `[]` | tolerations for pod assignment see: <https://kubernetes.io/docs/concepts/configuration/taint-and-toleration/> |
| listNext.topologySpreadConstraints | list | `[]` | pod topology spread configuration see: <https://kubernetes.io/docs/concepts/workloads/pods/pod-topology-spread-constraints/#api> |
| mailhog.enabled | bool | `true` | Whether the included SMTP test server should be used. Not required for a production deployment. See <https://github.com/codecentric/helm-charts/blob/master/charts/mailhog/values.yaml> for available options. |
| nameOverride | string | `""` | partially override the release name |
| notify.affinity | object | `{}` | affinity for pod assignment see: <https://kubernetes.io/docs/concepts/configuration/assign-pod-node/#affinity-and-anti-affinity> |
| notify.enabled | bool | `true` | whether the notification module should be enabled |
| notify.extraEnv | list | `[]` | extra environment vars on the container |
| notify.extraPodLabels | object | `{}` | specify additional labels to apply to the notify pod |
| notify.ha.database.existingSecret.key | string | `"postgresql-password"` | key inside the existing secret |
| notify.ha.database.existingSecret.name | string | `""` | name of an existing Kubernetes secret from which to retrieve the user's password. |
| notify.ha.database.host | string | `""` | hostname for the database used to store notification jobs if `postgres.enabled=true`, uses the included PostgreSQL database instead. |
| notify.ha.database.name | string | `"recruit_notify_jobstore"` | database name |
| notify.ha.database.password | string | `""` | password for the database |
| notify.ha.database.port | int | `5432` | database port |
| notify.ha.database.username | string | `""` | username to log into the database |
| notify.ha.enabled | bool | `false` | whether to enable high-availability mode for the notification module |
| notify.ingress.annotations | object | `{}` | additional annotations for the Ingress resource |
| notify.ingress.enabled | bool | `false` | if enabled, this ingress is used when setting up the webhook subscription in the FHIR server. Setting this ingress is useful if the FHIR server is external to the cluster and can only be reached via this ingress. The URL called by the external FHIR server with ingress enabled is as follows: "http(s)://ingress.hosts[0].host/on-list-change" If ingress.enabled is false, then the notify.service is used to construct the URL invoked by the FHIR server. |
| notify.ingress.hosts[0].host | string | `"notify.127.0.0.1.nip.io"` |  |
| notify.ingress.hosts[0].paths[0] | string | `"/"` |  |
| notify.ingress.ingressClassName | string | `""` | name of the IngressClass resource to use for this ingress |
| notify.ingress.tls | list | `[]` | TLS configuration |
| notify.mail.from | string | `"recruit@example.com"` | The sender email address for the created notification mails. |
| notify.mail.screeningListLinkTemplate | string | `"http://localhost:8080/recommendations/[list_id]"` | Used to link back to the screening list web app from an email. If the screening list ingress is enabled, it uses list.ingress.hosts.host to construct the URL, otherwise uses this value. Should include a '[list_id]' which is replaced with the internal screening list id. |
| notify.mail.server | object | `{"existingSecret":"","host":"","password":"","port":25,"username":""}` | configure the mail server used to send notification emails All of these values are only used when mailhog.enabled is set to false |
| notify.mail.server.existingSecret | string | `""` | Name of an existing secret containing an `smtp-password` key to use instead of the password value above |
| notify.mail.server.host | string | `""` | hostname of the external SMTP mail server |
| notify.mail.server.password | string | `""` | Mailserver password |
| notify.mail.server.port | int | `25` | mailserver port |
| notify.mail.server.username | string | `""` | Mailserver username |
| notify.metrics.serviceMonitor.additionalLabels | object | `{}` | additional labels to set on the ServiceMonitor object, e.g. `release: prometheus-operator` |
| notify.metrics.serviceMonitor.enabled | bool | `false` | if enabled, creates a ServiceMonitor instance for Prometheus Operator-based monitoring |
| notify.nodeSelector | object | `{}` | node labels for pod assignment see: <https://kubernetes.io/docs/concepts/scheduling-eviction/assign-pod-node/> |
| notify.podDisruptionBudget.enabled | bool | `false` | create a PodDisruptionBudget resource |
| notify.podDisruptionBudget.maxUnavailable | string | `""` | Maximum unavailable instances; ignored if there is no PodDisruptionBudget |
| notify.podDisruptionBudget.minAvailable | int | `1` | Minimum available instances; ignored if there is no PodDisruptionBudget |
| notify.podSecurityContext | object | `{}` | security context for the pod |
| notify.replicaCount | int | `1` | number of replicas for the notify component. should only be set to a number > 1 if `notify.ha.enabled=true` |
| notify.resources | object | `{}` | resource requests and limits for the container |
| notify.resourcesPreset | string | `"medium"` | set container resources according to one common preset (allowed values: none, nano, micro, small, medium, large, xlarge, 2xlarge). This is ignored if primary.resources is set (primary.resources is recommended for production). More information: <https://github.com/miracum/charts/blob/master/charts/common/templates/_resources.tpl#L1> |
| notify.revisionHistoryLimit | int | `5` | specify how many old ReplicaSets for this Deployment you want to retain. |
| notify.rules | object | `{}` | configure the Notification rules. See the [Configure Notifcation Rules](#configure-notifcation-rules) section below. |
| notify.service | object | `{"metricsPort":8081,"port":8080,"type":"ClusterIP"}` | the service used to expose the notify module web port |
| notify.service.metricsPort | int | `8081` | service port for the actuator/metrics endpoint |
| notify.service.port | int | `8080` | service port for the HTTP endpoint |
| notify.service.type | string | `"ClusterIP"` | service type |
| notify.serviceAccount.annotations | object | `{}` | Annotations to add to the service account |
| notify.serviceAccount.automountServiceAccountToken | bool | `false` | whether to automount the SA token. |
| notify.serviceAccount.create | bool | `false` | Specifies whether a service account should be created. |
| notify.serviceAccount.name | string | `""` | The name of the service account to use. If not set and create is true, a name is generated using the fullname template |
| notify.tolerations | list | `[]` | tolerations for pod assignment see: <https://kubernetes.io/docs/concepts/configuration/taint-and-toleration/> |
| notify.topologySpreadConstraints | list | `[]` | pod topology spread configuration see: <https://kubernetes.io/docs/concepts/workloads/pods/pod-topology-spread-constraints/#api> |
| ohdsi.cdmInitJob.enabled | bool | `false` | to make sure the job runs only once, set this to `false` after the first installation completed |
| ohdsi.cdmInitJob.resources.limits.cpu | string | `"2000m"` |  |
| ohdsi.cdmInitJob.resources.limits.ephemeral-storage | string | `"16Gi"` |  |
| ohdsi.cdmInitJob.resources.limits.memory | string | `"512Mi"` |  |
| ohdsi.cdmInitJob.resources.requests.cpu | string | `"2000m"` |  |
| ohdsi.cdmInitJob.resources.requests.ephemeral-storage | string | `"16Gi"` |  |
| ohdsi.cdmInitJob.resources.requests.memory | string | `"512Mi"` |  |
| ohdsi.enabled | bool | `true` | Whether the included OHDSI chart should be installed. See <https://github.com/chgl/charts/tree/master/charts/ohdsi/values.yaml> for available options. |
| ohdsi.postgres.nameOverride | string | `"ohdsi-postgres"` | overrides the chart's postgres server name to avoid conflicts with the included HAPI FHIR server chart |
| podAnnotations | object | `{}` | annotations to set on all Pod resources |
| postgres.auth.database | string | `"recruit"` | set the default database name to `recruit` |
| postgres.enabled | bool | `false` | enable the included postgres DB, currently used only by the notification module to store jobs when running in high-availability mode (`notify.ha.enabled=true`) |
| postgres.nameOverride | string | `"recruit-postgres"` | override the default name to avoid conflicts with the HAPI and OHDSI charts |
| query.affinity | object | `{}` | affinity for pod assignment see: <https://kubernetes.io/docs/concepts/configuration/assign-pod-node/#affinity-and-anti-affinity> |
| query.cohortSelectorLabels | list | `["UC1"]` | set cohortSelectorLabels[1]=Test |
| query.enabled | bool | `true` | Whether the query module should be enabled |
| query.extraEnv | list | `[]` | extra environment vars on the container |
| query.extraPodLabels | object | `{}` | specify additional labels to apply to the query pod |
| query.metrics.serviceMonitor.additionalLabels | object | `{}` | additional labels to apply the the ServiceMonitor object, eg. `release: prometheus` |
| query.metrics.serviceMonitor.enabled | bool | `false` | if enabled, creates a ServiceMonitor instance for Prometheus Operator-based monitoring |
| query.nodeSelector | object | `{}` | node labels for pod assignment see: <https://kubernetes.io/docs/concepts/scheduling-eviction/assign-pod-node/> |
| query.omop.cdmSchema | string | `"cds_cdm"` | name of the database schema containing the actual clinical data |
| query.omop.database | string | `"OHDSI"` | name of the db |
| query.omop.existingSecret | string | `""` | name of an existing secret to use instead of `omop.password`. Must include an `omop-password` key. |
| query.omop.host | string | `"localhost"` | hostname of the OHDSI OMOP db |
| query.omop.password | string | `"postgres"` | password to access the db |
| query.omop.port | int | `5432` | port of the db |
| query.omop.resultsSchema | string | `"cds_results"` | name of the database schema containing the results of the cohort generation |
| query.omop.username | string | `"postgres"` | username to access the db |
| query.podSecurityContext | object | `{}` | security context for the pod |
| query.replicaCount | int | `1` | Number of replicas of the query module to run. Running more than one replica for this component is discouraged. |
| query.resources | object | `{}` | resource requests and limits for the container |
| query.resourcesPreset | string | `"medium"` | set container resources according to one common preset (allowed values: none, nano, micro, small, medium, large, xlarge, 2xlarge). This is ignored if primary.resources is set (primary.resources is recommended for production). More information: <https://github.com/bitnami/charts/blob/main/bitnami/common/templates/_resources.tpl#L15> |
| query.revisionHistoryLimit | int | `5` | specify how many old ReplicaSets for this Deployment you want to retain. |
| query.schedule | string | `"*/5 * * * *"` | a UNIX cron expression defining the execution schedule of the query module |
| query.service | object | `{"metricsPort":8081,"port":8080,"type":"ClusterIP"}` | the service used to expose the query module web port |
| query.service.metricsPort | int | `8081` | the service port for the actuator/metrics endpoint |
| query.service.port | int | `8080` | the service port for the HTTP endpoint |
| query.service.type | string | `"ClusterIP"` | the service type |
| query.serviceAccount.annotations | object | `{}` | Annotations to add to the service account |
| query.serviceAccount.automountServiceAccountToken | bool | `false` | whether to automount the SA token. |
| query.serviceAccount.create | bool | `false` | Specifies whether a service account should be created. |
| query.serviceAccount.name | string | `""` | The name of the service account to use. If not set and create is true, a name is generated using the fullname template |
| query.shouldWaitForNotify | bool | `false` | whether the query module should wait for the notification module to be up before starting. implemented as an init container that waits on notify's `/actuator/health` endpoint |
| query.tolerations | list | `[]` | tolerations for pod assignment see: <https://kubernetes.io/docs/concepts/configuration/taint-and-toleration/> |
| query.topologySpreadConstraints | list | `[]` | pod topology spread configuration see: <https://kubernetes.io/docs/concepts/workloads/pods/pod-topology-spread-constraints/#api> |
| query.webAPI.auth | object | `{"enabled":false,"existingSecret":{"key":"webApiAuthPassword","name":""},"loginPath":"/user/login/db","password":"","username":""}` | configure authentication settings for the WebAPI |
| query.webAPI.auth.enabled | bool | `false` | set to true if the WebAPI requires authentication to access |
| query.webAPI.auth.existingSecret | object | `{"key":"webApiAuthPassword","name":""}` | use an existing secret to retrieve the login credentials from |
| query.webAPI.auth.existingSecret.key | string | `"webApiAuthPassword"` | the key containing the password |
| query.webAPI.auth.existingSecret.name | string | `""` | name of an existing Kubernetes secret that contains the user password |
| query.webAPI.auth.loginPath | string | `"/user/login/db"` | the login method/path to use. See <https://github.com/OHDSI/Atlas/blob/master/js/config/app.js#L20> for a list of possible values |
| query.webAPI.auth.password | string | `""` | the password used for login. |
| query.webAPI.auth.username | string | `""` | the username to login as. Note that this user needs permissions to query and generate cohorts |
| query.webAPI.dataSource | string | `"CDS-CDMV5"` | name of the OMOP datasource used to generate the cohorts from. |
| query.webAPI.url | string | `"http://example:8080/WebAPI"` | URL of the ATLAS WebAPI endpoint. Usually ends in /WebAPI. |
| querySqlOnFhir.affinity | object | `{}` | affinity for pod assignment see: <https://kubernetes.io/docs/concepts/configuration/assign-pod-node/#affinity-and-anti-affinity> |
| querySqlOnFhir.enabled | bool | `false` | Whether the Trino SQL-based query module should be enabled |
| querySqlOnFhir.extraEnv | list | `[]` | extra environment vars on the container |
| querySqlOnFhir.extraPodLabels | object | `{}` | specify additional labels to apply to the query pod |
| querySqlOnFhir.extraVolumeMounts | list | `[]` | extra volumes to mount inside the container |
| querySqlOnFhir.extraVolumes | list | `[]` | extra volumes |
| querySqlOnFhir.metrics.serviceMonitor.additionalLabels | object | `{}` | additional labels to apply the the ServiceMonitor object, eg. `release: prometheus` |
| querySqlOnFhir.metrics.serviceMonitor.enabled | bool | `false` | if enabled, creates a ServiceMonitor instance for Prometheus Operator-based monitoring |
| querySqlOnFhir.nodeSelector | object | `{}` | node labels for pod assignment see: <https://kubernetes.io/docs/concepts/scheduling-eviction/assign-pod-node/> |
| querySqlOnFhir.podSecurityContext | object | `{}` | security context for the pod |
| querySqlOnFhir.replicaCount | int | `1` | Number of replicas of the query module to run. Running more than one replica for this component is discouraged. |
| querySqlOnFhir.resources | object | `{}` | resource requests and limits for the container |
| querySqlOnFhir.resourcesPreset | string | `"medium"` | set container resources according to one common preset (allowed values: none, nano, micro, small, medium, large, xlarge, 2xlarge). This is ignored if primary.resources is set (primary.resources is recommended for production). More information: <https://github.com/miracum/charts/blob/master/charts/common/templates/_resources.tpl#L1> |
| querySqlOnFhir.revisionHistoryLimit | int | `5` | specify how many old ReplicaSets for this Deployment you want to retain. |
| querySqlOnFhir.schedule | string | `"@hourly"` | a Spring cron expression defining the execution schedule of the query module |
| querySqlOnFhir.service | object | `{"metricsPort":8081,"port":8080,"type":"ClusterIP"}` | the service used to expose the query module web port |
| querySqlOnFhir.service.metricsPort | int | `8081` | the service port for the actuator/metrics endpoint |
| querySqlOnFhir.service.port | int | `8080` | the service port for the HTTP endpoint |
| querySqlOnFhir.service.type | string | `"ClusterIP"` | the service type |
| querySqlOnFhir.serviceAccount.annotations | object | `{}` | Annotations to add to the service account |
| querySqlOnFhir.serviceAccount.automountServiceAccountToken | bool | `false` | whether to automount the SA token. |
| querySqlOnFhir.serviceAccount.create | bool | `false` | Specifies whether a service account should be created. |
| querySqlOnFhir.serviceAccount.name | string | `""` | The name of the service account to use. If not set and create is true, a name is generated using the fullname template |
| querySqlOnFhir.shouldWaitForNotify | bool | `false` | whether the query module should wait for the notification module to be up before starting. implemented as an init container that waits on notify's `/actuator/health` endpoint |
| querySqlOnFhir.sqlOnFhir.url | string | `""` | base URL of a sql-on-fhir server (e.g. Pathling) implementing the SQLQueryRun operation,    used for SQLQuery Library resources that depend on a ViewDefinition or use a SQL dialect    other than trino. e.g. `http://pathling:8080/fhir` |
| querySqlOnFhir.tolerations | list | `[]` | tolerations for pod assignment see: <https://kubernetes.io/docs/concepts/configuration/taint-and-toleration/> |
| querySqlOnFhir.topologySpreadConstraints | list | `[]` | pod topology spread configuration see: <https://kubernetes.io/docs/concepts/workloads/pods/pod-topology-spread-constraints/#api> |
| querySqlOnFhir.trino.auth.existingSecret | object | `{"key":"trino-password","name":""}` | use an existing secret to retrieve the login credentials from |
| querySqlOnFhir.trino.auth.existingSecret.key | string | `"trino-password"` | the key containing the password |
| querySqlOnFhir.trino.auth.existingSecret.name | string | `""` | name of an existing Kubernetes secret that contains the user password |
| querySqlOnFhir.trino.auth.password | string | `""` | the password used for login. |
| querySqlOnFhir.trino.auth.username | string | `""` | the username to login as. |
| querySqlOnFhir.trino.jdbcUrl | string | `""` | JDBC URL of the Trino server, e.g. `jdbc:trino://localhost:8080/fhir/default`    the URL can optionally include the catalog and schema, whether this is necessary depends    on how the SQL cohorts are written in the FHIR Library. |
| tests.automountServiceAccountToken | bool | `false` |  |
| tests.resources | object | `{}` | configure the test pods resource requests and limits |
| tests.resourcesPreset | string | `"nano"` | set container resources according to one common preset (allowed values: none, nano, micro, small, medium, large, xlarge, 2xlarge). This is ignored if primary.resources is set (primary.resources is recommended for production). More information: <https://github.com/miracum/charts/blob/master/charts/common/templates/_resources.tpl#L1> |
| waitForPostgresInitContainer | object | `{}` |  |

## Configure Notifcation Rules

The notification rules can be directly configured inside your values.yaml. For example:

```yaml
notify:
  enabled: true
  rules:
    # create custom notification schedules using https://www.cronmaker.com
    # these are later referenced used when configuring the notification frequency per user.
    # Note that the user will only receive an email notification if the scheduled time has been
    # reached _and_ there has been a _new_ patient recommendation since the last one.An identical
    # email won't be relentlessly sent everyMorning/Monday/Hour etc...
    schedules:
      everyMorning: "0 0 8 1/1 * ? *"
      everyMonday: "0 0 8 ? * MON *"
      everyHour: "0 0 0/1 1/1 * ? *"
      everyFiveMinutes: "0 0/5 * 1/1 * ? *"

    # trials are identified by their acronym which corresponds to the cohort's title in Atlas or the "[acronym=XYZ]" tag
    trials:
      # a value of '*' matches every trial, so 'everything@example.com' will receive an email whenever any screeninglist
      # gets updated.
      - acronym: "*"
        subscriptions:
          - email: "everything@example.com"

      - acronym: "SAMPLE"
        # the new "accessibleBy" key allows specifying users either by username or email address that
        # are allowed to access the screening list
        accessibleBy:
          users:
            - "user1"
            - "user.two@example.com"
        subscriptions:
          - email: "everyMorning@example.com"
            # each 'notify'-value corresponds to one schedule
            notify: "everyMorning"
            # a lack of a 'notify'-key with an associated schedule means that the user will be notified immediately.
          - email: "immediately-sample@example.com"
            # For example, the following entry means that if the 'SAMPLE' trial received new screening recommendations,
            # an email is sent to 'everyMonday@example.com' on the next monday. This is useful for aggregating notifications
            # about screening recommendations.
          - email: "everyMonday@example.com"
            notify: "everyMonday"

      - acronym: "AMICA"
        subscriptions:
          - email: "immediately-amica@example.com"
          - email: "everyHour1@example.com"
            notify: "everyHour"
          - email: "everyHour2@example.com"
            notify: "everyHour"
          - email: "everyFiveMinutes@example.com"
            notify: "everyFiveMinutes"
```

## Distributed Tracing

See the documentation on distributed tracing for more information: <https://miracum.github.io/recruit/deployment/kubernetes/#distributed-tracing>.
