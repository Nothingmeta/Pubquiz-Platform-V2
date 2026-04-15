require("../../Pubquiz Platform V2/wwwroot/js/components/countdown-timer.js");

describe("countdown-timer", () => {
    beforeEach(() => {
        cy.document().then((doc) => {
            doc.body.innerHTML = '<countdown-timer duration="3"></countdown-timer>';
        });
    });

    it("renders initial duration", () => {
        cy.get("countdown-timer")
            .shadow()
            .find("#time")
            .should("have.text", "3s");
    });

    it("counts down and emits finished", () => {
        cy.clock();

        cy.get("countdown-timer").then(($el) => {
            const timer = $el[0];
            const onFinished = cy.stub().as("onFinished");
            const onTick = cy.stub().as("onTick");

            timer.addEventListener("finished", onFinished);
            timer.addEventListener("tick", onTick);

            timer.start();
        });

        cy.tick(1000);
        cy.get("countdown-timer").shadow().find("#time").should("have.text", "2s");

        cy.tick(1000);
        cy.get("countdown-timer").shadow().find("#time").should("have.text", "1s");

        cy.tick(1000);
        cy.get("countdown-timer").shadow().find("#time").should("have.text", "0s");

        cy.get("@onTick").should("have.callCount", 3);
        cy.get("@onFinished").should("have.been.calledOnce");
    });

    it("run(duration) updates duration and starts", () => {
        cy.clock();

        cy.get("countdown-timer").then(($el) => {
            $el[0].run(5);
        });

        cy.get("countdown-timer").shadow().find("#time").should("have.text", "5s");
        cy.tick(2000);
        cy.get("countdown-timer").shadow().find("#time").should("have.text", "3s");
    });
});