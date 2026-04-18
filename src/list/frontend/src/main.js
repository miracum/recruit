/* eslint-disable no-console */
import { createApp } from "vue";
import Buefy from "buefy";
import VueLogger from "vuejs-logger";
import { library } from "@fortawesome/fontawesome-svg-core";
import { fas } from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import VueKeycloakJs from "@dsb-norge/vue-keycloak-js";
import axios from "axios";
import router from "./router";
import App from "./App.vue";
import { setKeycloak } from "./auth";

library.add(fas);

const isProduction = process.env.NODE_ENV === "production";

const loggerOptions = {
  isEnabled: true,
  logLevel: isProduction ? "error" : "debug",
  stringifyArguments: false,
  showLogLevel: true,
  showMethodName: false,
  separator: ":",
  showConsoleColors: true,
};

function createVueApp() {
  const app = createApp(App);
  app.component("VueFontawesome", FontAwesomeIcon);
  app.use(Buefy, {
    defaultIconComponent: "vue-fontawesome",
    defaultIconPack: "fas",
  });
  app.use(VueLogger, loggerOptions);
  app.use(router);
  app.mount("#app");
  return app;
}

axios
  .get(process.env.VUE_APP_CONFIG_URL || "/config")
  .then((response) => {
    const config = response.data;
    console.info("Using config: ", config);
    if (!config.isKeycloakDisabled) {
      const app = createApp(App);
      app.component("VueFontawesome", FontAwesomeIcon);
      app.use(Buefy, {
        defaultIconComponent: "vue-fontawesome",
        defaultIconPack: "fas",
      });
      app.use(VueLogger, loggerOptions);
      app.use(router);
      app.use(VueKeycloakJs, {
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
      createVueApp();
    }
  })
  .catch((error) => {
    console.error("Failed to fetch config: ", error);
  });
