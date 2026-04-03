class CountdownTimer extends HTMLElement {
  constructor() {
    super();
    this._duration = Number(this.getAttribute('duration')) || 30;
    this._remaining = this._duration;
    this._interval = null;
    this.attachShadow({ mode: 'open' });
    this.shadowRoot.innerHTML = `
      <style>
        :host { display:block; margin-top:8px; }
        .timer { font-weight:700; color:#2c7be5; background:#f8f9fb; padding:6px 10px; border-radius:6px; display:inline-block; }
        .small { font-size:12px; color:#6c757d; margin-left:8px; }
      </style>
      <div>
        <span class="timer" id="time">--</span>
        <span class="small" id="label">time left</span>
      </div>
    `;
  }

  static get observedAttributes() {
    return ['duration'];
  }

  attributeChangedCallback(name, oldVal, newVal) {
    if (name === 'duration') {
      const v = Number(newVal);
      if (!Number.isNaN(v) && v > 0) {
        this._duration = v;
        this.reset();
      }
    }
  }

  connectedCallback() {
    this._render();
  }

  disconnectedCallback() {
    this.stop();
  }

  _render() {
    const el = this.shadowRoot.getElementById('time');
    el.textContent = this._format(this._remaining);
  }

  _format(sec) {
    const s = Math.max(0, Math.floor(sec));
    return `${s}s`;
  }

  start() {
    this.stop();
    this._remaining = this._duration;
    this._render();
    this.dispatchEvent(new CustomEvent('started', { detail: { duration: this._duration } }));
    this._interval = setInterval(() => {
      this._remaining -= 1;
      this._render();
      this.dispatchEvent(new CustomEvent('tick', { detail: { remaining: this._remaining } }));
      if (this._remaining <= 0) {
        this.stop();
        this.dispatchEvent(new CustomEvent('finished', { bubbles: true, composed: true, detail: {} }));
      }
    }, 1000);
  }

  stop() {
    if (this._interval) {
      clearInterval(this._interval);
      this._interval = null;
      this.dispatchEvent(new CustomEvent('stopped', { detail: { remaining: this._remaining } }));
    }
  }

  reset() {
    this.stop();
    this._remaining = this._duration;
    this._render();
  }

  // convenience: set duration and start
  run(duration) {
    if (typeof duration === 'number') {
      this.setAttribute('duration', String(duration));
    }
    this.start();
  }
}

customElements.define('countdown-timer', CountdownTimer);