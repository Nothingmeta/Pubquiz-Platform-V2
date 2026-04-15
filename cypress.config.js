const { defineConfig } = require("cypress");
const { devServer } = require("@cypress/webpack-dev-server");

module.exports = defineConfig({
  e2e: {
    baseUrl: "http://localhost:5000",
    supportFile: false,
    setupNodeEvents(on, config) {
      return config;
    },
  },
  component: {
    specPattern: "cypress/component/**/*.cy.js",
    supportFile: false,
    indexHtmlFile: "cypress/support/component-index.html",
    devServer(devServerConfig) {
      return devServer({
        ...devServerConfig,
        framework: "react",
        webpackConfig: {
          mode: "development",
          resolve: {
            extensions: [".js"],
          },
        },
      });
    },
  },
});