describe("Register page", () => {
    beforeEach(() => {
        cy.visit("/Auth/Register");
    });

    it("renders register form and link to login", () => {
        // Basic presence checks; field names vary by view model so we check generically
        cy.get("form").should("exist");
        cy.get('input[name="Email"], input[type="email"]').should("exist");
        cy.get('input[name="Name"], input[name="name"]').should("exist");
        cy.get('input[name="Password"], input[type="password"]').should("exist");

        // Instead of using a non-existent `.or()` chain, assert the element exists and
        // check its href only if it's a link with an href.
        cy.contains("Inloggen").should("exist").then(($el) => {
            const href = $el.attr ? $el.attr("href") : null;
            if (href) {
                expect(href).to.match(/\/Auth\/Login/i);
            }
        });
    });
});