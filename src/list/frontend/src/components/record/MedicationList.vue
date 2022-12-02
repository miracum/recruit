<template>
  <div class="medication-list">
    <div class="medication-statement">
      <h2 class="title is-5">Anamnese / Medikationsplan</h2>
      <b-table
        :paginated="true"
        :per-page="10"
        :pagination-simple="true"
        :data="bormalizedMedicationStatements"
        :striped="true"
        sort-icon="menu-up"
      >
        <b-table-column v-slot="props" label="Medikament">
          <b-tag type="is-primary" class="medication-statement-display"
            ><template v-if="props.row.medicationCodeableConcept.text">
              {{ props.row.medicationCodeableConcept.text }}
            </template>
            <template v-else>unbekannt</template></b-tag
          >
        </b-table-column>
        <b-table-column v-slot="props" field="effectiveDateTime" label="Zeitpunkt" sortable centered>
          <b-tag type="is-primary" class="medication-statement-effective"
            ><template v-if="props.row.effectiveDateTime">
              {{ new Date(props.row.effectiveDateTime).toLocaleDateString() }}
            </template>
            <template v-else>unbekannt</template></b-tag
          >
        </b-table-column>
        <b-table-column v-slot="props" field="recordedDate" label="Dokumentationszeitpunkt" sortable centered>
          <span class="tag is-success">
            <template v-if="props.row.recordedDate">
              {{ new Date(props.row.recordedDate).toLocaleDateString() }}
            </template>
            <template v-else>unbekannt</template></span
          >
        </b-table-column>
        <template slot="empty">
          <section class="section">
            <div class="content has-text-grey has-text-centered">
              <p>
                <b-icon icon="frown" size="is-large" />
              </p>
              <p>Keine Daten vorhanden.</p>
            </div>
          </section>
        </template>
      </b-table>
    </div>
    <div class="medication-administration">
      <h2 class="title is-5">Während des Aufenthalts verabreichte Medikation</h2>
      <b-table
        :paginated="true"
        :per-page="10"
        :data="medicationAdministrations"
        :striped="true"
        :pagination-simple="true"
        sort-icon="menu-up"
      >
        <b-table-column v-slot="props" label="Medikament"
          ><b-tag type="is-primary" class="medication-administration-display"
            ><template v-if="props.row.medicationCodeableConcept.text">
              {{ props.row.medicationCodeableConcept.text }}
            </template>
            <template v-else>unbekannt</template></b-tag
          ></b-table-column
        >
        <b-table-column v-slot="props" field="effectiveDateTime" label="Zeitpunkt" sortable centered>
          <b-tag type="is-primary" class="medication-administration-effective"
            ><template v-if="props.row.effectiveDateTime">
              {{ new Date(props.row.effectiveDateTime).toLocaleDateString() }}
            </template>
            <template v-else>unbekannt</template></b-tag
          >
        </b-table-column>
        <b-table-column v-slot="props" field="authoredOn" label="Dokumentationszeitpunkt" sortable centered>
          <b-tag type="is-primary">
            <b-tag type="is-primary" class="medication-administration-authored-on"
              ><template v-if="props.row.authoredOn">
                {{ new Date(props.row.authoredOn).toLocaleDateString() }}
              </template>
              <template v-else>unbekannt</template></b-tag
            ></b-tag
          >
        </b-table-column>
        <template slot="empty">
          <section class="section">
            <div class="content has-text-grey has-text-centered">
              <p>
                <b-icon icon="frown" size="is-large" />
              </p>
              <p>Keine Daten vorhanden.</p>
            </div>
          </section>
        </template>
      </b-table>
    </div>
  </div>
</template>

<script>
import fhirpath from "fhirpath";

export default {
  name: "MedicationList",
  components: {},
  props: {
    medicationStatements: {
      type: Array,
      required: false,
      default: () => [],
    },
    medicationAdministrations: {
      type: Array,
      required: false,
      default: () => [],
    },
  },
  data() {
    return {};
  },
  method: {
    normalizeResources(resources) {
      resources.map((medicationStatement) => {
        const normalizedMedicationStatement = medicationStatement;

        const effectiveDateTime = fhirpath.evaluate(medicationStatement, "effectiveDateTime | effectivePeriod.start")[0];

        normalizedMedicationStatement.effectiveDateTime = effectiveDateTime;

        if (!medicationStatement.medicationCodeableConcept) {
          normalizedMedicationStatement.medicationCodeableConcept = {};
        }

        const display = fhirpath.evaluate(
          normalizedMedicationStatement,
          "medicationCodeableConcept.text | medicationCodeableConcept.coding.display | medicationCodeableConcept.coding.code"
        )[0];

        if (display) {
          normalizedMedicationStatement.medicationCodeableConcept.text = display;
        }

        return normalizedMedicationStatement;
      });
    },
  },
  computed: {
    normalizedMedicationStatements() {
      return this.normalizeResources(this.medicationStatements);
    },
    normalizedMedicationAdministration() {
      return this.normalizeResources(this.medicationAdministration);
    },
  },
};
</script>

<style scoped>
header {
  margin-bottom: 1.25rem;
}
</style>
