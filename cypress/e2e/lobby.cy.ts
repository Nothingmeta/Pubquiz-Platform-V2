describe("Lobby (authenticated) -> Lobby view", () => {
    const testUser = { email: "e2e_lobby@test.local", password: "Password123!", name: "E2E Lobby", role: "quizmaster" };

    before(() => {
        // Create test user and get auth cookie (development-only endpoint)
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

    it("creates a quiz, starts a lobby and shows the Lobby view", () => {
        const quizName = `E2E Quiz ${Date.now()}`;

        // Create a quick quiz (returns JSON { quizId, quizSlug })
        cy.request({
            method: "POST",
            url: "/Quiz/CreateQuick",
            headers: { "content-type": "application/json" },
            body: { QuizName: quizName }
        }).then((createQuizResp) => {
            expect(createQuizResp.status).to.eq(200);
            const quizId = createQuizResp.body?.quizId;
            expect(quizId).to.exist;

            // Create a lobby for the quiz. We set followRedirect: false to capture the Location header.
            cy.request({
                method: "POST",
                url: "/Quiz/CreateLobby",
                form: true,
                followRedirect: false,
                body: { quizId: quizId }
            }).then((createLobbyResp) => {
                // Expect a redirect to /Quiz/Lobby?lobbyCode=...
                expect([302, 303, 307, 308]).to.include(createLobbyResp.status);
                const location = createLobbyResp.headers.location;
                expect(location).to.match(/\/Quiz\/Lobby\?lobbyCode=/);

                // Visit the lobby URL and assert Lobby view content
                cy.visit(location);
                cy.contains("Lobby:").should("be.visible");
                cy.contains(quizName).should("be.visible");
                cy.contains("Lobbycode").should("be.visible");
                cy.get("#playerList").should("exist");
            });
        });
    });
});