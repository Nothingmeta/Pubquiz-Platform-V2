describe("Home (authenticated)", () => {
    const testUser = { email: "e2e@test.local", password: "Password123!", name: "E2E Tester", role: "quizmaster" };

    // Helper to extract cookie name/value from a Set-Cookie header string
    const parseSetCookiePair = (setCookieStr: string) => {
        // e.g. ".AspNetCore.Cookies=abcdefg...; path=/; HttpOnly; SameSite=Lax"
        const parts = setCookieStr.split(";");
        const nameValue = parts[0].trim();
        const eq = nameValue.indexOf("=");
        if (eq === -1) return null;
        const name = nameValue.substring(0, eq);
        const value = nameValue.substring(eq + 1);
        return { name, value };
    };

    beforeEach(() => {
        // Request the development-only sign-in endpoint, capture Set-Cookie and apply it to the browser session
        cy.request({
            method: "POST",
            url: "/test/create-and-signin",
            form: true,
            body: testUser,
            followRedirect: false,
            failOnStatusCode: false
        }).then((resp) => {
            // Ensure the endpoint succeeded
            expect(resp.status).to.be.oneOf([200, 201, 302]);

            const setCookieHeader = resp.headers["set-cookie"];
            if (setCookieHeader) {
                // set-cookie can be a single string or an array
                const headerArray = Array.isArray(setCookieHeader) ? setCookieHeader : [setCookieHeader];
                headerArray.forEach((sc) => {
                    const parsed = parseSetCookiePair(sc);
                    if (parsed) {
                        // apply cookie to the browser so subsequent cy.visit sends it
                        cy.setCookie(parsed.name, parsed.value, { httpOnly: true, sameSite: "lax", path: "/" });
                    }
                });
            } else {
                // If no Set-Cookie was present, fail so you can inspect why (env, endpoint disabled, etc.)
                throw new Error("No Set-Cookie header returned from /test/create-and-signin. Ensure the app runs in Development and the test endpoint is enabled.");
            }
        });

        // Now visit the site root (Index). The auth cookie is applied so protected UI should appear.
        cy.visit("/");
    });

    it("shows welcome text and Start quiz link", () => {
        cy.contains("Welkom").should("be.visible");
        cy.get("a").contains("Start quiz").should("have.attr", "href").and("include", "/Quiz/LobbyOverview");
    });

    it("shows authenticated UI (logout / quizmaster links)", () => {
        // Logout button exists on Index when logged in
        cy.contains("Log uit").should("be.visible");

        // Because test user is a quizmaster, the "Beheer quizzen" link should be visible
        cy.contains("Beheer quizzen").should("be.visible");
    });

    it("navigates to LobbyOverview when Start quiz is clicked", () => {
        cy.contains("Start quiz").click();
        cy.url().should("include", "/Quiz/LobbyOverview");
        cy.contains("Beschikbare Quiz Lobbies").should("be.visible");
    });
});