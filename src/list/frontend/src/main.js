import Vue from "vue";
import Buefy from "buefy";
import VueLogger from "vuejs-logger";
import { library } from "@fortawesome/fontawesome-svg-core";
import { fas } from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import VueKeycloakJs from "@dsb-norge/vue-keycloak-js";
import axios from "axios";
import router from "./router";
import App from "./App.vue";

library.add(fas);

const isProduction = process.env.NODE_ENV === "production";

const options = {
  isEnabled: true,
  logLevel: isProduction ? "error" : "debug",
  stringifyArguments: false,
  showLogLevel: true,
  showMethodName: false,
  separator: ":",
  showConsoleColors: true,
};

Vue.use(VueLogger, options);

Vue.component("VueFontawesome", FontAwesomeIcon);

Vue.use(Buefy, {
  defaultIconComponent: "vue-fontawesome",
  defaultIconPack: "fas",
});

Vue.config.productionTip = false;

axios
  .get(process.env.VUE_APP_CONFIG_URL || "/config")
  .then((response) => {
    // handle success
    Vue.$log.info("Using config: ", response.data);
    if (!response.data.isKeycloakDisabled) {
      Vue.use(VueKeycloakJs, {
        config: response.data,
        init: {
          onLoad: "login-required",
          checkLoginIframe: !response.data.checkLoginIframeDisabled,
        },
        onReady: () => {
          new Vue({
            router,
            render: (h) => h(App),
          }).$mount("#app");
        },
      });
    } else {
      new Vue({
        router,
        render: (h) => h(App),
      }).$mount("#app");
    }
  })
  .catch((error) => {
    Vue.$log.error("Failed to fetch config: ", error);
  });
