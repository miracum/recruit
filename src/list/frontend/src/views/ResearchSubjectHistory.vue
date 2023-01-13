<template>
  <div class="researchsubject-history">
    <b-loading :active="isLoading" />
    <template v-if="!isLoading">
      <b-message v-if="failedToLoad" type="is-danger">
        Historie konnte nicht geladen werden:
        <br />
        <pre>{{ errorMessage }}</pre>
      </b-message>
      <template v-else>
        <header class="has-text-centered">
          <h1 class="title is-3">Patient {{ mrNumber || subject.id }}</h1>
          <h2 class="subtitle is-5">
            geboren {{ new Date(subject.birthDate).getFullYear() }},
            {{ subject.gender === "male" ? "männlich" : "weiblich" }}
          </h2>
        </header>
        <div class="timeline">
          <div v-for="(entry, index) in history" :key="index">
            <div class="card history-item">
              <span class="dot has-background-primary"></span>
              <div class="card-content">
                <p class="history-item-date">
                  {{ new Date(entry.meta.lastUpdated).toLocaleString("de-DE") }}
                </p>
                <p>
                  Status:
                  <strong>{{ translateSubjectStatus(entry.status) }}</strong>
                </p>
                <template v-if="getHistoryNote(entry)">
                  <p>Notiz:</p>
                  <pre>{{ getHistoryNote(entry) }}</pre>
                </template>
              </div>
            </div>
          </div>
        </div>
      </template>
    </template>
  </div>
</template>

<script>
import fhirpath from "fhirpath";
import Constants from "@/const";
import Api from "@/api";

export default {
  name: "ResearchSubjectHistory",
  components: {},
  props: {
    subjectId: {
      type: String,
      required: false,
      default: () => null,
    },
  },
  data() {
    return {
      history: {},
      subject: {},
      noData: false,
      errorMessage: "",
      isLoading: true,
      failedToLoad: false,
    };
  },
  computed: {
    mrNumber() {
      return fhirpath.evaluate(
        this.subject,
        "Patient.identifier.where(type.coding.system=%identifierType and type.coding.code='MR').value",
        {
          identifierType: Constants.SYSTEM_IDENTIFIER_TYPE,
        }
      )[0];
    },
  },
  async mounted() {
    try {
      this.history = await Api.fetchSubjectHistory(this.subjectId);

      if (this.history.length === 0) {
        this.noData = true;
      } else {
        this.subject = this.history[0].individual;
      }
    } catch (exc) {
      this.errorMessage = exc;
      this.failedToLoad = true;
    } finally {
      this.isLoading = false;
    }
  },
  methods: {
    getHistoryNote(researchSubject) {
      return fhirpath.evaluate(researchSubject, "ResearchSubject.extension(%noteExtensionUrl).valueString", {
        noteExtensionUrl: Constants.URL_NOTE_EXTENSION,
      })[0];
    },
    translateSubjectStatus(status) {
      return Constants.STATUS_TRANSLATION[status];
    },
  },
};
</script>

<style scoped>
.timeline {
  position: relative;
  border-left: 1px solid #1b2259;
}

.timeline .history-item {
  position: relative;
  left: 20px;
  margin: 10px 0;
}

.timeline .history-item .dot {
  display: block;
  position: absolute;
  width: 10px;
  height: 10px;
  border-radius: 50%;
  left: -25.5px;
  top: calc(50% - 5px);
}
</style>
