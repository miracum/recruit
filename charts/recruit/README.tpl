# {{ .Project.ShortName }}

[{{ .Project.Name }}]({{ .Project.URL }}) - {{ .Project.Description }}

## TL;DR;

```console
helm repo add {{ .Repository.Name }} {{ .Repository.URL }}
helm repo update
helm install {{ .Release.Name }} {{ .Repository.Name }}/{{ .Chart.Name }} -n {{ .Release.Namespace }}
```

You can find more exhaustive documentation at the {{ .Project.Name }} documentation site: <https://miracum.github.io/recruit/deployment/kubernetes>.

## Introduction

This chart deploys {{ .Project.App }} on a [Kubernetes](http://kubernetes.io) cluster using the [Helm](https://helm.sh) package manager.

## Prerequisites
{{ range .Prerequisites }}
- {{ . }}
{{- end }}

## Upgrades & Breaking Changes

See [UPGRADING.md](./docs/UPGRADING.md) for information on breaking changes introduced by major version bumps and instructions on how to update.

## Installing the Chart

To install the chart with the release name `{{ .Release.Name }}`:

```console
helm install {{ .Release.Name }} {{ .Repository.Name }}/{{ .Chart.Name }} -n {{ .Release.Namespace }}
```

The command deploys {{ .Project.App }} on the Kubernetes cluster in the default configuration. The [configuration](#configuration) section lists the parameters that can be configured during installation.

> **Tip**: List all releases using `helm list`

## Uninstalling the Chart

To uninstall/delete the `{{ .Release.Name }}`:

```console
helm delete {{ .Release.Name }} -n {{ .Release.Namespace }}
```

The command removes all the Kubernetes components associated with the chart and deletes the release.

## Configuration

The following table lists the configurable parameters of the `{{ .Chart.Name }}` chart and their default values.

{{ .Chart.Values }}

Specify each parameter using the `--set key=value[,key=value]` argument to `helm install`. For example:

```console
helm install {{ .Release.Name }} {{ .Repository.Name }}/{{ .Chart.Name }} -n {{ .Release.Namespace }} --set {{ .Chart.ValuesExample }}
```

Alternatively, a YAML file that specifies the values for the parameters can be provided while
installing the chart. For example:

```console
helm install {{ .Release.Name }} {{ .Repository.Name }}/{{ .Chart.Name }} -n {{ .Release.Namespace }} --values values.yaml
```

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
