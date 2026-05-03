module.exports = {
  transpileDependencies: ["fhirpath", "fhirclient", "debug", "@lhncbc/ucum-lhc"],
  configureWebpack: {
    resolve: {
      alias: {
        "@dsb-norge/vue-keycloak-js$": "@dsb-norge/vue-keycloak-js/dist/dsb-vue-keycloak.es.js",
      },
    },
  },
};
