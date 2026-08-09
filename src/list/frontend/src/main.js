import { createApp } from "vue";
import Buefy from "buefy";
import { library } from "@fortawesome/fontawesome-svg-core";
import { fas } from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import VueKeycloakJs from "@dsb-norge/vue-keycloak-js";
import axios from "axios";
import router from "./router";
import App from "./App.vue";
import log from "./log";

library.add(fas);

const app = createApp(App);

app.config.globalProperties.$log = log;

app.component("VueFontawesome", FontAwesomeIcon);

app.use(Buefy, {
  defaultIconComponent: "vue-fontawesome",
  defaultIconPack: "fas",
});

app.use(router);

axios
  .get(import.meta.env.VITE_APP_CONFIG_URL || "/config")
  .then((response) => {
    // handle success
    const config = response.data;
    log.info("Using config: ", config);
    if (!config.isKeycloakDisabled) {
      app.use(VueKeycloakJs, {
        config: config.keycloak,
        init: {
          onLoad: "login-required",
          checkLoginIframe: !config.checkLoginIframeDisabled,
        },
        onReady: () => {
          app.mount("#app");
        },
      });
    } else {
      app.mount("#app");
    }
  })
  .catch((error) => {
    log.error("Failed to fetch config: ", error);
  });
