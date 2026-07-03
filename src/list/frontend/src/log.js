/* eslint-disable no-console */
// Minimal replacement for vuejs-logger, which has no Vue 3-compatible release.

const isProduction = process.env.NODE_ENV === "production";

const levels = ["debug", "info", "warn", "error"];
const minLevelIndex = levels.indexOf(isProduction ? "error" : "debug");

const log = {};

levels.forEach((level, index) => {
  log[level] = (...args) => {
    if (index >= minLevelIndex) {
      console[level](`[${level.toUpperCase()}]`, ...args);
    }
  };
});

export default log;
