const state = { selectedId: null, tickets: [], models: [] };
const $ = (selector) => document.querySelector(selector);
const escapeHtml = (value = "") => String(value).replace(/[&<>'"]/g, char => ({
  "&": "&amp;", "<": "&lt;", ">": "&gt;", "'": "&#39;", '"': "&quot;"
}[char]));
const statusClass = value => String(value || "").replace(/([a-z])([A-Z])/g, "$1_$2").toLowerCase();

async function api(path, options = {}) {
  const response = await fetch(path, options);
  const body = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) {
    const detail = body?.detail || body?.title || body?.error || `HTTP ${response.status}`;
    const validation = body?.errors ? Object.values(body.errors).flat().join(" ") : "";
    throw new Error(validation || detail);
  }
  return body;
}

function setMessage(element, message, isError = false) {
  element.textContent = message;
  element.classList.toggle("error", isError);
}

async function loadHealth() {
  try {
    const response = await fetch("/health");
    if (!response.ok) throw new Error();
    $("#healthBadge").classList.add("online");
    $("#healthBadge").lastChild.textContent = " Agent radi";
  } catch {
    $("#healthBadge").classList.remove("online");
    $("#healthBadge").lastChild.textContent = " Agent nije dostupan";
  }
}

async function loadSettings() {
  const settings = await api("/api/settings");
  $("#goldCounter").textContent = settings.newGoldSinceLastTrain;
  $("#retrainThreshold").textContent = `od ${settings.retrainGoldThreshold} do retreninga`;
  $("#routeThreshold").textContent = `${Math.round(settings.autoRouteThreshold * 100)}%`;
}

async function loadModels() {
  state.models = await api("/api/models");
  const active = state.models.find(model => model.isActive);
  $("#activeModel").textContent = active ? `v${active.versionNumber}` : "—";
  $("#modelAccuracy").textContent = active ? `${Math.round(active.testAccuracy * 100)}% test accuracy` : "nema modela";
  $("#modelList").innerHTML = state.models.length ? state.models.map(model => `
    <article class="model-card ${model.isActive ? "active" : ""}">
      ${model.isActive ? '<span class="active-flag">aktivan</span>' : ""}
      <h3>Model v${model.versionNumber}</h3>
      <dl>
        <dt>DB ID</dt><dd>${model.id}</dd>
        <dt>Tačnost</dt><dd>${Math.round(model.testAccuracy * 100)}%</dd>
        <dt>Korekcije</dt><dd>${model.trainingReviewCount}</dd>
        <dt>Review cutoff</dt><dd>#${model.trainingCutoffReviewId}</dd>
      </dl>
    </article>`).join("") : '<p class="empty">Nema modela.</p>';
}

async function loadTickets() {
  state.tickets = await api("/api/tickets?limit=100");
  $("#ticketCount").textContent = state.tickets.length;
  $("#ticketList").innerHTML = state.tickets.length ? state.tickets.map(ticket => `
    <button class="ticket-card ${ticket.id === state.selectedId ? "selected" : ""}" data-ticket-id="${ticket.id}" type="button">
      <span class="ticket-id">#${ticket.id}</span>
      <span class="ticket-title"><strong>${escapeHtml(ticket.subject)}</strong><span>${escapeHtml(ticket.assignedTeam || ticket.requesterEmail)}</span></span>
      <span class="status ${statusClass(ticket.status)}">${escapeHtml(ticket.status)}</span>
    </button>`).join("") : '<p class="empty">Nema ticketa.</p>';
  document.querySelectorAll("[data-ticket-id]").forEach(button =>
    button.addEventListener("click", () => selectTicket(Number(button.dataset.ticketId))));
}

function confidenceRow(label, value) {
  const percent = Math.round((value || 0) * 100);
  return `<div class="confidence-row"><span>${label}</span><span class="bar"><i style="width:${percent}%"></i></span><strong>${percent}%</strong></div>`;
}

async function selectTicket(id) {
  state.selectedId = id;
  await loadTickets();
  const ticket = await api(`/api/tickets/${id}`);
  const prediction = ticket.prediction;
  $("#ticketDetail").className = "";
  $("#ticketDetail").innerHTML = `
    <div class="detail-head"><div><span class="ticket-id">TICKET #${ticket.id}</span><h3>${escapeHtml(ticket.subject)}</h3></div><span class="status ${statusClass(ticket.status)}">${escapeHtml(ticket.status)}</span></div>
    <p class="detail-description">${escapeHtml(ticket.description)}</p>
    ${prediction ? `<div class="decision-grid">
      <div><small>Kategorija</small><strong>${escapeHtml(prediction.category)}</strong></div>
      <div><small>Prioritet</small><strong>${escapeHtml(prediction.priority)}</strong></div>
      <div><small>Dodijeljeni tim</small><strong>${escapeHtml(prediction.assignedTeam)}</strong></div>
      <div><small>Model DB ID</small><strong>#${prediction.modelVersionId}</strong></div>
    </div><div class="confidence">${confidenceRow("Kategorija", prediction.categoryConfidence)}${confidenceRow("Prioritet", prediction.priorityConfidence)}</div>` : '<p class="empty">Agent još obrađuje ticket…</p>'}
    ${ticket.failureReason ? `<p class="reviewed">Recovery zapis: ${escapeHtml(ticket.failureReason)}</p>` : ""}
    ${ticket.review ? `<p class="reviewed">Gold label: <strong>${escapeHtml(ticket.review.goldCategory)} / ${escapeHtml(ticket.review.goldPriority)}</strong> · ${escapeHtml(ticket.review.reviewer)}</p>` : ""}`;

  const reviewButton = $("#reviewForm button");
  $("#reviewForm [name=ticketId]").value = id;
  reviewButton.disabled = !prediction || Boolean(ticket.review) || ticket.status === "Failed";
  if (prediction && !ticket.review) {
    $("#reviewForm [name=category]").value = prediction.category;
    $("#reviewForm [name=priority]").value = prediction.priority;
  }
}

$("#ticketForm").addEventListener("submit", async event => {
  event.preventDefault();
  const ticketForm = event.currentTarget;
  const form = new FormData(ticketForm);
  try {
    setMessage($("#ticketMessage"), "Šaljem…");
    const ticket = await api("/api/tickets", {
      method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify(Object.fromEntries(form))
    });
    setMessage($("#ticketMessage"), `Ticket #${ticket.id} je u queue-u. Agent ga obrađuje u pozadini.`);
    ticketForm.reset();
    ticketForm.elements.requesterEmail.value = "demo@example.com";
    await selectTicket(ticket.id);
  } catch (error) { setMessage($("#ticketMessage"), error.message, true); }
});

$("#reviewForm").addEventListener("submit", async event => {
  event.preventDefault();
  const form = new FormData(event.currentTarget);
  const ticketId = form.get("ticketId");
  try {
    setMessage($("#reviewMessage"), "Spremam korekciju…");
    await api(`/api/tickets/${ticketId}/reviews`, {
      method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ category: form.get("category"), priority: form.get("priority"), reviewer: form.get("reviewer") })
    });
    setMessage($("#reviewMessage"), "Gold label je sačuvan i brojač je povećan.");
    await Promise.all([selectTicket(Number(ticketId)), loadSettings(), loadModels()]);
  } catch (error) { setMessage($("#reviewMessage"), error.message, true); }
});

$("#refreshButton").addEventListener("click", async () => {
  await Promise.all([loadTickets(), loadSettings(), loadModels()]);
  if (state.selectedId) await selectTicket(state.selectedId);
});

async function refresh() {
  try {
    await Promise.all([loadHealth(), loadTickets(), loadSettings(), loadModels()]);
    if (state.selectedId) await selectTicket(state.selectedId);
  } catch (error) { console.error(error); }
}

refresh();
setInterval(refresh, 2500);
