// Small Markdown renderer for agent output. Everything is escaped first, so model text can't inject HTML.
window.escapeHtml = (text) => String(text ?? "")
  .replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");

window.renderMarkdown = (() => {
  const inline = (text) => escapeHtml(text)
    .replace(/`([^`]+)`/g, "<code>$1</code>")
    .replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>")
    .replace(/(^|[^*])\*([^*\s][^*]*)\*/g, "$1<em>$2</em>")
    .replace(/\[([^\]]+)\]\((https:[^)\s]+)\)/g, '<a href="$2" target="_blank" rel="noreferrer">$1</a>');

  const cells = (row) => row.trim().replace(/^\||\|$/g, "").split("|").map((c) => c.trim());
  const isDivider = (line) => /^\s*\|?[\s:|-]+\|?\s*$/.test(line) && line.includes("-");

  return (source) => {
    const lines = source.split("\n");
    const out = [];
    let i = 0;

    const list = (pattern, tag) => {
      const items = [];
      while (i < lines.length && pattern.test(lines[i])) items.push(`<li>${inline(lines[i++].replace(pattern, ""))}</li>`);
      out.push(`<${tag}>${items.join("")}</${tag}>`);
    };

    while (i < lines.length) {
      const line = lines[i];

      if (line.startsWith("```")) {
        const code = [];
        i++;
        while (i < lines.length && !lines[i].startsWith("```")) code.push(lines[i++]);
        i++;
        out.push(`<pre><code>${escapeHtml(code.join("\n"))}</code></pre>`);
      } else if (line.includes("|") && i + 1 < lines.length && isDivider(lines[i + 1])) {
        const head = cells(line).map((c) => `<th>${inline(c)}</th>`).join("");
        i += 2;
        const body = [];
        while (i < lines.length && lines[i].includes("|")) {
          body.push(`<tr>${cells(lines[i++]).map((c) => `<td>${inline(c)}</td>`).join("")}</tr>`);
        }
        out.push(`<div class="table-wrap"><table><thead><tr>${head}</tr></thead><tbody>${body.join("")}</tbody></table></div>`);
      } else if (/^#{1,4}\s/.test(line)) {
        out.push(`<h4>${inline(line.replace(/^#+\s/, ""))}</h4>`);
        i++;
      } else if (/^\s*[-*]\s+/.test(line)) {
        list(/^\s*[-*]\s+/, "ul");
      } else if (/^\s*\d+\.\s+/.test(line)) {
        list(/^\s*\d+\.\s+/, "ol");
      } else if (line.trim() === "") {
        i++;
      } else {
        const paragraph = [];
        while (i < lines.length && lines[i].trim() !== "" && !/^(```|#{1,4}\s|\s*[-*]\s+|\s*\d+\.\s+)/.test(lines[i])) {
          if (lines[i].includes("|") && i + 1 < lines.length && isDivider(lines[i + 1])) break;
          paragraph.push(lines[i++]);
        }
        out.push(`<p>${inline(paragraph.join(" "))}</p>`);
      }
    }

    return out.join("");
  };
})();
