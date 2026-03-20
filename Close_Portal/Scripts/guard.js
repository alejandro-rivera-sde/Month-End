/* =========================================================
   guard.js — Módulo de Gestión de Guardia
   Modelo: 1 guardia con N spots (uno por departamento)
   Patrón: WebMethod via $.ajax, modales en document.body
   ========================================================= */

// ─── Estado global ─────────────────────────────────────────
let gd_guard = null;           // guardia actual (o null)
let gd_pendingSpot = null;     // spot seleccionado para asignar usuario
let gd_myDepartmentId = -1;    // departamento del usuario en sesión (-1 = sin restricción)
let gd_isOwner = false;        // true = Owner, puede gestionar cualquier spot
let gd_canCreateGuard = false; // true = AR o Owner, puede crear/eliminar guardias

// ─── INIT ──────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
    loadGuardStatus();
    loadHistory();

    // Re-renderizar contenido dinámico al cambiar idioma
    window.onLanguageChange = () => {
        loadGuardStatus();
        loadHistory();
    };
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
        gd_myDepartmentId = resp.myDepartmentId ?? -1;
        gd_isOwner = resp.isOwner ?? false;
        gd_canCreateGuard = resp.canCreateGuard ?? false;
        renderGuardPanel();
        updateHeaderBadge();
        // Cargar sección de locaciones después de conocer el estado de la guardia
        loadActiveLocations();
    });
}

// ─── RENDER GUARD PANEL ────────────────────────────────────
function renderGuardPanel() {
    const noGuard = document.getElementById('gdNoGuard');
    const activePanel = document.getElementById('gdActivePanel');

    if (!gd_guard) {
        noGuard.style.display = 'flex';
        activePanel.style.display = 'none';
        const btnCreate = noGuard.querySelector('.gd-btn-create');
        if (btnCreate) btnCreate.style.display = gd_canCreateGuard ? 'inline-flex' : 'none';
        return;
    }

    noGuard.style.display = 'none';
    activePanel.style.display = 'block';

    // Cabecera
    const draftLabel = gd_guard.isDraft
        ? ` <span class="gd-draft-badge">${t('gd.draft', 'Borrador')}</span>` : '';
    document.getElementById('gdCreatedInfo').innerHTML =
        `${t('gd.created_by', 'Creada por')} ${escapeHtml(gd_guard.createdBy)} — ${gd_guard.createdAtFmt}${draftLabel}`;

    // Botón "Crear guardia" — solo visible en borrador + locaciones asignadas + AR/Owner
    const btnConfirm = document.getElementById('gdBtnConfirmGuard');
    if (btnConfirm) {
        const showConfirm = gd_canCreateGuard && gd_guard.isDraft && gd_guard.hasLocations && !gd_guard.isFinished;
        btnConfirm.style.display = showConfirm ? 'inline-flex' : 'none';
    }

    // Botón Eliminar
    const btnRemove = document.getElementById('gdBtnRemove');
    btnRemove.style.display = (!gd_guard.isFinished && gd_canCreateGuard) ? 'inline-flex' : 'none';

    // Banner de inicio programado
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

    // Banner de cierre estimado
    const estEndStatus = document.getElementById('gdEstEndStatus');
    if (estEndStatus) {
        if (gd_guard.estimatedEndTimeFmt && !gd_guard.isFinished) {
            estEndStatus.style.display = 'flex';
            document.getElementById('gdEstEndStatusText').textContent =
                `${t('gd.estimated_end', 'Cierre estimado:')} ${gd_guard.estimatedEndTimeFmt}`;
        } else {
            estEndStatus.style.display = 'none';
        }
    }

    // Spots
    renderSpots(gd_guard.spots || []);
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

        // Owners gestionan cualquier spot; Admins solo el de su departamento
        const canManageThisSpot = gd_isOwner || spot.departmentId === gd_myDepartmentId;

        const actionBtn = gd_guard.isFinished || !canManageThisSpot
            ? ''
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
    } else if (gd_guard.isFinished) {
        badge.className = 'gd-header-badge inactive';
        text.textContent = t('gd.badge.finished', 'Guardia finalizada');
    } else if (gd_guard.isDraft) {
        badge.className = 'gd-header-badge draft';
        const step = gd_guard.hasLocations
            ? t('gd.badge.draft_ready', 'Borrador — listo para confirmar')
            : t('gd.badge.draft_locs', 'Borrador — selecciona locaciones');
        text.textContent = step;
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

// ─── STEP 1: RESERVE DATES (modal solo con fechas) ─────────
function createGuard() {
    if (!gd_canCreateGuard) {
        showToast(t('gd.toast.no_permission_create', 'Solo el departamento AR puede crear guardias.'), 'error');
        return;
    }
    closeGdModal();

    const now = new Date();
    now.setMinutes(now.getMinutes() + 1);
    const defaultDt = toLocalISOString(now);

    document.body.insertAdjacentHTML('beforeend', `
    <div class="gd-overlay" id="gdOverlay" onclick="onOverlayClick(event)">
        <div class="gd-modal" id="gdModal">
            <div class="gd-modal-header">
                <span class="material-icons">event</span>
                <h3>${t('gd.modal.reserve_title', 'Reservar fecha de guardia')}</h3>
                <button type="button" class="gd-modal-close" onclick="closeGdModal()">
                    <span class="material-icons">close</span>
                </button>
            </div>
            <div class="gd-modal-body">
                <div class="gd-modal-error" id="gdModalError" style="display:none;"></div>
                <div class="gd-field-group">
                    <label>${t('gd.modal.start_label', 'Inicio de la guardia')}</label>
                    <div class="gd-datetime-row">
                        <input type="datetime-local" id="gdCreateStartTime" value="${defaultDt}" />
                    </div>
                    <div class="gd-field-error" id="gdStartError" style="display:none;">
                        ${t('gd.err.start_required', 'Fecha de inicio requerida.')}
                    </div>
                </div>
                <div class="gd-field-group">
                    <label>
                        ${t('gd.modal.est_end_label', 'Cierre estimado')}
                        <span class="gd-field-hint">(${t('gd.modal.optional', 'opcional')})</span>
                    </label>
                    <div class="gd-datetime-row">
                        <input type="datetime-local" id="gdCreateEstEndTime" />
                    </div>
                    <span class="gd-field-hint">
                        ${t('gd.modal.est_end_hint', 'Referencia informativa. El cierre real ocurre cuando todas las locaciones involucradas cierran.')}
                    </span>
                    <div class="gd-field-error" id="gdEstEndError" style="display:none;">
                        ${t('gd.err.est_end_invalid', 'La fecha estimada debe ser posterior al inicio.')}
                    </div>
                </div>
            </div>
            <div class="gd-modal-footer">
                <button type="button" class="gd-btn-cancel" onclick="closeGdModal()">
                    ${t('common.cancel', 'Cancelar')}
                </button>
                <button type="button" class="gd-btn-confirm" id="gdBtnConfirmCreate"
                        onclick="submitReserveDates()">
                    <span class="material-icons">event_available</span>
                    ${t('gd.modal.confirm_reserve', 'Reservar fecha')}
                </button>
            </div>
        </div>
    </div>`);
}

function submitReserveDates() {
    const startVal = document.getElementById('gdCreateStartTime').value;
    const estEndVal = document.getElementById('gdCreateEstEndTime')?.value || '';
    const errEl = document.getElementById('gdStartError');
    const estErrEl = document.getElementById('gdEstEndError');

    if (!startVal) { errEl.style.display = 'block'; return; }
    errEl.style.display = 'none';

    if (estEndVal && new Date(estEndVal) <= new Date(startVal)) {
        estErrEl.style.display = 'block'; return;
    }
    if (estErrEl) estErrEl.style.display = 'none';

    const btn = document.getElementById('gdBtnConfirmCreate');
    btn.disabled = true;
    btn.innerHTML = `<span class="material-icons gd-spin">autorenew</span> ${t('gd.modal.saving', 'Procesando...')}`;

    gdCall('ReserveDates', { startTime: startVal, estimatedEndTime: estEndVal }, (resp) => {
        if (resp.success) {
            closeGdModal();
            showToast(t('gd.toast.reserved', 'Fecha reservada. Ahora selecciona las locaciones.'), 'success');
            loadGuardStatus();
        } else {
            const modalErr = document.getElementById('gdModalError');
            if (modalErr) { modalErr.style.display = 'block'; modalErr.textContent = resp.message; }
            btn.disabled = false;
            btn.innerHTML = `<span class="material-icons">event_available</span> ${t('gd.modal.confirm_reserve', 'Reservar fecha')}`;
        }
    });
}

// ─── STEP 3: CONFIRM GUARD ─────────────────────────────────
function submitConfirmGuard() {
    if (!gd_guard) return;
    const guardId = gd_guard.guardId;

    const btn = document.getElementById('gdBtnConfirmGuard');
    btn.disabled = true;
    btn.innerHTML = `<span class="material-icons gd-spin">autorenew</span> ${t('gd.modal.saving', 'Procesando...')}`;

    gdCall('ConfirmGuard', { guardId }, (resp) => {
        if (resp.success) {
            showToast(t('gd.toast.confirmed', 'Guardia creada. Asigna los responsables.'), 'success');
            loadGuardStatus();
        } else {
            showToast(resp.message || t('gd.toast.error', 'Error.'), 'error');
            btn.disabled = false;
            btn.innerHTML = `<span class="material-icons">rocket_launch</span> ${t('gd.btn.confirm_guard', 'Crear guardia')}`;
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
                <button type="button" class="gd-btn-danger" id="gdBtnDanger" disabled>
                    <span class="material-icons">timer</span>
                    <span id="gdBtnDangerLabel">${t('gd.modal.delete_cooldown', 'Eliminar')} (3)</span>
                </button>
            </div>
        </div>
    </div>`);

    // Captura el guardId antes del intervalo
    const guardId = gd_guard.guardId;
    let remaining = 3;
    const label = document.getElementById('gdBtnDangerLabel');
    const btn = document.getElementById('gdBtnDanger');

    const tick = setInterval(() => {
        remaining--;
        if (remaining <= 0) {
            clearInterval(tick);
            btn.disabled = false;
            btn.innerHTML = `<span class="material-icons">delete</span> ${t('gd.modal.delete', 'Eliminar')}`;
            btn.onclick = () => submitRemoveGuard(guardId);
        } else {
            label.textContent = `${t('gd.modal.delete_cooldown', 'Eliminar')} (${remaining})`;
        }
    }, 1000);
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

// ─── STEP 2: LOAD / RENDER ACTIVE LOCATIONS SECTION ────────
function loadActiveLocations() {
    gdCall('GetAllActiveLocations', {}, (resp) => {
        const section = document.getElementById('gdLocSection');
        const countEl = document.getElementById('gdLocCount');
        const emptyEl = document.getElementById('gdLocEmpty');
        const gridEl = document.getElementById('gdLocGrid');

        if (!resp.success || !resp.data || resp.data.length === 0) {
            section.style.display = 'none';
            return;
        }

        section.style.display = 'block';

        // Si la guardia es un borrador → mostrar picker editable
        if (gd_guard && gd_guard.isDraft) {
            emptyEl.style.display = 'none';
            countEl.textContent = resp.data.length;
            renderLocationPicker(gridEl, resp.data);
            return;
        }

        // Si está confirmada → mostrar estado de solicitudes
        if (gd_guard && gd_guard.isConfirmed) {
            gdCall('GetActiveGuardLocations', {}, (locResp) => {
                const items = locResp.success ? (locResp.data || []) : [];
                countEl.textContent = items.length;
                if (items.length === 0) {
                    emptyEl.style.display = 'flex';
                    gridEl.innerHTML = '';
                } else {
                    emptyEl.style.display = 'none';
                    gridEl.innerHTML = items.map(loc => renderLocationCard(loc)).join('');
                }
            });
            return;
        }

        // Sin guardia → ocultar
        section.style.display = 'none';
    });
}

function renderLocationPicker(gridEl, allLocations) {
    // Cargar las ya guardadas para marcar solo esas
    gdCall('GetActiveGuardLocations', {}, (savedResp) => {
        const savedIds = new Set(
            (savedResp.success && savedResp.data && savedResp.data.length > 0)
                ? savedResp.data.map(l => l.locationId)
                : []
        );
        const defaultAll = savedIds.size === 0;
        const checkedCount = defaultAll ? allLocations.length : savedIds.size;

        gridEl.innerHTML = `
            <div class="gd-loc-picker-inline">
                <div class="gd-loc-picker-header">
                    <span class="material-icons">warehouse</span>
                    <span id="gdLocPickerCount">${t('gd.modal.loc_title', 'Locaciones involucradas')} (${checkedCount}/${allLocations.length})</span>
                    <button type="button" class="gd-loc-toggle-all" onclick="gdToggleAllLocations(true)">${t('gd.modal.select_all', 'Todas')}</button>
                    <button type="button" class="gd-loc-toggle-all" onclick="gdToggleAllLocations(false)">${t('gd.modal.deselect_all', 'Ninguna')}</button>
                </div>
                <p class="gd-field-hint" style="margin:4px 0 12px;">
                    ${t('gd.modal.loc_hint', 'Desmarca las locaciones que no tendrán operaciones en este cierre.')}
                </p>
                <div class="gd-field-error" id="gdLocError" style="display:none;">
                    ${t('gd.err.loc_required', 'Selecciona al menos una locación.')}
                </div>
                <div class="gd-loc-picker-list">
                    ${allLocations.map(loc => {
            const isChecked = defaultAll || savedIds.has(loc.locationId);
            return `<label class="gd-loc-check-item">
                            <input type="checkbox" class="gd-loc-checkbox"
                                   value="${loc.locationId}" ${isChecked ? 'checked' : ''}
                                   onchange="gdUpdateLocCount()" />
                            <span class="gd-loc-check-name">${escapeHtml(loc.locationName)}</span>
                        </label>`;
        }).join('')}
                </div>
                <div class="gd-loc-picker-footer">
                    <button type="button" class="gd-btn-confirm" id="gdBtnSaveLocs"
                            onclick="submitSaveLocations()">
                        <span class="material-icons">check</span>
                        ${t('gd.loc.save_btn', 'Confirmar locaciones')}
                    </button>
                </div>
            </div>`;
    });
}

function gdToggleAllLocations(checked) {
    document.querySelectorAll('.gd-loc-checkbox').forEach(cb => cb.checked = checked);
    gdUpdateLocCount();
}

function gdUpdateLocCount() {
    const total = document.querySelectorAll('.gd-loc-checkbox').length;
    const checked = document.querySelectorAll('.gd-loc-checkbox:checked').length;
    const lbl = document.getElementById('gdLocPickerCount');
    if (lbl) lbl.textContent = `${t('gd.modal.loc_title', 'Locaciones involucradas')} (${checked}/${total})`;
    const errEl = document.getElementById('gdLocError');
    if (errEl) errEl.style.display = checked === 0 ? 'block' : 'none';
}

// ─── STEP 2 SUBMIT: SAVE LOCATIONS ─────────────────────────
function submitSaveLocations() {
    if (!gd_guard) return;
    const guardId = gd_guard.guardId;
    const checked = [...document.querySelectorAll('.gd-loc-checkbox:checked')].map(cb => parseInt(cb.value));

    const errEl = document.getElementById('gdLocError');
    if (checked.length === 0) {
        if (errEl) errEl.style.display = 'block';
        return;
    }
    if (errEl) errEl.style.display = 'none';

    const btn = document.getElementById('gdBtnSaveLocs');
    btn.disabled = true;
    btn.innerHTML = `<span class="material-icons gd-spin">autorenew</span> ${t('gd.modal.saving', 'Guardando...')}`;

    gdCall('SaveGuardLocations', { guardId, locationIds: checked }, (resp) => {
        if (resp.success) {
            showToast(t('gd.toast.locs_saved', 'Locaciones guardadas. Ya puedes crear la guardia.'), 'success');
            loadGuardStatus();
        } else {
            showToast(resp.message || t('gd.toast.error', 'Error.'), 'error');
            btn.disabled = false;
            btn.innerHTML = `<span class="material-icons">check</span> ${t('gd.loc.save_btn', 'Confirmar locaciones')}`;
        }
    });
}

function renderLocationCard(loc) {
    const statusMeta = {
        'Pending': { cls: 'gd-loc-pending', icon: 'pending', label: t('gd.loc.status.pending', 'Pendiente') },
        'Approved': { cls: 'gd-loc-approved', icon: 'check_circle', label: t('gd.loc.status.approved', 'Aprobado') },
        'Rejected': { cls: 'gd-loc-rejected', icon: 'cancel', label: t('gd.loc.status.rejected', 'Rechazado') },
        'NoRequest': { cls: 'gd-loc-norequest', icon: 'hourglass_empty', label: t('gd.loc.status.norequest', 'Sin solicitud aún') }
    };
    const meta = statusMeta[loc.status] || statusMeta['NoRequest'];

    const reviewRow = (loc.status !== 'Pending' && loc.status !== 'NoRequest' && loc.reviewedBy)
        ? `<div class="gd-loc-row">
               <span class="material-icons gd-loc-row-icon">rate_review</span>
               <span>${escapeHtml(loc.reviewedBy)} · ${escapeHtml(loc.reviewedAt)}</span>
           </div>`
        : '';

    const requestRow = loc.requestedBy
        ? `<div class="gd-loc-row">
               <span class="material-icons gd-loc-row-icon">person</span>
               <span>${escapeHtml(loc.requestedBy)} · ${escapeHtml(loc.requestedAt)}</span>
           </div>`
        : '';

    const notesRow = loc.reviewNotes
        ? `<div class="gd-loc-notes">${escapeHtml(loc.reviewNotes)}</div>`
        : '';

    return `
    <div class="gd-loc-card ${meta.cls}">
        <div class="gd-loc-card-header">
            <span class="gd-loc-name">${escapeHtml(loc.locationName)}</span>
            <span class="gd-loc-status-badge ${meta.cls}-badge">
                <span class="material-icons">${meta.icon}</span>
                ${meta.label}
            </span>
        </div>
        <div class="gd-loc-card-body">
            ${requestRow}
            ${reviewRow}
            ${notesRow}
        </div>
    </div>`;
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