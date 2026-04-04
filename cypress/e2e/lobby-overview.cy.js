describe("LobbyOverview (authenticated)", () => {
  const testUser = { email: "e2e_lo@test.local", password: "Password123!", name: "E2E LO", role: "quizmaster" };

  before(() => {
    // create/test-signin (development-only endpoint) so protected page is reachable
    cy.request({
      method: "POST",
      url: "/test/create-and-signin",
      form: true,
      body: testUser,
      failOnStatusCode: false
    }).then((resp) => {
      expect(resp.status).to.be.oneOf([200, 201]);
    });
  });

  it("shows lobby overview UI for quizmaster", () => {
    cy.visit("/Quiz/LobbyOverview");
    cy.contains("Beschikbare Quiz Lobbies").should("be.visible");

    // join/search form
    cy.get('form[action*="/Quiz/Lobby"], form').within(() => {
      cy.get('input[name="lobbyCode"]').should("exist");
      cy.get("button").contains("Zoek & Join").should("exist");
    });

    // If quizmaster and quizzes exist, there will be a create form with select
    cy.get("body").then(($body) => {
      if ($body.find("select[name='quizId']").length) {
        cy.get("select[name='quizId']").should("exist");
        cy.get('button').contains("Start Quiz").should("exist");
      }
    });
  });
});