import js from "@eslint/js";
import pluginVue from "eslint-plugin-vue";
import vitest from "@vitest/eslint-plugin";
import eslintConfigPrettier from "eslint-config-prettier/flat";
import globals from "globals";

const isProduction = process.env.NODE_ENV === "production";

export default [
  {
    ignores: ["dist/**", "coverage/**", "tests/e2e/**"],
  },
  js.configs.recommended,
  ...pluginVue.configs["flat/recommended"],
  eslintConfigPrettier,
  {
    languageOptions: {
      ecmaVersion: "latest",
      sourceType: "module",
      globals: {
        ...globals.node,
        ...globals.browser,
      },
    },
    rules: {
      "no-console": isProduction ? "error" : "off",
      "no-debugger": isProduction ? "error" : "off",
      quotes: ["error", "double"],
      "max-len": "off",
    },
  },
  {
    files: ["tests/unit/**/*.spec.js", "**/__tests__/*.js"],
    plugins: { vitest },
    rules: {
      ...vitest.configs.recommended.rules,
    },
    languageOptions: {
      globals: {
        ...vitest.configs.env.languageOptions.globals,
      },
    },
  },
];
