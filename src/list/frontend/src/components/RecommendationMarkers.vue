<template>
  <div class="recommendation-markers">
    <template v-if="isLoading">
      <b-skeleton width="30%" :animated="true"></b-skeleton>
    </template>
    <b-message v-else-if="errorMessage" type="is-danger">
      Fehler beim Laden:
      <br />
      <pre>{{ errorMessage }}</pre>
    </b-message>
    <template v-else>
      <b-field grouped group-multiline>
        <div v-if="allRecommendedStudies.length > 0" class="control">
          <b-tooltip position="is-right" multilined>
            <b-taglist attached>
              <b-tag type="is-dark"><b-icon icon="lightbulb" size="is-small" type="is-white"> </b-icon></b-tag>
              <b-tag type="is-info" class="all-recommendations-count">{{ allRecommendedStudies.length }}</b-tag>
            </b-taglist>
            <template #content>
              Der Patient wurde für folgende Studien vorgeschlagen:
              <ol type="1">
                <li v-for="(study, index) in allRecommendedStudies" :key="index">
                  {{ getAcronymFromStudy(study) || study.title }}
                </li>
              </ol>
            </template>
          </b-tooltip>
        </div>
        <div v-if="participatingStudies.length > 0" class="control">
          <b-tooltip position="is-right" multilined>
            <b-taglist attached>
              <b-tag type="is-dark"><b-icon icon="graduation-cap" size="is-small" type="is-white"> </b-icon></b-tag>
              <b-tag type="is-danger is-light" class="participating-studies-count">{{ participatingStudies.length }}</b-tag>
            </b-taglist>
            <template #content>
              Der Patient ist bereits in folgende Studien eingeschlossen:
              <ol type="1">
                <li v-for="(study, index) in participatingStudies" :key="index">
                  {{ getAcronymFromStudy(study) || study.title }}
                </li>
              </ol>
            </template>
          </b-tooltip>
        </div>
        <div v-if="isNoLongerEligible" class="control">
          <b-tooltip position="is-right" multilined>
            <b-tag type="is-dark" class="is-no-longer-eligible"
              ><b-icon icon="user-times" size="is-small" type="is-white"> </b-icon
            ></b-tag>
            <template #content>
              Der Rekrutierungsvorschlag wurde vom System nachträglich als nicht mehr passend identifiziert. Mögliche Gründe
              sind der Tod des Patienten oder eine neue Datenlage laut derer die Ein- und Ausschlusskriterien nicht mehr erfüllt
              werden.
            </template>
          </b-tooltip>
        </div>
      </b-field>
    </template>
  </div>
</template>

<script>
import fhirpath from "fhirpath";
import Constants from "@/const";

export default {
  name: "RecommendationMarkers",
  components: {},
  props: {
    // ResearchSubject.individual should be resolved and replaced with the actual Patient object
    patientId: { default: () => null, type: String },
    allRecommendedStudies: { default: () => null, type: Array },
    participatingStudies: { default: () => null, type: Array },
    isLoading: { default: () => false, type: Boolean },
    errorMessage: { default: () => "", type: String },
    isNoLongerEligible: { default: () => false, type: Boolean },
  },
  methods: {
    getAcronymFromStudy(study) {
      return fhirpath.evaluate(study, "ResearchStudy.extension(%acronymSystem).valueString", {
        acronymSystem: Constants.SYSTEM_STUDY_ACRONYM,
      })[0];
    },
  },
};
</script>
