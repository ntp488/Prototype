/* Fleet Process Atlas: interactive viewing without modifying process definitions.
 * Drag nodes to customize your personal layout; connections follow automatically.
 * Only browser-local positions are persisted (localStorage, one layout per drill level).
 */
(function () {
  'use strict';
  const SVG = 'http://www.w3.org/2000/svg';
  const PREFIX = 'nodal-fleet-view-layout-v3:';
  let viewport = null, world = null, handlers = null, pointer = null;
  let level = 'repair', motionFrame = 0, suppressClick = false;
  let minimap = null, miniClick = null, minimapVisible = true;
  let transform = { x: 48, y: 60, scale: 0.82 };
  const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));
  const reduced = () => window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  const nodes = () => world ? [...world.querySelectorAll('.fc-node[data-process-id]')] : [];
  const byId = id => nodes().find(n => n.dataset.processId === id);
  const layoutKey = () => PREFIX + level;
  function load() {
    try { return JSON.parse(window.localStorage.getItem(layoutKey()) || '{}'); }
    catch { return {}; }
  }
  function save() {
    if (!world) return;
    const saved = {};
    for (const n of nodes()) saved[n.dataset.processId] = { x: n.offsetLeft, y: n.offsetTop };
    try { window.localStorage.setItem(layoutKey(), JSON.stringify(saved)); }
    catch { /* Private browsing may disable storage; dragging still works. */ }
  }
  function redraw() {
    if (world) world.style.transform = `translate3d(${transform.x}px,${transform.y}px,0) scale(${transform.scale})`;
    drawMinimap();
  }
  function drawMinimap() {
    if (!minimap || !world || !viewport || minimap.hidden) return;
    const ctx = minimap.getContext('2d');
    if (!ctx) return;
    const w=minimap.width, h=minimap.height, pad=12, b=bounds();
    const bw=Math.max(1,b.right-b.left), bh=Math.max(1,b.bottom-b.top);
    const scale=Math.min((w-pad*2)/bw,(h-pad*2)/bh);
    const px=x=>pad+(x-b.left)*scale, py=y=>pad+(y-b.top)*scale;
    ctx.clearRect(0,0,w,h);
    ctx.fillStyle='#0b1b2cea';ctx.fillRect(0,0,w,h);
    ctx.strokeStyle='#44647f';ctx.lineWidth=1;
    for(const line of world.querySelectorAll('.fc-wire[data-from][data-to]')) {
      const a=byId(line.dataset.from),z=byId(line.dataset.to);
      if(!a||!z)continue;
      ctx.beginPath();ctx.moveTo(px(a.offsetLeft+a.offsetWidth/2),py(a.offsetTop+a.offsetHeight/2));
      ctx.lineTo(px(z.offsetLeft+z.offsetWidth/2),py(z.offsetTop+z.offsetHeight/2));ctx.stroke();
    }
    for (const node of nodes()) {
      const pressure=Number(node.dataset.pressure||0);
      ctx.fillStyle=pressure>=35?'#e8a17b':pressure>=15?'#c6bf7e':'#518eb5';
      if(node.classList.contains('fc-engine-current'))ctx.fillStyle='#6fffe2';
      ctx.fillRect(px(node.offsetLeft),py(node.offsetTop),Math.max(4,node.offsetWidth*scale),Math.max(3,node.offsetHeight*scale));
    }
    const left=(0-transform.x)/transform.scale,top=(0-transform.y)/transform.scale;
    const right=(viewport.clientWidth-transform.x)/transform.scale,bottom=(viewport.clientHeight-transform.y)/transform.scale;
    ctx.strokeStyle='#d6faff';ctx.lineWidth=1.8;
    ctx.strokeRect(px(left),py(top),(right-left)*scale,(bottom-top)*scale);
    minimap.dataset.minLeft=String(b.left);minimap.dataset.minTop=String(b.top);
    minimap.dataset.minScale=String(scale);
  }
  function createMinimap() {
    if(!viewport || minimap)return;
    minimap=document.createElement('canvas');
    minimap.className='fc-minimap';minimap.width=190;minimap.height=124;
    minimap.title='Minimap: click to navigate';minimap.setAttribute('aria-label','Clickable overview minimap');
    minimap.setAttribute('role','button');minimap.setAttribute('tabindex','0');
    minimap.hidden=!minimapVisible;
    miniClick=event=>{
      if(!viewport||!minimap||!world)return;
      const rect=minimap.getBoundingClientRect();
      const cx=(event.clientX-rect.left)*minimap.width/rect.width;
      const cy=(event.clientY-rect.top)*minimap.height/rect.height;
      const scale=Number(minimap.dataset.minScale||1);
      const worldX=Number(minimap.dataset.minLeft||0)+(cx-12)/scale;
      const worldY=Number(minimap.dataset.minTop||0)+(cy-12)/scale;
      animateView({...transform,x:viewport.clientWidth/2-worldX*transform.scale,y:viewport.clientHeight/2-worldY*transform.scale},500);
      event.stopPropagation();
    };
    minimap.addEventListener('click',miniClick);
    minimap.addEventListener('keydown',event=>{
      if(event.key==='Enter'||event.key===' '){event.preventDefault();miniClick({clientX:minimap.getBoundingClientRect().left+minimap.clientWidth/2,clientY:minimap.getBoundingClientRect().top+minimap.clientHeight/2,stopPropagation(){}});}
    });
    viewport.appendChild(minimap);drawMinimap();
  }
  function bounds() {
    const arr = [...nodes(), ...(world ? world.querySelectorAll('.fc-leaf') : [])];
    if (!arr.length) return { left: 0, top: 0, right: 800, bottom: 450 };
    return {
      left: Math.min(...arr.map(n => n.offsetLeft)),
      top: Math.min(...arr.map(n => n.offsetTop)),
      right: Math.max(...arr.map(n => n.offsetLeft + n.offsetWidth)),
      bottom: Math.max(...arr.map(n => n.offsetTop + n.offsetHeight))
    };
  }
  function updateWires() {
    if (!world) return;
    const b = bounds();
    const svg = world.querySelector('.fc-wires');
    // The wires must remain large enough when nodes are moved beyond original bounds.
    const width = Math.max(800, b.right + 160), height = Math.max(450, b.bottom + 140);
    world.style.width = `${width}px`;
    world.style.height = `${height}px`;
    if (!svg) return;
    svg.setAttribute('width', width);
    svg.setAttribute('height', height);
    svg.setAttribute('viewBox', `0 0 ${width} ${height}`);
    for (const path of svg.querySelectorAll('[data-from][data-to]')) {
      const from = byId(path.dataset.from), to = byId(path.dataset.to);
      if (!from || !to) continue;
      const x1 = from.offsetLeft + from.offsetWidth;
      const y1 = from.offsetTop + from.offsetHeight / 2;
      const x2 = to.offsetLeft;
      const y2 = to.offsetTop + to.offsetHeight / 2;
      // Moving a target to the left creates a detour rather than a zero-length spline.
      const tension = Math.max(90, Math.abs(x2 - x1) * 0.45);
      path.setAttribute('d', `M${x1},${y1} C${x1 + tension},${y1} ${x2 - tension},${y2} ${x2},${y2}`);
    }
    drawMinimap();
  }
  function zoomAt(mult, x, y) {
    const next = clamp(transform.scale * mult, 0.18, 3.4);
    transform.x = x - (x - transform.x) * (next / transform.scale);
    transform.y = y - (y - transform.y) * (next / transform.scale);
    transform.scale = next;
    redraw();
  }
  function animateView(target, ms = 680) {
    cancelAnimationFrame(motionFrame);
    if (reduced()) { transform = { ...target }; redraw(); return; }
    const start = { ...transform }, started = performance.now();
    const step = time => {
      const t = clamp((time - started) / ms, 0, 1);
      const eased = 1 - Math.pow(1 - t, 3);
      transform.x = start.x + (target.x - start.x) * eased;
      transform.y = start.y + (target.y - start.y) * eased;
      transform.scale = start.scale + (target.scale - start.scale) * eased;
      redraw();
      if (t < 1) motionFrame = requestAnimationFrame(step);
    };
    motionFrame = requestAnimationFrame(step);
  }
  function particle(path, hue = '#80e5ee', duration = 950) {
    const svg = world?.querySelector('.fc-wires');
    if (!svg || !path || reduced()) return;
    const len = path.getTotalLength?.();
    if (!(len > 0)) return;
    const orb = document.createElementNS(SVG, 'circle');
    orb.setAttribute('r', '5.5'); orb.setAttribute('fill', hue);
    orb.setAttribute('class', 'fc-flow-particle');
    svg.appendChild(orb);
    const born = performance.now();
    const tick = now => {
      if (!orb.isConnected || !world) return;
      const t = clamp((now - born) / duration, 0, 1);
      const p = path.getPointAtLength(t * len);
      orb.setAttribute('cx', p.x); orb.setAttribute('cy', p.y);
      orb.style.opacity = String(Math.min(1, (1 - t) * 3));
      if (t < 1) requestAnimationFrame(tick); else orb.remove();
    };
    requestAnimationFrame(tick);
  }
  function flash(node) {
    if (!node || reduced()) return;
    node.classList.remove('fc-activity-pulse');
    void node.offsetWidth;
    node.classList.add('fc-activity-pulse');
    window.setTimeout(() => node.classList.remove('fc-activity-pulse'), 1250);
  }
  // Move between two true viewport-sized surfaces. Prefer the dashboard's
  // own scroll container on desktop; on compact layouts scroll the document.
  function scrollScreen(id) {
    const section = document.getElementById(id);
    const host = document.querySelector('.fleet-constellation');
    if (!section) return;
    const behavior = reduced() ? 'instant' : 'smooth';
    if (host && host.scrollHeight > host.clientHeight + 10 &&
        getComputedStyle(host).overflowY !== 'visible') {
      const top = host.scrollTop + section.getBoundingClientRect().top - host.getBoundingClientRect().top;
      host.scrollTo({ top, behavior });
    } else {
      section.scrollIntoView({ behavior, block: 'start' });
    }
  }
  function moveNode(node, x, y) {
    node.style.left = `${Math.max(20, Math.round(x))}px`;
    node.style.top = `${Math.max(20, Math.round(y))}px`;
    updateWires();
  }
  const api = {
    init(viewportId, worldId, initialLevel = 'repair') {
      api.dispose();
      viewport = document.getElementById(viewportId);
      world = document.getElementById(worldId);
      if (!viewport || !world) return;
      level = initialLevel;
      const down = event => {
        if (event.button !== 0 || event.target.closest('.fc-minimap,.fc-intelligence-drawer,.fc-focus-inspector')) return;
        const handle = event.target.closest('.fc-node-drag');
        if (handle && world.contains(handle)) {
          const node = handle.closest('.fc-node');
          pointer = { kind: 'node', id: event.pointerId, node, x: event.clientX, y: event.clientY,
                      originX: node.offsetLeft, originY: node.offsetTop, moved: false };
          viewport.setPointerCapture(event.pointerId);
          node.classList.add('fc-node-moving');
          event.preventDefault(); return;
        }
        if (event.target.closest('button,input,select,a,[role="button"],.fc-node,.fc-wire-hit')) return;
        pointer = { kind: 'canvas', id: event.pointerId, x: event.clientX, y: event.clientY,
                    originX: transform.x, originY: transform.y };
        viewport.setPointerCapture(event.pointerId);
        viewport.classList.add('fc-is-panning');
        event.preventDefault();
      };
      const move = event => {
        if (!pointer || pointer.id !== event.pointerId) return;
        const dx = event.clientX - pointer.x, dy = event.clientY - pointer.y;
        if (pointer.kind === 'node') {
          if (Math.abs(dx) + Math.abs(dy) > 4) pointer.moved = true;
          moveNode(pointer.node, pointer.originX + dx / transform.scale, pointer.originY + dy / transform.scale);
        } else {
          transform.x = pointer.originX + dx;
          transform.y = pointer.originY + dy;
          redraw();
        }
      };
      const up = event => {
        if (pointer && pointer.id === event.pointerId) {
          if (pointer.kind === 'node') {
            pointer.node.classList.remove('fc-node-moving');
            if (pointer.moved) { suppressClick = true; save(); }
          }
          pointer = null;
        }
        viewport.classList.remove('fc-is-panning');
      };
      const click = e => {
        if (!suppressClick) return;
        suppressClick = false;
        e.stopImmediatePropagation(); e.preventDefault();
      };
      const wheel = event => {
        event.preventDefault();
        const r = viewport.getBoundingClientRect();
        zoomAt(Math.exp(-event.deltaY * 0.0011), event.clientX - r.left, event.clientY - r.top);
      };
      const key = event => {
        const handle = document.activeElement?.closest('.fc-node-drag');
        if (handle && world.contains(handle) && event.key.startsWith('Arrow')) {
          const node = handle.closest('.fc-node');
          const n = event.shiftKey ? 50 : 16;
          moveNode(node, node.offsetLeft + (event.key === 'ArrowRight' ? n : event.key === 'ArrowLeft' ? -n : 0),
                         node.offsetTop + (event.key === 'ArrowDown' ? n : event.key === 'ArrowUp' ? -n : 0));
          save(); event.preventDefault(); return;
        }
        if (!viewport.contains(document.activeElement)) return;
        if (event.key === '+' || event.key === '=') { api.zoom(1.22); event.preventDefault(); }
        if (event.key === '-') { api.zoom(.82); event.preventDefault(); }
        if (event.key.toLowerCase() === 'f') { api.fit(); event.preventDefault(); }
      };
      viewport.addEventListener('pointerdown', down);
      viewport.addEventListener('pointermove', move);
      viewport.addEventListener('pointerup', up);
      viewport.addEventListener('pointercancel', up);
      viewport.addEventListener('click', click, true);
      viewport.addEventListener('wheel', wheel, { passive: false });
      window.addEventListener('keydown', key);
      handlers = { down, move, up, click, wheel, key };
      api.sync(initialLevel);
      createMinimap();
      requestAnimationFrame(() => api.fit());
    },
    sync(nextLevel) {
      if (!world) return;
      if (level !== nextLevel || !world.dataset.fleetLayoutApplied) {
        level = nextLevel;
        const saved = load();
        for (const node of nodes()) {
          const xy = saved[node.dataset.processId];
          node.style.left = `${Number.isFinite(xy?.x) ? Math.max(20, xy.x) : Number(node.dataset.defaultX || 20)}px`;
          node.style.top = `${Number.isFinite(xy?.y) ? Math.max(20, xy.y) : Number(node.dataset.defaultY || 20)}px`;
        }
        world.dataset.fleetLayoutApplied = nextLevel;
      }
      updateWires();
    },
    captureView() {
      const layout={};for(const node of nodes()) layout[node.dataset.processId]={x:node.offsetLeft,y:node.offsetTop};
      return {layout,center:{x:transform.x,y:transform.y},scale:transform.scale};
    },
    applyView(view) {
      if (!world||!view) return;
      const layouts=view.layout||view.Layout||{};
      for(const node of nodes()){
        const p=layouts[node.dataset.processId];
        if(p)moveNode(node,Number(p.x??p.X??0),Number(p.y??p.Y??0));
      }
      const center=view.center||view.Center||{};
      const x=Number(center.x??center.X??transform.x),y=Number(center.y??center.Y??transform.y);
      const scale=Number(view.scale??view.Scale??transform.scale);
      if(Number.isFinite(x)&&Number.isFinite(y)&&Number.isFinite(scale))
        animateView({x,y,scale:clamp(scale,0.18,3.4)},580);
      save();
    },
    setMinimap(visible) { minimapVisible=!!visible;if(minimap)minimap.hidden=!minimapVisible;drawMinimap(); },
    autoLayout(style) {
      const list=nodes();if(!list.length)return;
      const ordered=[...list].sort((a,b)=>Number(a.dataset.defaultX)-Number(b.dataset.defaultX));
      const columns=style==='grid'?Math.max(2,Math.ceil(Math.sqrt(list.length))):list.length;
      for (let i=0;i<ordered.length;i++) {
        const col=i%columns,row=Math.floor(i/columns);
        moveNode(ordered[i],65+col*380,105+row*320);
      }
      save();api.fit();
    },
    highlightCauses(ids) {
      const set=new Set(ids||[]);
      for(const node of nodes()) node.classList.toggle('fc-upstream-highlight',set.has(node.dataset.processId));
      for(const wire of world?.querySelectorAll('.fc-wire[data-from][data-to]')||[])
        wire.classList.toggle('fc-upstream-wire',set.has(wire.dataset.from)||set.has(wire.dataset.to));
      drawMinimap();
    },
    resetLayout(nextLevel) {
      if (nextLevel) level = nextLevel;
      try { window.localStorage.removeItem(layoutKey()); } catch { }
      for (const node of nodes()) {
        node.style.left = `${Number(node.dataset.defaultX || 20)}px`;
        node.style.top = `${Number(node.dataset.defaultY || 20)}px`;
      }
      updateWires(); api.fit();
    },
    fit() {
      if (!viewport || !world) return;
      const r = viewport.getBoundingClientRect(), b = bounds();
      if (r.width < 2 || r.height < 2) return;
      const w = Math.max(1, b.right - b.left), h = Math.max(1, b.bottom - b.top);
      const scale = clamp(Math.min((r.width - 130) / w, (r.height - 130) / h), 0.18, 1.14);
      animateView({ scale,
        x: (r.width - w * scale) / 2 - b.left * scale,
        y: (r.height - h * scale) / 2 - b.top * scale }, 450);
    },
    zoom(multiplier) {
      if (!viewport) return;
      const r = viewport.getBoundingClientRect();
      zoomAt(multiplier, r.width / 2, r.height / 2);
    },
    highlightFocus(currentIds,completedIds) {
      if (!world) return;
      const currents = new Set(currentIds||[]),completes=new Set(completedIds||[]);
      for(const n of nodes()){
        n.classList.toggle('fc-engine-current',currents.has(n.dataset.processId));
        n.classList.toggle('fc-engine-trail',!currents.has(n.dataset.processId)&&completes.has(n.dataset.processId));
      }
      for(const p of world.querySelectorAll('.fc-wire[data-from][data-to]'))
        p.classList.toggle('fc-engine-trail-wire',completes.has(p.dataset.from)&&(completes.has(p.dataset.to)||currents.has(p.dataset.to)));
      drawMinimap();
    },
    focusEngine(engineId, currentIds, completedIds) {
      if (!viewport || !world) return;
      api.highlightFocus(currentIds,completedIds);
      const currents = new Set(currentIds || []), completes = new Set(completedIds || []);
      const focusNodes = [...currents].map(byId).filter(Boolean);
      const focusTargets=focusNodes.length?focusNodes:[...completes].reverse().map(byId).filter(Boolean).slice(0,1);
      if(!focusTargets.length)return;
      focusTargets.forEach(flash);
      const r=viewport.getBoundingClientRect();
      const left=Math.min(...focusTargets.map(n=>n.offsetLeft)),right=Math.max(...focusTargets.map(n=>n.offsetLeft+n.offsetWidth));
      const top=Math.min(...focusTargets.map(n=>n.offsetTop)),bottom=Math.max(...focusTargets.map(n=>n.offsetTop+n.offsetHeight));
      const availableScale=Math.min((r.width-110)/Math.max(1,right-left),(r.height-150)/Math.max(1,bottom-top));
      const scale=clamp(Math.min(Math.max(transform.scale,0.91),availableScale),0.18,1.5);
      const x=(left+right)/2,y=(top+bottom)/2;
      animateView({scale,x:r.width/2-x*scale,y:r.height/2-y*scale},760);
      for(const edge of [...world.querySelectorAll('.fc-wire[data-from][data-to]')]
          .filter(p=>currents.has(p.dataset.to)&&completes.has(p.dataset.from)).slice(0,3))
        particle(edge,'#8fffe8',1150);
      scrollScreen('fc-overview-screen');
    },
    clearFocus() {
      for (const n of nodes()) n.classList.remove('fc-engine-current', 'fc-engine-trail');
      for (const p of world?.querySelectorAll('.fc-engine-trail-wire') || []) p.classList.remove('fc-engine-trail-wire');
    },
    animateUpdates(updates) {
      if (!world || !Array.isArray(updates) || !updates.length) return;
      // Limit simultanous effects even when a large demo fleet is changing each second.
      for (const change of updates.slice(0, 20)) {
        const n = byId(change.processId);
        if (n) flash(n);
        if (change.completed) {
          const outgoing = [...world.querySelectorAll('.fc-wire[data-from]')]
            .find(p => p.dataset.from === change.processId);
          if (outgoing) particle(outgoing, '#79e6ca');
        }
      }
    },
    getView() { return { ...transform }; },
    revealPanel() { scrollScreen('fc-detail-screen'); },
    revealGraph() { scrollScreen('fc-overview-screen'); },
    dispose() {
      cancelAnimationFrame(motionFrame);
      if (viewport && handlers) {
        for (const k of ['down','move','up']) viewport.removeEventListener('pointer' + (k === 'down' ? 'down' : k === 'move' ? 'move' : 'up'), handlers[k]);
        viewport.removeEventListener('pointercancel', handlers.up);
        viewport.removeEventListener('click', handlers.click, true);
        viewport.removeEventListener('wheel', handlers.wheel);
        window.removeEventListener('keydown', handlers.key);
      }
      if (viewport) viewport.classList.remove('fc-is-panning');
      if(minimap){minimap.removeEventListener('click',miniClick);minimap.remove();minimap=null;miniClick=null;}
      viewport = null; world = null; handlers = null; pointer = null;
    }
  };
  window.nodalFleet = api;
})();
