# Security

For how users sign in to the screening list and what they can access, see
[Authentication](options.md#authentication).

## Verifying release artifacts

The container images, the Helm chart, and the Compose archive of every release are built in GitHub Actions, which
publishes a signed [SLSA build provenance](https://slsa.dev/spec/v1.0/provenance) attestation for each of them. You can
use these to verify that an artifact was built from the recruIT repository and wasn't modified afterwards.

You need the [GitHub CLI](https://cli.github.com/) (`gh`), signed in to any GitHub account via `gh auth login`.

Only releases have attestations. Images built for pull requests or from the `master` branch don't.

<!-- x-release-please-start-version -->

### Container images

The images are built by a shared workflow in the [miracum/.github](https://github.com/miracum/.github) repository, which
has to be given as the signer:

```sh
gh attestation verify oci://ghcr.io/open-recruit/recruit/list:v11.0.0 \
    --repo open-recruit/recruit \
    --signer-workflow miracum/.github/.github/workflows/standard-build.yaml
```

The same works for the query module's image, `ghcr.io/open-recruit/recruit/query-sql-on-fhir`.

### Helm chart

```sh
gh attestation verify oci://ghcr.io/open-recruit/recruit/charts/recruit:11.0.0 \
    --repo open-recruit/recruit \
    --signer-workflow miracum/.github/.github/workflows/standard-chart-publish.yaml
```

### Compose archive

```sh
gh release download v11.0.0 --repo open-recruit/recruit --pattern recruit-compose.tgz
gh attestation verify recruit-compose.tgz --repo open-recruit/recruit
```

<!-- x-release-please-end -->

`gh attestation verify` exits with a non-zero status if verification fails, so it can also be used as a check in
deployment scripts. See the [GitHub documentation](https://docs.github.com/en/actions/how-tos/secure-your-work/use-artifact-attestations/use-artifact-attestations#verifying-artifact-attestations-with-the-github-cli)
for more options, e.g. verifying offline.

## Kubernetes hardening

The Helm chart runs recruIT's own containers as non-root users with a read-only root filesystem, all Linux
capabilities dropped, privilege escalation disallowed, and the `RuntimeDefault` seccomp profile, following the
[NSA/CISA Kubernetes Hardening Guidance](https://www.cisa.gov/resources-tools/resources/kubernetes-hardening-guide).
The container images are distroless, or chiseled for the list module, and contain no shell or package manager.

Every change to the chart is scanned with [Kubescape](https://kubescape.io/). The reports for the chart of the current
release, rendered with its default values, are published here:

- [NSA/CISA framework](https://openrecruit.dev/kubescape-reports/nsa.html)
- [All controls](https://openrecruit.dev/kubescape-reports/allcontrols.html)

The reports include the chart's dependencies, i.e. the HAPI FHIR server, PostgreSQL, and MailDev, which are hardened
less strictly. They also flag every workload for missing `NetworkPolicy` resources: the chart doesn't include any,
since they depend too much on the deployment, see [Zero-trust networking](../deployment/kubernetes.md#zero-trust-networking)
for a starting point.
