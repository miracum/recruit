import FHIR from "fhirclient";
import fhirpath from "fhirpath";
import Vue from "vue";
import axios from "axios";

import Constants from "@/const";

function createFhirClient() {
  let fhirUrl = process.env.VUE_APP_FHIR_URL;
  if (!fhirUrl) {
    // this is an awkward workaround for FHIR.client not accepting relative paths as valid URLs
    fhirUrl = `${window.location.protocol}//${window.location.host}/fhir`;
  }

  return FHIR.client({
    serverUrl: fhirUrl,
    tokenResponse: {
      access_token: Vue.prototype.$keycloak?.token,
    },
  });
}

const actions = {
  getFhirClient() {
    return createFhirClient();
  },
  async fetchConfig() {
    const response = await axios.get(process.env.VUE_APP_CONFIG_URL || "/config");
    return response.data;
  },
  async updateResearchSubject(subjectId, note, status) {
    const client = createFhirClient();
    const patch = [];

    if (status) {
      patch.push({
        op: "replace",
        path: "/status",
        value: status,
      });
    }

    if (note) {
      patch.push({
        op: "add",
        path: "/extension",
        value: [
          {
            url: Constants.URL_NOTE_EXTENSION,
            valueString: note,
          },
        ],
      });
    }

    await client.request({
      url: `ResearchSubject/${subjectId}`,
      method: "PATCH",
      body: JSON.stringify(patch),
      headers: { "Content-Type": "application/json-patch+json" },
    });
  },

  async updateListStatus(listId, status) {
    const client = createFhirClient();
    const patch = [];

    if (status) {
      patch.push({
        op: "replace",
        path: "/status",
        value: status,
      });
    }

    await client.request({
      url: `List/${listId}`,
      method: "PATCH",
      body: JSON.stringify(patch),
      headers: { "Content-Type": "application/json-patch+json" },
    });
  },
  async fetchCurrentAndRetiredLists() {
    const client = createFhirClient();

    const screeningLists = await client.request(
      `List/?code=${Constants.SYSTEM_SCREENING_LIST}%7Cscreening-recommendations&status=current,retired`,
      {
        resolveReferences: ["extension.0.valueReference", ""],
        flat: true,
        pageLimit: 0,
      }
    );

    if (screeningLists.length !== 0) {
      return screeningLists;
    }
    return [];
  },
  async fetchListById(id) {
    const client = createFhirClient();

    const allResources = await client.request(`List/?_id=${id}&_include=List:item`, {
      resolveReferences: ["extension.0.valueReference", "individual", "study"],
      flat: true,
    });

    if (allResources.length === 0) {
      return {};
    }

    const list = fhirpath.evaluate(allResources, "List")[0];

    if (!list) {
      throw new Error("No list found for the given id.");
    }

    if (list.entry) {
      // a manual "resolveReferences" implementation since fhir.js doesn't support
      // reference resolution on arrays, ie. the List.entry field.
      // see https://github.com/smart-on-fhir/client-js/issues/73
      list.entry = list.entry.map((entry) => {
        const subject = fhirpath.evaluate(allResources, "ResearchSubject.where(id=%subjectId)", {
          subjectId: entry.item.reference.split("/")[1],
        })[0];

        const entryWithResolvedItem = entry;
        entryWithResolvedItem.item = subject;
        return entryWithResolvedItem;
      });
    }

    return list;
  },
  async fetchSubjectHistory(subjectId) {
    const client = createFhirClient();
    return client.request(`ResearchSubject/${subjectId}/_history`, {
      flat: true,
      pageLimit: 0,
      resolveReferences: ["individual"],
    });
  },
  async fetchPatientRecord(patientId) {
    const client = createFhirClient();
    return client.request(`Patient/${patientId}/$everything?_count=250&_pretty=false`, {
      flat: true,
      pageLimit: 0,
    });
  },
  async fetchLatestEncounterWithLocation(patientId, maxNumberOfEncounters = 5) {
    const client = createFhirClient();

    // fetch the latest 5 (or maxNumberOfEncounters) Encounters for the given patient, sorted by date
    // and include the Encounter's location to reduce the number of server round-trips
    const entries = await client.request(
      `Encounter?subject=Patient/${patientId}` +
        `&_count=${maxNumberOfEncounters}` +
        "&_include=Encounter:location" +
        "&_pretty=false" +
        "&_sort=-date",
      {
        flat: true,
        pageLimit: 1,
      }
    );

    // unfortunately, using resolveReferences to directly replace the encounter.location.reference
    // with the the actual Location object doesn't work. So we need to build a manual
    // lookup from Location.id to the Location object.
    const locations = entries.filter((entry) => entry.resourceType === "Location");
    const locationLookup = new Map(locations.map((location) => [`Location/${location.id}`, location]));

    // select the encounters so we only need to iterate over them
    const encounters = entries
      .filter((entry) => entry.resourceType === "Encounter")
      .sort((e1, e2) => {
        // in the rare case that multiple encounters start at the same time
        // order the encounter whose status is in-progress or without an end-date
        // before the other.
        if (e1.period?.start === e2.period?.start) {
          if (e1.status === "in-progress" || !e1.period?.end) {
            return -1;
          }

          return 1;
        }

        return 0;
      });

    Vue.$log.debug(`Found ${encounters.length} encounters for Patient/${patientId}`);

    // eslint-disable-next-line no-restricted-syntax
    for (const encounter of encounters) {
      // if there's a location associated with the encounter then that's already a good sign
      // that this is the most recent encounter we can use for displaying
      if (encounter.location) {
        Vue.$log.debug(`Found Encounter/${encounter.id} with location containing ${encounter.location?.length} entries`);

        // sort updates in-place
        encounter.location.sort((a, b) => {
          if (a.period?.start && b.period?.start) {
            return new Date(b.period.start) - new Date(a.period.start);
          }
          // only sort if period.start is set for both items. If not, treat them equally as no temporal
          // comparison is feasible
          return 0;
        });

        // eslint-disable-next-line no-restricted-syntax
        for (const locationEntry of encounter.location) {
          const locationReference = locationEntry.location.reference;
          if (locationReference) {
            // get the actual Location resource via the lookup call
            const location = locationLookup.get(locationReference);

            Vue.$log.debug(
              `Found location entry referencing location "${location.name}" with status "${locationEntry.status}"`
            );

            // replace reference with the actual location object
            locationEntry.location = location;

            return { encounter, locationEntry };
          }

          // if no reference is set, there still might be a display element we could use
          const locationDisplay = locationEntry.location.display;
          if (locationDisplay) {
            Vue.$log.debug(`Found location entry with display "${locationDisplay}" with status "${locationEntry.status}"`);

            // replace reference with a "Location" object where only the name is set
            // this makes it easier to work with later on, since we don't have to duplicate
            // these various edge cases when actually displaying the location
            locationEntry.location = { name: locationDisplay };

            return { encounter, locationEntry };
          }

          // if neither reference nor display are set, maybe there's still the identifier value we could use
          const locationIdentifier = locationEntry.location.identifier;
          if (locationIdentifier) {
            Vue.$log.debug(
              `Found location entry with identifier "${locationIdentifier.value}" with status "${locationEntry.status}"`
            );

            locationEntry.location = { name: locationIdentifier.value };

            return { encounter, locationEntry };
          }
        }
      }

      // if Encounter.location doesn't contain a viable location, try using the Encounter.serviceProvider.display
      if (encounter.serviceProvider?.display) {
        return {
          encounter,
          locationEntry: { location: { name: encounter.serviceProvider.display } },
        };
      }
    }

    return null;
  },
  async fetchAllRecommendationsByPatientId(patientId) {
    const client = createFhirClient();

    return client.request(`ResearchSubject?patient=Patient/${patientId}&_include=ResearchSubject:study&_pretty=false`, {
      flat: true,
      pageLimit: 0,
      resolveReferences: ["study"],
    });
  },
  async deleteList(listId) {
    let answer;

    const client = createFhirClient();

    const screeningList = await client.request(`List/${listId}`, {
      flat: true,
      pageLimit: 0,
    });

    const studyReference = screeningList.extension[0].valueReference.reference;
    Vue.$log.debug(`Found ${studyReference} for List/${listId}`);

    answer = await client.delete(`List/${listId}`);
    Vue.$log.debug(`Delete List ${listId} response:`, answer);

    answer = await client.delete(`ResearchSubject/?study=${studyReference}`);
    Vue.$log.debug(`Delete subjects for ${studyReference} response:`, answer);

    answer = await client.delete(studyReference);
    Vue.$log.debug(`Delete ${studyReference} response:`, answer);
  },
};

export default actions;
