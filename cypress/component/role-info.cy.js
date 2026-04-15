require("../../Pubquiz Platform V2/wwwroot/js/components/role-info.js");

describe("role-info", () => {
  const mountWithSelect = () => {
    cy.document().then((doc) => {
      doc.body.innerHTML = `
        <select id="roleSelect">
          <option value="">-- kies rol --</option>
          <option value="speler">Speler</option>
          <option value="quizmaster">Quizmaster</option>
          <option value="onbekend">Onbekend</option>
        </select>
        <role-info for="roleSelect"></role-info>
      `;
    });
  };

  it("shows the default hint when no role is selected", () => {
    mountWithSelect();

    cy.get("role-info")
      .shadow()
      .find(".box")
      .should("contain.text", "Selecteer een rol om de beschrijving te zien.");
  });

  it("shows speler description on select change", () => {
    mountWithSelect();

    cy.get("#roleSelect").select("speler");

    cy.get("role-info")
      .shadow()
      .find(".box")
      .should("contain.text", "Speler: Neem deel aan quizzen, beantwoord vragen en bekijk je scores.");
  });

  it("shows quizmaster description on select change", () => {
    mountWithSelect();

    cy.get("#roleSelect").select("quizmaster");

    cy.get("role-info")
      .shadow()
      .find(".box")
      .should("contain.text", "Quizmaster: Maak en beheer quizzen, start lobbies en beheer spelers.");
  });
});