<template>
  <div class="condition-list">
    <b-table :data="normalizedObservations" :striped="true" sort-icon="menu-up">
      <b-table-column v-slot="props" label="Parameter">
        <span class="observation-display">
          <template v-if="props.row.code.text">
            {{ props.row.code.text }}
          </template>
          <template v-else>unbekannt</template></span
        ></b-table-column
      >
      <b-table-column v-slot="props" label="Wert">{{ getObservationValue(props.row) }}</b-table-column>
      <b-table-column v-slot="props" label="Zeitpunkt" centered>
        <b-tag type="is-primary"
          ><template v-if="props.row.effectiveDateTime">
            {{ new Date(props.row.effectiveDateTime).toLocaleDateString() }}
          </template>
          <template v-else>unbekannt</template></b-tag
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
</template>

<script>
import fhirpath from "fhirpath";

export default {
  name: "ObservationList",
  components: {},
  props: {
    items: {
      type: Array,
      required: false,
      default: () => [],
    },
  },
  data() {
    return {};
  },
  computed: {
    normalizedObservations() {
      return this.items.map((observation) => {
        const normalizedObservation = observation;

        const effectiveDateTime = fhirpath.evaluate(
          observation,
          "effectiveDateTime | effectivePeriod.start | effectiveInstant"
        )[0];

        normalizedObservation.effectiveDateTime = effectiveDateTime;

        if (!observation.code) {
          normalizedObservation.code = {};
        }

        const display = fhirpath.evaluate(observation, "code.text | code.coding.display | code.coding.code")[0];

        if (display) {
          normalizedObservation.code.text = display;
        }

        return normalizedObservation;
      });
    },
  },
  methods: {
    getObservationValue(o) {
      if (Array.isArray(o.component)) {
        return o.component.map((c) => {
          const result = this.getObservationValue(c, true);
          return `${result}; `;
        });
      }

      if (Object.prototype.hasOwnProperty.call(o, "valueBoolean")) {
        return !o.valueBoolean || o.valueBoolean === "false" ? "Negative" : "Positive";
      }

      if (Object.prototype.hasOwnProperty.call(o, "valueCodeableConcept")) {
        return fhirpath.evaluate(
          o,
          "valueCodeableConcept.text | valueCodeableConcept.coding.display | valueCodeableConcept.coding.code"
        )[0];
      }

      if (Object.prototype.hasOwnProperty.call(o, "valueQuantity")) {
        let { value } = o.valueQuantity;
        const { unit } = o.valueQuantity;

        if (!Number.isNaN(parseFloat(value))) {
          value = Math.round(value * 100) / 100;
        }

        return `${value} ${unit}`;
      }

      if (Object.prototype.hasOwnProperty.call(o, "valueRatio")) {
        return `${o.valueRatio.numerator} / ${o.valueRatio.denominator}`;
      }

      return fhirpath.evaluate(o, "valueString | valueInteger | valueRange | valueTime | valueDateTime | valuePeriod")[0];
    },
  },
};
</script>

<style scoped>
header {
  margin-bottom: 1.25rem;
}
</style>
