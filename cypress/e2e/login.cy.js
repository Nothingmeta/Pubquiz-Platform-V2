describe("Login page", () => {
  beforeEach(() => {
    cy.visit("/Auth/Login");
  });

  it("renders login form and links", () => {
    cy.get('input[name="Email"], #email').should("exist");
    cy.get('input[name="password"], #password').should("exist");
    cy.get("form").within(() => {
      cy.get("button[type=submit]").should("exist");
    });
    cy.contains("Maak er hier één aan").should("have.attr", "href").and("include", "/Auth/Register");
  });
});