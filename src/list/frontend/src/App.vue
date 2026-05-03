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
    <footer class="footer" style="background-color: #f0f3fb">
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
    isAdmin: function isAdmin() {
      return this.isAuthenticated && this.$keycloak.hasResourceRole("admin");
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
// Override Bulma primary color (HSL components for bulma 1.x)
:root {
  --bulma-primary-h: 233;
  --bulma-primary-s: 53%;
  --bulma-primary-l: 23%;

  --bulma-success-h: 164;
  --bulma-success-s: 100%;
  --bulma-success-l: 32%;

  --bulma-link-h: 233;
  --bulma-link-s: 53%;
  --bulma-link-l: 23%;
}

$fullhd: 1652px;

// Import Bulma and Buefy styles
@import "bulma";
@import "buefy/src/scss/buefy";
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

.navbar-brand>.navbar-item>picture>img {
  border-radius: 50%;
  min-height: 3rem;
}

.navbar-menu {
  background-color: #1b2259;
}
</style>
