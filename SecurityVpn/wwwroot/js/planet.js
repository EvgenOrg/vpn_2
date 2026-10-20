// wwwroot/js/planet.js
// Точечная планета на canvas (без внешних библиотек). Подключается через JS isolation:
//   const module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/planet.js");
//   var id = await module.InvokeAsync<int>("create", canvasElement);
// Экземпляров может быть несколько (например, на странице входа и на главной), поэтому состояние — в объекте, а не в модуле.

const TAU = Math.PI * 2;
const rad = (d) => (d * Math.PI) / 180;
const clamp = (v, a, b) => Math.min(b, Math.max(a, v));
const mix = (a, b, t) => a + (b - a) * t;
const smooth = (a, b, v) => { const t = clamp((v - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); };

/* ---------- шум для «материков» ---------- */
function hash3(x, y, z) {
    let h = Math.imul(x, 374761393) ^ Math.imul(y, 668265263) ^ Math.imul(z, 1274126177);
    h = Math.imul(h ^ (h >>> 13), 1103515245);
    h ^= h >>> 16;
    return (h >>> 0) / 4294967296;
}
const fade = (t) => t * t * (3 - 2 * t);
function noise3(x, y, z) {
    const xi = Math.floor(x), yi = Math.floor(y), zi = Math.floor(z);
    const xf = fade(x - xi), yf = fade(y - yi), zf = fade(z - zi);
    const c = (dx, dy, dz) => hash3(xi + dx, yi + dy, zi + dz);
    return mix(
        mix(mix(c(0, 0, 0), c(1, 0, 0), xf), mix(c(0, 1, 0), c(1, 1, 0), xf), yf),
        mix(mix(c(0, 0, 1), c(1, 0, 1), xf), mix(c(0, 1, 1), c(1, 1, 1), xf), yf),
        zf
    );
}
const fbm = (x, y, z) => noise3(x, y, z) * 0.55 + noise3(x * 2.1, y * 2.1, z * 2.1) * 0.3 + noise3(x * 4.3, y * 4.3, z * 4.3) * 0.15;

/* ---------- точки на сфере (общие для всех экземпляров) ---------- */
const COUNT = 2600;
const px = new Float32Array(COUNT), py = new Float32Array(COUNT), pz = new Float32Array(COUNT);
const land = new Uint8Array(COUNT);
(function buildPoints() {
    const golden = Math.PI * (3 - Math.sqrt(5));
    for (let i = 0; i < COUNT; i++) {
        const y = 1 - (2 * (i + 0.5)) / COUNT;
        const r = Math.sqrt(1 - y * y);
        const th = i * golden;
        const x = Math.cos(th) * r, z = Math.sin(th) * r;
        px[i] = x; py[i] = y; pz[i] = z;
        land[i] = fbm(x * 1.7 + 11.3, y * 1.7 + 4.1, z * 1.7 + 7.9) > 0.535 ? 1 : 0;
    }
})();

const reduceMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

class Planet {
    constructor(canvas) {
        this.canvas = canvas;
        this.ctx = canvas.getContext('2d');
        this.dpr = 1; this.W = 0; this.H = 0;
        this.vpn = 'idle';
        this.server = null;
        this.yaw = 0.6; this.tilt = 0.3; this.arc = 0; this.tint = 0; this.dim = 0;
        this.last = performance.now();
        this.alive = true;

        this.resize();
        this.observer = new ResizeObserver(() => this.resize());
        this.observer.observe(canvas);

        this.loop = (now) => {
            if (!this.alive) return;
            requestAnimationFrame(this.loop);
            this.frame(now);
        };
        requestAnimationFrame(this.loop);
    }

    resize() {
        this.dpr = Math.min(window.devicePixelRatio || 1, 2);
        this.W = this.canvas.clientWidth;
        this.H = this.canvas.clientHeight;
        this.canvas.width = Math.max(1, Math.round(this.W * this.dpr));
        this.canvas.height = Math.max(1, Math.round(this.H * this.dpr));
    }

    dispose() {
        this.alive = false;
        this.observer.disconnect();
    }

    frame(now) {
        const { ctx } = this;
        if (document.hidden || !ctx || this.W === 0) { this.last = now; return; }

        const dt = Math.min(0.05, (now - this.last) / 1000 || 0.016);
        this.last = now;

        const W = this.W, H = this.H;
        const cx = W / 2, cy = H / 2, R = Math.min(W, H) * 0.31;

        // плавное «проявление» планеты при первом показе
        this.dim += (1 - this.dim) * (1 - Math.exp(-dt * 2.5));

        // вращение: свободное, пока нет соединения; при соединении поворачиваем к серверу
        if (this.server && this.vpn !== 'idle') {
            const ty = -rad(this.server.lon);
            let d = ty - this.yaw;
            d = Math.atan2(Math.sin(d), Math.cos(d));
            this.yaw += d * (1 - Math.exp(-dt * 2.2));
            this.tilt += (clamp(rad(this.server.lat), -0.2, 0.55) - this.tilt) * (1 - Math.exp(-dt * 2.2));
        } else {
            this.yaw += dt * (reduceMotion ? 0.02 : 0.09);
            this.tilt += (0.3 - this.tilt) * (1 - Math.exp(-dt * 1.5));
        }
        const arcTarget = this.vpn === 'connected' ? 1 : this.vpn === 'connecting' ? 0.55 : 0;
        this.arc += (arcTarget - this.arc) * (1 - Math.exp(-dt * 1.6));
        this.tint += ((this.vpn === 'connected' ? 1 : 0) - this.tint) * (1 - Math.exp(-dt * 2));

        ctx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
        ctx.clearRect(0, 0, W, H);

        const accent = [167, 139, 250], ok = [74, 222, 128];
        const glow = accent.map((c, i) => Math.round(mix(c, ok[i], this.tint)));
        const dim = this.dim;

        // свечение атмосферы
        let g = ctx.createRadialGradient(cx, cy, R * 0.9, cx, cy, R * 1.6);
        g.addColorStop(0, `rgba(${glow},${0.5 * dim})`);
        g.addColorStop(0.3, `rgba(${glow},${0.16 * dim})`);
        g.addColorStop(1, `rgba(${glow},0)`);
        ctx.fillStyle = g;
        ctx.beginPath(); ctx.arc(cx, cy, R * 1.6, 0, TAU); ctx.fill();

        // тело планеты
        g = ctx.createRadialGradient(cx - R * 0.35, cy - R * 0.4, R * 0.1, cx, cy, R);
        g.addColorStop(0, `rgba(58,30,130,${0.95 * dim})`);
        g.addColorStop(1, `rgba(12,6,38,${0.96 * dim})`);
        ctx.fillStyle = g;
        ctx.beginPath(); ctx.arc(cx, cy, R, 0, TAU); ctx.fill();

        // точки
        const cyw = Math.cos(this.yaw), syw = Math.sin(this.yaw), ct = Math.cos(this.tilt), st = Math.sin(this.tilt);
        const lx = -0.5, ly = 0.6, lz = 0.62;
        for (let i = 0; i < COUNT; i++) {
            const x = px[i], y = py[i], z = pz[i];
            const x1 = x * cyw + z * syw;
            const z1 = -x * syw + z * cyw;
            const y2 = y * ct - z1 * st;
            const z2 = y * st + z1 * ct;
            if (z2 <= 0) continue;
            const lit = clamp(x1 * lx + y2 * ly + z2 * lz, 0, 1);
            const edge = smooth(0, 0.45, z2);
            const isLand = land[i] === 1;
            const a = edge * (isLand ? 0.95 : 0.42) * (0.35 + 0.65 * lit) * dim;
            if (a < 0.03) continue;
            const base = isLand ? 225 : 150;
            const rr = Math.round(mix(base * 0.8, ok[0], this.tint * (isLand ? 0.7 : 0.3)));
            const gg = Math.round(mix(base * 0.72, ok[1], this.tint * (isLand ? 0.7 : 0.3)));
            const bb = Math.round(mix(255, ok[2], this.tint * (isLand ? 0.6 : 0.2)));
            ctx.fillStyle = `rgba(${rr},${gg},${bb},${a.toFixed(3)})`;
            const s = (isLand ? 2.1 : 1.3) * (0.55 + 0.45 * z2) * (R / 140 + 0.35);
            ctx.fillRect(cx + x1 * R - s / 2, cy - y2 * R - s / 2, s, s);
        }

        // кромка
        ctx.lineWidth = 1;
        ctx.strokeStyle = `rgba(${glow},${0.35 * dim})`;
        ctx.beginPath(); ctx.arc(cx, cy, R, 0, TAU); ctx.stroke();

        if (this.server) this.drawServer(now, cx, cy, R, glow);
    }

    project(v, lift) {
        const cyw = Math.cos(this.yaw), syw = Math.sin(this.yaw), ct = Math.cos(this.tilt), st = Math.sin(this.tilt);
        const x = v[0] * lift, y = v[1] * lift, z = v[2] * lift;
        const x1 = x * cyw + z * syw;
        const z1 = -x * syw + z * cyw;
        return [x1, y * ct - z1 * st, y * st + z1 * ct];
    }

    drawServer(now, cx, cy, R, glow) {
        const { ctx } = this;
        const vec = (lat, lon) => {
            const la = rad(lat), lo = rad(lon);
            return [Math.cos(la) * Math.sin(lo), Math.sin(la), Math.cos(la) * Math.cos(lo)];
        };
        const target = vec(this.server.lat, this.server.lon);
        const origin = vec(this.server.lat - 20, this.server.lon - 48); // условная точка «вы», чтобы нарисовать маршрут
        const X = (p) => cx + p[0] * R, Y = (p) => cy - p[1] * R;

        // дуга соединения
        if (this.arc > 0.02) {
            const steps = 48, upto = Math.floor(steps * this.arc);
            const dot = clamp(origin[0] * target[0] + origin[1] * target[1] + origin[2] * target[2], -1, 1);
            const omega = Math.acos(dot) || 1e-6;
            const sinO = Math.sin(omega);
            const at = (t) => {
                const a = Math.sin((1 - t) * omega) / sinO, b = Math.sin(t * omega) / sinO;
                const v = [a * origin[0] + b * target[0], a * origin[1] + b * target[1], a * origin[2] + b * target[2]];
                return this.project(v, 1 + 0.2 * Math.sin(Math.PI * t));
            };
            ctx.lineWidth = 1.6;
            ctx.strokeStyle = `rgba(${glow},0.85)`;
            ctx.beginPath();
            let pen = false;
            for (let i = 0; i <= upto; i++) {
                const p = at(i / steps);
                if (p[2] <= 0) { pen = false; continue; }
                if (pen) ctx.lineTo(X(p), Y(p)); else { ctx.moveTo(X(p), Y(p)); pen = true; }
            }
            ctx.stroke();

            // «пакет» бежит по дуге
            if (this.vpn === 'connected') {
                const p = at(((now * 0.00045) % 1) * this.arc);
                if (p[2] > 0) {
                    ctx.fillStyle = 'rgba(255,255,255,0.95)';
                    ctx.beginPath(); ctx.arc(X(p), Y(p), 2.4, 0, TAU); ctx.fill();
                }
            }
            const o = this.project(origin, 1);
            if (o[2] > 0) {
                ctx.strokeStyle = 'rgba(255,255,255,0.8)'; ctx.lineWidth = 1.4;
                ctx.beginPath(); ctx.arc(X(o), Y(o), 3.6, 0, TAU); ctx.stroke();
            }
        }

        // метка сервера с пульсирующим кольцом
        const p = this.project(target, 1);
        if (p[2] > 0.02) {
            const ph = reduceMotion ? 0.5 : (now * 0.0007) % 1;
            ctx.strokeStyle = `rgba(${glow},${(1 - ph) * 0.8})`;
            ctx.lineWidth = 1.5;
            ctx.beginPath(); ctx.arc(X(p), Y(p), 5 + ph * 16, 0, TAU); ctx.stroke();
            ctx.fillStyle = this.vpn === 'connected' ? 'rgb(74,222,128)' : 'rgb(255,255,255)';
            ctx.beginPath(); ctx.arc(X(p), Y(p), 4, 0, TAU); ctx.fill();
        }
    }
}

const instances = new Map();
let nextId = 1;

export function create(canvas) {
    const id = nextId++;
    instances.set(id, new Planet(canvas));
    return id;
}

export function setState(id, state) {
    const p = instances.get(id);
    if (p) p.vpn = state;
}

export function setServer(id, lat, lon) {
    const p = instances.get(id);
    if (p) p.server = { lat, lon };
}

export function clearServer(id) {
    const p = instances.get(id);
    if (p) p.server = null;
}

export function dispose(id) {
    const p = instances.get(id);
    if (p) { p.dispose(); instances.delete(id); }
}
