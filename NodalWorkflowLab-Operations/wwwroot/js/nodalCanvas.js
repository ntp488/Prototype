/*
 * Nodal Canvas — pointer interaction and SVG visualization only.
 * Graph state, validation, and workflow execution live in Blazor/C#.
 * This file deliberately has no dependency on a JavaScript graph library.
 */
(() => {
    'use strict';

    const NS = 'http://www.w3.org/2000/svg';
    const instances = new Map();
    const clamp = (x, low, high) => Math.max(low, Math.min(high, x));

    function call(state, name, ...args) {
        return state.dotNet.invokeMethodAsync(name, ...args).catch(error => {
            console.warn(`Nodal callback ${name}:`, error);
        });
    }

    function stateFor(id) {
        const state = instances.get(id);
        if (!state) throw new Error(`Canvas '${id}' has not been initialized.`);
        return state;
    }

    function findPort(state, nodeId, portId, direction) {
        // Compare dataset strings instead of interpolating untrusted IDs into CSS selectors.
        return [...state.nodes.querySelectorAll('.port')].find(el =>
            el.dataset.nodeId === nodeId && el.dataset.portId === portId &&
            el.dataset.direction === direction);
    }

    function centerOfPort(state, element) {
        if (!element) return null;
        const rect = element.getBoundingClientRect();
        const world = state.world.getBoundingClientRect();
        return {
            x: (rect.left + rect.width / 2 - world.left) / state.zoom,
            y: (rect.top + rect.height / 2 - world.top) / state.zoom
        };
    }

    function toWorld(state, clientX, clientY) {
        const rect = state.viewport.getBoundingClientRect();
        return {
            x: (clientX - rect.left - state.panX) / state.zoom,
            y: (clientY - rect.top - state.panY) / state.zoom
        };
    }

    function curve(a, b) {
        const pull = Math.max(55, Math.min(230, Math.abs(b.x - a.x) * .53));
        return `M ${a.x} ${a.y} C ${a.x + pull} ${a.y}, ${b.x - pull} ${b.y}, ${b.x} ${b.y}`;
    }

    function path(className, d, edgeId) {
        const el = document.createElementNS(NS, 'path');
        el.setAttribute('class', className);
        el.setAttribute('d', d);
        if (edgeId) el.dataset.edgeId = edgeId;
        return el;
    }

    function draw(state) {
        state.frame = 0;
        const fragment = document.createDocumentFragment();

        for (const edge of state.edges) {
            const from = centerOfPort(state, findPort(state, edge.from, edge.out, 'out'));
            const to = centerOfPort(state, findPort(state, edge.to, edge.in, 'in'));
            if (!from || !to) continue;

            const d = curve(from, to);
            let styles = 'wire';
            if (edge.out === 'true') styles += ' branch-yes';
            if (edge.out === 'false') styles += ' branch-no';
            if (edge.id === state.activeEdge) styles += ' active';
            if (edge.id === state.selectedEdge) styles += ' selected';
            fragment.append(path(styles, d, edge.id));
            fragment.append(path('wire-hit', d, edge.id));
        }

        if (state.connection) {
            const connection = state.connection;
            const start = centerOfPort(state, findPort(state, connection.from, connection.out, 'out'));
            if (start) fragment.append(path('wire wire-temp', curve(start, connection.end)));
        }
        state.wires.replaceChildren(fragment);
    }

    function schedule(state) {
        if (state.frame) return;
        state.frame = requestAnimationFrame(() => draw(state));
    }

    function transform(state) {
        state.world.style.transform = `translate(${state.panX}px, ${state.panY}px) scale(${state.zoom})`;
        const label = state.viewport.querySelector('#zoom-label');
        if (label) label.textContent = `${Math.round(state.zoom * 100)}%`;
    }

    function zoomAt(state, factor, clientX, clientY) {
        const before = toWorld(state, clientX, clientY);
        const rect = state.viewport.getBoundingClientRect();
        state.zoom = clamp(state.zoom * factor, .16, 2.5);
        state.panX = clientX - rect.left - before.x * state.zoom;
        state.panY = clientY - rect.top - before.y * state.zoom;
        transform(state);
    }

    function cancelConnection(state) {
        state.connection = null;
        state.viewport.classList.remove('connect-mode');
        for (const el of state.nodes.querySelectorAll('.port.armed')) el.classList.remove('armed');
        schedule(state);
    }

    function getNodeElement(state, id) {
        return [...state.nodes.querySelectorAll('.node')].find(n => n.dataset.nodeId === id);
    }

    function connect(state, socket) {
        if (!state.connection) return;
        const c = state.connection;
        if (socket.dataset.direction === 'in') {
            call(state, 'OnConnect', c.from, c.out, socket.dataset.nodeId, socket.dataset.portId);
        }
        cancelConnection(state);
    }

    function isRunning(state) {
        return state.viewport.dataset.running === 'true';
    }

    function onPointerDown(state, e) {
        if (e.button !== 0 && e.button !== 1) return;
        const target = e.target;
        const socket = target.closest('.port');
        const wire = target.closest('.wire-hit');
        const node = target.closest('.node');
        const header = target.closest('.node-head');

        if (socket) {
            if (isRunning(state)) return;
            e.preventDefault();
            e.stopPropagation();
            if (socket.dataset.direction === 'in') {
                if (state.connection) connect(state, socket);
                return;
            }
            cancelConnection(state);
            state.connection = {
                from: socket.dataset.nodeId,
                out: socket.dataset.portId,
                startX: e.clientX, startY: e.clientY,
                end: toWorld(state, e.clientX, e.clientY),
                moved: false
            };
            socket.classList.add('armed');
            state.viewport.classList.add('connect-mode');
            schedule(state);
            return;
        }

        if (wire) {
            if (e.button !== 0) return;
            e.preventDefault();
            cancelConnection(state);
            call(state, 'OnEdgeSelected', wire.dataset.edgeId);
            return;
        }

        if (node) {
            if (e.button !== 0) return;
            if (!target.closest('button,input,textarea,select')) {
                call(state, 'OnNodeSelected', node.dataset.nodeId);
            }
            if (header && !target.closest('button') && !isRunning(state)) {
                e.preventDefault();
                state.gesture = {
                    kind: 'node', id: node.dataset.nodeId,
                    x: e.clientX, y: e.clientY,
                    startX: parseFloat(node.style.left) || 0,
                    startY: parseFloat(node.style.top) || 0,
                    moved: false
                };
            }
            return;
        }

        if (target.closest('.viewport-controls')) return;
        if (state.connection) cancelConnection(state);
        if (e.button === 0) call(state, 'OnCanvasDeselected');
        e.preventDefault();
        state.gesture = {
            kind: 'pan', x: e.clientX, y: e.clientY,
            startX: state.panX, startY: state.panY
        };
        state.viewport.classList.add('panning');
    }

    function onPointerMove(state, e) {
        if (state.connection) {
            const c = state.connection;
            c.end = toWorld(state, e.clientX, e.clientY);
            if (e.buttons && Math.hypot(e.clientX - c.startX, e.clientY - c.startY) > 6) c.moved = true;
            schedule(state);
        }

        const gesture = state.gesture;
        if (!gesture) return;
        if (gesture.kind === 'pan') {
            state.panX = gesture.startX + e.clientX - gesture.x;
            state.panY = gesture.startY + e.clientY - gesture.y;
            transform(state);
        } else if (gesture.kind === 'node') {
            const el = getNodeElement(state, gesture.id);
            if (!el) return;
            const dx = (e.clientX - gesture.x) / state.zoom;
            const dy = (e.clientY - gesture.y) / state.zoom;
            el.style.left = `${Math.round(gesture.startX + dx)}px`;
            el.style.top = `${Math.round(gesture.startY + dy)}px`;
            if (Math.abs(dx) + Math.abs(dy) > 1) gesture.moved = true;
            schedule(state);
        }
    }

    function onPointerUp(state, e) {
        const gesture = state.gesture;
        state.gesture = null;
        state.viewport.classList.remove('panning');
        if (gesture?.kind === 'node' && gesture.moved) {
            const el = getNodeElement(state, gesture.id);
            if (el) call(state, 'OnNodeMoved', gesture.id,
                parseFloat(el.style.left) || 0, parseFloat(el.style.top) || 0);
        }

        if (state.connection) {
            const hit = document.elementFromPoint(e.clientX, e.clientY);
            const input = hit?.closest('.port[data-direction="in"]');
            if (input) connect(state, input);
            else if (state.connection.moved) cancelConnection(state);
            // Otherwise the user clicked an output socket and can now click an input.
        }
    }

    function keyboard(state, e) {
        const el = e.target;
        const editing = el instanceof HTMLElement &&
            (el.isContentEditable || !!el.closest('input,textarea,select,[contenteditable="true"]'));
        if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
            e.preventDefault();
            call(state, 'OnRunRequested');
        } else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') {
            e.preventDefault();
            call(state, 'OnSaveRequested');
        } else if (!editing) {
            if (e.key === 'Escape') {
                cancelConnection(state);
                call(state, 'OnCanvasDeselected');
            } else if (e.key === 'Delete' || e.key === 'Backspace') {
                e.preventDefault();
                call(state, 'OnDeleteRequested');
            } else if (e.key.toLowerCase() === 'f' && !e.ctrlKey && !e.metaKey) {
                window.nodalCanvas.fit(state.id);
            }
        }
    }

    window.nodalCanvas = {
        initialize(id, dotNet) {
            const viewport = document.getElementById(id);
            if (!viewport) throw new Error(`Canvas #${id} was not found.`);
            if (instances.has(id)) window.nodalCanvas.dispose(id);
            const state = {
                id, viewport, dotNet,
                world: viewport.querySelector('#world'),
                nodes: viewport.querySelector('#nodes'),
                wires: viewport.querySelector('#wires'),
                edges: [], selectedEdge: null, activeEdge: null,
                panX: 0, panY: 0, zoom: 1, frame: 0,
                connection: null, gesture: null, toastTimer: null
            };
            const down = e => onPointerDown(state, e);
            const move = e => onPointerMove(state, e);
            const up = e => onPointerUp(state, e);
            const wheel = e => {
                e.preventDefault();
                zoomAt(state, e.deltaY < 0 ? 1.095 : 1 / 1.095, e.clientX, e.clientY);
            };
            const keydown = e => keyboard(state, e);
            const context = e => e.preventDefault();
            viewport.addEventListener('pointerdown', down);
            viewport.addEventListener('wheel', wheel, { passive: false });
            viewport.addEventListener('contextmenu', context);
            window.addEventListener('pointermove', move);
            window.addEventListener('pointerup', up);
            window.addEventListener('keydown', keydown);
            state.cleanup = () => {
                viewport.removeEventListener('pointerdown', down);
                viewport.removeEventListener('wheel', wheel);
                viewport.removeEventListener('contextmenu', context);
                window.removeEventListener('pointermove', move);
                window.removeEventListener('pointerup', up);
                window.removeEventListener('keydown', keydown);
                if (state.frame) cancelAnimationFrame(state.frame);
                if (state.toastTimer) clearTimeout(state.toastTimer);
            };
            instances.set(id, state);
            transform(state);
            return null; // Workflow documents are now persisted on the server.
        },

        renderEdges(id, edges, selectedEdge, activeEdge) {
            const state = stateFor(id);
            state.edges = edges || [];
            state.selectedEdge = selectedEdge;
            state.activeEdge = activeEdge;
            schedule(state);
        },

        fit(id) {
            const state = stateFor(id);
            const nodes = [...state.nodes.querySelectorAll('.node')];
            if (!nodes.length) return;
            const minX = Math.min(...nodes.map(n => parseFloat(n.style.left) || 0));
            const minY = Math.min(...nodes.map(n => parseFloat(n.style.top) || 0));
            const maxX = Math.max(...nodes.map(n => (parseFloat(n.style.left) || 0) + n.offsetWidth));
            const maxY = Math.max(...nodes.map(n => (parseFloat(n.style.top) || 0) + n.offsetHeight));
            const viewport = state.viewport.getBoundingClientRect();
            state.zoom = clamp(Math.min((viewport.width - 105) / (maxX - minX),
                (viewport.height - 130) / (maxY - minY), 1.05), .16, 2.5);
            state.panX = (viewport.width - (maxX - minX) * state.zoom) / 2 - minX * state.zoom;
            state.panY = (viewport.height - (maxY - minY) * state.zoom) / 2 - minY * state.zoom;
            transform(state);
            schedule(state);
        },

        zoomBy(id, factor) {
            const state = stateFor(id);
            const rect = state.viewport.getBoundingClientRect();
            zoomAt(state, factor, rect.left + rect.width / 2, rect.top + rect.height / 2);
        },

        centerWorld(id) {
            const state = stateFor(id);
            const rect = state.viewport.getBoundingClientRect();
            return toWorld(state, rect.left + rect.width / 2, rect.top + rect.height / 2);
        },

        cancelConnection(id) { cancelConnection(stateFor(id)); },

        openImport(inputId) {
            const input = document.getElementById(inputId);
            if (input) { input.value = ''; input.click(); }
        },

        download(filename, content) {
            const url = URL.createObjectURL(new Blob([content], { type: 'application/json' }));
            const link = document.createElement('a');
            link.href = url;
            link.download = filename;
            document.body.append(link);
            link.click();
            link.remove();
            setTimeout(() => URL.revokeObjectURL(url), 1200);
        },

        publish(type, detail) {
            window.dispatchEvent(new CustomEvent(`nodelab:${type}`, { detail }));
        },

        scrollLog(elementId) {
            requestAnimationFrame(() => {
                const el = document.getElementById(elementId);
                if (el) el.scrollTop = el.scrollHeight;
            });
        },

        toast(id, message) {
            const state = stateFor(id);
            const el = state.viewport.querySelector('#toast');
            if (!el) return;
            el.textContent = message;
            el.classList.add('visible');
            if (state.toastTimer) clearTimeout(state.toastTimer);
            state.toastTimer = setTimeout(() => el.classList.remove('visible'), 3000);
        },

        dispose(id) {
            const state = instances.get(id);
            if (!state) return;
            state.cleanup();
            instances.delete(id);
        }
    };

    // Same familiar API as the standalone demo, now backed by async Blazor calls.
    window.NodeLab = {
        events: ['node-start', 'node-complete', 'node-output', 'edge-traverse', 'run-complete', 'run-error', 'run-canceled', 'message'],
        on(event, handler) {
            const name = `nodelab:${event}`;
            window.addEventListener(name, handler);
            return () => window.removeEventListener(name, handler);
        },
        run(nodeId = null, payload = null) {
            return stateFor('viewport').dotNet.invokeMethodAsync('OnExternalRun', nodeId, payload);
        },
        getGraph() {
            return stateFor('viewport').dotNet.invokeMethodAsync('OnGetGraph');
        },
        getWorkflowId() {
            return stateFor('viewport').dotNet.invokeMethodAsync('OnGetWorkflowId');
        }
    };
})();
