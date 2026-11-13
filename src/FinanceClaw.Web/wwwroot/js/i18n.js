/* Interface texts in Spanish and English. Step content comes from /api/steps?lang=… */

const I18N = {
  es: {
    locale: "es-ES",
    "brand.sub": "GitHub Copilot SDK para .NET",
    "lang.label": "Idioma",
    "hud.engine": "Motor",
    "hud.model": "Modelo",
    "hud.context": "Contexto",
    "aria.rail": "Pasos de la demo",
    "aria.chat": "Conversación con el agente",
    "aria.context": "Información de contexto",
    "aria.input": "Mensaje para el agente",
    "aria.layers": "Capas activas en este paso",
    "composer.placeholder": "Pregunta a Finance Claw…",
    "composer.send": "Enviar",
    "composer.stop": "Parar",
    "tab.concept": "Concepto",
    "tab.code": "Código",
    "tab.portfolio": "Cartera",
    "tab.activity": "Actividad",
    "activity.intro": "Cada decisión de los hooks y cada herramienta ejecutada, en directo. Es lo mismo que se guarda en <code>workspace/audit.log</code>.",
    "approval.reject": "Rechazar <kbd>N</kbd>",
    "approval.approve": "Aprobar <kbd>S</kbd>",
    "approval.buy": "COMPRAR",
    "approval.sell": "VENDER",
    "approval.simulated": "Operación simulada: no se envía a ningún bróker.",
    "approval.kind.trade": "Operación",
    "approval.kind.write": "Escritura de fichero",
    "approval.kind.shell": "Comando de shell",
    "approval.kind.mcp": "Herramienta MCP",
    "approval.approved": "Aprobado por el presentador",
    "approval.rejected": "Rechazado por el presentador",
    "approval.title": "Aprobación",
    "approval.timeout": "Sin respuesta: se rechaza por defecto",
    layers: ["Bucle agéntico", "Herramientas", "Permisos y hooks", "Skills y agentes", "MCP", "Sesiones", "Tu modelo · BYOK", "Agent Framework"],
    "kind.custom": "tu código",
    "kind.builtin": "integrada",
    "kind.mcp": "MCP",
    step: "PASO {n}",
    "step.missingEnv": "Este paso necesita {vars}. Rellena el fichero .env y reinicia la web.",
    "session.starting": "arrancando el runtime…",
    "session.none": "sin sesión",
    "saved.title": "Sesiones guardadas",
    "saved.hint": "ResumeSessionAsync recupera la conversación desde disco.",
    "saved.loading": "Consultando ListSessionsAsync…",
    "saved.new": "Nueva sesión",
    "saved.newHint": "Empieza una conversación con un SessionId propio",
    "event.skill": "Skill cargada: <b>{name}</b>",
    "activity.skill": "Skill cargada bajo demanda",
    "activity.mainAgent": "agente principal",
    "event.compacting": "Compactando el contexto en segundo plano…",
    "event.compacted": "Contexto compactado: {before} → {after} tokens",
    "branch.label": "Sub-agente",
    "activity.subagentStart": "Sub-agente iniciado",
    "activity.subagentEnd": "Terminado · {n} herramientas",
    "blocked.prompt": "Prompt bloqueado por OnUserPromptSubmitted",
    "blocked.tool": "Bloqueado por OnPreToolUse: {target}",
    "history.mark": "Conversación recuperada con ResumeSessionAsync",
    "concept.why": "Por qué importa",
    "concept.watch": "Fíjate en",
    "concept.apis": "APIs",
    "concept.production": "Antes de producción",
    "code.intro": "El fichero real que ejecuta este paso.",
    "code.config": ' En <span class="d">magenta</span>, lo que añade a la configuración.',
    "code.missing": "El código fuente no está disponible en esta instalación.",
    "welcome.hint": "Elige una sugerencia o escribe tu pregunta. Teclas 1–8 para cambiar de paso.",
    "portfolio.intro": "Lo que hay en <code>workspace/portfolio.csv</code> con precios simulados.",
    "portfolio.total": "Valor total",
    "portfolio.pnl": "Ganancia latente",
  },
  en: {
    locale: "en-US",
    "brand.sub": "GitHub Copilot SDK for .NET",
    "lang.label": "Language",
    "hud.engine": "Engine",
    "hud.model": "Model",
    "hud.context": "Context",
    "aria.rail": "Demo steps",
    "aria.chat": "Conversation with the agent",
    "aria.context": "Context information",
    "aria.input": "Message for the agent",
    "aria.layers": "Layers active in this step",
    "composer.placeholder": "Ask Finance Claw…",
    "composer.send": "Send",
    "composer.stop": "Stop",
    "tab.concept": "Concept",
    "tab.code": "Code",
    "tab.portfolio": "Portfolio",
    "tab.activity": "Activity",
    "activity.intro": "Every hook decision and every tool run, live. It is the same record that goes to <code>workspace/audit.log</code>.",
    "approval.reject": "Reject <kbd>N</kbd>",
    "approval.approve": "Approve <kbd>Y</kbd>",
    "approval.buy": "BUY",
    "approval.sell": "SELL",
    "approval.simulated": "Simulated trade: nothing is sent to a broker.",
    "approval.kind.trade": "Trade",
    "approval.kind.write": "File write",
    "approval.kind.shell": "Shell command",
    "approval.kind.mcp": "MCP tool",
    "approval.approved": "Approved by the presenter",
    "approval.rejected": "Rejected by the presenter",
    "approval.title": "Approval",
    "approval.timeout": "No answer: rejected by default",
    layers: ["Agentic loop", "Tools", "Permissions and hooks", "Skills and agents", "MCP", "Sessions", "Your model · BYOK", "Agent Framework"],
    "kind.custom": "your code",
    "kind.builtin": "built-in",
    "kind.mcp": "MCP",
    step: "STEP {n}",
    "step.missingEnv": "This step needs {vars}. Fill in the .env file and restart the web app.",
    "session.starting": "starting the runtime…",
    "session.none": "no session",
    "saved.title": "Saved sessions",
    "saved.hint": "ResumeSessionAsync brings the conversation back from disk.",
    "saved.loading": "Calling ListSessionsAsync…",
    "saved.new": "New session",
    "saved.newHint": "Start a conversation with your own SessionId",
    "event.skill": "Skill loaded: <b>{name}</b>",
    "activity.skill": "Skill loaded on demand",
    "activity.mainAgent": "main agent",
    "event.compacting": "Compacting the context in the background…",
    "event.compacted": "Context compacted: {before} → {after} tokens",
    "branch.label": "Sub-agent",
    "activity.subagentStart": "Sub-agent started",
    "activity.subagentEnd": "Done · {n} tools",
    "blocked.prompt": "Prompt blocked by OnUserPromptSubmitted",
    "blocked.tool": "Blocked by OnPreToolUse: {target}",
    "history.mark": "Conversation restored with ResumeSessionAsync",
    "concept.why": "Why it matters",
    "concept.watch": "Watch for",
    "concept.apis": "APIs",
    "concept.production": "Before production",
    "code.intro": "The actual file this step runs.",
    "code.config": ' In <span class="d">magenta</span>, what it adds to the configuration.',
    "code.missing": "The source code is not available in this installation.",
    "welcome.hint": "Pick a suggestion or type your question. Keys 1–8 switch steps.",
    "portfolio.intro": "What is in <code>workspace/portfolio.csv</code>, with simulated prices.",
    "portfolio.total": "Total value",
    "portfolio.pnl": "Unrealized gain",
  },
};

/* Texts the server sends in Spanish (approval questions, hook reasons, errors). */
const SERVER_EN = [
  [/^¿Aprobar la operación\?$/, "Approve the trade?"],
  [/^¿Ejecutar este comando\?$/, "Run this command?"],
  [/^¿Escribir (.+)\?$/, "Write $1?"],
  [/^¿Llamar a (.+)\?$/, "Call $1?"],
  [/^Finance Claw no opera con información privilegiada\.$/, "Finance Claw does not trade on insider information."],
  [/^Paso desconocido\.$/, "Unknown step."],
  [/^El agente sigue trabajando\.$/, "The agent is still working."],
];

const LANG_KEY = "finance-claw-lang";
let lang = (() => {
  try { return localStorage.getItem(LANG_KEY) === "en" ? "en" : "es"; } catch { return "es"; }
})();

function t(key, vars) {
  const text = I18N[lang][key] ?? I18N.es[key] ?? key;
  return vars ? text.replace(/\{(\w+)\}/g, (_, name) => vars[name] ?? "") : text;
}

const locale = () => t("locale");

function fromServer(text) {
  if (lang !== "en" || typeof text !== "string") return text;
  for (const [pattern, english] of SERVER_EN) if (pattern.test(text)) return text.replace(pattern, english);
  return text;
}

/* Static markup: data-i18n (text), data-i18n-html, data-i18n-placeholder, data-i18n-aria. */
function applyStaticTexts() {
  document.documentElement.lang = lang;
  document.querySelectorAll("[data-i18n]").forEach((el) => { el.textContent = t(el.dataset.i18n); });
  document.querySelectorAll("[data-i18n-html]").forEach((el) => { el.innerHTML = t(el.dataset.i18nHtml); });
  document.querySelectorAll("[data-i18n-placeholder]").forEach((el) => { el.placeholder = t(el.dataset.i18nPlaceholder); });
  document.querySelectorAll("[data-i18n-aria]").forEach((el) => { el.setAttribute("aria-label", t(el.dataset.i18nAria)); });
  document.querySelectorAll("[data-lang]").forEach((el) => { el.setAttribute("aria-pressed", String(el.dataset.lang === lang)); });
}

function storeLanguage(next) {
  lang = next === "en" ? "en" : "es";
  try { localStorage.setItem(LANG_KEY, lang); } catch { /* the choice just won't survive a reload */ }
}
