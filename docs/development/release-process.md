# Release process

The recruitment infrastructure consists of three modules: the query module, the notification module, and the screening
list. Each component resides in its own Git repository:

| Module         | Repository                                              |
| -------------- | ------------------------------------------------------- |
| Screening List | <https://gitlab.miracum.org/miracum/uc1/recruit/list>   |
| Query          | <https://gitlab.miracum.org/miracum/uc1/recruit/query>  |
| Notify         | <https://gitlab.miracum.org/miracum/uc1/recruit/notify> |

Development happens in the individual repositories and whenever there's a new release in one of them, a new container
image is build. Usually, this happens whenever there's a new commit to the master branch because each project uses
[semantic-release](https://github.com/semantic-release/semantic-release).

Because all three modules (plus at least a FHIR server) are required to run the entire application, this repository is
used to bundle them within one `docker-compose.yml`. The [Helm Chart](https://gitlab.miracum.org/miracum/charts/-/tree/master/charts/recruit)
works the same way.

To avoid having to manually bump version numbers for each release in one of the repositories above, we use [Renovate](https://github.com/renovatebot/renovate)
to automatically update the image tags in the compose file. Renovate creates a new MR whenever one of the components is updated
and is configured to merge it without intervention. This merge in turn triggers the `semantic-release` in the `.gitlab-ci.yml`,
which creates a new release and tag in this repository.

Note that `.releaserc.json` is configured to bump the minor version whenever there's a commit of type
`chore(deps)` to the master branch.
