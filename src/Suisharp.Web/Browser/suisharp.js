const root = document.getElementById('suisharp-root');
const status = document.getElementById('connection');
const elements = new Map();
const composing = new WeakSet();
const lastInput = new WeakMap();

function showStatus(message) {
  status.textContent = message;
  status.hidden = false;
}

function applyAppearance(element, node) {
  const classes = (node.cssClass ?? '').split(/\s+/).filter(name => name && !name.startsWith('suisharp-'));
  element.className = [`suisharp-${node.kind}`, ...classes].join(' ');
  // 全宣言を置換して削除も反映する。CSS文字列をHTMLとして解釈しない。
  element.style.cssText = '';
  for (const [name, value] of Object.entries(node.style ?? {}))
    element.style.setProperty(name, value);
  // Layoutは子Componentを持つ要素に適用し、Styleとは別の宣言として反映する。
  if (node.kind === 'component' || node.children.length > 0) {
    for (const [name, value] of Object.entries(node.layout ?? {}))
      element.style.setProperty(name, value);
  }
  element.hidden = !node.isVisible;
  if (element.hidden) {
    element.style.setProperty('display', 'none', 'important');
    // hiddenを設定してからblurすることで、blur由来の入力も送らない。
    if (element.contains(document.activeElement)) document.activeElement.blur();
    composing.delete(element);
    for (const input of element.querySelectorAll('input')) composing.delete(input);
  }
}

function forget(element) {
  for (const child of element.children) {
    if (child.dataset.suisharpId) forget(child);
  }
  elements.delete(element.dataset.suisharpId);
}

// 明示的Updateで届いた範囲だけ反映する。状態の監視・評価はしない。
function render(node) {
  let element = elements.get(node.id);
  if (!element) {
    element = document.createElement(node.kind === 'textbox' ? 'input' : node.kind === 'button' ? 'button' : node.kind === 'text' ? 'span' : 'div');
    element.dataset.suisharpId = node.id;
    element.className = `suisharp-${node.kind}`;
    if (node.kind === 'button') element.type = 'button';
    if (node.kind === 'textbox') {
      element.type = 'text';
      element.setAttribute('aria-label', 'テキスト入力');
    } else if (node.kind !== 'component') element.append(document.createTextNode(''));
    elements.set(node.id, element);
  }
  applyAppearance(element, node);
  if (node.kind === 'textbox') {
    // 同値の再代入でキャレットを動かさず、IME変換中の入力を壊さない。
    if (!composing.has(element)) {
      if (element.value !== (node.value ?? '')) element.value = node.value ?? '';
      lastInput.set(element, element.value);
    }
    return element;
  }
  if (node.kind !== 'component') element.firstChild.textContent = node.value ?? '';
  const wanted = new Set(node.children.map(child => child.id));
  for (const child of [...element.children]) {
    if (!wanted.has(child.dataset.suisharpId)) {
      forget(child);
      child.remove();
    }
  }
  let index = 0;
  for (const child of node.children) {
    const childElement = render(child);
    if (element.children[index] !== childElement)
      element.insertBefore(childElement, element.children[index] ?? null);
    index++;
  }
  return element;
}

const url = new URL(location.href);
url.hash = '';
url.protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
const socket = new WebSocket(url);
socket.addEventListener('message', event => {
  const message = JSON.parse(event.data);
  if (message.operation === 'mount') {
    elements.clear();
    root.replaceChildren(render(message.node));
  } else if (message.operation === 'update') {
    if (elements.has(message.node.id)) render(message.node);
  } else if (message.operation === 'error') {
    showStatus(message.message);
  }
});
socket.addEventListener('close', () => {
  showStatus('接続が終了しました。再読み込みで新しい画面を開きます。');
  for (const control of root.querySelectorAll('button,input')) control.disabled = true;
});
function sendInput(element) {
  if (!element.matches('input[data-suisharp-id]') || element.closest('[hidden]') || socket.readyState !== WebSocket.OPEN) return;
  if (lastInput.get(element) === element.value) return;
  lastInput.set(element, element.value);
  socket.send(JSON.stringify({ event: 'input', id: element.dataset.suisharpId, value: element.value }));
}
root.addEventListener('compositionstart', event => composing.add(event.target));
root.addEventListener('compositionend', event => {
  composing.delete(event.target);
  sendInput(event.target);
});
root.addEventListener('input', event => {
  if (!event.isComposing && !composing.has(event.target)) sendInput(event.target);
});
socket.addEventListener('error', () => { showStatus('接続できませんでした'); });
root.addEventListener('click', event => {
  const button = event.target.closest('button[data-suisharp-id]');
  if (button && !button.closest('[hidden]') && root.contains(button) && socket.readyState === WebSocket.OPEN)
    socket.send(JSON.stringify({ event: 'click', id: button.dataset.suisharpId }));
});
