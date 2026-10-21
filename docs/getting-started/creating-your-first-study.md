# Creating your first study

This will guide you through creating your first study using recruIT.

The query module regularly searches the FHIR server for
`ResearchStudy` resources that comply to the [https://openrecruit.dev/fhir/StructureDefinition/recruit-research-study](https://openrecruit.dev/fhir/en/StructureDefinition-recruit-research-study.html) profile.
These are resources, that reference a `Group` via `enrollment` which in turn references `Library` resources with the
actual SQL that represent the eligibility criteria of the study.

So, to create your own resource, all you have to do is define
a compliant `ResearchStudy` resource and send it to the FHIR server that is polled by the `query` module.

To make it easier, the screening list module also has an editor for trials accessible by admin users.
In the left sidebar, click on `Administration` → `Trial Editor`.

![The Trial Editor with the title, acronym and description of a new study filled in](../_img/screenshots/trial-editor/my-first-study-light.png#only-light)
![The Trial Editor with the title, acronym and description of a new study filled in](../_img/screenshots/trial-editor/my-first-study-dark.png#only-dark)

Congratulations, you've just created your first cohort definition, displayed potentially eligible patients in the
screening list, and received a notification email about them!
