/* =========================================================
   invitation_registration.js — Alta con Invitación
   Patrón: WebMethod via $.ajax, modales en document.body
   ========================================================= */

// ─── Estado global ─────────────────────────────────────
let inv_allInvitations = [];
let inv_currentFilter = 'all';

// ─── INIT ───────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
    loadRoles();
    loadOms();
    loadInvitations();
});

// ─── AJAX HELPER ────────────────────────────────────────
function invCall(method, data, onSuccess) {
    $.ajax({
        type: 'POST',
        url: 'InvitationRegistration.aspx/' + method,
        data: JSON.stringify(data),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: (resp) => {
            const d = resp.d !== undefined ? resp.d : resp;
            onSuccess(d);
        },
        error: (xhr) => {
            console.error('[invitation_registration.js]', method, xhr.responseText);
            showInvToast(t('inv.toast.error'), 'error');
        }
    });
}

// ─── LOAD ROLES ─────────────────────────────────────────
function loadRoles() {
    invCall('GetAvailableRoles', {}, (resp) => {
        const ddl = document.getElementById('invRole');
        ddl.innerHTML = `<option value="">${t('inv.role.placeholder')}</option>`;
        if (!resp.success || !resp.data.length) return;

        resp.data.forEach(r => {
            const opt = document.createElement('option');
            opt.value = r.roleId;
            opt.textContent = r.roleName;
            ddl.appendChild(opt);
        });
    });
}

// ─── LOAD OMS ───────────────────────────────────────────
function loadOms() {
    const container = document.getElementById('invOmsChecklist');
    container.innerHTML = `<div class="inv-loading">
        <span class="material-icons inv-spin">autorenew</span> ${t('inv.oms.loading')}
    </div>`;

    invCall('GetAvailableOms', {}, (resp) => {
        if (!resp.success || !resp.data.length) {
            container.innerHTML = `<div class="inv-loading">
                <span class="material-icons" style="color:var(--error-color)">error</span>
                Sin OMS disponibles
            </div>`;
            return;
        }

        container.innerHTML = '';

        // Agrupar por WmsId
        const groups = {};
        resp.data.forEach(o => {
            if (!groups[o.wmsId])
                groups[o.wmsId] = { code: o.wmsCode, items: [] };
            groups[o.wmsId].items.push(o);
        });

        Object.values(groups).forEach(group => {
            const header = document.createElement('div');
            header.className = 'inv-oms-group-header';
            header.textContent = group.code;
            container.appendChild(header);

            group.items.forEach(o => {
                const div = document.createElement('div');
                div.className = 'inv-oms-item';
                div.dataset.omsId = o.omsId;
                div.innerHTML = `
                    <input type="checkbox" class="inv-oms-check" value="${o.omsId}" tabindex="-1" aria-hidden="true" />
                    <span class="inv-oms-check-box"></span>
                    <span class="inv-oms-name">${escapeHtml(o.omsName)}</span>`;
                div.addEventListener('click', () => {
                    const cb = div.querySelector('.inv-oms-check');
                    cb.checked = !cb.checked;
                    div.classList.toggle('selected', cb.checked);
                });
                container.appendChild(div);
            });
        });
    });
}

// ─── SEND INVITATION ────────────────────────────────────
function sendInvitation() {
    clearFormErrors();

    const email = document.getElementById('invEmail').value.trim();
    const roleId = parseInt(document.getElementById('invRole').value);
    const omsIds = Array.from(document.querySelectorAll('.inv-oms-check:checked'))
        .map(cb => parseInt(cb.value));

    let valid = true;

    if (!email || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
        showFieldErr('invEmailError', t('inv.err.email_invalid'));
        valid = false;
    }
    if (!roleId) {
        showFieldErr('invRoleError', t('inv.err.role_required'));
        valid = false;
    }
    if (!omsIds.length) {
        showFieldErr('invOmsError', t('inv.err.oms_required'));
        valid = false;
    }
    if (!valid) return;

    const btn = document.getElementById('btnSendInvitation');
    btn.disabled = true;
    btn.innerHTML = `<span class="material-icons inv-spin">autorenew</span> ${t('inv.btn.sending')}`;

    invCall('SendInvitation', { email, roleId, omsIds }, (resp) => {
        btn.disabled = false;
        btn.innerHTML = `<span class="material-icons">send</span> ${t('inv.btn.send')}`;

        if (resp.success) {
            resetForm();
            showInvToast(`${t('inv.toast.sent')} ${email}`, 'success');
            loadInvitations();
        } else {
            showFormMessage(resp.message || t('inv.toast.error'), 'error');
        }
    });
}

// ─── LOAD INVITATIONS ───────────────────────────────────
function loadInvitations() {
    const list = document.getElementById('invitationsList');
    list.innerHTML = `<div class="inv-loading">
        <span class="material-icons inv-spin">autorenew</span> ${t('inv.list.loading')}
    </div>`;

    invCall('GetInvitations', {}, (resp) => {
        if (!resp.success) {
            list.innerHTML = `<div class="inv-loading">
                <span class="material-icons" style="color:var(--error-color)">error</span>
                ${escapeHtml(resp.message || t('inv.toast.error'))}
            </div>`;
            return;
        }

        inv_allInvitations = resp.data || [];
        document.getElementById('invCount').textContent = inv_allInvitations.length;
        filterInvitations(inv_currentFilter);
    });
}

// ─── FILTER INVITATIONS ─────────────────────────────────
function filterInvitations(filter) {
    inv_currentFilter = filter;

    document.querySelectorAll('.inv-tab').forEach(tab => {
        tab.classList.toggle('active', tab.dataset.filter === filter);
    });

    let items = inv_allInvitations;
    if (filter === 'pending') items = items.filter(i => i.isActive && !i.acceptedAt);
    if (filter === 'accepted') items = items.filter(i => !!i.acceptedAt);
    if (filter === 'cancelled') items = items.filter(i => !i.isActive && !i.acceptedAt);

    renderInvitations(items);
}

// ─── RENDER INVITATIONS ─────────────────────────────────
function renderInvitations(items) {
    const list = document.getElementById('invitationsList');

    if (!items.length) {
        list.innerHTML = `<div class="inv-empty">
            <span class="material-icons">mail_outline</span>
            <p>${t('inv.list.empty')}</p>
        </div>`;
        return;
    }

    list.innerHTML = items.map(inv => {
        const statusClass = inv.acceptedAt ? 'accepted'
            : inv.isActive ? 'pending'
                : 'cancelled';

        const statusLabel = inv.acceptedAt ? t('inv.status.accepted')
            : inv.isActive ? t('inv.status.pending')
                : t('inv.status.cancelled');

        // OMS: solo nombre, sin código
        const omsHtml = inv.omsNames
            ? inv.omsNames.split(',').map(n =>
                `<span class="inv-oms-pill">${escapeHtml(n.trim())}</span>`).join('')
            : inv.omsCodes
                ? inv.omsCodes.split(',').map(c =>
                    `<span class="inv-oms-pill">${escapeHtml(c.trim())}</span>`).join('')
                : '<span style="color:var(--text-secondary);font-size:12px;">—</span>';

        const cancelBtn = (inv.isActive && !inv.acceptedAt)
            ? `<button type="button" class="inv-btn-cancel-item"
                       onclick="confirmCancel(${inv.invitationId}, '${escapeHtml(inv.email)}')"
                       title="${t('inv.btn.cancel')}">
                   <span class="material-icons">cancel</span>
               </button>`
            : '';

        const acceptedInfo = inv.acceptedAt
            ? `<div class="inv-card-accepted">
                   <span class="material-icons">check_circle</span>
                   ${t('inv.status.accepted')} — ${escapeHtml(inv.acceptedAt)}
               </div>`
            : '';

        return `
        <div class="inv-card inv-card-${statusClass}">
            <div class="inv-card-top">
                <div class="inv-card-email">
                    <span class="material-icons">alternate_email</span>
                    ${escapeHtml(inv.email)}
                </div>
                <div class="inv-card-actions">
                    <span class="inv-status-pill ${statusClass}">${statusLabel}</span>
                    ${cancelBtn}
                </div>
            </div>
            <div class="inv-card-meta">
                <span class="inv-meta-item">
                    <span class="material-icons">admin_panel_settings</span>
                    ${escapeHtml(inv.roleName)}
                </span>
                <span class="inv-meta-item">
                    <span class="material-icons">person</span>
                    ${escapeHtml(inv.invitedByName)}
                </span>
                <span class="inv-meta-item">
                    <span class="material-icons">schedule</span>
                    ${escapeHtml(inv.createdAt)}
                </span>
            </div>
            <div class="inv-card-oms">${omsHtml}</div>
            ${acceptedInfo}
        </div>`;
    }).join('');
}

// ─── CONFIRM CANCEL ─────────────────────────────────────
function confirmCancel(invitationId, email) {
    closeInvModal();

    document.body.insertAdjacentHTML('beforeend', `
    <div class="inv-overlay" id="invOverlay" onclick="onInvOverlayClick(event)">
        <div class="inv-modal" id="invModal">
            <div class="inv-modal-header">
                <span class="material-icons" style="color:var(--error-color)">warning</span>
                <h3>${t('inv.btn.cancel')}</h3>
                <button type="button" class="inv-modal-close" onclick="closeInvModal()">
                    <span class="material-icons">close</span>
                </button>
            </div>
            <div class="inv-modal-body">
                <div class="inv-confirm-icon material-icons">mail_off</div>
                <p>¿Estás seguro que deseas cancelar la invitación de</p>
                <p><strong>${escapeHtml(email)}</strong>?</p>
                <p style="font-size:12px;color:var(--text-secondary);margin-top:8px;">
                    El link de invitación quedará inválido de inmediato.
                </p>
            </div>
            <div class="inv-modal-footer">
                <button type="button" class="inv-btn-modal-cancel" onclick="closeInvModal()">
                    ${t('common.cancel')}
                </button>
                <button type="button" class="inv-btn-modal-danger" id="invBtnDanger"
                        onclick="submitCancel(${invitationId})">
                    <span class="material-icons">cancel</span>
                    ${t('inv.btn.cancel')}
                </button>
            </div>
        </div>
    </div>`);
}

function onInvOverlayClick(e) {
    if (e.target.id === 'invOverlay') closeInvModal();
}

function closeInvModal() {
    const el = document.getElementById('invOverlay');
    if (el) el.remove();
}

function submitCancel(invitationId) {
    const btn = document.getElementById('invBtnDanger');
    btn.disabled = true;
    btn.innerHTML = `<span class="material-icons inv-spin">autorenew</span> ${t('common.loading')}`;

    invCall('CancelInvitation', { invitationId }, (resp) => {
        closeInvModal();
        if (resp.success) {
            showInvToast(t('inv.toast.cancelled'), 'success');
            loadInvitations();
        } else {
            showInvToast(resp.message || t('inv.toast.error'), 'error');
        }
    });
}

// ─── FORM HELPERS ───────────────────────────────────────
function resetForm() {
    document.getElementById('invEmail').value = '';
    document.getElementById('invRole').value = '';
    document.querySelectorAll('.inv-oms-check').forEach(cb => cb.checked = false);
    document.querySelectorAll('.inv-oms-item').forEach(d => d.classList.remove('selected'));
    clearFormErrors();
    hideFormMessage();
}

function clearFormErrors() {
    ['invEmailError', 'invRoleError', 'invOmsError'].forEach(id => {
        const el = document.getElementById(id);
        if (el) { el.style.display = 'none'; el.textContent = ''; }
    });
}

function showFieldErr(id, msg) {
    const el = document.getElementById(id);
    if (el) { el.style.display = 'block'; el.textContent = msg; }
}

function showFormMessage(msg, type) {
    const el = document.getElementById('invFormMessage');
    el.style.display = 'block';
    el.className = `inv-form-message ${type}`;
    el.textContent = msg;
}

function hideFormMessage() {
    const el = document.getElementById('invFormMessage');
    if (el) el.style.display = 'none';
}

// ─── TOAST ──────────────────────────────────────────────
function showInvToast(message, type = 'success') {
    const existing = document.getElementById('invToast');
    if (existing) existing.remove();

    const icon = type === 'success' ? 'check_circle' : 'error';
    document.body.insertAdjacentHTML('beforeend', `
    <div id="invToast" class="inv-toast ${type}">
        <span class="material-icons">${icon}</span>
        ${escapeHtml(message)}
    </div>`);

    setTimeout(() => {
        const el = document.getElementById('invToast');
        if (el) el.remove();
    }, 4000);
}

// ─── UTILS ──────────────────────────────────────────────
function escapeHtml(str) {
    if (!str) return '';
    return String(str)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;')
        .replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}

function t(key) {
    const lang = document.documentElement.lang || 'es';
    return (translations[lang] && translations[lang][key]) || key;
}