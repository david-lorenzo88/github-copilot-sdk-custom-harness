const $ = (selector) => document.querySelector(selector);
const reducedMotion = matchMedia("(prefers-reduced-motion: reduce)").matches;

const icons = {
  bolt: '<path d="M13 2 4 14h7l-1 8 9-12h-7z"/>',
  file: '<path d="M6 2h8l5 5v15H6z"/><path d="M14 2v5h5"/>',
  plug: '<path d="M9 2v6M15 2v6M6 8h12v4a6 6 0 0 1-12 0zM12 18v4"/>',
  star: '<path d="m12 2 3 7 7 .6-5.3 4.6 1.6 7L12 17.5 5.7 21l1.6-7L2 9.6 9 9z"/>',
  branch: '<path d="M6 3v12M18 9a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM6 21a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM18 9a9 9 0 0 1-9 9"/>',
  shield: '<path d="M12 2 4 5v7c0 5 3.5 8 8 10 4.5-2 8-5 8-10V5z"/><path d="m9 9 6 6M15 9l-6 6"/>',
  warn: '<path d="M12 3 2 21h20zM12 10v5M12 18v.5"/>',
  wave: '<path d="M2 12c3-6 5-6 8 0s5 6 8 0 3-4 4-2"/>',
};
const icon = (name) => `<svg class="icon" viewBox="0 0 24 24" aria-hidden="true">${icons[name]}</svg>`;

const layers = ["Bucle agéntico", "Herramientas", "Permisos y hooks", "Skills y agentes", "MCP", "Sesiones", "Tu modelo · BYOK", "Agent Framework"];
const activeLayers = { 1: [0], 2: [0, 1], 3: [0, 1, 2], 4: [0, 1, 2, 3], 5: [0, 1, 2, 3, 4], 6: [0, 1, 2, 3, 4, 5], 7: [0, 1, 2, 3, 4, 6], 8: [0, 1, 2, 3, 4, 7] };
const customTools = new Set(["get_stock_price", "place_trade"]);
const kindLabels = { custom: "tu código", builtin: "integrada", mcp: "MCP" };
const approvalLabels = { trade: "Operación", write: "Escritura de fichero", shell: "Comando de shell", mcp: "Herramienta MCP" };

const state = {
  steps: [],
  step: null,
  run: null,
  events: null,
  busy: false,
  turn: null,
  branch: null,
  tools: new Map(),
  branches: new Map(),
  approvals: [],
  unseen: 0,
  selection: 0,
};

const money = (value) => value.toLocaleString("es-ES", { style: "currency", currency: "USD", maximumFractionDigits: 2 });

async function api(path, options) {
  const response = await fetch(path, options);
  const body = response.status === 202 || response.headers.get("content-length") === "0" ? null : await response.json().catch(() => null);
  if (!response.ok) throw new Error(body?.error ?? `Error ${response.status}`);
  return body;
}
const post = (path, body) => api(path, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body ?? {}) });

function countUp(element, target, format) {
  if (reducedMotion) { element.textContent = format(target); return; }
  const started = performance.now();
  const tick = (now) => {
    const progress = Math.min((now - started) / 900, 1);
    element.textContent = format(target * (1 - (1 - progress) ** 3));
    if (progress < 1) requestAnimationFrame(tick);
  };
  requestAnimationFrame(tick);
}

function replay(element, className) {
  element.classList.remove(className);
  void element.offsetWidth;
  element.classList.add(className);
}

/* ---------- rail and step selection ---------- */

function renderRail() {
  $("#rail").innerHTML = state.steps.map((step) => `
    <li><button data-step="${step.number}">
      <span class="node">${step.number}</span>
      <span class="rail-text"><b>${escapeHtml(step.title)}</b><small>${escapeHtml(step.tagline)}</small></span>
    </button></li>`).join("");
  $("#rail").addEventListener("click", (e) => {
    const button = e.target.closest("button[data-step]");
    if (button) selectStep(Number(button.dataset.step));
  });
}

async function selectStep(number) {
  const step = state.steps.find((s) => s.number === number);
  if (!step || state.step?.number === number) return;

  const selection = ++state.selection;
  closeRun();
  state.step = step;
  history.replaceState(null, "", `#${number}`);

  document.querySelectorAll("#rail button").forEach((b) => {
    const n = Number(b.dataset.step);
    b.classList.toggle("active", n === number);
    b.classList.toggle("done", n < number);
  });
  $("#rail-progress").style.setProperty("--progress", (number - 1) / (state.steps.length - 1));
  replay($("#stage"), "warp");

  $("#chat-step").textContent = `PASO ${String(number).padStart(2, "0")}`;
  $("#chat-title").textContent = step.title;
  $("#engine").textContent = number === 7 ? "Foundry · BYOK" : number === 8 ? "Agent Framework" : "GitHub Copilot";
  $("#model").textContent = "—";
  setSession("");
  resetGauge();
  renderConcept(step);
  renderCode(step);
  renderPrompts(step);
  renderWelcome(step);
  setBusy(false);

  if (step.missingEnv.length) {
    setComposerEnabled(false);
    addNotice("warn", `Este paso necesita ${step.missingEnv.join(", ")}. Rellena el fichero .env y reinicia la web.`);
    return;
  }
  if (number === 6) {
    setComposerEnabled(false);
    await offerSavedSessions(selection);
    return;
  }
  await startRun(selection);
}

async function startRun(selection, resumeSessionId) {
  setComposerEnabled(false);
  setSession("arrancando el runtime…");
  try {
    const run = await post("/api/runs", { step: state.step.number, resumeSessionId });
    if (selection !== state.selection) return;
    state.run = run;
    setSession(run.sessionId);
    connectEvents(run.id);
    setComposerEnabled(true);
    $("#input").focus({ preventScroll: true });
  } catch (error) {
    if (selection !== state.selection) return;
    setSession("sin sesión");
    addNotice("error", error.message);
  }
}

function closeRun() {
  state.events?.close();
  state.events = null;
  state.run = null;
  state.turn = null;
  state.branch = null;
  state.tools.clear();
  state.branches.clear();
  state.approvals = [];
  if ($("#approval").open) $("#approval").close();
}

function connectEvents(runId) {
  state.events = new EventSource(`/api/runs/${runId}/events`);
  state.events.onmessage = (message) => handleEvent(JSON.parse(message.data));
}

async function offerSavedSessions(selection) {
  const card = document.createElement("div");
  card.className = "saved enter";
  card.innerHTML = `<h3>Sesiones guardadas</h3><p class="saved-hint">ResumeSessionAsync recupera la conversación desde disco.</p><div class="saved-list">Consultando ListSessionsAsync…</div>`;
  $("#feed").append(card);

  let saved = [];
  try { saved = await api("/api/sessions/saved"); }
  catch (error) { addNotice("error", error.message); }
  if (selection !== state.selection) return;

  const list = card.querySelector(".saved-list");
  list.innerHTML = saved.map((s) => `
    <button class="saved-item" data-id="${escapeHtml(s.id)}">
      <b>${escapeHtml(s.summary || s.id)}</b>
      <small>${escapeHtml(s.id)} · ${new Date(s.modified).toLocaleString("es-ES")}</small>
    </button>`).join("") + `<button class="saved-item saved-new" data-new="1"><b>Nueva sesión</b><small>Empieza una conversación con un SessionId propio</small></button>`;

  list.addEventListener("click", (e) => {
    const item = e.target.closest(".saved-item");
    if (!item) return;
    $("#feed").innerHTML = "";
    startRun(selection, item.dataset.id);
  }, { once: false });
}

/* ---------- agent events ---------- */

function handleEvent(event) {
  switch (event.type) {
    case "turn-start": beginTurn(event.prompt); break;
    case "turn-end": endTurn(); break;
    case "delta": appendText(event.text); break;
    case "history": addHistory(event.role, event.text); break;
    case "tool-start": addTool(event); break;
    case "tool-end": finishTool(event); break;
    case "skill": addEvent("skill", "star", `Skill cargada: <b>${escapeHtml(event.name)}</b>`, event.description); burst(); logActivity("skill", event.name, "Skill cargada bajo demanda"); break;
    case "subagent-start": addBranch(event); break;
    case "subagent-end": endBranch(event); break;
    case "hook-blocked": hookBlocked(event); break;
    case "audit": logActivity("audit", event.tool, `${event.agent === "main" ? "agente principal" : event.agent} · ${event.args}`, event.at); break;
    case "approval-request": enqueueApproval(event); break;
    case "approval-resolved": approvalResolved(event); break;
    case "context": setGauge(event.current, event.limit); break;
    case "usage": if (event.model) $("#model").textContent = event.model; break;
    case "compaction-start": addEvent("compaction", "wave", "Compactando el contexto en segundo plano…"); $("#gauge").classList.add("compacting"); break;
    case "compaction-end": $("#gauge").classList.remove("compacting"); addEvent("compaction", "wave", `Contexto compactado: ${(event.before ?? 0).toLocaleString("es-ES")} → ${(event.after ?? 0).toLocaleString("es-ES")} tokens`); break;
    case "warning": addNotice("warn", event.message); break;
    case "error": addNotice("error", event.message); break;
  }
}

function clearWelcome() { $("#feed .welcome")?.remove(); }

function beginTurn(prompt) {
  clearWelcome();
  setBusy(true);
  addBubble("user", escapeHtml(prompt));

  const block = document.createElement("div");
  block.className = "agent enter";
  block.innerHTML = `<div class="agent-tag">${icon("bolt")}<span>Finance Claw</span></div><div class="agent-body"></div><div class="thinking"><i></i><i></i><i></i></div>`;
  $("#feed").append(block);
  state.turn = { block, body: block.querySelector(".agent-body"), text: null, raw: "", pending: false };
  state.branch = null;
  scrollFeed(true);
}

function endTurn() {
  if (state.turn) {
    flushText();
    state.turn.block.querySelector(".thinking")?.remove();
    state.turn.text?.classList.remove("streaming");
    if (!state.turn.body.children.length) state.turn.block.remove();
  }
  document.querySelectorAll(".tool.running").forEach((tool) => tool.classList.replace("running", "cancelled"));
  state.turn = null;
  setBusy(false);
  loadPortfolio();
  $("#input").focus({ preventScroll: true });
}

function appendText(text) {
  if (!state.turn) return;
  if (!state.turn.text) {
    state.turn.text = document.createElement("div");
    state.turn.text.className = "agent-text streaming";
    state.turn.body.append(state.turn.text);
    state.turn.raw = "";
  }
  state.turn.raw += text;
  if (!state.turn.pending) {
    state.turn.pending = true;
    requestAnimationFrame(flushText);
  }
}

function flushText() {
  const turn = state.turn;
  if (!turn) return;
  turn.pending = false;
  if (turn.text) turn.text.innerHTML = renderMarkdown(turn.raw);
  scrollFeed();
}

function sealText() {
  if (!state.turn?.text) return;
  flushText();
  state.turn.text.classList.remove("streaming");
  state.turn.text = null;
}

function toolKind(event) {
  if (event.mcpServer || event.name.startsWith("market-")) return "mcp";
  return customTools.has(event.name) ? "custom" : "builtin";
}

function addTool(event) {
  // skill and task are shown by their own, richer events.
  if (!state.turn || event.name === "skill" || event.name === "task") return;
  sealText();

  const kind = toolKind(event);
  const tool = document.createElement("details");
  tool.className = `tool running ${kind}`;
  tool.innerHTML = `
    <summary>
      <span class="tool-state"></span>
      ${icon(kind === "mcp" ? "plug" : kind === "custom" ? "bolt" : "file")}
      <span class="tool-name">${escapeHtml(event.name)}</span>
      <span class="tool-kind">${kindLabels[kind]}</span>
    </summary>
    <pre>${escapeHtml(event.args)}</pre>`;

  const parent = (event.nested && state.branch) || state.turn.body;
  parent.append(tool);
  state.tools.set(event.id, tool);
  scrollFeed();
}

function finishTool(event) {
  const tool = state.tools.get(event.id);
  if (!tool) return;
  tool.classList.remove("running");
  tool.classList.add(event.success ? "ok" : "failed");
  if (event.error) {
    const error = document.createElement("div");
    error.className = "tool-error";
    error.textContent = event.error;
    tool.append(error);
    tool.open = true;
  }
}

function addBranch(event) {
  if (!state.turn) return;
  sealText();
  const branch = document.createElement("div");
  branch.className = "branch enter";
  branch.innerHTML = `<div class="branch-head">${icon("branch")}<span>Sub-agente · <b>${escapeHtml(event.name)}</b></span><i class="branch-state"></i></div><div class="branch-body"></div>`;
  state.turn.body.append(branch);
  state.branch = branch.querySelector(".branch-body");
  state.branches.set(event.id, branch);
  logActivity("agent", event.name, "Sub-agente iniciado");
  scrollFeed();
}

function endBranch(event) {
  const branch = state.branches.get(event.id);
  branch?.classList.add("closed");
  state.branch = null;
  logActivity("agent", event.name, `Terminado · ${event.tools ?? 0} herramientas`);
}

function addEvent(kind, iconName, html, description) {
  if (!state.turn) return;
  sealText();
  const line = document.createElement("div");
  line.className = `event ${kind} enter`;
  line.innerHTML = `${icon(iconName)}<span>${html}${description ? `<small>${escapeHtml(description)}</small>` : ""}</span>`;
  state.turn.body.append(line);
  scrollFeed();
}

function burst() {
  if (reducedMotion) return;
  const ring = document.createElement("div");
  ring.className = "burst";
  $(".chat").append(ring);
  ring.addEventListener("animationend", () => ring.remove());
}

function hookBlocked(event) {
  const message = event.target === "prompt" ? "Prompt bloqueado por OnUserPromptSubmitted" : `Bloqueado por OnPreToolUse: ${event.target}`;
  clearWelcome();
  const line = document.createElement("div");
  line.className = "blocked enter";
  line.innerHTML = `${icon("shield")}<div><b>${escapeHtml(message)}</b><small>${escapeHtml(event.reason)}</small></div>`;
  (state.turn?.body ?? $("#feed")).append(line);
  sealText();

  replay($("#flash"), "fire");
  replay($(".chat"), "shake");
  logActivity("denied", event.target, event.reason);
  scrollFeed();
}

function addBubble(role, html) {
  const bubble = document.createElement("div");
  bubble.className = `bubble ${role} enter`;
  bubble.innerHTML = html;
  $("#feed").append(bubble);
  scrollFeed(true);
}

function addHistory(role, text) {
  clearWelcome();
  if (!$("#feed .history-mark")) {
    const mark = document.createElement("div");
    mark.className = "history-mark enter";
    mark.textContent = "Conversación recuperada con ResumeSessionAsync";
    $("#feed").append(mark);
  }
  addBubble(role === "user" ? "user" : "assistant", role === "user" ? escapeHtml(text) : renderMarkdown(text));
}

function addNotice(kind, message) {
  clearWelcome();
  const notice = document.createElement("div");
  notice.className = `notice ${kind} enter`;
  notice.innerHTML = `${icon("warn")}<span>${escapeHtml(message)}</span>`;
  (state.turn?.body ?? $("#feed")).append(notice);
  scrollFeed(true);
}

function scrollFeed(force) {
  const feed = $("#feed");
  if (force || feed.scrollHeight - feed.scrollTop - feed.clientHeight < 140) feed.scrollTop = feed.scrollHeight;
}

/* ---------- approvals ---------- */

function enqueueApproval(request) {
  state.approvals.push(request);
  logActivity("approval", approvalLabels[request.kind], request.question);
  if (state.approvals.length === 1) showApproval();
}

function showApproval() {
  const request = state.approvals[0];
  const dialog = $("#approval");
  if (!request) {
    if (dialog.open) dialog.close();
    return;
  }

  dialog.dataset.kind = request.kind;
  $("#approval-kind").textContent = approvalLabels[request.kind];
  $("#approval-question").textContent = request.question;
  $("#approval-detail").innerHTML = renderApprovalDetail(request);

  const timer = $("#approval-timer");
  timer.style.animationDuration = `${request.timeoutSeconds}s`;
  replay(timer, "run");

  if (!dialog.open) dialog.showModal();
  replay(dialog.querySelector(".approval-card"), "pop");
  $("#approval-reject").focus();
}

function renderApprovalDetail(request) {
  if (!request.detail) return "";
  if (request.kind === "trade") {
    try {
      const trade = JSON.parse(request.detail);
      const estimate = state.prices?.[String(trade.symbol).toUpperCase()];
      return `<div class="ticket ${trade.action === "sell" ? "sell" : "buy"}">
        <span class="ticket-action">${trade.action === "sell" ? "VENDER" : "COMPRAR"}</span>
        <span class="ticket-qty">${Number(trade.quantity)}</span>
        <span class="ticket-symbol">${escapeHtml(trade.symbol)}</span>
        ${estimate ? `<span class="ticket-total">≈ ${money(estimate * trade.quantity)}</span>` : ""}
      </div><p class="approval-note">Operación simulada: no se envía a ningún bróker.</p>`;
    } catch { /* falls through to the raw view */ }
  }
  if (request.kind === "write") {
    // Skip the git header: it only adds local paths to what the audience sees.
    const lines = request.detail.split("\n");
    const diff = lines.slice(lines.findIndex((l) => l.startsWith("@@")) + 1).map((line) => {
      const cls = /^\+(?!\+\+)/.test(line) ? "add" : /^-(?!--)/.test(line) ? "del" : "";
      return `<span class="${cls}">${escapeHtml(line)}</span>`;
    }).join("\n");
    return `<pre class="approval-pre diff">${diff}</pre>`;
  }
  return `<pre class="approval-pre">${escapeHtml(request.detail)}</pre>`;
}

async function answerApproval(approved) {
  const request = state.approvals[0];
  if (!request || !state.run) return;
  state.approvals.shift();
  showApproval();
  logActivity(approved ? "approved" : "rejected", approvalLabels[request.kind], approved ? "Aprobado por el presentador" : "Rechazado por el presentador");
  await post(`/api/runs/${state.run.id}/approvals/${request.id}`, { approved }).catch(() => {});
}

function approvalResolved(event) {
  const index = state.approvals.findIndex((a) => a.id === event.id);
  if (index < 0) return;
  state.approvals.splice(index, 1);
  logActivity("rejected", "Aprobación", "Sin respuesta: se rechaza por defecto");
  if (index === 0) showApproval();
}

/* ---------- context panels ---------- */

function renderConcept(step) {
  const lit = activeLayers[step.number];
  $("#panel-concept").innerHTML = `
    <p class="lead">${escapeHtml(step.concept)}</p>
    <div class="stack" aria-label="Capas activas en este paso">
      ${layers.map((name, i) => `<span class="layer ${lit.includes(i) ? "on" : ""}" style="--i:${i}">${name}</span>`).join("")}
    </div>
    <h3>Por qué importa</h3>
    <p>${escapeHtml(step.whyItMatters)}</p>
    <h3>Fíjate en</h3>
    <ul class="watch">${step.watchFor.map((item, i) => `<li style="--i:${i}">${escapeHtml(item)}</li>`).join("")}</ul>
    <h3>APIs</h3>
    <div class="apis">${step.apis.map((api, i) => `<code style="--i:${i}">${escapeHtml(api)}</code>`).join("")}</div>
    <div class="production"><b>Antes de producción</b><p>${escapeHtml(step.production)}</p></div>`;
}

function highlightCSharp(source) {
  const token = /(\/\/\/?[^\n]*)|("""[\s\S]*?"""|\$?"(?:[^"\\\n]|\\.)*")|\b(public|private|static|async|await|using|namespace|class|return|new|var|if|else|where|for|foreach|while|in|is|not|null|true|false|const|sealed|record|string|bool|int|params)\b|(\bconfig\.\w+)|\b([A-Z][A-Za-z0-9]+)\b/g;
  let html = "";
  let last = 0;
  for (const match of source.matchAll(token)) {
    html += escapeHtml(source.slice(last, match.index));
    const text = escapeHtml(match[0]);
    const cls = match[1] ? "c" : match[2] ? "s" : match[3] ? "k" : match[4] ? "d" : "t";
    html += `<span class="${cls}">${text}</span>`;
    last = match.index + match[0].length;
  }
  return html + escapeHtml(source.slice(last));
}

function renderCode(step) {
  $("#panel-code").innerHTML = step.source
    ? `<p class="panel-intro">El fichero real que ejecuta este paso.${step.source.includes("config.") ? ' En <span class="d">magenta</span>, lo que añade a la configuración.' : ""}</p>
       <div class="code-file">src/FinanceClaw/Steps/${escapeHtml(step.sourceFile)}</div>
       <pre class="code"><code>${highlightCSharp(step.source)}</code></pre>`
    : `<p class="panel-intro">El código fuente no está disponible en esta instalación.</p>`;
}

function renderPrompts(step) {
  $("#prompts").innerHTML = step.prompts.map((p, i) =>
    `<button type="button" style="--i:${i}" data-prompt="${escapeHtml(p.text)}" title="${escapeHtml(p.text)}">${escapeHtml(p.label)}</button>`).join("");
}

function renderWelcome(step) {
  $("#feed").innerHTML = `
    <div class="welcome">
      <div class="welcome-num" data-text="${String(step.number).padStart(2, "0")}">${String(step.number).padStart(2, "0")}</div>
      <h2>${escapeHtml(step.title)}</h2>
      <p>${escapeHtml(step.tagline)}</p>
      <small>Elige una sugerencia o escribe tu pregunta. Teclas 1–8 para cambiar de paso.</small>
    </div>`;
}

async function loadPortfolio() {
  const data = await api("/api/portfolio").catch(() => null);
  if (!data) return;

  state.prices = Object.fromEntries(data.positions.map((p) => [p.symbol, p.price]));
  const pnl = data.totalValue - data.totalCost;
  const ordered = [...data.positions].sort((a, b) => b.value - a.value);

  const previous = $("#panel-portfolio .total-value");
  if (!previous) {
    $("#panel-portfolio").innerHTML = `
      <p class="panel-intro">Lo que hay en <code>workspace/portfolio.csv</code> con precios simulados.</p>
      <div class="totals">
        <div><small>Valor total</small><strong class="total-value"></strong></div>
        <div><small>Ganancia latente</small><strong class="total-pnl ${pnl >= 0 ? "up" : "down"}"></strong></div>
      </div>
      <ul class="positions">${ordered.map((p, i) => {
        const weight = (p.value / data.totalValue) * 100;
        const gain = ((p.value - p.cost) / p.cost) * 100;
        return `<li style="--i:${i}">
          <div class="pos-head"><b>${escapeHtml(p.symbol)}</b><span>${money(p.value)}</span></div>
          <div class="pos-bar"><span style="--w:${weight}%"></span></div>
          <div class="pos-foot"><span>${weight.toFixed(1)} %</span><span class="${gain >= 0 ? "up" : "down"}">${gain >= 0 ? "+" : ""}${gain.toFixed(1)} %</span></div>
        </li>`;
      }).join("")}</ul>`;
    countUp($("#panel-portfolio .total-value"), data.totalValue, money);
    countUp($("#panel-portfolio .total-pnl"), pnl, money);

    const tape = ordered.map((p) => {
      const gain = ((p.value - p.cost) / p.cost) * 100;
      return `<span><b>${escapeHtml(p.symbol)}</b> ${p.price.toFixed(2)} <i class="${gain >= 0 ? "up" : "down"}">${gain >= 0 ? "▲" : "▼"} ${Math.abs(gain).toFixed(1)}%</i></span>`;
    }).join("");
    $("#ticker").innerHTML = tape + tape + tape + tape;
  }
}

function logActivity(kind, title, detail, at) {
  const item = document.createElement("li");
  item.className = `activity-item ${kind} enter`;
  const time = (at ? new Date(at) : new Date()).toLocaleTimeString("es-ES");
  item.innerHTML = `<div><b>${escapeHtml(title)}</b><time>${time}</time></div><small>${escapeHtml(detail)}</small>`;
  $("#activity").prepend(item);
  while ($("#activity").children.length > 120) $("#activity").lastElementChild.remove();

  if (!document.querySelector('[data-tab="activity"]').classList.contains("active")) {
    state.unseen++;
    const badge = $("#activity-badge");
    badge.textContent = state.unseen;
    badge.hidden = false;
    replay(badge, "bump");
  }
}

/* ---------- HUD and composer ---------- */

function setSession(text) { $("#session").textContent = text; }

function resetGauge() {
  $("#gauge-fill").style.width = "0";
  $("#gauge-text").textContent = "";
  $("#gauge").classList.remove("hot", "compacting");
}

function setGauge(current, limit) {
  if (!limit) return;
  const percent = Math.min(100, (current / limit) * 100);
  $("#gauge-fill").style.width = `${percent}%`;
  $("#gauge-text").textContent = `${Math.round(current / 1000)}k / ${Math.round(limit / 1000)}k`;
  $("#gauge").classList.toggle("hot", percent >= 50);
}

function setComposerEnabled(enabled) {
  $("#input").disabled = !enabled;
  $("#send").disabled = !enabled;
  document.querySelectorAll("#prompts button").forEach((b) => { b.disabled = !enabled; });
}

function setBusy(busy) {
  state.busy = busy;
  $("#send").hidden = busy;
  $("#stop").hidden = !busy;
  if (state.run) setComposerEnabled(!busy);
  $("#stop").disabled = false;
}

async function send(prompt) {
  const text = prompt.trim();
  if (!text || !state.run || state.busy) return;
  $("#input").value = "";
  autoGrow();
  await post(`/api/runs/${state.run.id}/messages`, { prompt: text }).catch((error) => addNotice("error", error.message));
}

function autoGrow() {
  const input = $("#input");
  input.style.height = "auto";
  input.style.height = `${Math.min(input.scrollHeight, 140)}px`;
  input.style.overflowY = input.scrollHeight > 140 ? "auto" : "hidden";
}

function activateTab(name) {
  document.querySelectorAll(".tabs button").forEach((b) => b.classList.toggle("active", b.dataset.tab === name));
  document.querySelectorAll(".panel").forEach((p) => {
    const active = p.dataset.panel === name;
    p.classList.toggle("active", active);
    if (active) replay(p, "panel-in");
  });
  if (name === "activity") {
    state.unseen = 0;
    $("#activity-badge").hidden = true;
  }
}

/* ---------- wiring ---------- */

$("#composer").addEventListener("submit", (e) => { e.preventDefault(); send($("#input").value); });
$("#input").addEventListener("input", autoGrow);
$("#input").addEventListener("keydown", (e) => {
  if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); send($("#input").value); }
});
$("#stop").addEventListener("click", () => {
  if (!state.run) return;
  $("#stop").disabled = true;
  post(`/api/runs/${state.run.id}/stop`).catch(() => {});
});
$("#prompts").addEventListener("click", (e) => {
  const button = e.target.closest("button[data-prompt]");
  if (button) send(button.dataset.prompt);
});
document.querySelector(".tabs").addEventListener("click", (e) => {
  const button = e.target.closest("button[data-tab]");
  if (button) activateTab(button.dataset.tab);
});
$("#approval-approve").addEventListener("click", () => answerApproval(true));
$("#approval-reject").addEventListener("click", () => answerApproval(false));
$("#approval").addEventListener("cancel", (e) => e.preventDefault());

document.addEventListener("keydown", (e) => {
  if ($("#approval").open) {
    const key = e.key.toLowerCase();
    if (key === "s") { e.preventDefault(); answerApproval(true); }
    else if (key === "n") { e.preventDefault(); answerApproval(false); }
    return;
  }
  if (e.target.matches("textarea, input") && $("#input").value !== "") return;
  if (e.metaKey || e.ctrlKey || e.altKey) return;
  if (/^[1-8]$/.test(e.key)) { e.preventDefault(); selectStep(Number(e.key)); }
});

(async function init() {
  state.steps = await api("/api/steps");
  renderRail();
  await loadPortfolio();
  const fromHash = Number(location.hash.slice(1));
  selectStep(state.steps.some((s) => s.number === fromHash) ? fromHash : 1);
})();
