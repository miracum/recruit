<template>
  <div class="screening-list-card card">
    <div class="card-content">
      <div class="media">
        <div class="media-left">
          <b-tag type="is-primary" size="is-large" rounded>{{ numberOfResearchSubjects }}</b-tag>
        </div>
        <div class="media-content">
          <div class="content">
            <router-link
              :to="{
                name: 'patient-recommendations-by-id',
                params: { listId: list.id },
              }"
            >
              <h4 class="title is-4 mb-0">
                {{ displayName }}
              </h4>
            </router-link>
          </div>
        </div>
        <div v-if="showActiveToggle" class="media-right">
          <b-field>
            <b-switch :value="isActive" @click.native.prevent="onInput">{{ isActive ? "Aktiv" : "Inaktiv" }}</b-switch>
          </b-field>
        </div>
        <div v-if="!isActive">
          <b-tooltip label="ALLE Daten zur Studie vom FHIR Server löschen" position="is-left">
            <b-button size="is-small" icon-left="trash" target="_blank" rel="noopener noreferrer" @click="deleteStudy">
            </b-button>
          </b-tooltip>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import fhirpath from "fhirpath";
import Constants from "@/const";

export default {
  name: "ScreeningListCard",
  components: {},
  props: {
    list: {
      type: Object,
      required: false,
      default: () => ({
        id: null,
      }),
    },
    showActiveToggle: {
      type: Boolean,
      required: false,
      default: () => false,
    },
  },
  computed: {
    displayName() {
      const study = fhirpath.evaluate(this.list, "List.extension(%url).valueReference", {
        url: Constants.URL_LIST_BELONGS_TO_STUDY_EXTENSION,
      })[0];

      const acronym = fhirpath.evaluate(study, "ResearchStudy.extension(%acronymSystem).valueString", {
        acronymSystem: Constants.SYSTEM_STUDY_ACRONYM,
      })[0];

      return acronym || study.title || study.description;
    },
    numberOfResearchSubjects() {
      return this.list?.entry?.length || 0;
    },
    isActive() {
      return this.list?.status === "current";
    },
  },
  methods: {
    onInput() {
      this.$emit("input", { event: !this.isActive, list: this.list });
    },
    deleteStudy() {
      this.$emit("deleteList", { list: this.list });
    },
  },
};
</script>

<style scoped></style>
