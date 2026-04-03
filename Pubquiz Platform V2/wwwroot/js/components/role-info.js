class RoleInfo extends HTMLElement {
  constructor() {
    super();
    this._onChange = this._onChange.bind(this);
    this._descriptions = {
      speler: "Speler: Neem deel aan quizzen, beantwoord vragen en bekijk je scores.",
      quizmaster: "Quizmaster: Maak en beheer quizzen, start lobbies en beheer spelers."
    };
    this.attachShadow({ mode: "open" });
    this.shadowRoot.innerHTML = `
      <style>
        :host {
          display: block;
          margin-top: 8px;
          font-size: 13px;
          color: #333;
        }
        .box {
          padding: 8px 10px;
          border-radius: 6px;
          background: #f8f9fb;
          border: 1px solid #e3e6ea;
        }
        .hint-empty {
          color: #6c757d;
          font-style: italic;
        }
      </style>
      <div class="box"><span class="text hint-empty">Selecteer een rol om de beschrijving te zien.</span></div>
    `;
  }

  connectedCallback() {
    const selectId = this.getAttribute("for");
    if (!selectId) return;
    this._select = document.getElementById(selectId);
    if (!this._select) return;
    this._update(); // initial
    this._select.addEventListener("change", this._onChange);
  }

  disconnectedCallback() {
    if (this._select) {
      this._select.removeEventListener("change", this._onChange);
    }
  }

  _onChange() {
    this._update();
  }

  _update() {
    const value = this._select?.value || "";
    const container = this.shadowRoot.querySelector(".box");
    container.innerHTML = "";
    if (!value) {
      const span = document.createElement("span");
      span.className = "text hint-empty";
      span.textContent = "Selecteer een rol om de beschrijving te zien.";
      container.appendChild(span);
      return;
    }
    const desc = this._descriptions[value] || "Geen beschrijving beschikbaar.";
    const p = document.createElement("div");
    p.textContent = desc;
    container.appendChild(p);
  }
}

customElements.define("role-info", RoleInfo);