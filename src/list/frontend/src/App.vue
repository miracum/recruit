<template>
  <div id="app">
    <header>
      <b-navbar type="is-primary">
        <template #brand>
          <b-navbar-item tag="router-link" :to="{ path: '/' }">
            <picture>
              <source srcset="@/assets/miracum-logo.webp" type="image/webp" />
              <source srcset="@/assets/miracum-logo.png" type="image/png" />
              <img src="@/assets/miracum-logo.png" alt="MIRACUM Logo" />
            </picture>
            <span class="navbar-item has-text-white">MIRACUM Rekrutierungsunterstützung</span>
          </b-navbar-item>
        </template>

        <template #end>
          <b-navbar-item type="is-primary" tag="div">
            <b-icon pack="fas" size="is-small" type="is-white" :icon="isAdmin ? 'user-cog' : 'user'"></b-icon>
            <span class="mr-3 ml-3 has-text-white">{{ username }}</span>
            <div class="buttons">
              <b-tooltip :label="isDarkTheme ? 'Helles Design aktivieren' : 'Dunkles Design aktivieren'" position="is-left">
                <b-button outlined size="is-small" type="is-white" :icon-left="isDarkTheme ? 'sun' : 'moon'" @click="toggleTheme">
                </b-button>
              </b-tooltip>
              <b-button v-if="isAuthenticated" outlined type="is-white" size="is-small" @click="logout">Ausloggen
              </b-button>
              <b-tooltip label="Benutzerhilfe öffnen" position="is-left">
                <b-button tag="a" outlined size="is-small" type="is-white" icon-left="question" href="/help/manual.pdf">
                </b-button>
              </b-tooltip>
            </div>
          </b-navbar-item>
        </template>
      </b-navbar>
    </header>
    <main>
      <section class="container content">
        <router-view />
      </section>
    </main>
    <footer class="footer">
      <div class="content has-text-centered is-size-7 has-text-grey-light">
        <p>{{ version }}</p>
      </div>
    </footer>
  </div>
</template>

<script>
export default {
  name: "App",
  data() {
    return {
      isDarkTheme: false,
    };
  },
  computed: {
    version: () => import.meta.env.VITE_APP_VERSION,
    username: function username() {
      return (this.$keycloak && this.$keycloak.fullName) || "Anonym";
    },
    isAuthenticated: function isAuthenticated() {
      return (this.$keycloak && this.$keycloak.authenticated) || false;
    },
    isAdmin: function isAdmin() {
      return this.isAuthenticated && this.$keycloak.hasResourceRole("admin");
    },
  },
  created() {
    const storedTheme = localStorage.getItem("theme");
    this.isDarkTheme = storedTheme ? storedTheme === "dark" : window.matchMedia("(prefers-color-scheme: dark)").matches;
    if (storedTheme) {
      document.documentElement.dataset.theme = storedTheme;
    }
  },
  methods: {
    logout: function logout() {
      this.$keycloak.logoutFn();
    },
    toggleTheme: function toggleTheme() {
      this.isDarkTheme = !this.isDarkTheme;
      const theme = this.isDarkTheme ? "dark" : "light";
      document.documentElement.dataset.theme = theme;
      localStorage.setItem("theme", theme);
    },
  },
};
</script>

<style lang="scss">
// Set your colors and import Bulma's core + Buefy styles.
// Bulma 1.x / Buefy 3.x are configured via the Sass module system (`@use ... with`)
// instead of plain variable overrides before a classic `@import`.
$brand-primary: #1b2259;
$brand-success: #00a579;

@use "bulma/sass" with (
  $primary: $brand-primary,
  $link: $brand-primary,
  $success: $brand-success,
);
@use "buefy/src/scss/buefy";
</style>

<style>
#app {
  height: 100%;
  display: flex;
  min-height: 100vh;
  flex-direction: column;
}

main {
  flex: 1 0 auto;
  width: 100%;
  margin-top: 15px;
}

.navbar-brand>.navbar-item>picture>img {
  border-radius: 50%;
  min-height: 3rem;
}

.navbar-menu {
  background-color: #1b2259;
}
</style>
