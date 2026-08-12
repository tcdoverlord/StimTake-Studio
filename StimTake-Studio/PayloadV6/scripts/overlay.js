'use strict';

(function () {
  const topList = document.getElementById('top-list');
  const vipName = document.getElementById('vip-name');
  const vipTotal = document.getElementById('vip-total');
  const lastTipperValue = document.getElementById('last-tipper-value');
  const actionFrame = document.getElementById('action-frame');
  let currentActionTimer = null;
  let lastEventAt = -1;
  let state = { supporters: {}, lastUsername: '', lastAmount: 0 };

  function cleanName(value) { return String(value || '').trim(); }
  function rows() {
    return Object.keys(state.supporters).map(name => ({ name, total: Number(state.supporters[name] || 0) }))
      .filter(row => row.name && row.total > 0)
      .sort((a, b) => b.total - a.total || a.name.localeCompare(b.name));
  }
  function render() {
    const ranked = rows();
    topList.innerHTML = '';
    if (!ranked.length) {
      topList.innerHTML = '<div class="empty">Waiting for real tips…</div>';
      vipName.textContent = 'Waiting for first tip…';
      vipTotal.textContent = '';
    } else {
      vipName.textContent = '👑 ' + ranked[0].name;
      vipTotal.textContent = ranked[0].total + ' tokens';
      ranked.slice(0, 10).forEach((row, index) => {
        const item = document.createElement('div'); item.className = 'tipper';
        const rank = document.createElement('div'); rank.className = 'rank'; rank.textContent = String(index + 1);
        const name = document.createElement('div'); name.className = 'name'; name.textContent = row.name;
        const total = document.createElement('div'); total.className = 'total'; total.textContent = row.total + ' tokens';
        item.append(rank, name, total); topList.appendChild(item);
      });
    }
    lastTipperValue.textContent = state.lastUsername
      ? state.lastUsername + ' • ' + state.lastAmount + (state.lastAmount === 1 ? ' token' : ' tokens')
      : 'Waiting…';
  }
  function applyStatus(payload) {
    if (!payload || typeof payload !== 'object') return;
    const source = payload.supporters || payload.session_support || {};
    const next = {};
    Object.keys(source).forEach(name => {
      const clean = cleanName(name); const total = Number(source[name] || 0);
      if (clean && total > 0) next[clean] = total;
    });
    state.supporters = next;
    state.lastUsername = cleanName(payload.last_username || '');
    state.lastAmount = Math.max(0, Number(payload.last_amount || 0));
    render();
  }
  function stopAction() {
    if (currentActionTimer) clearTimeout(currentActionTimer);
    currentActionTimer = null;
    actionFrame.style.display = 'none';
    actionFrame.removeAttribute('src');
  }
  function playAction(url, durationSeconds) {
    if (!url || !String(url).startsWith('/external-modules/action-slot-')) return;
    stopAction();
    actionFrame.style.display = 'block';
    actionFrame.src = url;
    const duration = Math.max(1, Math.min(3600, Number(durationSeconds || 6)));
    currentActionTimer = setTimeout(stopAction, duration * 1000);
  }
  function handleEvent(envelope) {
    if (!envelope || typeof envelope !== 'object') return;
    const at = Number(envelope.at || 0);
    if (at <= lastEventAt) return;
    lastEventAt = at;
    const type = String(envelope.type || '').toLowerCase();
    const payload = envelope.payload && typeof envelope.payload === 'object' ? envelope.payload : {};
    if (type === 'show-action-triggered') playAction(payload.url, payload.duration);
    if (type === 'module-action' && String(payload.action || '').toLowerCase() === 'stop') stopAction();
  }
  async function pollStatus() {
    try {
      const response = await fetch('/api/studio-status?t=' + Date.now(), { cache: 'no-store' });
      if (response.ok) applyStatus(await response.json());
    } catch (_) {}
  }
  async function pollEvent() {
    try {
      const response = await fetch('/api/event?t=' + Date.now(), { cache: 'no-store' });
      if (response.ok) handleEvent(await response.json());
    } catch (_) {}
  }
  setInterval(pollStatus, 1000);
  setInterval(pollEvent, 200);
  pollStatus(); pollEvent(); render();
})();
