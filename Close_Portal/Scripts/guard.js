/* =========================================================
   guardia.js — Módulo de Gestión de Guardia
   Patrón: WebMethod via $.ajax, modales en document.body
   Theme:  usa clases CSS con variables de Variables.css
   i18n:   helper t() para traducir HTML dinámico
   ========================================================= */

// ─── Estado global ─────────────────────────────────────
let gd_owners = [];   // cache de owners cargados
let gd_schedule = [];   // cache de turnos cargados
let gd_pendingOwner = null; // owner seleccionado para asignar turno

// ─── INIT ───────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
    loadOwners();
    loadSchedule();
});

// ─── TRADUCCIÓN ─────────────────────────────────────────
/**
 * t(key) — Devuelve la traducción de la clave según el idioma activo.
 * Si no existe la clave, retorna el fallback o la clave misma.
 */
function t(key, fallback) {
    const lang = document.documentElement.getAttribute('data-language') || 'es';
    if (typeof translations !== 'undefined' && translations[lang] && translations[lang][key]) {
        return translations[lang][key];
    }
    return fallback !== undefined ? fallback : key;
}

// ─── HELPERS AJAX ───────────────────────────────────────
function gdCall(method, data, onSuccess) {
    $.ajax({
        type: 'POST',
        url: 'Guard.aspx/' + method,
        data: JSON.stringify(data),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: (resp) => {
            const d = resp.d !== undefined ? resp.d : resp;
            onSuccess(d);
        },
        error: (xhr) => {
            console.error('[guard.js]', method, xhr.responseText);
            showToast(t('gd.toast.error', 'Error de comunicación con el servidor.'), 'error');
        }
    });
}

// ─── LOAD OWNERS ───────────────────────────────────────
function loadOwners() {
    const list = document.getElementById('ownersList');
    list.innerHTML = `
        <div class="gd-loading">
            <span class="material-icons gd-spin">autorenew</span>
            <span>${t('gd.owners.loading', 'Cargando owners...')}</span>
        </div>`;

    gdCall('GetOwners', {}, (resp) => {
        if (!resp.success) {
            list.innerHTML = `
                <div class="gd-loading">
                    <span class="material-icons" style="color:var(--error-color)">error</span>
                    ${escapeHtml(resp.message || t('gd.owners.loading_error', 'Error al cargar owners'))}
                </div>`;
            return;
        }

        gd_owners = resp.data || [];
        document.getElementById('ownersCount').textContent = gd_owners.length;
        renderOwners(gd_owners);
    });
}

function renderOwners(owners) {
    const list = document.getElementById('ownersList');

    if (owners.length === 0) {
        list.innerHTML = `
            <div class="gd-loading">
                <span class="material-icons">person_off</span>
                ${t('gd.owners.empty', 'Sin owners disponibles')}
            </div>`;
        return;
    }

    list.innerHTML = owners.map(o => `
        <div class="gd-owner-card ${o.hasActiveGuard ? 'gd-on-guard' : ''}" data-owner-id="${o.userId}">
            <div class="gd-avatar">${escapeHtml(o.initials)}</div>
            <div class="gd-owner-info">
                <div class="gd-owner-name">${escapeHtml(o.username)}</div>
                <div class="gd-owner-email">${escapeHtml(o.email)}</div>
            </div>
            ${o.hasActiveGuard
            ? `<span class="gd-guard-pill active">
                       <span class="material-icons" style="font-size:12px;vertical-align:middle">shield</span>
                       ${t('gd.owners.on_guard', 'En guardia')}
                   </span>`
            : `<button type="button" class="gd-btn-assign" onclick="openAssignModal(${o.userId})">
                       <span class="material-icons">add</span>
                       ${t('gd.owners.assign', 'Asignar')}
                   </button>`
        }
        </div>
    `).join('');
}

// ─── FILTER OWNERS ─────────────────────────────────────
function filterOwners() {
    const q = document.getElementById('ownerSearch').value.toLowerCase().trim();
    const filtered = q
        ? gd_owners.filter(o =>
            o.username.toLowerCase().includes(q) ||
            o.email.toLowerCase().includes(q))
        : gd_owners;
    renderOwners(filtered);
}

// ─── LOAD SCHEDULE ─────────────────────────────────────
function loadSchedule() {
    gdCall('GetSchedule', {}, (resp) => {
        if (!resp.success) return;
        gd_schedule = resp.data || [];
        renderSchedule(gd_schedule);
        updateHeaderBadge(gd_schedule);
    });
}

function renderSchedule(items) {
    const tbody = document.getElementById('scheduleBody');
    const wrapper = document.getElementById('scheduleTableWrapper');
    const emptyEl = document.getElementById('emptySchedule');
    const countEl = document.getElementById('scheduleCount');

    countEl.textContent = items.length;

    if (items.length === 0) {
        wrapper.style.display = 'none';
        emptyEl.style.display = 'flex';
        return;
    }

    wrapper.style.display = 'block';
    emptyEl.style.display = 'none';

    tbody.innerHTML = items.map(s => {
        const [startDate, startTime] = splitDateTime(s.startTimeFmt);
        const [endDate, endTime] = splitDateTime(s.endTimeFmt);

        const statusHtml = s.isActive
            ? `<span class="gd-status-active">
                   <span class="material-icons">radio_button_checked</span>
                   ${t('gd.status.active', 'Activo')}
               </span>`
            : `<span class="gd-status-upcoming">
                   <span class="material-icons">schedule</span>
                   ${t('gd.status.upcoming', 'Próximo')}
               </span>`;

        return `
        <tr class="${s.isActive ? 'gd-row-active' : ''}">
            <td>
                <div class="gd-table-user">
                    <div class="gd-avatar-sm">${escapeHtml(s.initials)}</div>
                    <div>
                        <div class="gd-table-user-name">${escapeHtml(s.username)}</div>
                        <div class="gd-table-user-email">${escapeHtml(s.email)}</div>
                    </div>
                </div>
            </td>
            <td>
                <div class="gd-datetime">
                    <div class="gd-date">${startDate}</div>
                    <div class="gd-time">${startTime} ${t('gd.hrs', 'hrs')}</div>
                </div>
            </td>
            <td>
                <div class="gd-datetime">
                    <div class="gd-date">${endDate}</div>
                    <div class="gd-time">${endTime} ${t('gd.hrs', 'hrs')}</div>
                </div>
            </td>
            <td style="color:var(--text-secondary);font-size:13px;">
                ${escapeHtml(s.assignedBy)}
            </td>
            <td>${statusHtml}</td>
            <td>
                <button type="button" class="gd-btn-delete"
                        onclick="confirmRemove(${s.guardId}, '${escapeHtml(s.username)}')"
                        title="${t('gd.modal.remove_title', 'Eliminar turno')}">
                    <span class="material-icons">delete_outline</span>
                </button>
            </td>
        </tr>`;
    }).join('');
}

// ─── HEADER BADGE ──────────────────────────────────────
function updateHeaderBadge(items) {
    const activeGuards = items.filter(s => s.isActive);
    const badge = document.getElementById('guardStatusBadge');
    const text = document.getElementById('guardStatusText');

    if (activeGuards.length > 0) {
        const names = activeGuards.map(s => s.username).join(', ');
        badge.className = 'gd-header-badge active';
        text.textContent = `${t('gd.badge.active', 'Guardia activa:')} ${names}`;
    } else {
        badge.className = 'gd-header-badge inactive';
        text.textContent = t('gd.badge.inactive', 'Sin guardia activa');
    }
}

// ─── MODAL ASIGNAR TURNO ───────────────────────────────
// MODAL FIX: creado con insertAdjacentHTML en document.body
// position:fixed falla dentro del dashboard layout

function openAssignModal(userId) {
    const targetId = parseInt(userId, 10);

    // Guardar en variable LOCAL primero — closeGdModal() pone gd_pendingOwner = null
    // y se llamaba ANTES de usar la variable en el template, causando el crash.
    const owner = gd_owners.find(o => parseInt(o.userId, 10) === targetId);

    if (!owner) {
        console.error('[guard.js] Owner no encontrado. userId:', userId,
            '| ids disponibles:', gd_owners.map(o => o.userId));
        return;
    }

    closeGdModal();

    gd_pendingOwner = owner;   // asignar DESPUÉS de validar

    // Datetime mínimo = ahora + 1 min
    const now = new Date();
    now.setMinutes(now.getMinutes() + 1);
    const minDt = toLocalISOString(now);

    // Sugerencia fin = inicio + 8 horas
    const suggested = new Date(now);
    suggested.setHours(suggested.getHours() + 8);
    const suggestedEnd = toLocalISOString(suggested);



    document.body.insertAdjacentHTML('beforeend', `
    <div class="gd-overlay" id="gdOverlay" onclick="onOverlayClick(event)">
        <div class="gd-modal" id="gdModal">
            <div class="gd-modal-header">
                <span class="material-icons">schedule</span>
                <h3>${t('gd.modal.assign_title', 'Asignar turno de guardia')}</h3>
                <button type="button" class="gd-modal-close" onclick="closeGdModal()">
                    <span class="material-icons">close</span>
                </button>
            </div>
            <div class="gd-modal-body">

                <!-- Info del owner -->
                <div class="gd-modal-owner-card">
                    <div class="gd-avatar">${escapeHtml(gd_pendingOwner.initials)}</div>
                    <div class="gd-owner-info">
                        <div class="gd-owner-name">${escapeHtml(gd_pendingOwner.username)}</div>
                        <div class="gd-owner-email">${escapeHtml(gd_pendingOwner.email)}</div>
                    </div>
                </div>

                <!-- Error general -->
                <div class="gd-modal-error" id="gdModalError"></div>

                <!-- Fecha inicio -->
                <div class="gd-field-group">
                    <label>${t('gd.modal.start_label', 'Inicio del turno')}</label>
                    <input type="datetime-local" id="gdStartTime"
                           min="${minDt}" value="${minDt}" oninput="validateDates()" />
                    <div class="gd-field-error" id="gdStartError">
                        ${t('gd.err.start_required', 'Fecha de inicio requerida.')}
                    </div>
                </div>

                <!-- Fecha fin -->
                <div class="gd-field-group">
                    <label>${t('gd.modal.end_label', 'Fin del turno')}</label>
                    <input type="datetime-local" id="gdEndTime"
                           min="${minDt}" value="${suggestedEnd}" oninput="validateDates()" />
                    <div class="gd-field-error" id="gdEndError">
                        ${t('gd.err.end_after_start', 'La fecha de fin debe ser posterior al inicio.')}
                    </div>
                </div>

            </div>
            <div class="gd-modal-footer">
                <button type="button" class="gd-btn-cancel" onclick="closeGdModal()">
                    ${t('common.cancel', 'Cancelar')}
                </button>
                <button type="button" class="gd-btn-confirm" id="gdBtnConfirm" onclick="submitAssign()">
                    <span class="material-icons">check</span>
                    ${t('gd.modal.confirm', 'Confirmar')}
                </button>
            </div>
        </div>
    </div>`);
}

function onOverlayClick(e) {
    if (e.target.id === 'gdOverlay') closeGdModal();
}

function closeGdModal() {
    const el = document.getElementById('gdOverlay');
    if (el) el.remove();
    gd_pendingOwner = null;
}

// ─── VALIDACIÓN DE FECHAS ──────────────────────────────
function validateDates() {
    const startEl = document.getElementById('gdStartTime');
    const endEl = document.getElementById('gdEndTime');
    if (!startEl || !endEl) return true;

    const startVal = startEl.value;
    const endVal = endEl.value;
    let valid = true;

    if (!startVal) {
        showFieldError('gdStartError', t('gd.err.start_required', 'Fecha de inicio requerida.'));
        valid = false;
    } else {
        hideFieldError('gdStartError');
    }

    if (!endVal) {
        showFieldError('gdEndError', t('gd.err.end_required', 'Fecha de fin requerida.'));
        valid = false;
    } else if (endVal <= startVal) {
        showFieldError('gdEndError', t('gd.err.end_after_start', 'La fecha de fin debe ser posterior al inicio.'));
        valid = false;
    } else {
        hideFieldError('gdEndError');
    }

    const btn = document.getElementById('gdBtnConfirm');
    if (btn) btn.disabled = !valid;

    return valid;
}

function showFieldError(id, msg) {
    const el = document.getElementById(id);
    if (el) { el.style.display = 'block'; el.textContent = msg; }
}

function hideFieldError(id) {
    const el = document.getElementById(id);
    if (el) el.style.display = 'none';
}

// ─── SUBMIT ASSIGN ─────────────────────────────────────
function submitAssign() {
    if (!validateDates()) return;
    if (!gd_pendingOwner) return;

    const startTime = document.getElementById('gdStartTime').value;
    const endTime = document.getElementById('gdEndTime').value;
    const btn = document.getElementById('gdBtnConfirm');

    btn.disabled = true;
    btn.innerHTML = `<span class="material-icons gd-spin">autorenew</span> ${t('gd.modal.saving', 'Guardando...')}`;

    gdCall('AssignGuard', {
        userId: gd_pendingOwner.userId,
        startTime: startTime,
        endTime: endTime
    }, (resp) => {
        if (resp.success) {
            const ownerName = gd_pendingOwner.username;
            closeGdModal();
            showToast(
                t('gd.toast.assigned', 'Turno asignado a').replace('{name}', ownerName)
                + ` ${ownerName}. ` + t('gd.toast.notification_sent', 'Notificación enviada.'),
                'success'
            );
            loadOwners();
            loadSchedule();
        } else {
            const errEl = document.getElementById('gdModalError');
            if (errEl) {
                errEl.style.display = 'block';
                errEl.textContent = resp.message || t('gd.err.assign_failed', 'Error al asignar el turno.');
            }
            btn.disabled = false;
            btn.innerHTML = `<span class="material-icons">check</span> ${t('gd.modal.confirm', 'Confirmar')}`;
        }
    });
}

// ─── CONFIRM REMOVE ────────────────────────────────────
function confirmRemove(guardId, username) {
    closeGdModal();

    document.body.insertAdjacentHTML('beforeend', `
    <div class="gd-overlay" id="gdOverlay" onclick="onOverlayClick(event)">
        <div class="gd-modal gd-confirm-modal" id="gdModal">
            <div class="gd-modal-header">
                <span class="material-icons" style="color:var(--error-color)">warning</span>
                <h3>${t('gd.modal.remove_title', 'Eliminar turno')}</h3>
                <button type="button" class="gd-modal-close" onclick="closeGdModal()">
                    <span class="material-icons">close</span>
                </button>
            </div>
            <div class="gd-confirm-body">
                <div class="gd-confirm-icon material-icons">event_busy</div>
                <p>${t('gd.modal.remove_msg', '¿Estás seguro que deseas eliminar el turno de guardia de')}<br>
                   <strong>${escapeHtml(username)}</strong>?</p>
                <p style="margin-top:8px;font-size:12px;">
                    ${t('gd.modal.remove_warning', 'Esta acción no se puede deshacer.')}
                </p>
            </div>
            <div class="gd-modal-footer">
                <button type="button" class="gd-btn-cancel" onclick="closeGdModal()">
                    ${t('common.cancel', 'Cancelar')}
                </button>
                <button type="button" class="gd-btn-danger" id="gdBtnDanger" onclick="submitRemove(${guardId})">
                    <span class="material-icons">delete</span>
                    ${t('gd.modal.delete', 'Eliminar')}
                </button>
            </div>
        </div>
    </div>`);
}

function submitRemove(guardId) {
    const btn = document.getElementById('gdBtnDanger');
    btn.disabled = true;
    btn.innerHTML = `<span class="material-icons gd-spin">autorenew</span> ${t('gd.modal.deleting', 'Eliminando...')}`;

    gdCall('RemoveGuard', { guardId }, (resp) => {
        closeGdModal();
        if (resp.success) {
            showToast(t('gd.toast.removed', 'Turno eliminado correctamente.'), 'success');
            loadOwners();
            loadSchedule();
        } else {
            showToast(resp.message || t('gd.err.remove_failed', 'Error al eliminar el turno.'), 'error');
        }
    });
}

// ─── TOAST ─────────────────────────────────────────────
function showToast(message, type = 'success') {
    const existing = document.getElementById('gdToast');
    if (existing) existing.remove();

    const icon = type === 'success' ? 'check_circle' : 'error';

    document.body.insertAdjacentHTML('beforeend', `
    <div id="gdToast" class="gd-toast ${type}">
        <span class="material-icons" style="font-size:20px;">${icon}</span>
        ${escapeHtml(message)}
    </div>`);

    setTimeout(() => {
        const t = document.getElementById('gdToast');
        if (t) t.remove();
    }, 4000);
}

// ─── UTILS ─────────────────────────────────────────────
function escapeHtml(str) {
    if (!str) return '';
    return str
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

/** "dd/MM/yyyy HH:mm" → ["dd/MM/yyyy", "HH:mm"] */
function splitDateTime(dtFmt) {
    if (!dtFmt) return ['—', ''];
    const parts = dtFmt.split(' ');
    return [parts[0] || '—', parts[1] || ''];
}

/** Date → string "yyyy-MM-ddTHH:mm" (input datetime-local) */
function toLocalISOString(date) {
    const pad = n => String(n).padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
        `T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}