<template>
  <div class="last-stay">
    <template v-if="isLoading">
      <b-skeleton width="30%" :animated="true"></b-skeleton>
      <b-skeleton width="70%" :animated="true"></b-skeleton>
      <b-skeleton width="70%" :animated="true"></b-skeleton>
    </template>
    <b-message v-else-if="errorMessage" type="is-danger">
      Fehler beim Laden:
      <br />
      <pre>{{ errorMessage }}</pre>
    </b-message>
    <template v-else>
      <template v-if="encounterPeriod">
        <span v-if="encounterPeriod.end" class="is-size-7 has-text-weight-semibold">
          {{ new Date(encounterPeriod.start).toLocaleDateString() }}
          -
          {{ new Date(encounterPeriod.end).toLocaleDateString() }}:
        </span>
        <span v-else class="is-size-7 has-text-weight-semibold">
          seit
          {{ new Date(encounterPeriod.start).toLocaleDateString() }}:
        </span>
        <br />
      </template>
      <p v-if="latestEncounterLocation">
        <span class="has-text-weight-semibold">{{ latestEncounterLocation.name }}</span>
        <br />
        <span v-for="(telecom, index) in latestEncounterLocation.telecom" :key="index">
          <b-icon pack="fas" :icon="getIconNameFromContactPointSystem(telecom.system)" size="is-small"> </b-icon>
          {{ telecom.value }}
          <br />
        </span>
      </p>
    </template>
  </div>
</template>

<script>
export default {
  name: "LastStay",
  components: {},
  props: {
    // ResearchSubject.individual should be resolved and replaced with the actual Patient object
    subject: { default: () => null, type: Object },
    latestEncounterAndLocation: { default: () => null, type: Object },
    isLoading: { default: () => false, type: Boolean },
    errorMessage: { default: () => "", type: String },
  },
  data() {
    return {
      contactPointSystemToIcon: {
        phone: "phone",
        fax: "fax",
        email: "at",
        pager: "pager",
        url: "fa-external-link-alt",
        sms: "sms",
        other: "phone",
        default: "phone",
      },
    };
  },
  computed: {
    // if https://www.hl7.org/fhir/encounter-definitions.html#Encounter.location.period is set,
    // use it to display the stay begin and end. If it is not set,
    // use https://www.hl7.org/fhir/encounter-definitions.html#Encounter.period.
    // In general, Encounter.location.period may be more accurate, so if available, use it.
    // Fallback to Encounter.period otherwise.
    encounterPeriod() {
      const locationEntryComponent = this.latestEncounterAndLocation?.locationEntry;
      return locationEntryComponent?.period || this.latestEncounterAndLocation?.encounter?.period;
    },
    latestEncounterLocation() {
      return this.latestEncounterAndLocation?.locationEntry?.location;
    },
  },
  methods: {
    getIconNameFromContactPointSystem(system) {
      return this.contactPointSystemToIcon[system] || this.contactPointSystemToIcon.default;
    },
  },
};
</script>

<style scoped>
.last-stay {
  max-width: 200px;
}
</style>
