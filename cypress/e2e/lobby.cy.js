describe("Lobby (authenticated) -> Lobby view", () => {
  const testUser = {
    email: "e2e_lobby@test.local",
    password: "Password123!",
    name: "E2E Lobby",
    role: "quizmaster",
  };

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
      failOnStatusCode: false,
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
            path: "/",
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

  it("creates a quiz, starts a lobby and shows the Lobby view", () => {
    const quizName = `E2E Quiz ${Date.now()}`;

    cy.request({
      method: "POST",
      url: "/Quiz/CreateQuick",
      headers: { "content-type": "application/json" },
      body: { QuizName: quizName },
    }).then((createQuizResp) => {
      expect(createQuizResp.status).to.eq(200);
      const quizId = createQuizResp.body?.quizId;
      expect(quizId).to.exist;

      cy.request({
        method: "POST",
        url: "/Quiz/CreateLobby",
        form: true,
        followRedirect: false,
        body: { quizId: quizId },
      }).then((createLobbyResp) => {
        expect([302, 303, 307, 308]).to.include(createLobbyResp.status);
        const location = createLobbyResp.headers.location;
        expect(location).to.match(/\/Quiz\/Lobby\?lobbyCode=/);

        cy.visit(location);
        cy.contains("Lobby:").should("be.visible");
        cy.contains(quizName).should("be.visible");
        cy.contains("Lobbycode").should("be.visible");
        cy.get("#playerList").should("exist");
      });
    });
  });
});