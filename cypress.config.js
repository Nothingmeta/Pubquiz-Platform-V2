const { defineConfig } = require("cypress");

module.exports = defineConfig({
  e2e: {
    baseUrl: "http://localhost:5000",
    supportFile: false,
    setupNodeEvents(on, config) {
      // add plugins/event handlers here if needed
      return config;
    },
  },
});