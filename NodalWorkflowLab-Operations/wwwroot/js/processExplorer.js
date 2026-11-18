(function () {
  'use strict';
  let viewport = null, world = null, handlers = null;
  let state = { x: 14, y: 30, scale: 0.65 };
  let drag = null;
  function draw() { if (world) world.style.transform = `translate(${state.x}px,${state.y}px) scale(${state.scale})`; }
  function bounds() {
    const nodes = world ? [...world.querySelectorAll('.process-graph-node')] : [];
    if (!nodes.length) return { left: 0, top: 0, right: 780, bottom: 390 };
    return { left: Math.min(...nodes.map(n => n.offsetLeft)), top: Math.min(...nodes.map(n => n.offsetTop)),
             right: Math.max(...nodes.map(n => n.offsetLeft + n.offsetWidth)),
             bottom: Math.max(...nodes.map(n => n.offsetTop + n.offsetHeight)) };
  }
  const api = {
    init(viewportId, worldId) {
      api.dispose();
      viewport = document.getElementById(viewportId);
      world = document.getElementById(worldId);
      if (!viewport || !world) return;
      const down = e => {
        if (e.button !== 0 || e.target.closest('button,select,textarea,input,a')) return;
        drag = { x: e.clientX, y: e.clientY, ox: state.x, oy: state.y, id: e.pointerId };
        viewport.setPointerCapture(e.pointerId);
        viewport.classList.add('is-panning');
        e.preventDefault();
      };
      const move = e => {
        if (!drag || drag.id !== e.pointerId) return;
        state.x = drag.ox + e.clientX - drag.x;
        state.y = drag.oy + e.clientY - drag.y;
        draw();
      };
      const up = e => {
        if (drag && drag.id === e.pointerId) drag = null;
        viewport.classList.remove('is-panning');
      };
      const wheel = e => {
        e.preventDefault();
        const rect = viewport.getBoundingClientRect();
        const mx = e.clientX - rect.left, my = e.clientY - rect.top;
        const prior = state.scale, next = Math.min(1.5, Math.max(.26, prior * Math.exp(-e.deltaY * .0011)));
        state.x = mx - (mx - state.x) * next / prior;
        state.y = my - (my - state.y) * next / prior;
        state.scale = next; draw();
      };
      viewport.addEventListener('pointerdown', down);
      viewport.addEventListener('pointermove', move);
      viewport.addEventListener('pointerup', up);
      viewport.addEventListener('pointercancel', up);
      viewport.addEventListener('wheel', wheel, { passive: false });
      handlers = { down, move, up, wheel };
      requestAnimationFrame(() => api.fit());
    },
    fit() {
      if (!viewport || !world) return;
      const rect = viewport.getBoundingClientRect();
      const b = bounds();
      const bw = Math.max(1, b.right - b.left), bh = Math.max(1, b.bottom - b.top);
      state.scale = Math.min(1.05, Math.max(.26, Math.min((rect.width - 72) / bw, (rect.height - 100) / bh)));
      state.x = (rect.width - bw * state.scale) / 2 - b.left * state.scale;
      state.y = (rect.height - bh * state.scale) / 2 - b.top * state.scale;
      draw();
    },
    zoom(multiplier) {
      if (!viewport) return;
      const rect = viewport.getBoundingClientRect(), mx = rect.width / 2, my = rect.height / 2;
      const prior = state.scale, next = Math.min(1.5, Math.max(.26, prior * multiplier));
      state.x = mx - (mx - state.x) * next / prior;
      state.y = my - (my - state.y) * next / prior;
      state.scale = next; draw();
    },
    dispose() {
      if (viewport && handlers) {
        viewport.removeEventListener('pointerdown', handlers.down);
        viewport.removeEventListener('pointermove', handlers.move);
        viewport.removeEventListener('pointerup', handlers.up);
        viewport.removeEventListener('pointercancel', handlers.up);
        viewport.removeEventListener('wheel', handlers.wheel);
      }
      if (viewport) viewport.classList.remove('is-panning');
      viewport = null; world = null; handlers = null; drag = null;
    }
  };
  window.nodalProcess = api;
})();
