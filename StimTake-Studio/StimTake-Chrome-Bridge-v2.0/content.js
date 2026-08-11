'use strict';

/*
  StimTake Chrome Bridge v2.0
  Receiver only.

  Purpose:
  Watch Chaturbate's rendered room chat, detect received-tip alerts,
  normalize username + token amount + optional message, and forward the
  event to the local StimTake Studio service.

  This extension does NOT send tips, buy tokens, read payment information,
  request Chaturbate passwords, or request Chaturbate API tokens.
*/

(() => {
  if (window.__stimTakeChromeBridgeV2Loaded) return;
  window.__stimTakeChromeBridgeV2Loaded = true;

  const VERSION = '2.0.0';
  const SOURCE = 'chaturbate-browser';
  const handledTipRows = new WeakSet();
  const tipEventIds = new WeakMap();
  let observedChat = null;
  let eventSequence = 0;

  const CHAT_SELECTORS = [
    'div.msg-list-fvm.message-list',
    'div.msg-list-wrapper-split:nth-child(2) > div:nth-child(2)',
    '#ChatTabContainer div.msg-list-wrapper-split > div:nth-child(2)',
    '[class*="msg-list-wrapper"]',
    '[class*="message-list"]'
  ];

  const FULL_TIP_PATTERNS = [
    /^(?<user>[A-Za-z0-9_]{1,80})\s+(?:has\s+)?tipped\s+(?<amount>\d[\d,]*)\s+tokens?\b(?:\s*[-–—:]\s*(?<message>.*))?$/i
  ];

  const TIP_ONLY_PATTERN =
    /^tipped\s+(?<amount>\d[\d,]*)\s+tokens?\b(?:\s*[-–—:]\s*(?<message>.*))?$/i;

  function cleanText(value, max = 600) {
    return String(value || '')
      .replace(/[\u200B-\u200D\uFEFF]/g, '')
      .replace(/[\u00A0\s]+/g, ' ')
      .trim()
      .slice(0, max);
  }

  function roomName() {
    const parts = location.pathname.split('/').filter(Boolean);
    return parts.length ? parts[0] : '';
  }

  function reportStatus(status, detail) {
    chrome.runtime.sendMessage({
      type: 'STIMTAKE_OBSERVER_STATUS',
      status,
      detail,
      pageUrl: location.href,
      room: roomName(),
      pageTitle: document.title
    }).catch(() => {});
  }

  function reportMutation() {
    chrome.runtime.sendMessage({ type: 'STIMTAKE_MUTATION' }).catch(() => {});
  }

  function detectRequest(message) {
    const text = String(message || '');
    if (/(?:^|\s)(?:!dice|!roll|roll\s+(?:the\s+)?dice)(?:\s|$)/i.test(text)) return 'dice';
    if (/(?:^|\s)(?:!wheel|!spin|spin\s+(?:the\s+)?wheel)(?:\s|$)/i.test(text)) return 'wheel';
    return '';
  }

  function simpleHash(text) {
    let hash = 2166136261;
    for (let i = 0; i < text.length; i++) {
      hash ^= text.charCodeAt(i);
      hash = Math.imul(hash, 16777619);
    }
    return (hash >>> 0).toString(16).padStart(8, '0');
  }

  function fingerprint(event) {
    return [
      event.room || '',
      String(event.username || '').toLowerCase(),
      event.amount || 0,
      String(event.message || '').toLowerCase(),
      String(event.dom_text || '').toLowerCase()
    ].join('|');
  }

  function parseFullTip(text) {
    const normalized = cleanText(text, 1000);
    if (!normalized) return null;

    for (const pattern of FULL_TIP_PATTERNS) {
      const match = normalized.match(pattern);
      if (!match || !match.groups) continue;

      const username = cleanText(match.groups.user, 80);
      const amount = Number(String(match.groups.amount || '').replace(/,/g, ''));
      const message = cleanText(match.groups.message || '', 300);

      if (!username || !Number.isInteger(amount) || amount <= 0) continue;

      return {
        username,
        amount,
        message,
        dom_text: normalized,
        parser: 'full-text'
      };
    }

    return null;
  }

  function findUsernameNearTip(element) {
    if (!element) return null;

    // The confirmed live rendering uses adjacent rows inside the observed
    // chat list. Advance the current node as we climb so an inner tip span can
    // be paired with the username row beside its containing tip row.
    let current = element;
    for (let depth = 0; current && depth < 6; depth++, current = current.parentElement) {
      if (current === observedChat) break;

      let sibling = current.previousElementSibling;

      // Ignore empty/decorative siblings, but do not jump across a non-empty
      // chat row: the username row must be adjacent to this tip row.
      while (sibling) {
        const text = cleanText(sibling.innerText || sibling.textContent, 120);
        if (text) {
          const username = extractUsername(text);
          if (username) return { username, tipRow: current };
          break;
        }
        sibling = sibling.previousElementSibling;
      }

    }

    return null;
  }

  function extractUsername(text) {
    const normalized = cleanText(text, 120);
    return /^[A-Za-z0-9_]{1,80}$/.test(normalized) ? normalized : '';
  }

  function canonicalFullTipRow(element, text) {
    let current = element;

    // Nested wrappers often repeat the same innerText. Use the highest wrapper
    // with exactly the same tip text, but never promote the whole chat list.
    for (let depth = 0; current && current.parentElement && depth < 6; depth++) {
      const parent = current.parentElement;
      if (parent === observedChat) break;

      const parentText = cleanText(parent.innerText || parent.textContent, 1000);
      if (parentText !== text) break;
      current = parent;
    }

    return current;
  }

  function canonicalSplitTipRow(tipRow, combinedText) {
    const parent = tipRow && tipRow.parentElement;
    if (!parent || parent === observedChat) return tipRow;

    const parentText = cleanText(parent.innerText || parent.textContent, 1000);
    return parentText === combinedText
      ? canonicalFullTipRow(parent, combinedText)
      : tipRow;
  }

  function parseTipOnly(element, text) {
    const normalized = cleanText(text, 600);
    const match = normalized.match(TIP_ONLY_PATTERN);
    if (!match || !match.groups) return null;

    const amount = Number(String(match.groups.amount || '').replace(/,/g, ''));
    if (!Number.isInteger(amount) || amount <= 0) return null;

    const adjacent = findUsernameNearTip(element);
    if (!adjacent) return null;

    const domText = cleanText(adjacent.username + ' ' + normalized, 1000);

    return {
      username: adjacent.username,
      amount,
      message: cleanText(match.groups.message || '', 300),
      dom_text: domText,
      raw_tip_row: normalized,
      tip_element: canonicalSplitTipRow(adjacent.tipRow, domText),
      parser: 'split-node'
    };
  }

  function candidateLooksInteresting(text) {
    return /\btipped\b|\btokens?\b/i.test(text);
  }

  function sendCandidate(node, text, reason) {
    const candidate = {
      reason: reason || 'tip-like text',
      room: roomName(),
      text: cleanText(text, 800),
      tag: node && node.tagName || '',
      className: cleanText(node && node.className, 300),
      html: cleanText(node && node.outerHTML, 1800),
      time: new Date().toISOString()
    };

    console.debug('[StimTake Bridge] candidate DOM node:', candidate);

    chrome.runtime.sendMessage({
      type: 'STIMTAKE_CANDIDATE',
      candidate
    }).catch(() => {});
  }

  function reportDuplicate(parsed) {
    chrome.runtime.sendMessage({
      type: 'STIMTAKE_DUPLICATE',
      username: parsed.username,
      amount: parsed.amount,
      rawTipRow: parsed.raw_tip_row || parsed.dom_text || ''
    }).catch(() => {});
  }

  async function sendTip(parsed) {
    const tipRow = parsed.tip_element;

    if (tipRow && handledTipRows.has(tipRow)) {
      console.debug('[StimTake Bridge] duplicate tip DOM row suppressed:', {
        username: parsed.username,
        amount: parsed.amount,
        rawTipRow: parsed.raw_tip_row || parsed.dom_text || ''
      });
      reportDuplicate(parsed);
      return true;
    }

    if (tipRow) handledTipRows.add(tipRow);

    const eventTime = Date.now();
    let eventId = tipRow ? tipEventIds.get(tipRow) : '';

    if (!eventId) {
      eventId = 'dom-' + eventTime.toString(36) + '-' +
        (++eventSequence).toString(36) + '-' + simpleHash(fingerprint({
        room: roomName(),
        username: parsed.username,
        amount: parsed.amount,
        message: parsed.message,
        dom_text: parsed.dom_text
      }));

      if (tipRow) tipEventIds.set(tipRow, eventId);
    }

    const event = {
      type: 'tip',
      username: parsed.username,
      amount: parsed.amount,
      message: parsed.message,
      request: detectRequest(parsed.message),
      source: SOURCE,
      room: roomName(),
      parser: parsed.parser || '',
      event_id: eventId,
      timestamp: new Date().toISOString()
    };

    console.info('[StimTake Bridge] TIP DETECTED:', event);

    try {
      const result = await chrome.runtime.sendMessage({
        type: 'STIMTAKE_TIP',
        event,
        rawTipRow: parsed.raw_tip_row || parsed.dom_text || ''
      });

      if (!result || !result.ok) {
        if (tipRow) handledTipRows.delete(tipRow);
        console.warn('[StimTake Bridge] StimTake delivery failed:', result && result.error);
      } else {
        console.info('[StimTake Bridge] delivered to StimTake.');
      }
    } catch (error) {
      if (tipRow) handledTipRows.delete(tipRow);
      console.warn('[StimTake Bridge] extension messaging failed:', error);
    }

    return true;
  }

  function inspectElement(element) {
    if (!element || element.nodeType !== Node.ELEMENT_NODE) return false;
    if (element === observedChat) return false;

    const text = cleanText(element.innerText || element.textContent, 1000);
    if (!text) return false;

    // Best case: username and "tipped N tokens" are visible in the same
    // container's combined text.
    const full = parseFullTip(text);
    if (full) {
      full.raw_tip_row = text;
      full.tip_element = canonicalFullTipRow(element, text);
      sendTip(full);
      return true;
    }

    // Confirmed live-room structure can split username and tip text across
    // sibling/nested nodes.
    const split = parseTipOnly(element, text);
    if (split) {
      sendTip(split);
      return true;
    }

    if (candidateLooksInteresting(text)) {
      sendCandidate(element, text, 'Tip-like DOM text was seen but could not be fully parsed.');
    }

    return false;
  }

  function inspectNode(node) {
    if (!node) return false;

    const elements = [];

    if (node.nodeType === Node.TEXT_NODE) {
      if (node.parentElement) elements.push(node.parentElement);
    } else if (node.nodeType === Node.ELEMENT_NODE) {
      elements.push(node);
      elements.push(...Array.from(node.querySelectorAll('*')).slice(0, 120));
    }

    for (const element of elements) {
      if (inspectElement(element)) return true;
    }

    return false;
  }

  function findChatContainer() {
    for (const selector of CHAT_SELECTORS) {
      const element = document.querySelector(selector);
      if (element) return { element, selector };
    }
    return null;
  }

  function startObserver(found) {
    const chat = found.element;
    observedChat = chat;

    console.info('[StimTake Bridge] v2.0 loaded. Receiver-only mode.');
    console.info('[StimTake Bridge] watching Chaturbate chat DOM:', chat);

    reportStatus(
      'WATCHING',
      'Attached to chat using selector: ' + found.selector
    );

    const observer = new MutationObserver((mutations) => {
      for (const mutation of mutations) {
        if (mutation.type !== 'childList') continue;

        reportMutation();

        let recognized = false;

        for (const node of mutation.addedNodes) {
          if (inspectNode(node)) {
            recognized = true;
            break;
          }

          const parent = node.parentElement ||
            (node.nodeType === Node.TEXT_NODE ? node.parentElement : null);

          if (parent && inspectElement(parent)) {
            recognized = true;
            break;
          }
        }

        // Important fallback: React may add username and tip phrase in
        // different children during one render. Inspect the mutation target
        // and nearby ancestor containers as combined text.
        if (!recognized && mutation.target) {
          let target = mutation.target.nodeType === Node.ELEMENT_NODE
            ? mutation.target
            : mutation.target.parentElement;

          for (let depth = 0; target && depth < 4; depth++, target = target.parentElement) {
            if (inspectElement(target)) break;
          }
        }
      }
    });

    observer.observe(chat, {
      childList: true,
      subtree: true
    });

    document.documentElement.dataset.stimtakeBridge = 'watching-v2';
  }

  function waitForChat(attempt = 0) {
    const found = findChatContainer();

    if (found) {
      startObserver(found);
      return;
    }

    if (attempt === 0 || attempt % 10 === 0) {
      console.debug('[StimTake Bridge] waiting for Chaturbate chat container...');
      reportStatus(
        'WAITING',
        'Chaturbate room detected, but the chat container is not available yet.'
      );
    }

    setTimeout(() => waitForChat(attempt + 1), 1000);
  }

  reportStatus('LOADED', 'Content script loaded. Looking for Chaturbate chat.');
  waitForChat();
})();
