/* eslint-disable no-console */
import { createApp } from "vue";
import Buefy from "buefy";
import { library } from "@fortawesome/fontawesome-svg-core";
import { fas } from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import VueKeyCloak from "@dsb-norge/vue-keycloak-js";
import axios from "axios";
import router from "./router";
import App from "./App.vue";
import { setKeycloak } from "./auth";

library.add(fas);

function createVueApp() {
  const app = createApp(App);
  app.component("VueFontawesome", FontAwesomeIcon);
  app.use(Buefy, {
    defaultIconComponent: "vue-fontawesome",
    defaultIconPack: "fas",
  });
  app.use(router);
  return app;
}

axios
  .get(process.env.VUE_APP_CONFIG_URL || "/config")
  .then((response) => {
    const config = response.data;
    console.info("Using config: ", config);
    const app = createVueApp();
    if (config.isKeycloakDisabled === false) {
      app.use(VueKeyCloak, {
        config: config.keycloak,
        init: {
          onLoad: "login-required",
          checkLoginIframe: !config.checkLoginIframeDisabled,
        },
        onReady: (keycloak) => {
          setKeycloak(keycloak);
          app.mount("#app");
        },
      });
    } else {
      app.mount("#app");
    }
  })
  .catch((error) => {
    console.error("Failed to fetch config: ", error);
  });
