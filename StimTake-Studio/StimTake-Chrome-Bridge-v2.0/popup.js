'use strict';

const DEFAULTS = {
  version: '2.0.0',
  observerStatus: 'WAITING',
  sent: 0,
  candidates: 0,
  observedMutations: 0,
  duplicatesSuppressed: 0,
  lastDetectedUsername: '',
  lastDetectedAmount: null,
  lastRawTipRow: '',
  room: '',
  pageTitle: '',
  pageUrl: '',
  lastTip: null,
  lastCandidate: null,
  lastDelivery: null,
  lastError: null,
  lastObserverMessage: ''
};

function setText(id, value) {
  document.getElementById(id).textContent = value == null || value === '' ? 'None' : String(value);
}

async function readState() {
  const { bridgeState } = await chrome.storage.local.get('bridgeState');
  return Object.assign({}, DEFAULTS, bridgeState || {});
}

async function render() {
  const state = await readState();

  const observer = document.getElementById('observerStatus');
  observer.textContent = state.observerStatus || 'UNKNOWN';
  observer.className = 'value ' +
    (state.observerStatus === 'WATCHING' ? 'good' :
     state.observerStatus === 'WAITING' ? 'warn' : 'muted');

  setText('observerMessage', state.lastObserverMessage || 'No observer message');
  setText('sent', state.sent || 0);
  setText('candidates', state.candidates || 0);
  setText('mutations', state.observedMutations || 0);
  setText('duplicates', state.duplicatesSuppressed || 0);
  setText('room', state.room || 'None');
  setText('lastUsername', state.lastDetectedUsername || 'None');
  setText(
    'lastAmount',
    state.lastDetectedAmount == null ? 'None' : state.lastDetectedAmount
  );
  setText('lastRawTipRow', state.lastRawTipRow || 'None');
  setText('pageTitle', state.pageTitle || 'None');
  setText('pageUrl', state.pageUrl || 'None');
  setText('lastDelivery', state.lastDelivery || 'None');

  document.getElementById('lastTip').textContent =
    state.lastTip ? JSON.stringify(state.lastTip, null, 2) : 'None yet';

  document.getElementById('lastCandidate').textContent =
    state.lastCandidate ? JSON.stringify(state.lastCandidate, null, 2) : 'None yet';

  const err = document.getElementById('lastError');
  err.textContent = state.lastError || 'None';
  err.className = 'value ' + (state.lastError ? 'bad' : 'good');
}


document.getElementById('copyDiag').addEventListener('click', async () => {
  const state = await readState();
  const diagnostic = {
    version: state.version,
    observerStatus: state.observerStatus,
    observerMessage: state.lastObserverMessage,
    room: state.room,
    pageTitle: state.pageTitle,
    pageUrl: state.pageUrl,
    sent: state.sent,
    candidates: state.candidates,
    observedMutations: state.observedMutations,
    duplicatesSuppressed: state.duplicatesSuppressed,
    lastDetectedUsername: state.lastDetectedUsername,
    lastDetectedAmount: state.lastDetectedAmount,
    lastRawTipRow: state.lastRawTipRow,
    lastTip: state.lastTip,
    lastCandidate: state.lastCandidate,
    lastDelivery: state.lastDelivery,
    lastError: state.lastError
  };

  await navigator.clipboard.writeText(JSON.stringify(diagnostic, null, 2));

  const button = document.getElementById('copyDiag');
  const old = button.textContent;
  button.textContent = 'COPIED';
  setTimeout(() => { button.textContent = old; }, 1400);
});

document.getElementById('clear').addEventListener('click', async () => {
  await chrome.runtime.sendMessage({ type: 'STIMTAKE_CLEAR' });
  await render();
});

chrome.storage.onChanged.addListener(render);
render();
