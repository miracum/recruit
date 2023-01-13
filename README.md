# recruIT

[![OpenSSF Scorecard](https://api.securityscorecards.dev/projects/github.com/miracum/recruit/badge)](https://api.securityscorecards.dev/projects/github.com/miracum/recruit)
[![SLSA 3](https://slsa.dev/images/gh-badge-level3.svg)](https://slsa.dev)
![License](https://img.shields.io/github/license/miracum/recruit)

See the documentation site at <https://miracum.github.io/recruit> for more information.

## Build Documentation

The static documentation site is build using [Material for MkDocs](https://squidfunk.github.io/mkdocs-material/).
See the [official installation instructions](https://squidfunk.github.io/mkdocs-material/getting-started/#installation)
for installing the `mkdocs` tool.

Then you can just run the following to build and serve the documentation locally and open your browser at <http://localhost:8000/>.

```sh
mkdocs serve
```

## Build FHIR IG

```sh
docker run --rm -it -v $PWD/fhir-ig:/usr/src/build ghcr.io/miracum/ig-build-tools:latest

root@eddc76b8b235:/usr/src/build# ./_genonce.sh
```

## Contributing

See [CONTRIBUTING.md](docs/development/contributing.md)
