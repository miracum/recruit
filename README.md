# recruIT

[![OpenSSF Scorecard](https://img.shields.io/ossf-scorecard/github.com/open-recruit/recruit?label=openssf%20scorecard&style=flat)](https://scorecard.dev/viewer/?uri=github.com/open-recruit/recruit)
[![SLSA 3](https://slsa.dev/images/gh-badge-level3.svg)](https://slsa.dev)
![License](https://img.shields.io/github/license/open-recruit/recruit)

See the documentation site at <https://openrecruit.dev/> for more information.

## Build Documentation

The static documentation site is built using [Zensical](https://zensical.org/) and configured in [zensical.toml](zensical.toml).
With [uv](https://docs.astral.sh/uv/) installed, run the following to build and serve the documentation locally
and open your browser at <http://localhost:8000/>:

```sh
uv run zensical serve
```

## Build FHIR IG

```sh
docker run --rm -it -v $PWD/fhir-ig:/usr/src/build ghcr.io/miracum/ig-build-tools:latest

root@eddc76b8b235:/usr/src/build# ./_genonce.sh
```

## Contributing

See [CONTRIBUTING.md](docs/development/contributing.md)
