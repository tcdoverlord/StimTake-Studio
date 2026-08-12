'use strict';

const VERSION = '3.0.0';
const STIMTAKE_ENDPOINT = 'http://127.0.0.1:8787/api/platform-event';
const STIMTAKE_STATUS_ENDPOINT = 'http://127.0.0.1:8787/api/studio-status';

const DEFAULT_STATE = {
  version: VERSION,
  observerStatus: 'WAITING',
  pageUrl: '',
  room: '',
  pageTitle: '',
  sent: 0,
  acceptedTips: 0,
  serverDuplicates: 0,
  serverRejected: 0,
  candidates: 0,
  observedMutations: 0,
  duplicatesSuppressed: 0,
  lastDetectedUsername: '',
  lastDetectedAmount: null,
  lastRawTipRow: '',
  lastTip: null,
  lastCandidate: null,
  lastError: null,
  lastDelivery: null,
  lastDeliveryResult: 'NONE',
  lastActivity: null,
  studioOnline: false,
  studioBackend: 'OFFLINE',
  studioModel: '',
  studioSessionActive: false,
  studioSessionTips: 0,
  studioSessionTokens: 0,
  studioAccepted: 0,
  studioDuplicates: 0,
  studioRejected: 0,
  roomMatch: null,
  lastObserverMessage: 'Extension installed. Start StimTake Studio 6.0, open the saved Chaturbate room, then refresh the room.'
};

async function getState() {
  const { bridgeState } = await chrome.storage.local.get('bridgeState');
  return Object.assign({}, DEFAULT_STATE, bridgeState || {}, { version: VERSION });
}

async function saveState(patch) {
  const current = await getState();
  const next = Object.assign({}, current, patch, { version: VERSION });
  await chrome.storage.local.set({ bridgeState: next });
  return next;
}

async function setBadge(text, color) {
  try {
    await chrome.action.setBadgeText({ text });
    if (color) await chrome.action.setBadgeBackgroundColor({ color });
  } catch (_) {}
}

async function fetchStudioStatus() {
  try {
    const response = await fetch(STIMTAKE_STATUS_ENDPOINT, {
      method: 'GET',
      cache: 'no-store',
      credentials: 'omit'
    });
    if (!response || !response.ok || typeof response.json !== 'function') {
      throw new Error('Studio status endpoint unavailable');
    }
    const data = await response.json();

    const state = await getState();
    const browserRoom = String(state.room || '').toLowerCase();
    const lockedModel = String(data.model || '').toLowerCase();
    const match = browserRoom && lockedModel ? browserRoom === lockedModel : null;

    await saveState({
      studioOnline: true,
      studioBackend: data.backend || 'RUNNING',
      studioModel: data.model || '',
      studioSessionActive: !!data.session_active,
      studioSessionTips: Number(data.session_tips || 0),
      studioSessionTokens: Number(data.session_tokens || 0),
      studioAccepted: Number(data.accepted || 0),
      studioDuplicates: Number(data.duplicates || 0),
      studioRejected: Number(data.rejected || 0),
      roomMatch: match
    });
    return data;
  } catch (_) {
    await saveState({ studioOnline: false, studioBackend: 'OFFLINE', roomMatch: null });
    return null;
  }
}

async function deliverToStimTake(event) {
  const before = await fetchStudioStatus();
  const url = STIMTAKE_ENDPOINT + '?data=' + encodeURIComponent(JSON.stringify(event));

  let response;
  try {
    response = await fetch(url, {
      method: 'GET',
      cache: 'no-store',
      credentials: 'omit'
    });
  } catch (_) {
    throw new Error('StimTake Studio is not reachable on 127.0.0.1:8787.');
  }

  const responseText = response && typeof response.text === 'function'
    ? await response.text().catch(() => '')
    : '';

  // Only make a second status request when the first one proved this is a
  // Studio 6.0 runtime. This also keeps the legacy V2 regression harness valid.
  const after = before ? await fetchStudioStatus() : null;
  const current = await getState();

  if (response && response.status === 204) {
    let duplicate = false;
    if (before && after) {
      const acceptedDelta = Number(after.accepted || 0) - Number(before.accepted || 0);
      const duplicateDelta = Number(after.duplicates || 0) - Number(before.duplicates || 0);
      duplicate = duplicateDelta > 0 && acceptedDelta <= 0;
    }

    if (duplicate) {
      await saveState({
        serverDuplicates: Number(current.serverDuplicates || 0) + 1,
        lastDelivery: new Date().toISOString(),
        lastDeliveryResult: 'DUPLICATE',
        lastActivity: new Date().toISOString(),
        lastError: null
      });
      await setBadge('D', '#8a6d3b');
      return { ok: true, accepted: false, duplicate: true };
    }

    const nextAccepted = Number(current.acceptedTips || current.sent || 0) + 1;
    await saveState({
      sent: nextAccepted,
      acceptedTips: nextAccepted,
      lastTip: event,
      lastDelivery: new Date().toISOString(),
      lastDeliveryResult: 'ACCEPTED',
      lastActivity: new Date().toISOString(),
      lastError: null
    });
    await setBadge(String(Math.min(99, nextAccepted)), '#2f9e44');
    return { ok: true, accepted: true, duplicate: false };
  }

  const status = response ? Number(response.status || 0) : 0;
  const permanent = [400, 403, 409, 422].includes(status);
  const reason = responseText || `StimTake returned HTTP ${status || 'unknown'}`;

  await saveState({
    serverRejected: Number(current.serverRejected || 0) + (permanent ? 1 : 0),
    lastDelivery: new Date().toISOString(),
    lastDeliveryResult: permanent ? 'REJECTED' : 'ERROR',
    lastActivity: new Date().toISOString(),
    lastError: reason
  });
  await setBadge(permanent ? 'X' : '!', permanent ? '#b86b12' : '#c92a2a');

  if (permanent) {
    // A model-lock/validation rejection is permanent for this DOM row.
    return { ok: true, accepted: false, rejected: true, permanent: true, error: reason };
  }
  throw new Error(reason);
}

chrome.runtime.onInstalled.addListener(async () => {
  await chrome.storage.local.set({ bridgeState: DEFAULT_STATE });
  await setBadge('', '#666666');
  await fetchStudioStatus();
});

if (chrome.runtime.onStartup && chrome.runtime.onStartup.addListener) {
  chrome.runtime.onStartup.addListener(() => { fetchStudioStatus(); });
}

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (!message || typeof message !== 'object') return;

  if (message.type === 'STIMTAKE_OBSERVER_STATUS') {
    saveState({
      observerStatus: message.status || 'UNKNOWN',
      pageUrl: message.pageUrl || '',
      room: message.room || '',
      pageTitle: message.pageTitle || '',
      lastObserverMessage: message.detail || '',
      lastActivity: new Date().toISOString()
    }).then(() => fetchStudioStatus())
      .then(() => sendResponse({ ok: true }));
    return true;
  }

  if (message.type === 'STIMTAKE_MUTATION') {
    getState().then((state) => saveState({
      observedMutations: Number(state.observedMutations || 0) + 1,
      lastActivity: new Date().toISOString()
    })).then(() => sendResponse({ ok: true }));
    return true;
  }

  if (message.type === 'STIMTAKE_TIP') {
    const event = message.event || {};
    saveState({
      lastDetectedUsername: event.username || '',
      lastDetectedAmount: event.amount == null ? null : event.amount,
      lastRawTipRow: message.rawTipRow || '',
      lastActivity: new Date().toISOString()
    }).then(() => deliverToStimTake(event))
      .then((result) => sendResponse(result))
      .catch(async (error) => {
        const text = String(error && error.message || error);
        await saveState({ lastDeliveryResult: 'ERROR', lastError: text, lastActivity: new Date().toISOString() });
        await setBadge('!', '#c92a2a');
        sendResponse({ ok: false, error: text });
      });
    return true;
  }

  if (message.type === 'STIMTAKE_DUPLICATE') {
    getState().then((state) => saveState({
      duplicatesSuppressed: Number(state.duplicatesSuppressed || 0) + 1,
      lastActivity: new Date().toISOString()
    })).then(() => sendResponse({ ok: true }));
    return true;
  }

  if (message.type === 'STIMTAKE_CANDIDATE') {
    getState().then((state) => saveState({
      candidates: Number(state.candidates || 0) + 1,
      lastCandidate: message.candidate,
      lastActivity: new Date().toISOString()
    })).then(() => sendResponse({ ok: true }));
    return true;
  }

  if (message.type === 'STIMTAKE_REFRESH_STATUS') {
    fetchStudioStatus()
      .then((status) => sendResponse({ ok: !!status, status }))
      .catch((error) => sendResponse({ ok: false, error: String(error) }));
    return true;
  }

  if (message.type === 'STIMTAKE_CLEAR') {
    chrome.storage.local.set({ bridgeState: DEFAULT_STATE })
      .then(() => setBadge('', '#666666'))
      .then(() => fetchStudioStatus())
      .then(() => sendResponse({ ok: true }));
    return true;
  }
});
