describe("Login page", () => {
  beforeEach(() => {
    cy.clearCookies();
    cy.clearLocalStorage();
    cy.visit("/Auth/Login");
  });

  it("renders login inputs, button, and register link", () => {
    cy.get("#email").should("exist");
    cy.get("#password").should("exist");
    cy.get("#loginBtn").should("exist").and("have.attr", "type", "button");
    cy.contains("Maak er hier één aan")
      .should("have.attr", "href")
      .and("include", "/Auth/Register");
  });
});