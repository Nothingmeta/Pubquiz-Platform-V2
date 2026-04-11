describe("Home (authenticated)", () => {
    const testUser = { email: "e2e@test.local", password: "Password123!", name: "E2E Tester", role: "quizmaster" };

    const parseSetCookiePair = (setCookieStr: string) => {
        const parts = setCookieStr.split(";");
        const nameValue = parts[0].trim();
        const eq = nameValue.indexOf("=");
        if (eq === -1) return null;

        const name = nameValue.substring(0, eq);
        const value = nameValue.substring(eq + 1);
        return { name, value };
    };

    const signInAndApplyCookies = () => {
        cy.request({
            method: "POST",
            url: "/test/create-and-signin",
            form: true,
            body: testUser,
            followRedirect: false,
            failOnStatusCode: false
        }).then((resp) => {
            expect(resp.status).to.be.oneOf([200, 201, 302]);

            const setCookieHeader = resp.headers["set-cookie"];
            expect(setCookieHeader, "Expected auth cookies from /test/create-and-signin").to.exist;

            const headerArray = Array.isArray(setCookieHeader) ? setCookieHeader : [setCookieHeader];
            headerArray.forEach((sc) => {
                const parsed = parseSetCookiePair(sc);
                if (parsed) {
                    cy.setCookie(parsed.name, parsed.value, {
                        httpOnly: true,
                        sameSite: "strict",
                        path: "/"
                    });
                }
            });
        });
    };

    beforeEach(() => {
        cy.clearCookies();
        cy.clearLocalStorage();
        signInAndApplyCookies();
        cy.visit("/");
    });

    it("shows welcome text and Start quiz link", () => {
        cy.contains("Welkom").should("be.visible");
        cy.get("a").contains("Start quiz").should("have.attr", "href").and("include", "/Quiz/LobbyOverview");
    });

    it("shows authenticated UI (logout / quizmaster links)", () => {
        cy.contains("Log uit").should("be.visible");
        cy.contains("Beheer quizzen").should("be.visible");
    });

    it("navigates to LobbyOverview when Start quiz is clicked", () => {
        cy.contains("Start quiz").click();
        cy.url().should("include", "/Quiz/LobbyOverview");
        cy.contains("Beschikbare Quiz Lobbies").should("be.visible");
    });
});