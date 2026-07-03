<template>
  <div class="save-as-csv">
    <b-button size="is-small" icon-left="file-csv" @click="saveAsCsvFile"> Vorschläge als CSV-Datei herunterladen </b-button>
  </div>
</template>

<script>
import { stringify } from "csv-stringify/browser/esm/sync";

export default {
  name: "SaveAsCsv",
  components: {},
  props: {
    rows: { default: () => [], type: Array },
  },
  methods: {
    saveAsCsvFile() {
      const csvData = this.rows.map((patientViewModel) => ({
        id: patientViewModel.id,
        Vorschlagszeit: patientViewModel.date ? new Date(patientViewModel.date).toLocaleDateString() : "unbekannt",
        Patientennummer: patientViewModel.mrNumber,
        "Letzter Aufenthalt": patientViewModel.latestEncounterAndLocation?.locationEntry?.location?.name,
        Notizen: patientViewModel.note,
        Geburtsdatum: patientViewModel.subject?.individual?.birthDate,
        Geschlecht: patientViewModel.subject?.individual?.gender,
        Status: patientViewModel.subject?.status,
      }));
      const csv = stringify(csvData, {
        header: true,
      });
      this.saveAsFile(csv);
    },
    saveAsFile(data) {
      const date = new Date().toISOString().split("T")[0];
      const fileName = `${date}-rekrutierungsvorschläge.csv`;
      const a = document.createElement("a");
      document.body.appendChild(a);
      a.style = "display: none";
      const blob = new Blob([data], { type: "octet/stream" });
      const url = window.URL.createObjectURL(blob);
      a.href = url;
      a.download = fileName;
      a.click();
      window.URL.revokeObjectURL(url);
    },
  },
};
</script>
