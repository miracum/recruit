<template>
  <div class="overview">
    <b-loading :active="isLoading" />
    <b-message v-if="failedToLoad" type="is-danger">
      Rekrutierungsvorschläge konnten nicht geladen werden:
      <br />
      <pre>{{ errorMessage }}</pre>
    </b-message>
    <b-message v-else-if="noLists" type="is-warning"
      >Keine Rekrutierungsvorschläge vorhanden. <br />
      Ggf. fehlen die notwendingen Berechtigungen um Rekrutierungsvorschläge einsehen zu können. <br />
      Bitte wenden Sie sich an einen verantwortlichen Administrator.</b-message
    >
    <div v-else>
      <section></section>
      <section class="active-screening-lists">
        <h1 class="title is-3">Laufende Studien</h1>
        <screening-list-card
          v-for="(list, index) in activeScreeningLists"
          :key="'active-list-' + index"
          :list="list"
          :show-active-toggle="isLoggedInAsAdmin"
          @input="onListStatusToggled"
        />
      </section>
      <section v-if="isLoggedInAsAdmin" class="inactive-screening-lists">
        <section></section>
        <h1 class="title is-3">Inaktive Studien</h1>
        <screening-list-card
          v-for="(list, index) in inactiveScreeningLists"
          :key="'inactive-list-' + index"
          :list="list"
          :show-active-toggle="isLoggedInAsAdmin"
          @input="onListStatusToggled"
          @deleteList="onListDelete"
        />
      </section>
    </div>
  </div>
</template>

<script>
import Api from "@/api";

import ScreeningListCard from "@/components/ScreeningListCard.vue";

export default {
  name: "ScreeningListOverview",
  components: {
    ScreeningListCard,
  },
  data() {
    return {
      screeningLists: [],
      failedToLoad: false,
      isLoading: true,
      noLists: false,
      errorMessage: "",
    };
  },
  computed: {
    isLoggedInAsAdmin() {
      if (!this.$keycloak) {
        // if keycloak is not set-up, default to displaying the UI as an admin
        return true;
      }

      if (this.$keycloak.ready && this.$keycloak.authenticated) {
        return this.$keycloak.hasResourceRole("admin");
      }

      return false;
    },
    activeScreeningLists() {
      return this.screeningLists?.filter((list) => list.status === "current");
    },
    inactiveScreeningLists() {
      return this.screeningLists?.filter((list) => list.status === "retired");
    },
  },
  async mounted() {
    try {
      const screeningLists = await Api.fetchCurrentAndRetiredLists();
      if (screeningLists.length !== 0) {
        this.screeningLists = screeningLists;
      } else {
        this.noLists = true;
      }
    } catch (exc) {
      this.errorMessage = exc;
      this.failedToLoad = true;
    } finally {
      this.isLoading = false;
    }
  },
  methods: {
    async onListStatusToggled(e) {
      this.$log.debug(`List status toggled to ${e.event} for ${e.list}`, e.event);

      const newStatus = e.event ? "current" : "retired";

      try {
        await Api.updateListStatus(e.list.id, newStatus);
        this.$buefy.toast.open({
          message: "Status der Liste aktualisiert!",
          type: "is-success",
        });

        const listToUpdate = this.screeningLists.find((l) => l.id === e.list.id);
        this.$log.debug(`Setting status for ${listToUpdate.id} to ${newStatus}`);
        listToUpdate.status = newStatus;
      } catch (exc) {
        this.$log.error(exc);
        this.$buefy.toast.open({
          message: `Fehler beim Aktualisieren des Status: ${exc.message}.`,
          type: "is-danger",
          duration: 30_000,
        });
      }
    },
    async onListDelete(e) {
      this.$log.debug(`List deleted to for ${e.list}`, e.event);

      try {
        await Api.deleteList(e.list.id);
        this.$buefy.toast.open({
          message: "Liste wurde gelöscht!",
          type: "is-success",
        });
        const listIndex = this.screeningLists.findIndex((l) => l.id === e.list.id);
        this.screeningLists.splice(listIndex, 1);
      } catch (exc) {
        this.$log.error(exc);
        if (`${exc.message}`.includes("Cannot DELETE")) {
          this.$buefy.toast.open({
            message: "Ihr FHIR-Server erlaubt diese Operation nicht",
            type: "is-danger",
            duration: 30_000,
          });
        } else {
          this.$buefy.toast.open({
            message: `Löschen fehlgeschlagen: ${exc.message}.`,
            type: "is-danger",
            duration: 30_000,
          });
        }
      }
    },
  },
};
</script>

<style scoped>
.patient-recommendations {
  min-height: 100px;
  margin-top: 15px;
}

.study-description-header {
  margin-bottom: 1rem;
}
</style>
