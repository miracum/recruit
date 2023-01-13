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
            <b-icon pack="fas" size="is-small" type="is-white" icon="user"></b-icon>
            <span class="mr-3 ml-3 has-text-white">{{ username }}</span>
            <div class="buttons">
              <b-button v-if="isAuthenticated" outlined type="is-white" size="is-small" @click="logout">Ausloggen </b-button>
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
    <footer class="footer has-background-primary-muted">
      <div class="content has-text-centered is-size-7 has-text-grey-light">
        <p>{{ version }}</p>
      </div>
    </footer>
  </div>
</template>

<script>
export default {
  name: "App",
  computed: {
    version: () => process.env.VUE_APP_VERSION,
    username: function username() {
      return (this.$keycloak && this.$keycloak.fullName) || "Anonym";
    },
    isAuthenticated: function isAuthenticated() {
      return (this.$keycloak && this.$keycloak.authenticated) || false;
    },
  },
  methods: {
    logout: function logout() {
      this.$keycloak.logoutFn();
    },
  },
};
</script>

<style lang="scss">
// Import Bulma's core
@import "~bulma/sass/utilities/_all";

// Set your colors
$primary: #1b2259;
$primary-invert: findColorInvert($primary);

$primary-muted: #f0f3fb;
$primary-muted-invert: findColorInvert($primary-muted);

$success: #00a579;
$success-invert: findColorInvert($success);

// Setup $colors to use as bulma classes (e.g. 'is-twitter')
$colors: (
  "white": (
    $white,
    $black,
  ),
  "black": (
    $black,
    $white,
  ),
  "light": (
    $light,
    $light-invert,
  ),
  "dark": (
    $dark,
    $dark-invert,
  ),
  "primary": (
    $primary,
    $primary-invert,
  ),
  "primary-muted": (
    $primary-muted,
    $primary-muted-invert,
  ),
  "info": (
    $info,
    $info-invert,
  ),
  "success": (
    $success,
    $success-invert,
  ),
  "warning": (
    $warning,
    $warning-invert,
  ),
  "danger": (
    $danger,
    $danger-invert,
  ),
);

// Links
$link: $primary;
$link-invert: $primary-invert;
$link-focus-border: $primary;

$fullhd: 1652px + (2 * $gap);

// Import Bulma and Buefy styles
@import "~bulma";
@import "~buefy/src/scss/buefy";
</style>

<style>
#app {
  height: 100%;
  display: flex;
  min-height: 100vh;
  flex-direction: column;
  background-color: #ffffff;
}

main {
  flex: 1 0 auto;
  width: 100%;
  margin-top: 15px;
}

.navbar-brand > .navbar-item > picture > img {
  border-radius: 50%;
  min-height: 3rem;
}

.navbar-menu {
  background-color: #1b2259;
}
</style>
