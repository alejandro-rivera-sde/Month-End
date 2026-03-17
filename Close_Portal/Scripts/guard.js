/* =========================================================
   guard.js — Módulo de Gestión de Guardia
   Modelo: 1 guardia con N spots (uno por departamento)
   Patrón: WebMethod via $.ajax, modales en document.body
   ========================================================= */

// ─── Estado global ─────────────────────────────────────────
let gd_guard = null;       // guardia actual (o null)
let gd_pendingSpot = null; // spot seleccionado para asignar usuario

// ─── INIT ──────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
    loadGuardStatus();
    loadHistory();
});

// ─── TRADUCCIÓN ────────────────────────────────────────────
function t(key, fallback) {
    const lang = document.documentElement.getAttribute('data-language') || 'es';
    if (typeof translations !== 'undefined' && translations[lang]?.[key])
        return translations[lang][key];
    return fallback !== undefined ? fallback : key;
}

// ─── HELPER AJAX ───────────────────────────────────────────
function gdCall(method, data, onSuccess) {
    $.ajax({
        type: 'POST',
        url: 'Guard.aspx/' + method,
        data: JSON.stringify(data),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: (resp) => onSuccess(resp.d !== undefined ? resp.d : resp),
        error: (xhr) => {
            console.error('[guard.js]', method, xhr.responseText);
            showToast(t('gd.toast.error', 'Error de comunicación con el servidor.'), 'error');
        }
    });
}

// ─── LOAD GUARD STATUS ─────────────────────────────────────
function loadGuardStatus() {
    gdCall('GetGuardStatus', {}, (resp) => {
        if (!resp.success) {
            showToast(t('gd.toast.error', 'Error al cargar el estado de la guardia.'), 'error');
            return;
        }
        gd_guard = resp.guard || null;
        renderGuardPanel();
        updateHeaderBadge();
    });
}

// ─── RENDER GUARD PANEL ────────────────────────────────────
function renderGuardPanel() {
    const noGuard = document.getElementById('gdNoGuard');
    const activePanel = document.getElementById('gdActivePanel');

    if (!gd_guard) {
        noGuard.style.display = 'flex';
        activePanel.style.display = 'none';
        return;
    }

    noGuard.style.display = 'none';
    activePanel.style.display = 'block';

    // Cabecera
    document.getElementById('gdCreatedInfo').textContent =
        `${t('gd.created_by', 'Creada por')} ${escapeHtml(gd_guard.createdBy)} — ${gd_guard.createdAtFmt}`;

    // Botón Eliminar: visible mientras la guardia no haya cerrado
    const btnRemove = document.getElementById('gdBtnRemove');
    btnRemove.style.display = !gd_guard.isFinished ? 'inline-flex' : 'none';

    // Banner de inicio programado (si el inicio es en el futuro)
    const startStatus = document.getElementById('gdStartStatus');
    if (startStatus) {
        if (gd_guard.startTime && !gd_guard.isStarted) {
            startStatus.style.display = 'flex';
            document.getElementById('gdStartStatusText').textContent =
                `${t('gd.scheduled_at', 'Inicio programado:')} ${gd_guard.startTimeFmt}`;
        } else {
            startStatus.style.display = 'none';
        }
    }

    // Spots
    renderSpots(gd_guard.spots);
}

// ─── RENDER SPOTS ──────────────────────────────────────────
function renderSpots(spots) {
    const grid = document.getElementById('gdSpotsGrid');
    if (!spots || spots.length === 0) {
        grid.innerHTML = `<p style="color:var(--text-muted);font-size:13px;">${t('gd.spots.empty', 'Sin spots disponibles.')}</p>`;
        return;
    }

    grid.innerHTML = spots.map(spot => {
        const filled = spot.isFilled;
        const cardClass = `gd-spot-card ${filled ? 'gd-spot-filled' : 'gd-spot-pending'}`;

        const userBlock = filled
            ? `<div class="gd-spot-user">
                   <div class="gd-avatar gd-avatar-sm">${escapeHtml(spot.initials || '??')}</div>
                   <div class="gd-spot-user-info">
                       <div class="gd-spot-user-name">${escapeHtml(spot.username)}</div>
                       <div class="gd-spot-user-email">${escapeHtml(spot.email)}</div>
                   </div>
               </div>`
            : `<div class="gd-spot-empty-user">
                   <span class="material-icons">person_outline</span>
                   <span>${t('gd.spot.pending', 'Sin asignar')}</span>
               </div>`;

        const actionBtn = gd_guard.isFinished
            ? ''   // guardia iniciada — no se puede modificar spots
            : filled
                ? `<button type="button" class="gd-btn-spot-clear"
                           onclick="confirmUnassignSpot(${spot.spotId})"
                           title="${t('gd.spot.clear', 'Limpiar spot')}">
                       <span class="material-icons">person_remove</span>
                   </button>`
                : `<button type="button" class="gd-btn-spot-assign"
                           onclick="openAssignSpotModal(${spot.spotId}, ${spot.departmentId}, '${escapeHtml(spot.departmentCode)}', '${escapeHtml(spot.departmentName)}')">
                       <span class="material-icons">person_add</span>
                       ${t('gd.spot.assign', 'Asignar')}
                   </button>`;

        return `
        <div class="${cardClass}" data-spot-id="${spot.spotId}">
            <div class="gd-spot-dept">
                <span class="gd-dept-badge">${escapeHtml(spot.departmentCode)}</span>
                <span class="gd-dept-name">${escapeHtml(spot.departmentName)}</span>
            </div>
            ${userBlock}
            <div class="gd-spot-footer">
                ${filled
                ? `<span class="gd-spot-assigned-info">${t('gd.spot.assigned_by', 'Por')} ${escapeHtml(spot.assignedBy)} · ${spot.assignedAtFmt}</span>`
                : ''}
                ${actionBtn}
            </div>
        </div>`;
    }).join('');
}

// ─── HEADER BADGE ──────────────────────────────────────────
function updateHeaderBadge() {
    const badge = document.getElementById('guardStatusBadge');
    const text = document.getElementById('guardStatusText');

    if (!gd_guard) {
        badge.className = 'gd-header-badge inactive';
        text.textContent = t('gd.badge.none', 'Sin guardia activa');
    } else if (gd_guard.isStarted) {
        badge.className = 'gd-header-badge active';
        text.textContent = t('gd.badge.active', 'Guardia activa');
    } else {
        badge.className = 'gd-header-badge pending';
        const filled = (gd_guard.spots || []).filter(s => s.isFilled).length;
        const total = (gd_guard.spots || []).length;
        text.textContent = `${t('gd.badge.pending', 'Guardia pendiente')} (${filled}/${total})`;
    }
}

// ─── CREATE GUARD (modal con fecha/hora) ───────────────────
function createGuard() {
    closeGdModal();

    const now = new Date();
    now.setMinutes(now.getMinutes() + 1);
    const minDt = toLocalISOString(now);

    document.body.insertAdjacentHTML('beforeend', `
    <div class="gd-overlay" id="gdOverlay" onclick="onOverlayClick(event)">
        <div class="gd-modal" id="gdModal">
            <div class="gd-modal-header">
                <span class="material-icons">add_circle_outline</span>
                <h3>${t('gd.modal.create_title', 'Nueva guardia')}</h3>
                <button type="button" class="gd-modal-close" onclick="closeGdModal()">
                    <span class="material-icons">close</span>
                </button>
            </div>
            <div class="gd-modal-body">
                <div class="gd-modal-error" id="gdModalError" style="display:none;"></div>
                <div class="gd-field-group">
                    <label>${t('gd.modal.start_label', 'Inicio de la guardia')}</label>
                    <input type="datetime-local" id="gdCreateStartTime"
                           min="${minDt}" value="${minDt}" />
                    <div class="gd-field-error" id="gdStartError" style="display:none;">
                        ${t('gd.err.start_required', 'Fecha de inicio requerida.')}
                    </div>
                </div>
            </div>
            <div class="gd-modal-footer">
                <button type="button" class="gd-btn-cancel" onclick="closeGdModal()">
                    ${t('common.cancel', 'Cancelar')}
                </button>
                <button type="button" class="gd-btn-confirm" id="gdBtnConfirmCreate"
                        onclick="submitCreateGuard()">
                    <span class="material-icons">check</span>
                    ${t('gd.modal.confirm_create', 'Crear guardia')}
                </button>
            </div>
        </div>
    </div>`);
}

function submitCreateGuard() {
    const startVal = document.getElementById('gdCreateStartTime').value;
    const errEl = document.getElementById('gdStartError');

    if (!startVal) {
        errEl.style.display = 'block';
        return;
    }
    errEl.style.display = 'none';

    const btn = document.getElementById('gdBtnConfirmCreate');
    btn.disabled = true;
    btn.innerHTML = `<span class="material-icons gd-spin">autorenew</span> ${t('gd.modal.saving', 'Procesando...')}`;

    gdCall('CreateGuard', { startTime: startVal }, (resp) => {
        if (resp.success) {
            closeGdModal();
            showToast(t('gd.toast.created', 'Guardia creada. Asigna los responsables.'), 'success');
            loadGuardStatus();
        } else {
            const modalErr = document.getElementById('gdModalError');
            if (modalErr) {
                modalErr.style.display = 'block';
                modalErr.textContent = resp.message || t('gd.toast.error', 'Error al crear la guardia.');
            }
            btn.disabled = false;
            btn.innerHTML = `<span class="material-icons">check</span> ${t('gd.modal.confirm_create', 'Crear guardia')}`;
        }
    });
}

// ─── OPEN ASSIGN SPOT MODAL ────────────────────────────────
function openAssignSpotModal(spotId, departmentId, deptCode, deptName) {
    closeGdModal();
    gd_pendingSpot = { spotId, departmentId, deptCode, deptName };

    document.body.insertAdjacentHTML('beforeend', `
    <div class="gd-overlay" id="gdOverlay" onclick="onOverlayClick(event)">
        <div class="gd-modal" id="gdModal">
            <div class="gd-modal-header">
                <span class="material-icons">person_add</span>
                <h3>${t('gd.modal.assign_title', 'Asignar responsable')}</h3>
                <button type="button" class="gd-modal-close" onclick="closeGdModal()">
                    <span class="material-icons">close</span>
                </button>
            </div>
            <div class="gd-modal-body">
                <div class="gd-modal-dept-badge">
                    <span class="gd-dept-badge">${escapeHtml(deptCode)}</span>
                    <span class="gd-dept-name">${escapeHtml(deptName)}</span>
                </div>
                <div class="gd-modal-error" id="gdModalError" style="display:none;"></div>
                <div id="gdUserListLoading" class="gd-loading">
                    <span class="material-icons gd-spin">autorenew</span>
                    <span>${t('gd.modal.loading_users', 'Cargando usuarios...')}</span>
                </div>
                <div id="gdUserList" class="gd-user-list" style="display:none;"></div>
            </div>
            <div class="gd-modal-footer">
                <button type="button" class="gd-btn-cancel" onclick="closeGdModal()">
                    ${t('common.cancel', 'Cancelar')}
                </button>
            </div>
        </div>
    </div>`);

    // Cargar usuarios del departamento
    gdCall('GetUsersByDepartment', { departmentId }, (resp) => {
        document.getElementById('gdUserListLoading').style.display = 'none';
        const listEl = document.getElementById('gdUserList');

        if (!resp.success || !resp.data || resp.data.length === 0) {
            listEl.innerHTML = `<p style="color:var(--text-muted);font-size:13px;padding:8px 0;">
                ${t('gd.modal.no_users', 'No hay usuarios activos en este departamento.')}</p>`;
            listEl.style.display = 'block';
            return;
        }

        listEl.innerHTML = resp.data.map(u => `
            <div class="gd-user-option" onclick="submitAssignSpot(${u.userId}, '${escapeHtml(u.username)}')">
                <div class="gd-avatar gd-avatar-sm">${escapeHtml(u.initials)}</div>
                <div class="gd-user-option-info">
                    <div class="gd-owner-name">${escapeHtml(u.username)}</div>
                    <div class="gd-owner-email">${escapeHtml(u.email)}</div>
                </div>
                <span class="material-icons gd-user-option-arrow">chevron_right</span>
            </div>`).join('');
        listEl.style.display = 'block';
    });
}

function submitAssignSpot(userId, username) {
    if (!gd_pendingSpot) return;
    const spotId = gd_pendingSpot.spotId;

    gdCall('AssignSpot', { spotId, userId }, (resp) => {
        if (resp.success) {
            closeGdModal();
            showToast(`${username} ${t('gd.toast.spot_assigned', 'asignado correctamente.')}`, 'success');
            loadGuardStatus();
        } else {
            const errEl = document.getElementById('gdModalError');
            if (errEl) {
                errEl.style.display = 'block';
                errEl.textContent = resp.message || t('gd.toast.error', 'Error al asignar.');
            }
        }
    });
}

// ─── UNASSIGN SPOT ─────────────────────────────────────────
function confirmUnassignSpot(spotId) {
    closeGdModal();

    document.body.insertAdjacentHTML('beforeend', `
    <div class="gd-overlay" id="gdOverlay" onclick="onOverlayClick(event)">
        <div class="gd-modal gd-confirm-modal" id="gdModal">
            <div class="gd-modal-header">
                <span class="material-icons" style="color:var(--warning-color)">warning</span>
                <h3>${t('gd.modal.unassign_title', 'Limpiar spot')}</h3>
                <button type="button" class="gd-modal-close" onclick="closeGdModal()">
                    <span class="material-icons">close</span>
                </button>
            </div>
            <div class="gd-confirm-body">
                <div class="gd-confirm-icon material-icons">person_remove</div>
                <p>${t('gd.modal.unassign_msg', '¿Deseas quitar al responsable asignado a este spot?')}</p>
            </div>
            <div class="gd-modal-footer">
                <button type="button" class="gd-btn-cancel" onclick="closeGdModal()">
                    ${t('common.cancel', 'Cancelar')}
                </button>
                <button type="button" class="gd-btn-danger" onclick="submitUnassignSpot(${spotId})">
                    <span class="material-icons">person_remove</span>
                    ${t('gd.modal.confirm_unassign', 'Limpiar')}
                </button>
            </div>
        </div>
    </div>`);
}

function submitUnassignSpot(spotId) {
    gdCall('UnassignSpot', { spotId }, (resp) => {
        closeGdModal();
        if (resp.success) {
            showToast(t('gd.toast.spot_cleared', 'Spot liberado.'), 'success');
            loadGuardStatus();
        } else {
            showToast(resp.message || t('gd.toast.error', 'Error.'), 'error');
        }
    });
}

// ─── REMOVE GUARD ──────────────────────────────────────────
function confirmRemoveGuard() {
    if (!gd_guard) return;
    closeGdModal();

    document.body.insertAdjacentHTML('beforeend', `
    <div class="gd-overlay" id="gdOverlay" onclick="onOverlayClick(event)">
        <div class="gd-modal gd-confirm-modal" id="gdModal">
            <div class="gd-modal-header">
                <span class="material-icons" style="color:var(--error-color)">warning</span>
                <h3>${t('gd.modal.remove_guard_title', 'Eliminar guardia')}</h3>
                <button type="button" class="gd-modal-close" onclick="closeGdModal()">
                    <span class="material-icons">close</span>
                </button>
            </div>
            <div class="gd-confirm-body">
                <div class="gd-confirm-icon material-icons">event_busy</div>
                <p>${t('gd.modal.remove_guard_msg', '¿Estás seguro que deseas eliminar esta guardia y todos sus spots asignados?')}</p>
                <p style="margin-top:8px;font-size:12px;">${t('gd.modal.remove_warning', 'Esta acción no se puede deshacer.')}</p>
            </div>
            <div class="gd-modal-footer">
                <button type="button" class="gd-btn-cancel" onclick="closeGdModal()">
                    ${t('common.cancel', 'Cancelar')}
                </button>
                <button type="button" class="gd-btn-danger" id="gdBtnDanger"
                        onclick="submitRemoveGuard(${gd_guard.guardId})">
                    <span class="material-icons">delete</span>
                    ${t('gd.modal.delete', 'Eliminar')}
                </button>
            </div>
        </div>
    </div>`);
}

function submitRemoveGuard(guardId) {
    const btn = document.getElementById('gdBtnDanger');
    btn.disabled = true;
    btn.innerHTML = `<span class="material-icons gd-spin">autorenew</span> ${t('gd.modal.deleting', 'Eliminando...')}`;

    gdCall('RemoveGuard', { guardId }, (resp) => {
        closeGdModal();
        if (resp.success) {
            gd_guard = null;
            showToast(t('gd.toast.removed', 'Guardia eliminada.'), 'success');
            loadGuardStatus();
        } else {
            showToast(resp.message || t('gd.toast.error', 'Error al eliminar.'), 'error');
        }
    });
}

// ─── LOAD HISTORY ──────────────────────────────────────────
function loadHistory() {
    gdCall('GetGuardHistory', {}, (resp) => {
        if (!resp.success) return;
        const items = resp.data || [];
        const countEl = document.getElementById('historyCount');
        const emptyEl = document.getElementById('emptyHistory');
        const wrapperEl = document.getElementById('historyTableWrapper');
        const tbody = document.getElementById('historyBody');

        countEl.textContent = items.length;

        if (items.length === 0) {
            emptyEl.style.display = 'flex';
            wrapperEl.style.display = 'none';
            return;
        }

        emptyEl.style.display = 'none';
        wrapperEl.style.display = 'block';

        tbody.innerHTML = items.map(g => {
            const spotTags = (g.spots || []).map(s =>
                `<span class="gd-hist-spot ${s.isFilled ? '' : 'gd-hist-spot-empty'}">
                    <span class="gd-dept-badge gd-dept-badge-sm">${escapeHtml(s.departmentCode)}</span>
                    ${s.isFilled ? escapeHtml(s.username) : `<em>${t('gd.spot.pending', 'Sin asignar')}</em>`}
                 </span>`
            ).join('');

            return `
            <tr>
                <td style="color:var(--text-muted);font-size:12px;">#${g.guardId}</td>
                <td><div class="gd-hist-spots">${spotTags}</div></td>
                <td>
                    <div class="gd-datetime">
                        <div class="gd-date">${g.startTimeFmt !== '—' ? g.startTimeFmt : '—'}</div>
                    </div>
                </td>
                <td>
                    <div class="gd-datetime">
                        <div class="gd-date">${g.endTimeFmt !== '—' ? g.endTimeFmt : '—'}</div>
                    </div>
                </td>
                <td style="color:var(--text-secondary);font-size:13px;">${escapeHtml(g.createdBy || '—')}</td>
            </tr>`;
        }).join('');
    });
}

// ─── MODAL HELPERS ─────────────────────────────────────────
function onOverlayClick(e) {
    if (e.target.id === 'gdOverlay') closeGdModal();
}

function closeGdModal() {
    const el = document.getElementById('gdOverlay');
    if (el) el.remove();
    gd_pendingSpot = null;
}

// ─── TOAST ─────────────────────────────────────────────────
function showToast(message, type = 'success') {
    const existing = document.getElementById('gdToast');
    if (existing) existing.remove();
    const icon = type === 'success' ? 'check_circle' : type === 'error' ? 'error' : 'info';
    document.body.insertAdjacentHTML('beforeend', `
    <div id="gdToast" class="gd-toast ${type}">
        <span class="material-icons" style="font-size:20px;">${icon}</span>
        ${escapeHtml(message)}
    </div>`);
    setTimeout(() => { const el = document.getElementById('gdToast'); if (el) el.remove(); }, 4000);
}

// ─── UTILS ─────────────────────────────────────────────────
function escapeHtml(str) {
    if (!str) return '';
    return str
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

/** Date → string "yyyy-MM-ddTHH:mm" (input datetime-local) */
function toLocalISOString(date) {
    const pad = n => String(n).padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
        `T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}