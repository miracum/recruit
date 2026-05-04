<template>
  <div class="screening-list">
    <nav class="level">
      <!-- Left side -->
      <div class="level-left">
        <b-dropdown v-model="selectedFilterOptions" multiple>
          <template #trigger>
            <b-button type="is-primary" icon-right="sort-down">
              Vorschläge nach Status ausblenden:
              {{ selectedFilterOptions.length }}
            </b-button>
          </template>

          <b-dropdown-item
            v-for="(deFilterStatus, enFilterStatus) in recruitmentStatusOptions"
            :key="deFilterStatus"
            :value="enFilterStatus"
          >
            <span class="status-option-container">
              <b-icon pack="fas" size="is-small" icon="circle" :type="getTypeFromStatus(enFilterStatus)"></b-icon>

              <span>{{ deFilterStatus }}</span>
            </span>
          </b-dropdown-item>
        </b-dropdown>
      </div>

      <!-- Right side -->
      <div class="level-right">
        <save-as-csv :rows="patientViewModel" />
      </div>
    </nav>
    <section>
      <b-table
        :data="filteredSubjects"
        :loading="isLoading"
        :mobile-cards="true"
        sort-icon="menu-up"
        :striped="true"
        :hoverable="true"
        default-sort="date"
        default-sort-direction="desc"
      >
        <b-table-column v-slot="props" label="Vorschlagsdatum" field="date" sortable :visible="!hideRecommendationDate">
          <p class="subject-recommendation-date">
            <span v-if="props.row.date">
              {{ props.row.date.toLocaleDateString() }}
            </span>
            <span v-else> unbekannt </span>
          </p>
        </b-table-column>

        <b-table-column v-slot="props" label="Patientennummer" field="mrNumber" sortable>
          <p class="patient-id">
            {{ props.row.mrNumber }}
          </p>

          <recommendation-markers
            :all-recommended-studies="props.row.allRecommendedStudies"
            :participating-studies="props.row.participatingStudies"
            :is-no-longer-eligible="props.row.isNoLongerEligible"
            :is-loading="props.row.markerIsLoading"
            :error-message="props.row.markerErrorMessage"
          ></recommendation-markers>
        </b-table-column>

        <b-table-column
          v-slot="props"
          label="Demografie"
          field="subject.individual.birthDate"
          sortable
          :visible="!hideDemographics"
        >
          <span>
            geb.
            {{
              props.row.subject.individual.birthDate
                ? new Date(props.row.subject.individual.birthDate).getFullYear()
                : "unbekannt"
            }},
            {{
              props.row.subject.individual
                ? props.row.subject.individual.gender === "male"
                  ? "m"
                  : props.row.subject.individual.gender === "female"
                  ? "w"
                  : "u"
                : "u"
            }}
          </span>
        </b-table-column>

        <b-table-column v-slot="props" label="Letzter Aufenthalt" :visible="!hideLastVisit">
          <last-stay
            :subject="props.row.subject"
            :latest-encounter-and-location="props.row.latestEncounterAndLocation"
            :is-loading="props.row.lastStayIsLoading"
            :error-message="props.row.lastStayErrorMessage"
          ></last-stay>
        </b-table-column>
        <b-table-column v-slot="props" label="Notiz" field="note">
          <b-field>
            <b-input v-model="props.row.note" type="textarea"></b-input>
          </b-field>
        </b-table-column>

        <b-table-column v-slot="props" label="Status" field="subject.status" sortable>
          <b-dropdown v-model="props.row.subject.status">
            <b-button
              slot="trigger"
              :class="['button', 'recruitment-status-select', getTypeFromStatus(props.row.subject.status)]"
              type="button"
              size="is-small"
              icon-right="sort-down"
              >{{ recruitmentStatusOptions[props.row.subject.status] }}</b-button
            >
            <b-dropdown-item v-for="option in Object.keys(recruitmentStatusOptions)" :key="option" :value="option">
              <span class="status-option-container">
                <b-icon pack="fas" size="is-small" icon="circle" :type="getTypeFromStatus(option)"></b-icon>
                <span>{{ recruitmentStatusOptions[option] }}</span>
              </span>
            </b-dropdown-item>
          </b-dropdown>
        </b-table-column>

        <b-table-column v-slot="props" label="Aktionen">
          <div class="columns is-mobile">
            <div class="column">
              <b-tooltip label="Änderungen Speichern" position="is-bottom">
                <b-button
                  class="save-status"
                  type="is-primary"
                  size="is-small"
                  icon-left="save"
                  @click="onSaveRowChanges($event, props.row)"
                  >Speichern</b-button
                >
              </b-tooltip>
            </div>
          </div>

          <div class="columns is-vcentered">
            <div class="column">
              <b-tooltip v-if="!hideEhrButton" label="Patientenakte anzeigen" position="is-bottom" class="mr-2">
                <b-button
                  tag="router-link"
                  :to="{
                    name: 'patient-record',
                    params: { patientId: props.row.subject.individual.id },
                  }"
                  type="is-primary"
                  size="is-small"
                  icon-left="notes-medical"
                  outlined
                  target="_blank"
                  rel="noopener noreferrer"
                ></b-button>
              </b-tooltip>
              <b-tooltip label="Änderungshistorie anzeigen" position="is-bottom">
                <b-button
                  tag="router-link"
                  :to="{
                    name: 'researchsubject-history',
                    params: { subjectId: props.row.id },
                  }"
                  type="is-primary"
                  size="is-small"
                  icon-left="history"
                  outlined
                  target="_blank"
                  rel="noopener noreferrer"
                ></b-button>
              </b-tooltip>
            </div>
          </div>
        </b-table-column>

        <template slot="empty">
          <section class="section">
            <div class="content has-text-grey has-text-centered">
              <p>
                <b-icon icon="frown" size="is-large" />
              </p>
              <p>Keine Vorschläge vorhanden.</p>
            </div>
          </section>
        </template>
      </b-table>
    </section>
  </div>
</template>

<script>
import fhirpath from "fhirpath";
import Constants from "@/const";
import Api from "@/api";
import LastStay from "@/components/LastStay.vue";
import RecommendationMarkers from "@/components/RecommendationMarkers.vue";
import SaveAsCsv from "@/components/SaveAsCsv.vue";

export default {
  name: "ScreeningList",
  components: {
    LastStay,
    RecommendationMarkers,
    SaveAsCsv,
  },
  props: {
    items: {
      default: () => [],
      type: Array,
    },
    hideDemographics: {
      default: () => false,
      type: Boolean,
    },
    hideLastVisit: {
      default: () => false,
      type: Boolean,
    },
    hideEhrButton: {
      default: () => false,
      type: Boolean,
    },
    hideRecommendationDate: {
      default: () => false,
      type: Boolean,
    },
  },
  data() {
    return {
      selectedFilterOptions: [],
      latestEncounterAndLocationLookup: {},
      recommendationMarkerLookup: {},
      isLoading: false,
      recruitmentStatusOptions: {
        candidate: "Rekrutierungsvorschlag",
        screening: "Wird geprüft",
        ineligible: "Erfüllt E/A-Kriterien nicht",
        "on-study": "Wurde eingeschlossen",
        withdrawn: "Studienteilnahme abgelehnt",
      },
      fhirClient: {},
    };
  },
  computed: {
    patientViewModel() {
      return this.items.map((entry) => {
        const subject = entry.item;
        const mrNumber = fhirpath.evaluate(
          subject.individual,
          "Patient.identifier.where(type.coding.system=%identifierType and type.coding.code='MR').value",
          {
            identifierType: Constants.SYSTEM_IDENTIFIER_TYPE,
          }
        )[0];

        const note = fhirpath.evaluate(subject, "ResearchSubject.extension(%noteExtensionUrl).valueString", {
          noteExtensionUrl: Constants.URL_NOTE_EXTENSION,
        })[0];

        const statusCode = fhirpath.evaluate(entry, "flag.coding.where(system=%subjectStatus).code", {
          subjectStatus: Constants.SYSTEM_DETERMINED_SUBJECT_STATUS,
        })[0];

        const isNoLongerEligible = statusCode === "ineligible";

        return {
          id: subject.id,
          mrNumber: mrNumber || subject.individual.id,
          date: entry.date ? new Date(entry.date) : null,
          isNoLongerEligible,
          subject,
          note,
          latestEncounterAndLocation: this.latestEncounterAndLocationLookup[subject.individual.id]?.latestEncounterAndLocation,
          lastStayIsLoading: this.latestEncounterAndLocationLookup[subject.individual.id]?.lastStayIsLoading,
          lastStayErrorMessage: this.latestEncounterAndLocationLookup[subject.individual.id]?.lastStayErrorMessage,
          allRecommendedStudies: this.recommendationMarkerLookup[subject.individual.id]?.allRecommendedStudies,
          participatingStudies: this.recommendationMarkerLookup[subject.individual.id]?.participatingStudies,
          markerIsLoading: this.recommendationMarkerLookup[subject.individual.id]?.markerIsLoading,
          markerErrorMessage: this.recommendationMarkerLookup[subject.individual.id]?.markerErrorMessage,
        };
      });
    },
    filteredSubjects() {
      return this.patientViewModel.filter((entry) => !this.selectedFilterOptions.includes(entry.subject.status));
    },
  },
  async mounted() {
    this.items.map(async (element) => {
      let latestEncounterAndLocation = {};
      try {
        this.$set(this.latestEncounterAndLocationLookup, element.item.individual.id, {
          lastStayIsLoading: true,
        });
        latestEncounterAndLocation = await Api.fetchLatestEncounterWithLocation(element.item.individual.id);

        this.$set(this.latestEncounterAndLocationLookup, element.item.individual.id, {
          latestEncounterAndLocation,
          lastStayIsLoading: false,
          lastStayErrorMessage: "",
        });
      } catch (exc) {
        this.$set(this.latestEncounterAndLocationLookup, element.item.individual.id, {
          latestEncounterAndLocation,
          lastStayIsLoading: false,
          lastStayErrorMessage: exc,
        });
      }
    });

    this.items.map(async (element) => {
      let allRecommendedStudies = [];
      let participatingStudies = [];
      try {
        this.$set(this.recommendationMarkerLookup, element.item.individual.id, {
          markerIsLoading: true,
        });
        let allRecommendations = await Api.fetchAllRecommendationsByPatientId(element.item.individual.id);

        // ignore all subjects that refer to the same study as the one currently shown in this screening list
        allRecommendations = allRecommendations.filter(
          (resource) => resource.resourceType === "ResearchSubject" && resource.study.id !== element.item.study.id
        );

        // include only subjects/studies where the patient is not ineligible or withdrawn from
        // the filter ensures that if a patient's recruitment status is
        // set to `ineligible`, the referenced study is not included in the list of the patient's
        // total recommendations
        allRecommendedStudies = allRecommendations
          .filter(
            (resource) =>
              resource.resourceType === "ResearchSubject" && resource.status !== "ineligible" && resource.status !== "withdrawn"
          )
          .map((researchSubject) => researchSubject.study);

        participatingStudies = allRecommendations
          .filter((resource) => resource.resourceType === "ResearchSubject" && resource.status === "on-study")
          .map((researchSubject) => researchSubject.study);

        this.$set(this.recommendationMarkerLookup, element.item.individual.id, {
          allRecommendedStudies,
          participatingStudies,
          markerIsLoading: false,
          markerErrorMessage: "",
        });
      } catch (exc) {
        this.$set(this.recommendationMarkerLookup, element.item.individual.id, {
          allRecommendedStudies,
          participatingStudies,
          markerIsLoading: false,
          markerErrorMessage: exc,
        });
      }
    });
  },
  methods: {
    getTypeFromStatus(status) {
      const lookup = {
        candidate: "is-info",
        eligible: "is-success",
        ineligible: "is-danger",
        withdrawn: "is-dark",
        "on-study": "is-success",
        screening: "is-warning",
        default: "",
      };

      return lookup[status] || lookup.default;
    },
    async onSaveRowChanges(_event, row) {
      try {
        await Api.updateResearchSubject(row.id, row.note, row.subject.status);
        this.$buefy.toast.open({
          message: "Rekrutierungsstatus aktualisiert!",
          type: "is-success",
        });
      } catch (exc) {
        this.$log.error(exc);
        this.$buefy.toast.open({
          message: `Fehler beim setzen des Rekrutierungsstatus: ${exc.message}.`,
          type: "is-danger",
          duration: 30_000,
        });
      }
    },
  },
};
</script>

<style scoped>
.status-option-container > .icon {
  vertical-align: middle;
  margin-right: 1rem;
}

.status-option-container > span {
  vertical-align: middle;
}

.patient-id {
  word-wrap: break-word;
  max-width: 25ch;
}
</style>
