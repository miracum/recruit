# Upgrading & Breaking Changes

## v9

This updates all dependencies to their latest version. Both the included HAPI FHIR and the OHDSI sub-chart had breaking
changes in their new versions:

1. [OHDSI Helm chart breaking changes](https://github.com/chgl/charts/tree/master/charts/ohdsi#breaking-changes)

   The chart was updated from `0.8` to version `0.17`, both the `0.12` and the `0.14` breaking changes are relevant.

1. [HAPI FHIR server Helm chart breaking changes](https://github.com/hapifhir/hapi-fhir-jpaserver-starter/blob/helm-v0.8.0/charts/hapi-fhir-jpaserver/Chart.yaml#L15)

   Similar to the OHDSI chart, the included PostgreSQL sub-chart was updated to a major version.

### Steps for manually updating from version `8.6.4` to `9.0.0` while keeping the databases

The easiest way to update is uninstalling the version 8 release without deleting the persistent volumes,
explicitly setting the previously used PostgreSQL container image versions and passwords in your `values.yaml`,
and newly installing version 9.

We assume that the chart was initially installed as follows:

> ⚠️ [docs/migrations/v8-to-v9/values-v8.yaml](migrations/v8-to-v9/values-v8.yaml) has enabled the notify module's HA-mode.
> Since this is a recent feature, disabled by default, you most likely have not enabled it and may want to set
> `notify.ha.enabled=false` and `postgresql.enabled=false` to keep HA disabled.

```sh
# versions 8 and below are only available on <https://harbor.miracum.org/chartrepo/charts>.
# Version 9.0.0 is available on both the MIRACUM Harbor and the <https://github.com/miracum/charts/> repository.
helm repo add miracum-gitlab https://harbor.miracum.org/chartrepo/charts

# the sample values in docs/migrations/v8-to-v9/values-v8.yaml only override the normally randomized
# passwords used by the included PostgreSQL sub-charts.
# ℹ️ the following is run from within this chart repository, `cd charts/recruit`, to access the `migrations` sample values.
helm install --version=8.6.4 -f docs/migrations/v8-to-v9/values-v8.yaml --create-namespace -n recruit recruit miracum-gitlab/recruit

# check the installed release
$ helm ls -n recruit
NAME    NAMESPACE       REVISION        UPDATED                                 STATUS          CHART           APP VERSION
recruit recruit         1               2022-04-17 18:04:45.9515171 +0200 CEST  deployed        recruit-8.6.4

# just to be sure everything is in order
$ kubectl get all -n recruit
NAME                                        READY   STATUS    RESTARTS   AGE
pod/recruit-fhir-server-postgres-0          1/1     Running   0          71m
pod/recruit-fhirserver-7867c8b98d-zp5h4     1/1     Running   0          71m
pod/recruit-list-544b59bddb-pzqg5           1/1     Running   0          71m
pod/recruit-mailhog-7cc89fb79-dqb26         1/1     Running   0          71m
pod/recruit-notify-8459b944fb-lm6cl         1/1     Running   0          71m
pod/recruit-ohdsi-atlas-bd4f6fbcc-xjdvr     1/1     Running   0          71m
pod/recruit-ohdsi-postgres-0                1/1     Running   0          71m
pod/recruit-ohdsi-webapi-565f64648c-pwh7r   1/1     Running   0          71m
pod/recruit-query-7c9cfd6d9f-sgzts          1/1     Running   0          71m
pod/recruit-recruit-postgres-0              1/1     Running   0          71m

NAME                                            TYPE        CLUSTER-IP      EXTERNAL-IP   PORT(S)             AGE
service/recruit-fhir-server-postgres            ClusterIP   10.96.92.115    <none>        5432/TCP            71m
service/recruit-fhir-server-postgres-headless   ClusterIP   None            <none>        5432/TCP            71m
service/recruit-fhirserver                      ClusterIP   10.96.81.217    <none>        8080/TCP            71m
service/recruit-list                            ClusterIP   10.96.207.165   <none>        8080/TCP            71m
service/recruit-mailhog                         ClusterIP   10.96.138.139   <none>        8025/TCP,1025/TCP   71m
service/recruit-notify                          ClusterIP   10.96.106.99    <none>        8080/TCP            71m
service/recruit-ohdsi-atlas                     ClusterIP   10.96.81.11     <none>        8080/TCP            71m
service/recruit-ohdsi-postgres                  ClusterIP   10.96.93.159    <none>        5432/TCP            71m
service/recruit-ohdsi-postgres-headless         ClusterIP   None            <none>        5432/TCP            71m
service/recruit-ohdsi-webapi                    ClusterIP   10.96.242.106   <none>        8080/TCP            71m
service/recruit-query                           ClusterIP   10.96.68.92     <none>        8080/TCP            71m
service/recruit-recruit-postgres                ClusterIP   10.96.182.101   <none>        5432/TCP            71m
service/recruit-recruit-postgres-headless       ClusterIP   None            <none>        5432/TCP            71m

NAME                                   READY   UP-TO-DATE   AVAILABLE   AGE
deployment.apps/recruit-fhirserver     1/1     1            1           71m
deployment.apps/recruit-list           1/1     1            1           71m
deployment.apps/recruit-mailhog        1/1     1            1           71m
deployment.apps/recruit-notify         1/1     1            1           71m
deployment.apps/recruit-ohdsi-atlas    1/1     1            1           71m
deployment.apps/recruit-ohdsi-webapi   1/1     1            1           71m
deployment.apps/recruit-query          1/1     1            1           71m

NAME                                              DESIRED   CURRENT   READY   AGE
replicaset.apps/recruit-fhirserver-7867c8b98d     1         1         1       71m
replicaset.apps/recruit-list-544b59bddb           1         1         1       71m
replicaset.apps/recruit-mailhog-7cc89fb79         1         1         1       71m
replicaset.apps/recruit-notify-8459b944fb         1         1         1       71m
replicaset.apps/recruit-ohdsi-atlas-bd4f6fbcc     1         1         1       71m
replicaset.apps/recruit-ohdsi-webapi-565f64648c   1         1         1       71m
replicaset.apps/recruit-query-7c9cfd6d9f          1         1         1       71m

NAME                                            READY   AGE
statefulset.apps/recruit-fhir-server-postgres   1/1     71m
statefulset.apps/recruit-ohdsi-postgres         1/1     71m
statefulset.apps/recruit-recruit-postgres       1/1     71m

NAME                                        SCHEDULE   SUSPEND   ACTIVE   LAST SCHEDULE   AGE
cronjob.batch/recruit-ohdsi-achilles-cron   @daily     False     0        <none>          71m

# list the volume claims and associated volumes of the release
$ kubectl get pvc -n recruit
NAME                                  STATUS   VOLUME                                     CAPACITY   ACCESS MODES   STORAGECLASS   AGE
data-recruit-fhir-server-postgres-0   Bound    pvc-83ba8bd1-0c51-492a-8d4d-429778c9495b   8Gi        RWO            standard       22m
data-recruit-ohdsi-postgres-0         Bound    pvc-c5f9fb2b-d300-4481-b81c-8bd1a6982800   8Gi        RWO            standard       22m
data-recruit-recruit-postgres-0       Bound    pvc-e82451c9-ebbb-4bee-a4dc-80ed46dedc55   8Gi        RWO            standard       22m

$ kubectl get pv -n recruit
NAME                                       CAPACITY   ACCESS MODES   RECLAIM POLICY   STATUS   CLAIM                                         STORAGECLASS   REASON   AGE
pvc-83ba8bd1-0c51-492a-8d4d-429778c9495b   8Gi        RWO            Delete           Bound    recruit/data-recruit-fhir-server-postgres-0   standard                25m
pvc-c5f9fb2b-d300-4481-b81c-8bd1a6982800   8Gi        RWO            Delete           Bound    recruit/data-recruit-ohdsi-postgres-0         standard                25m
pvc-e82451c9-ebbb-4bee-a4dc-80ed46dedc55   8Gi        RWO            Delete           Bound    recruit/data-recruit-recruit-postgres-0       standard                25m
```

To begin updating to version 9 of this chart, first uninstall the old release:

```sh
# Uninstall the chart release
$ helm uninstall recruit -n recruit
release "recruit" uninstalled

# PVCs and volumes should be kept as-is
$ kubectl get pvc -n recruit
NAME                                  STATUS   VOLUME                                     CAPACITY   ACCESS MODES   STORAGECLASS   AGE
data-recruit-fhir-server-postgres-0   Bound    pvc-83ba8bd1-0c51-492a-8d4d-429778c9495b   8Gi        RWO            standard       28m
data-recruit-ohdsi-postgres-0         Bound    pvc-c5f9fb2b-d300-4481-b81c-8bd1a6982800   8Gi        RWO            standard       28m
data-recruit-recruit-postgres-0       Bound    pvc-e82451c9-ebbb-4bee-a4dc-80ed46dedc55   8Gi        RWO            standard       28m
```

Install version 9 of the chart using the provided `values-v9.yaml`:

```sh
$ helm upgrade --install --version=9.0.0 -f docs/migrations/v8-to-v9/values-v9.yaml --create-namespace -n recruit recruit miracum-gitlab/recruit
Release "recruit" has been upgraded. Happy Helming!
NAME: recruit
LAST DEPLOYED: Sun Apr 17 22:31:51 2022
NAMESPACE: recruit
STATUS: deployed
REVISION: 7
NOTES:
1. Get the screening list URL by running these commands:
  export POD_NAME=$(kubectl get pods --namespace recruit -l "app.kubernetes.io/instance=recruit,app.kubernetes.io/component=list" -o jsonpath="{.items[0].metadata.name}")
  echo "Visit http://127.0.0.1:8080 to use your application"
  kubectl --namespace recruit port-forward $POD_NAME 8080:8080


# After some time the new pods should be running and we can test whether all services are available
$ helm test recruit -n recruit
NAME: recruit
LAST DEPLOYED: Sun Apr 17 22:36:00 2022
NAMESPACE: recruit
STATUS: deployed
REVISION: 8
TEST SUITE:     recruit-fhirserver-test-endpoints
Last Started:   Sun Apr 17 22:38:53 2022
Last Completed: Sun Apr 17 22:38:54 2022
Phase:          Succeeded
TEST SUITE:     recruit-ohdsi-test-connection
Last Started:   Sun Apr 17 22:38:54 2022
Last Completed: Sun Apr 17 22:39:59 2022
Phase:          Succeeded
TEST SUITE:     recruit-test-health-probes
Last Started:   Sun Apr 17 22:39:59 2022
Last Completed: Sun Apr 17 22:40:01 2022
Phase:          Succeeded

# The upgrade is done.
```

## v8

The chart has been renamed from `uc1-recruit` to `recruit`.

The included HAPI FHIR server version was updated to 5.4.0. There isn't an automatic migration path for HAPI FHIR server
containers yet, so this is a breaking change unless you manually migrate your existing DB first.

This release also adds the OHDSI Helm chart as an optional dependency (enabled by default).

The pod/deployment labels have been updated to follow the Bitnami Helm conventions.

## v7

Starting with version 7.0.0 of this chart, the notification module has been updated to v3. The breaking change is related
to a completely revamped notification configuration, which allows setting the notification frequency.
See [Configure Notification Rules](../README.md#configure-notifcation-rules) below for an example config.

Additionally, several values that were previously globally scoped, have now been scoped to each component.
You can now configure `nodeSelector`, `affinity`, etc. per-component.

### v7.0.1

v7.0.1 is a small update with several unplanned breaking changes:

The `notify.mailServer` config has been split into `notify.mail.server` for SMTP settings and the additional
`notify.mail.from` and `notify.mail.screeningListLinkTemplate` settings.
See the [Configuration Section](../README.md#configuration) below for details

## v6

Starting with v6.0.0 of this chart all labels have been updated to be conformant to the Kubernetes' guidelines (<https://kubernetes.io/docs/concepts/overview/working-with-objects/common-labels/>).
This means that you can't just update your release using `helm upgrade` but need to uninstall the old release first.

This major release also updates the screening list component to v2, which requires an existing Keycloak installation to run.
The following values need to be set:

```yaml
list:
  auth:
    # if enabled, requires authentication before accessing the screening list
    enabled: true
    keycloak:
      clientId: "" # required
      realm: "UC1"
      url: "" # required
```

While it's not recommended, you can set `list.auth.enabled=false` to allow anonymous access to the screening list.
