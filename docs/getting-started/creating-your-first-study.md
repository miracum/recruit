# Creating your first study

This will guide you through creating your first study using recruIT.

The query module regularly searches the FHIR server for
`ResearchStudy` resources that comply to the [https://openrecruit.dev/fhir/StructureDefinition/recruit-research-study](https://openrecruit.dev/fhir/en/StructureDefinition-recruit-research-study.html) profile.
These are resources, that reference a `Group` via `enrollment` which in turn references `Library` resources with the
actual SQL that represent the eligibility criteria of the study.

So, to create your own resource, all you have to do is define
a compliant `ResearchStudy` resource and send it to the FHIR server that is polled by the `query` module.

To make it easier, the screening list module also has an editor for trials accessible by admin users.
In the left sidebar, click on `Administration` → `Trial Editor`:

![The Trial Editor with the title, acronym and description of a new study filled in](../_img/screenshots/trial-editor/my-first-study-light.png#only-light)
![The Trial Editor with the title, acronym and description of a new study filled in](../_img/screenshots/trial-editor/my-first-study-dark.png#only-dark)

Next, add the eligibility criteria by either selecting existing `SQLQuery` library resources from the server or
by writing your own query:

![The Trial Editor with a new eligibility criterion selecting patients with a history of essential hypertension](../_img/screenshots/trial-editor/hypertension-criterion-light.png#only-light)
![The Trial Editor with a new eligibility criterion selecting patients with a history of essential hypertension](../_img/screenshots/trial-editor/hypertension-criterion-dark.png#only-dark)

Next, you can click on `Submit to FHIR Server` to send the study to the server. The query module will eventually
recognize the new study and add patients to the screening list in a few minutes.

Finally, go back to `Recruitment` → `Trials`, click on your newly-created study and view the recommendations:

![The screening list of the new study with three recommended candidate patients](../_img/screenshots/trial-editor/my-first-study-screening-list-light.png#only-light)
![The screening list of the new study with three recommended candidate patients](../_img/screenshots/trial-editor/my-first-study-screening-list-dark.png#only-dark)

Congratulations, you've just created your first cohort definition and displayed potentially eligible patients in the screening list.
