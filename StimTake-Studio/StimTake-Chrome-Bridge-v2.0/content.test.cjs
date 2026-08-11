'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

class FakeElement {
  constructor(tagName = 'div', ownText = '') {
    this.nodeType = 1;
    this.tagName = tagName.toUpperCase();
    this.className = '';
    this.children = [];
    this.parentElement = null;
    this.ownText = ownText;
  }

  append(...children) {
    for (const child of children) {
      child.parentElement = this;
      this.children.push(child);
    }
    return this;
  }

  get previousElementSibling() {
    if (!this.parentElement) return null;
    const siblings = this.parentElement.children;
    const index = siblings.indexOf(this);
    return index > 0 ? siblings[index - 1] : null;
  }

  get textContent() {
    return [this.ownText, ...this.children.map((child) => child.textContent)]
      .filter(Boolean)
      .join('\n');
  }

  get innerText() {
    return this.textContent;
  }

  get outerHTML() {
    return `<${this.tagName.toLowerCase()}>${this.textContent}</${this.tagName.toLowerCase()}>`;
  }

  querySelectorAll(selector) {
    assert.equal(selector, '*');
    const descendants = [];
    for (const child of this.children) {
      descendants.push(child, ...child.querySelectorAll('*'));
    }
    return descendants;
  }
}

class FakeMutationObserver {
  constructor(callback) {
    this.callback = callback;
    FakeMutationObserver.instance = this;
  }

  observe(element, options) {
    this.element = element;
    this.options = options;
  }
}

const chat = new FakeElement('div');
chat.className = 'msg-list-fvm message-list';

const messages = [];
const chrome = {
  runtime: {
    async sendMessage(message) {
      messages.push(message);
      return { ok: true };
    }
  }
};

const context = vm.createContext({
  chrome,
  console: { debug() {}, info() {}, warn() {} },
  document: {
    title: 'Fixture room',
    documentElement: { dataset: {} },
    querySelector(selector) {
      return selector === 'div.msg-list-fvm.message-list' ? chat : null;
    }
  },
  location: {
    href: 'https://chaturbate.com/fixture_room/',
    pathname: '/fixture_room/'
  },
  MutationObserver: FakeMutationObserver,
  Node: { ELEMENT_NODE: 1, TEXT_NODE: 3 },
  setTimeout() {
    throw new Error('The fixture chat container should be found immediately.');
  },
  window: {}
});

const contentPath = path.join(__dirname, 'content.js');
vm.runInContext(fs.readFileSync(contentPath, 'utf8'), context, {
  filename: contentPath
});

assert.ok(FakeMutationObserver.instance, 'MutationObserver should attach');
assert.equal(FakeMutationObserver.instance.element, chat);
assert.equal(FakeMutationObserver.instance.options.childList, true);
assert.equal(FakeMutationObserver.instance.options.subtree, true);

function mutationFor(node, target = node.parentElement) {
  FakeMutationObserver.instance.callback([{
    type: 'childList',
    addedNodes: [node],
    target
  }]);
}

function addSplitTip(username, amount, tokenWord = 'tokens') {
  const usernameRow = new FakeElement('div', username);
  const phrase = new FakeElement('span', `tipped ${amount} ${tokenWord}`);
  const innerTipWrapper = new FakeElement('span').append(phrase);
  const tipRow = new FakeElement('div').append(innerTipWrapper);
  chat.append(usernameRow, tipRow);
  mutationFor(phrase, innerTipWrapper);
  return { phrase, tipRow };
}

function addNonTip(text) {
  const row = new FakeElement('div', text);
  chat.append(row);
  mutationFor(row, chat);
}

function sentTips() {
  return messages.filter((message) => message.type === 'STIMTAKE_TIP');
}

function suppressedDuplicates() {
  return messages.filter((message) => message.type === 'STIMTAKE_DUPLICATE');
}

const liveEvidence = [
  ['navel72', 67],
  ['justcallmetex', 25],
  ['bstudly', 67],
  ['poopyboy248', 20],
  ['sunnyson3', 25],
  ['higeva3943', 280],
  ['jar00d_op', 1],
  ['tsopanoskilos', 41],
  ['bstudly', 5]
];

const fixtureRows = liveEvidence.map(([username, amount]) =>
  addSplitTip(username, amount, amount === 1 ? 'token' : 'tokens')
);

assert.deepEqual(
  sentTips().map(({ event }) => [event.username, event.amount]),
  liveEvidence,
  'all supplied live rows should reconstruct username and amount'
);

assert.ok(
  sentTips().every(({ event }) => event.parser === 'split-node'),
  'the adjacent-row fixture should use the split-node parser'
);

assert.equal(
  sentTips().at(-1).rawTipRow,
  'tipped 5 tokens',
  'the raw tip phrase row should be retained for diagnostics'
);

const deliveredBeforeSameRowReplay = sentTips().length;
mutationFor(fixtureRows.at(-1).phrase);
assert.equal(sentTips().length, deliveredBeforeSameRowReplay);
assert.equal(suppressedDuplicates().length, 1);

const repeatedTip = addSplitTip('bstudly', 5);
assert.equal(sentTips().length, deliveredBeforeSameRowReplay + 1);
assert.notEqual(
  sentTips().at(-1).event.event_id,
  sentTips().at(-2).event.event_id,
  'separate identical tips should receive distinct event IDs'
);

mutationFor(repeatedTip.phrase);
assert.equal(sentTips().length, deliveredBeforeSameRowReplay + 1);
assert.equal(suppressedDuplicates().length, 2);

const beforeNegatives = sentTips().length;
addNonTip('Room goal: 500 tokens');
addNonTip('500 tokens from goal');
addNonTip('Notice: bstudly tipped for a special show');
addNonTip('Notice: bstudly tipped 5 tokens');
addSplitTip('zero_tip_user', 0);

const separatedUsername = new FakeElement('div', 'wrong_user');
const unrelatedRow = new FakeElement('div', 'ordinary chat text');
const separatedPhrase = new FakeElement('div', 'tipped 99 tokens');
chat.append(separatedUsername, unrelatedRow, separatedPhrase);
mutationFor(separatedPhrase, chat);

assert.equal(
  sentTips().length,
  beforeNegatives,
  'goals, follow-up notices, zero amounts, and non-adjacent usernames must not count'
);

const combinedRow = new FakeElement('div', 'letters_123 tipped 1 token');
chat.append(combinedRow);
mutationFor(combinedRow, chat);
assert.deepEqual(
  [sentTips().at(-1).event.username, sentTips().at(-1).event.amount],
  ['letters_123', 1],
  'a strict combined tip row should remain supported'
);

const beforeCrossParserReplay = sentTips().length;
const pairedUsername = new FakeElement('div', 'paired_user');
const pairedPhrase = new FakeElement('span', 'tipped 12 tokens');
const pairedTipRow = new FakeElement('div').append(pairedPhrase);
const pairedEvent = new FakeElement('div').append(pairedUsername, pairedTipRow);
chat.append(pairedEvent);
mutationFor(pairedEvent, chat);
mutationFor(pairedPhrase, pairedTipRow);
assert.equal(sentTips().length, beforeCrossParserReplay + 1);
assert.equal(
  suppressedDuplicates().length,
  3,
  'the same event wrapper must be suppressed across full and split parser paths'
);

addSplitTip('large_amount_user', '123,456');
assert.deepEqual(
  [sentTips().at(-1).event.username, sentTips().at(-1).event.amount],
  ['large_amount_user', 123456],
  'comma-formatted positive integer amounts should be accepted'
);

console.log(`PASS content.js: ${liveEvidence.length} supplied live rows reconstructed`);
console.log('PASS content.js: same DOM row/cross-parser replay suppressed; separate identical row delivered');
console.log('PASS content.js: goals, notices, zero amounts, and non-adjacent rows rejected');
console.log('PASS content.js: singular, plural, and comma-formatted positive amounts accepted');

async function testBackgroundAndPopupDiagnostics() {
  const backgroundPath = path.join(__dirname, 'background.js');
  const popupPath = path.join(__dirname, 'popup.js');
  const popupHtmlPath = path.join(__dirname, 'popup.html');
  const manifestPath = path.join(__dirname, 'manifest.json');

  const backgroundSource = fs.readFileSync(backgroundPath, 'utf8');
  const popupSource = fs.readFileSync(popupPath, 'utf8');
  const popupHtml = fs.readFileSync(popupHtmlPath, 'utf8');

  assert.doesNotThrow(() => new vm.Script(backgroundSource));
  assert.doesNotThrow(() => new vm.Script(popupSource));
  assert.doesNotThrow(() => JSON.parse(fs.readFileSync(manifestPath, 'utf8')));

  assert.doesNotMatch(backgroundSource, /STIMTAKE_TEST_LOCAL|StimTakeTestViewer/);
  assert.doesNotMatch(popupSource, /STIMTAKE_TEST_LOCAL|testLocal/);
  assert.doesNotMatch(popupHtml, /SEND LOCAL|testLocal/);
  assert.match(
    backgroundSource,
    /http:\/\/127\.0\.0\.1:8787\/api\/platform-event/
  );

  for (const id of [
    'observerStatus',
    'room',
    'mutations',
    'sent',
    'lastUsername',
    'lastAmount',
    'lastRawTipRow',
    'duplicates'
  ]) {
    assert.match(popupHtml, new RegExp(`id=["']${id}["']`));
  }

  const storage = {};
  let installedListener;
  let messageListener;
  let deliveredUrl = '';

  const backgroundChrome = {
    storage: {
      local: {
        async get(key) {
          return { [key]: storage[key] };
        },
        async set(patch) {
          Object.assign(storage, patch);
        }
      }
    },
    action: {
      async setBadgeText() {},
      async setBadgeBackgroundColor() {}
    },
    runtime: {
      onInstalled: {
        addListener(listener) {
          installedListener = listener;
        }
      },
      onMessage: {
        addListener(listener) {
          messageListener = listener;
        }
      }
    }
  };

  const backgroundContext = vm.createContext({
    chrome: backgroundChrome,
    console,
    Date,
    encodeURIComponent,
    fetch: async (url, options) => {
      deliveredUrl = url;
      assert.equal(options.method, 'GET');
      assert.equal(options.credentials, 'omit');
      return { ok: true, status: 204 };
    }
  });

  vm.runInContext(backgroundSource, backgroundContext, {
    filename: backgroundPath
  });
  await installedListener();

  function dispatch(message) {
    return new Promise((resolve, reject) => {
      const timeout = setTimeout(
        () => reject(new Error(`No background response for ${message.type}`)),
        1000
      );
      const responded = (result) => {
        clearTimeout(timeout);
        resolve(result);
      };
      const keepsChannelOpen = messageListener(message, {}, responded);
      if (keepsChannelOpen !== true) responded(undefined);
    });
  }

  await dispatch({
    type: 'STIMTAKE_OBSERVER_STATUS',
    status: 'WATCHING',
    detail: 'fixture observer attached',
    pageUrl: 'https://chaturbate.com/fixture_room/',
    room: 'fixture_room',
    pageTitle: 'Fixture room'
  });
  await dispatch({ type: 'STIMTAKE_MUTATION' });
  await dispatch({
    type: 'STIMTAKE_TIP',
    rawTipRow: 'tipped 67 tokens',
    event: {
      type: 'tip',
      username: 'navel72',
      amount: 67,
      source: 'chaturbate-browser',
      room: 'fixture_room'
    }
  });
  await dispatch({ type: 'STIMTAKE_DUPLICATE' });

  assert.equal(storage.bridgeState.observerStatus, 'WATCHING');
  assert.equal(storage.bridgeState.room, 'fixture_room');
  assert.equal(storage.bridgeState.observedMutations, 1);
  assert.equal(storage.bridgeState.sent, 1);
  assert.equal(storage.bridgeState.lastDetectedUsername, 'navel72');
  assert.equal(storage.bridgeState.lastDetectedAmount, 67);
  assert.equal(storage.bridgeState.lastRawTipRow, 'tipped 67 tokens');
  assert.equal(storage.bridgeState.duplicatesSuppressed, 1);
  assert.match(deliveredUrl, /^http:\/\/127\.0\.0\.1:8787\/api\/platform-event\?data=/);

  console.log('PASS background/popup: requested diagnostics persist and render targets exist');
  console.log('PASS static: content, background, popup, and manifest parse successfully');
  console.log('PASS bridge API: localhost endpoint preserved; synthetic test-tip path absent');
}

testBackgroundAndPopupDiagnostics().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
