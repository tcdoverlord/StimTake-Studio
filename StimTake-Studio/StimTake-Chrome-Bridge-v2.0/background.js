'use strict';

const VERSION = '2.0.0';
const STIMTAKE_ENDPOINT = 'http://127.0.0.1:8787/api/platform-event';

const DEFAULT_STATE = {
  version: VERSION,
  observerStatus: 'WAITING',
  pageUrl: '',
  room: '',
  pageTitle: '',
  sent: 0,
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
  lastActivity: null,
  lastObserverMessage: 'Extension installed. Open a Chaturbate room and refresh.'
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

async function deliverToStimTake(event) {
  const url = STIMTAKE_ENDPOINT + '?data=' +
    encodeURIComponent(JSON.stringify(event));

  const response = await fetch(url, {
    method: 'GET',
    cache: 'no-store',
    credentials: 'omit'
  });

  if (!response.ok && response.status !== 204) {
    throw new Error(`StimTake returned HTTP ${response.status}`);
  }

  const state = await getState();
  await saveState({
    sent: Number(state.sent || 0) + 1,
    lastTip: event,
    lastDelivery: new Date().toISOString(),
    lastActivity: new Date().toISOString(),
    lastError: null
  });
  await setBadge(String(Math.min(99, Number(state.sent || 0) + 1)), '#2f9e44');
}

chrome.runtime.onInstalled.addListener(async () => {
  await saveState(DEFAULT_STATE);
  await setBadge('', '#666666');
});

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
    }).then(() => sendResponse({ ok: true }));
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
      .then(() => sendResponse({ ok: true }))
      .catch(async (error) => {
        const text = String(error && error.message || error);
        await saveState({
          lastError: text,
          lastActivity: new Date().toISOString()
        });
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

  if (message.type === 'STIMTAKE_CLEAR') {
    chrome.storage.local.set({ bridgeState: DEFAULT_STATE })
      .then(() => setBadge('', '#666666'))
      .then(() => sendResponse({ ok: true }));
    return true;
  }
});
