describe("LobbyOverview (authenticated)", () => {
  const testUser = { email: "e2e_lo@test.local", password: "Password123!", name: "E2E LO", role: "quizmaster" };

  const parseSetCookiePair = (setCookieStr) => {
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
  });

  it("shows lobby overview UI for quizmaster", () => {
    cy.visit("/Quiz/LobbyOverview");
    cy.contains("Beschikbare Quiz Lobbies").should("be.visible");

    cy.get('form[action*="/Quiz/Lobby"], form').within(() => {
      cy.get('input[name="lobbyCode"]').should("exist");
      cy.get("button").contains("Zoek & Join").should("exist");
    });

    cy.get("body").then(($body) => {
      if ($body.find("select[name='quizId']").length) {
        cy.get("select[name='quizId']").should("exist");
        cy.get("button").contains("Start Quiz").should("exist");
      }
    });
  });
});