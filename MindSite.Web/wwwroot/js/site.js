// MindSite — site.js
// Dark Mode · Acessibilidade · Notificações · Helpers
/* Tema */
const MS = {
  init() {
    this.applyTheme(localStorage.getItem('ms-theme') || 'light');
    this.applyAccess(localStorage.getItem('ms-access') || 'normal');
    this.bindThemeBtn();
    this.bindAccessPanel();
    this.loadNotificacoes();
    this.bindNotifBell();
  },

  applyTheme(theme) {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem('ms-theme', theme);
    const btn = document.getElementById('btnTheme');
    if (btn) btn.innerHTML = theme === 'dark' ? iconSun() : iconMoon();
  },

  toggleTheme() {
    const cur = document.documentElement.getAttribute('data-theme') || 'light';
    this.applyTheme(cur === 'dark' ? 'light' : 'dark');
  },

  applyAccess(mode) {
    document.documentElement.setAttribute('data-access', mode);
    localStorage.setItem('ms-access', mode);
    // Marca botão ativo no painel
    document.querySelectorAll('.access-btn').forEach(b => {
      b.classList.toggle('ring-2', b.dataset.mode === mode);
      b.classList.toggle('ring-offset-1', b.dataset.mode === mode);
    });
  },

  bindThemeBtn() {
    const btn = document.getElementById('btnTheme');
    if (btn) btn.addEventListener('click', () => this.toggleTheme());
  },

  bindAccessPanel() {
    document.querySelectorAll('.access-btn').forEach(btn => {
      btn.addEventListener('click', () => this.applyAccess(btn.dataset.mode));
    });
    const openBtn  = document.getElementById('btnAcessibilidade');
    const closeBtn = document.getElementById('btnFecharAcesso');
    const panel    = document.getElementById('painelAcessibilidade');
    if (openBtn && panel) {
      openBtn.addEventListener('click', () => panel.classList.toggle('hidden'));
      closeBtn?.addEventListener('click', () => panel.classList.add('hidden'));
    }
  },

  // Notificações
  async loadNotificacoes() {
    const bell = document.getElementById('notifBadge');
    try {
      const r = await fetch('/Account/Notificacoes', { credentials: 'include' });
      if (!r.ok) return;
      const list = await r.json();
      if (bell) { bell.textContent = list.length; bell.classList.toggle('hidden', list.length === 0); }
      const box = document.getElementById('notifList');
      if (!box) return;
      box.innerHTML = list.length
        ? list.map(n => `
            <div data-notif-id="${n.id}"
                 class="px-4 py-3 border-b border-[color:var(--border)] hover:bg-[color:var(--bg2)] transition-colors cursor-pointer"
                 onclick="MS.lerNotif(${n.id}, '${n.url || ''}')">
              <p class="text-sm font-medium text-[color:var(--text)]">${n.titulo}${n.contador > 1 ? ` (${n.contador})` : ''}</p>
            </div>`).join('')
        : '<p class="text-center text-sm text-[color:var(--text-muted)] py-6">Sem notificações</p>';
    } catch { /* silencioso */ }
  },

  async lerNotif(id, url) {
    // Remove do DOM imediatamente
    const el = document.querySelector(`[data-notif-id="${id}"]`);
    if (el) el.remove();
    const remaining = document.querySelectorAll('[data-notif-id]').length;
    const badge = document.getElementById('notifBadge');
    if (badge) { badge.textContent = remaining; badge.classList.toggle('hidden', remaining === 0); }
    const box = document.getElementById('notifList');
    if (box && remaining === 0) box.innerHTML = '<p class="text-center text-sm text-[color:var(--text-muted)] py-6">Sem notificações</p>';
    // Deleta no servidor (fire and forget)
    fetch(`/Account/DismissNotificacao?id=${id}`, { method: 'POST' }).catch(() => {});
    // Navega se tiver URL
    if (url) window.location.href = url;
  },

  handleNotificacao(n) {
    const badge = document.getElementById('notifBadge');
    const box   = document.getElementById('notifList');
    if (!box) return;
    const existing = box.querySelector(`[data-notif-id="${n.id}"]`);
    if (existing) {
      const p = existing.querySelector('p');
      if (p) p.textContent = n.titulo + (n.contador > 1 ? ` (${n.contador})` : '');
    } else {
      const placeholder = box.querySelector('p.text-center');
      if (placeholder) placeholder.remove();
      const div = document.createElement('div');
      div.setAttribute('data-notif-id', n.id);
      div.className = 'px-4 py-3 border-b border-[color:var(--border)] hover:bg-[color:var(--bg2)] transition-colors cursor-pointer';
      div.onclick = () => MS.lerNotif(n.id, n.url || '');
      div.innerHTML = `<p class="text-sm font-medium text-[color:var(--text)]">${n.titulo}${n.contador > 1 ? ` (${n.contador})` : ''}</p>`;
      box.prepend(div);
      const count = box.querySelectorAll('[data-notif-id]').length;
      if (badge) { badge.textContent = count; badge.classList.remove('hidden'); }
    }
  },

  bindNotifBell() {
    const btn  = document.getElementById('btnNotif');
    const drop = document.getElementById('notifDropdown');
    if (!btn || !drop) return;
    btn.addEventListener('click', e => {
      e.stopPropagation();
      drop.classList.toggle('hidden');
    });
    document.addEventListener('click', () => drop.classList.add('hidden'));
  }
};

/* Modals genéricos */
function openModal(id)  { document.getElementById(id)?.classList.remove('hidden'); document.body.style.overflow='hidden'; }
function closeModal(id) { document.getElementById(id)?.classList.add('hidden');    document.body.style.overflow=''; }

// Fechar modal clicando fora
document.addEventListener('click', e => {
  if (e.target.classList.contains('modal-overlay'))
    e.target.classList.add('hidden'), document.body.style.overflow='';
});
document.addEventListener('keydown', e => {
  if (e.key === 'Escape')
    document.querySelectorAll('.modal-overlay:not(.hidden)').forEach(m => {
      m.classList.add('hidden'); document.body.style.overflow='';
    });
});

/* Filtro de tabela por texto */
function filtrarTabela(inputId, tbodyId) {
  const q   = document.getElementById(inputId)?.value.toLowerCase() ?? '';
  const rows = document.querySelectorAll(`#${tbodyId} tr`);
  rows.forEach(r => r.style.display = r.textContent.toLowerCase().includes(q) ? '' : 'none');
}

/* CSRF token helper */
function getToken() {
  return document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
}

/* Ícones SVG inline */
function iconMoon() {
  return `<svg xmlns="http:// www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
      d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z"/>
  </svg>`;
}
function iconSun() {
  return `<svg xmlns="http:// www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
      d="M12 3v1m0 16v1m9-9h-1M4 12H3m15.364-6.364l-.707.707M6.343 17.657l-.707.707M17.657 17.657l-.707-.707M6.343 6.343l-.707-.707M12 8a4 4 0 100 8 4 4 0 000-8z"/>
  </svg>`;
}

/* Filtro de contatos no Chat */
function filtrarContatos(q) {
  document.querySelectorAll('.contact-item').forEach(el => {
    const nome = el.querySelector('.contact-name')?.textContent.toLowerCase() ?? '';
    el.style.display = nome.includes(q.toLowerCase()) ? '' : 'none';
  });
}

/* Máscaras de input */
function mascaraCPF(v) {
  v = v.replace(/\D/g, '').slice(0, 11);
  if (v.length > 9) return `${v.slice(0,3)}.${v.slice(3,6)}.${v.slice(6,9)}-${v.slice(9)}`;
  if (v.length > 6) return `${v.slice(0,3)}.${v.slice(3,6)}.${v.slice(6)}`;
  if (v.length > 3) return `${v.slice(0,3)}.${v.slice(3)}`;
  return v;
}

function mascaraTel(v) {
  v = v.replace(/\D/g, '').slice(0, 11);
  if (v.length === 11) return `(${v.slice(0,2)}) ${v.slice(2,7)}-${v.slice(7)}`;
  if (v.length >= 6)  return `(${v.slice(0,2)}) ${v.slice(2,6)}-${v.slice(6)}`;
  if (v.length > 2)   return `(${v.slice(0,2)}) ${v.slice(2)}`;
  return v;
}

function validarDigitosCPF(cpf) {
  const d = cpf.replace(/\D/g, '');
  if (d.length !== 11 || /^(\d)\1{10}$/.test(d)) return false;
  let s = 0;
  for (let i = 0; i < 9; i++) s += +d[i] * (10 - i);
  let r = (s * 10) % 11; if (r >= 10) r = 0;
  if (r !== +d[9]) return false;
  s = 0;
  for (let i = 0; i < 10; i++) s += +d[i] * (11 - i);
  r = (s * 10) % 11; if (r >= 10) r = 0;
  return r === +d[10];
}

// Auto-aplicar máscaras em campos com data-mask
document.addEventListener('input', e => {
  const el = e.target;
  if (el.dataset.mask === 'cpf') el.value = mascaraCPF(el.value);
  if (el.dataset.mask === 'tel') el.value = mascaraTel(el.value);
}, true);

/* Validação de formulários */
const MsVal = {
  // Resolve onde injetar a mensagem de erro: se o pai imediato for um wrapper
  // de eye-toggle (div.relative com botão), usa o avô para não crescer o
  // container e desalinhar o ícone.
  _errContainer(el) {
    const p = el.parentElement;
    return (p && p.classList.contains('relative') && p.querySelector('button[onclick*="toggle"]'))
      ? p.parentElement : p;
  },
  // Mostra erro abaixo do campo
  erro(el, msg) {
    el.classList.add('input-error');
    el.classList.remove('input-ok');
    const c = this._errContainer(el);
    let span = c.querySelector(':scope > .field-error');
    if (!span) { span = document.createElement('span'); span.className = 'field-error'; c.appendChild(span); }
    span.textContent = msg;
  },
  // Remove erro do campo
  ok(el) {
    el.classList.remove('input-error');
    el.classList.add('input-ok');
    const span = this._errContainer(el).querySelector(':scope > .field-error');
    if (span) span.remove();
  },
  // Limpa todos os erros de um form
  limpar(form) {
    form.querySelectorAll('.input-error').forEach(e => e.classList.remove('input-error', 'input-ok'));
    form.querySelectorAll('.field-error').forEach(e => e.remove());
  },
  // Valida campos required/minlength/email/type + data-mask
  campo(el) {
    const v = el.value.trim();
    if (el.required && !v && el.type !== 'checkbox') { this.erro(el, 'Campo obrigatório'); return false; }
    if (el.required && el.type === 'checkbox' && !el.checked) { this.erro(el, 'Obrigatório'); return false; }
    if (el.minLength > 0 && v.length > 0 && v.length < el.minLength) { this.erro(el, `Mínimo ${el.minLength} caracteres`); return false; }
    if (el.type === 'email' && v && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v)) { this.erro(el, 'E-mail inválido'); return false; }
    if (el.min && el.type === 'number' && parseFloat(v) < parseFloat(el.min)) { this.erro(el, `Valor mínimo: ${el.min}`); return false; }
    if (el.max && el.type === 'number' && parseFloat(v) > parseFloat(el.max)) { this.erro(el, `Valor máximo: ${el.max}`); return false; }
    // Validação de CPF (formato + dígitos verificadores)
    if (el.dataset.mask === 'cpf' && v) {
      if (!/^\d{3}\.\d{3}\.\d{3}-\d{2}$/.test(v)) { this.erro(el, 'CPF inválido — formato: 000.000.000-00'); return false; }
      if (!validarDigitosCPF(v)) { this.erro(el, 'CPF inválido'); return false; }
    }
    // Validação de telefone
    if (el.dataset.mask === 'tel' && v) {
      if (!/^\(\d{2}\) \d{4,5}-\d{4}$/.test(v)) { this.erro(el, 'Telefone inválido — formato: (11) 99999-9999'); return false; }
    }
    if (v) this.ok(el); else el.classList.remove('input-error','input-ok');
    return true;
  },
  // Valida todo o formulário; retorna true se tudo ok
  form(form) {
    let valido = true;
    form.querySelectorAll('input,textarea,select').forEach(el => {
      if (!el.disabled && !this.campo(el)) valido = false;
    });
    return valido;
  },
  // Validação de senha == confirmação
  senhas(senhaId, confirmId) {
    const s = document.getElementById(senhaId);
    const c = document.getElementById(confirmId);
    if (!s || !c) return true;
    if (c.value && s.value !== c.value) { this.erro(c, 'Senhas não conferem'); return false; }
    if (c.value) this.ok(c);
    return true;
  }
};

// Feedback em tempo real para qualquer .ms-input ou .ms-textarea
document.addEventListener('blur', e => {
  if (!e.target.matches('.ms-input, .ms-textarea')) return;
  // Não exibir "Campo obrigatório" ao sair de campo de senha vazio
  if (!e.target.value && e.target.parentElement?.querySelector('button[onclick*="toggle"]')) return;
  MsVal.campo(e.target);
}, true);

/* Toast */
(function () {
  var s = document.createElement('style');
  s.textContent =
    '@keyframes msToastIn{from{transform:translateX(calc(100% + 48px));opacity:0}to{transform:translateX(0);opacity:1}}' +
    '@keyframes msToastOut{from{transform:translateX(0);opacity:1}to{transform:translateX(calc(100% + 48px));opacity:0}}';
  document.head.appendChild(s);
  window._msToasts = [];
})();

function showToast(msg, type) {
  var _cfg = {
    success: {
      iconColor: '#27ae60',
      icon: '<svg width="26" height="26" viewBox="0 0 24 24"><circle cx="12" cy="12" r="12" fill="#27ae60"/><polyline points="7 12.5 10.5 16 17 9" fill="none" stroke="#fff" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"/></svg>'
    },
    erro: {
      iconColor: '#e74c3c',
      icon: '<svg width="26" height="26" viewBox="0 0 26 26"><circle cx="13" cy="13" r="13" fill="#e74c3c"/><line x1="8.5" y1="8.5" x2="17.5" y2="17.5" stroke="#fff" stroke-width="2.4" stroke-linecap="round"/><line x1="17.5" y1="8.5" x2="8.5" y2="17.5" stroke="#fff" stroke-width="2.4" stroke-linecap="round"/></svg>'
    },
    info: {
      iconColor: '#e67e22',
      icon: '<svg width="26" height="26" viewBox="0 0 26 26"><polygon points="13,2 25,24 1,24" fill="#e67e22" rx="2"/><line x1="13" y1="10" x2="13" y2="17" stroke="#fff" stroke-width="2.4" stroke-linecap="round"/><circle cx="13" cy="21" r="1.4" fill="#fff"/></svg>'
    }
  };
  var c = _cfg[type] || _cfg.success;

  /* wrapper externo (fundo bege + barra marrom embaixo) */
  var el = document.createElement('div');
  el.style.cssText =
    'position:fixed;right:24px;z-index:9999;pointer-events:auto;' +
    'border-radius:10px;overflow:hidden;' +
    'background:var(--bg2);' +
    'box-shadow:0 4px 18px rgba(0,0,0,.18);' +
    'min-width:280px;max-width:380px;' +
    'animation:msToastIn .36s cubic-bezier(.22,.68,0,1.2) forwards;' +
    'transition:top .22s ease;';

  /* linha principal (ícone + texto + fechar) */
  var row = document.createElement('div');
  row.style.cssText =
    'display:flex;align-items:center;gap:12px;' +
    'padding:13px 14px 13px 14px;';

  var iconEl = document.createElement('span');
  iconEl.innerHTML = c.icon;
  iconEl.style.cssText = 'flex-shrink:0;display:flex;align-items:center;';

  var textEl = document.createElement('span');
  textEl.style.cssText =
    'flex:1;color:var(--text);font-size:13.5px;font-weight:700;' +
    'line-height:1.45;word-break:break-word;';
  textEl.textContent = String(msg);

  var closeBtn = document.createElement('button');
  closeBtn.textContent = 'x';
  closeBtn.style.cssText =
    'background:none;border:none;color:var(--text-muted);font-size:13px;font-weight:700;' +
    'cursor:pointer;opacity:.55;padding:0 2px;flex-shrink:0;' +
    'align-self:flex-start;transition:opacity .15s;line-height:1;';
  closeBtn.onmouseenter = function () { closeBtn.style.opacity = '1'; };
  closeBtn.onmouseleave = function () { closeBtn.style.opacity = '.55'; };

  /* barra marrom escura na base (progresso) */
  var bar = document.createElement('div');
  bar.style.cssText =
    'height:12px;width:100%;background:var(--brown-dark);' +
    'transform-origin:left;transition:transform 2s linear;transform:scaleX(1);';

  function _restack() {
    var top = 80;
    window._msToasts.forEach(function (item) {
      item.style.top = top + 'px';
      top += (item.offsetHeight || 70) + 10;
    });
  }

  function dismiss() {
    el.style.animation = 'msToastOut .32s ease-in forwards';
    el.addEventListener('animationend', function () {
      var i = window._msToasts.indexOf(el);
      if (i !== -1) window._msToasts.splice(i, 1);
      el.remove();
      _restack();
    }, { once: true });
  }

  closeBtn.addEventListener('click', dismiss);
  row.appendChild(iconEl);
  row.appendChild(textEl);
  row.appendChild(closeBtn);
  el.appendChild(row);
  el.appendChild(bar);
  document.body.appendChild(el);
  window._msToasts.push(el);
  _restack();

  requestAnimationFrame(function () {
    requestAnimationFrame(function () { bar.style.transform = 'scaleX(0)'; });
  });

  setTimeout(dismiss, 2000);
}

// Substitui alert() nativo pelo toast em todo o site
window.alert = function (msg) { showToast(String(msg)); };

/* Init */
document.addEventListener('DOMContentLoaded', () => MS.init());
