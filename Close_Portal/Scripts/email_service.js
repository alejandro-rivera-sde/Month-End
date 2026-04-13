/* =========================================================
   email_service.js — Email Service IT module
   ========================================================= */

// AppRoot: raíz de la app, independiente del virtual directory
if (!window.AppRoot) {
    (function () {
        var path = window.location.pathname;
        var idx = path.toLowerCase().indexOf('/pages/');
        window.AppRoot = idx !== -1 ? path.substring(0, idx + 1) : '/';
    })();
}

document.addEventListener('DOMContentLoaded', () => loadConfig());

// ─── AJAX ──────────────────────────────────────────────────
function esCall(method, data, onSuccess) {
    $.ajax({
        type: 'POST',
        url: window.AppRoot + 'Pages/IT/EmailService.aspx/' + method,
        data: JSON.stringify(data),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: (resp) => onSuccess(resp.d !== undefined ? resp.d : resp),
        error: (xhr) => {
            console.error('[email_service.js]', method, xhr.responseText);
            esToast('Error de comunicación con el servidor.', 'error');
        }
    });
}

// ─── LOAD CONFIG ───────────────────────────────────────────
function loadConfig() {
    esCall('GetEmailConfig', {}, (resp) => {
        if (!resp.success) { esToast(resp.message, 'error'); return; }
        renderServiceControl(resp);
        renderGroups(resp.groups || []);
        renderAlerts(resp.alerts || [], resp.availableGroups || []);
        renderSmtp(resp.smtp);
        updateStatusBadge(resp);
    });
}

// ─── STATUS BADGE ──────────────────────────────────────────
function updateStatusBadge(resp) {
    const badge = document.getElementById('esStatusBadge');
    const text = document.getElementById('esStatusText');
    if (!resp.notificationsEnabled) {
        badge.className = 'es-header-badge es-badge-off';
        text.textContent = 'Servicio desactivado';
    } else if (resp.testMode) {
        badge.className = 'es-header-badge es-badge-test';
        text.textContent = 'Modo prueba activo';
    } else {
        badge.className = 'es-header-badge es-badge-on';
        text.textContent = 'Servicio activo';
    }
}

// ─── SERVICE CONTROL ───────────────────────────────────────
function renderServiceControl(resp) {
    const mainToggle = document.getElementById('esMainToggle');
    const testToggle = document.getElementById('esTestToggle');
    const testWrap = document.getElementById('esTestEmailWrap');
    const testInput = document.getElementById('esTestEmail');

    mainToggle.checked = resp.notificationsEnabled;
    document.getElementById('esMainToggleText').textContent =
        resp.notificationsEnabled ? 'Activado' : 'Desactivado';

    testToggle.checked = resp.testMode;
    document.getElementById('esTestToggleText').textContent =
        resp.testMode ? 'Activo' : 'Desactivado';

    testWrap.style.display = resp.testMode ? 'flex' : 'none';
    if (resp.testRecipient) testInput.value = resp.testRecipient;

    updateMainCard(resp.notificationsEnabled);
    updateTestCard(resp.testMode);
}

function updateMainCard(on) {
    const c = document.getElementById('esMainCard');
    if (c) c.className = 'es-control-card' + (on ? ' es-card-on' : ' es-card-off');
    const t = document.getElementById('esMainToggleText');
    if (t) t.textContent = on ? 'Activado' : 'Desactivado';
}

function updateTestCard(on) {
    const c = document.getElementById('esTestCard');
    if (c) c.className = 'es-control-card' + (on ? ' es-card-test' : '');
    const t = document.getElementById('esTestToggleText');
    if (t) t.textContent = on ? 'Activo' : 'Desactivado';
    const w = document.getElementById('esTestEmailWrap');
    if (w) w.style.display = on ? 'flex' : 'none';
}

function toggleNotifications(enabled) {
    esCall('SetNotificationsEnabled', { enabled }, (resp) => {
        if (resp.success) {
            updateMainCard(enabled);
            esToast(enabled ? 'Notificaciones activadas.' : 'Notificaciones desactivadas.',
                enabled ? 'success' : 'info');
            loadConfig();
        } else {
            esToast(resp.message, 'error');
            document.getElementById('esMainToggle').checked = !enabled;
        }
    });
}

function toggleTestMode(enabled) {
    const recipient = document.getElementById('esTestEmail')?.value || '';
    esCall('SetTestMode', { enabled, testRecipient: recipient }, (resp) => {
        if (resp.success) {
            updateTestCard(enabled);
            esToast(enabled ? 'Modo prueba activado.' : 'Modo prueba desactivado.', 'info');
            loadConfig();
        } else {
            esToast(resp.message, 'error');
            document.getElementById('esTestToggle').checked = !enabled;
        }
    });
}

let _debounceTimer = null;
function debounceSaveTestEmail(value) {
    clearTimeout(_debounceTimer);
    _debounceTimer = setTimeout(() => {
        const enabled = document.getElementById('esTestToggle')?.checked || false;
        esCall('SetTestMode', { enabled, testRecipient: value }, (resp) => {
            const icon = document.getElementById('esTestEmailIcon');
            if (icon) icon.style.display = resp.success ? 'block' : 'none';
        });
    }, 700);
}

// ─── GROUPS ────────────────────────────────────────────────
const COLOR_MAP = {
    red: '#dc2626', blue: '#2563eb', green: '#16a34a',
    amber: '#d97706', purple: '#7c3aed', teal: '#0891b2', cyan: '#0891b2'
};

const DYNAMIC_TYPE_LABEL = { role: 'Dinámico · Rol', spot: 'Dinámico · Spot activo' };

function renderGroups(groups) {
    const grid = document.getElementById('esGroupsGrid');

    if (!groups || groups.length === 0) {
        grid.innerHTML = `
        <div class="es-empty-groups">
            <span class="material-icons">inbox</span>
            <p>Sin grupos configurados.</p>
        </div>
        <button type="button" class="es-btn-add-group" onclick="openGroupModal()">
            <span class="material-icons">add</span> Crear primer grupo
        </button>`;
        return;
    }

    grid.innerHTML = groups.map(g => {
        const color = COLOR_MAP[g.color] || '#6366f1';

        // ── Grupos dinámicos: solo lectura ──────────────────
        if (g.isDynamic) {
            const typeLabel = DYNAMIC_TYPE_LABEL[g.groupType] || 'Dinámico';
            return `
        <div class="es-group-card es-group-dynamic" data-group-id="${g.groupId}">
            <div class="es-group-header">
                <span class="material-icons es-group-icon" style="color:${color}">${escH(g.icon)}</span>
                <div class="es-group-info">
                    <div class="es-group-label">
                        ${escH(g.label)}
                        <span class="es-dynamic-badge">${escH(typeLabel)}</span>
                    </div>
                    <div class="es-group-desc">${escH(g.description)}</div>
                </div>
            </div>
            <div class="es-dynamic-note">
                <span class="material-icons" style="font-size:16px;vertical-align:middle;margin-right:6px;">info</span>
                Este grupo se resuelve automáticamente en tiempo real. No requiere configuración manual.
            </div>
        </div>`;
        }

        // ── Grupos estáticos: editable ──────────────────────
        const memberPills = (g.members || []).length > 0
            ? g.members.map(m => `
                <span class="es-member-pill">
                    <span class="es-member-email">${escH(m.email)}</span>
                    ${m.displayName ? `<span class="es-member-name">${escH(m.displayName)}</span>` : ''}
                    <button type="button" class="es-member-remove"
                            onclick="removeMember(${m.memberId}, ${g.groupId})"
                            title="Quitar">
                        <span class="material-icons">close</span>
                    </button>
                </span>`).join('')
            : `<span class="es-members-empty">Sin miembros</span>`;

        return `
        <div class="es-group-card" data-group-id="${g.groupId}">
            <div class="es-group-header">
                <span class="material-icons es-group-icon" style="color:${color}">${escH(g.icon)}</span>
                <div class="es-group-info">
                    <div class="es-group-label">${escH(g.label)}</div>
                    <div class="es-group-desc">${escH(g.description)}</div>
                </div>
                <span class="es-group-count">${(g.members || []).length}</span>
                <div class="es-group-actions">
                    <button type="button" class="es-btn-icon"
                            onclick="openGroupModal(${g.groupId}, '${escH(g.label)}', '${escH(g.description)}', '${escH(g.icon)}', '${escH(g.color)}')"
                            title="Editar grupo">
                        <span class="material-icons">edit</span>
                    </button>
                    <button type="button" class="es-btn-icon es-btn-icon-danger"
                            onclick="confirmDeleteGroup(${g.groupId}, '${escH(g.label)}')"
                            title="Eliminar grupo">
                        <span class="material-icons">delete_outline</span>
                    </button>
                </div>
            </div>
            <div class="es-members-area">
                <div class="es-member-pills" id="pills-${g.groupId}">${memberPills}</div>
                <div class="es-add-member-row">
                    <input type="email" class="es-input es-member-input"
                           id="newEmail-${g.groupId}"
                           placeholder="correo@dominio.com" />
                    <input type="text" class="es-input es-member-name-input"
                           id="newName-${g.groupId}"
                           placeholder="Nombre (opcional)" />
                    <button type="button" class="es-btn-add-member"
                            onclick="addMember(${g.groupId})">
                        <span class="material-icons">person_add</span>
                        Agregar
                    </button>
                </div>
            </div>
        </div>`;
    }).join('') + `
    <button type="button" class="es-btn-add-group" onclick="openGroupModal()">
        <span class="material-icons">add</span> Nuevo grupo
    </button>`;
}

// ─── ADD MEMBER ────────────────────────────────────────────
function addMember(groupId) {
    const emailEl = document.getElementById(`newEmail-${groupId}`);
    const nameEl = document.getElementById(`newName-${groupId}`);
    const email = emailEl?.value?.trim() || '';
    const name = nameEl?.value?.trim() || '';

    if (!email) { emailEl.focus(); return; }

    esCall('AddMember', { groupId, email, displayName: name }, (resp) => {
        if (resp.success) {
            emailEl.value = '';
            if (nameEl) nameEl.value = '';
            esToast(`${email} agregado al grupo.`, 'success');
            loadConfig();
        } else {
            esToast(resp.message, 'error');
        }
    });
}

// ─── REMOVE MEMBER ─────────────────────────────────────────
function removeMember(memberId, groupId) {
    esCall('RemoveMember', { memberId }, (resp) => {
        if (resp.success) {
            esToast('Correo eliminado del grupo.', 'info');
            loadConfig();
        } else {
            esToast(resp.message, 'error');
        }
    });
}

// ─── GROUP MODAL (crear / editar) ──────────────────────────
function openGroupModal(groupId, label, description, icon, color) {
    const isEdit = !!groupId;
    const colors = ['red', 'blue', 'green', 'amber', 'purple', 'teal'];
    const icons = ['group', 'emergency', 'computer', 'support_agent', 'notifications', 'business',
        'hub', 'mail_outline', 'send', 'corporate_fare'];

    const colorOptions = colors.map(c => {
        const hex = COLOR_MAP[c] || '#888';
        return `
        <label class="es-color-opt" title="${c}">
            <input type="radio" name="groupColor" value="${c}" ${(color || 'blue') === c ? 'checked' : ''}/>
            <span class="es-color-dot" style="background:${hex}"></span>
        </label>`;
    }).join('');

    const iconOptions = icons.map(i =>
        `<label class="es-icon-opt">
            <input type="radio" name="groupIcon" value="${i}" ${(icon || 'group') === i ? 'checked' : ''}/>
            <span class="material-icons">${i}</span>
        </label>`
    ).join('');

    document.body.insertAdjacentHTML('beforeend', `
    <div class="es-overlay" id="esOverlay" onclick="esCloseModal(event)">
        <div class="es-modal">
            <div class="es-modal-header">
                <span class="material-icons">group</span>
                <h3>${isEdit ? 'Editar grupo' : 'Nuevo grupo'}</h3>
                <button type="button" class="es-modal-close" onclick="esCloseModal()">
                    <span class="material-icons">close</span>
                </button>
            </div>
            <div class="es-modal-body">
                <div class="es-modal-error" id="esModalError" style="display:none;"></div>
                ${!isEdit ? `
                <div class="es-field">
                    <label>Clave única <span class="es-field-hint">(sin espacios, ej: NotifyFinance)</span></label>
                    <input type="text" id="mgKey" class="es-input" placeholder="GroupKey" />
                </div>` : ''}
                <div class="es-field">
                    <label>Nombre del grupo</label>
                    <input type="text" id="mgLabel" class="es-input"
                           value="${escH(label || '')}" placeholder="Ej: Equipo Finanzas" />
                </div>
                <div class="es-field">
                    <label>Descripción</label>
                    <input type="text" id="mgDesc" class="es-input"
                           value="${escH(description || '')}" placeholder="Cuándo se usa este grupo" />
                </div>
                <div class="es-field">
                    <label>Color</label>
                    <div class="es-color-picker">${colorOptions}</div>
                </div>
                <div class="es-field">
                    <label>Ícono</label>
                    <div class="es-icon-picker">${iconOptions}</div>
                </div>
            </div>
            <div class="es-modal-footer">
                <button type="button" class="es-btn-cancel" onclick="esCloseModal()">Cancelar</button>
                <button type="button" class="es-btn-confirm" id="esMgSaveBtn"
                        onclick="saveGroup(${groupId || 'null'})">
                    <span class="material-icons">${isEdit ? 'save' : 'add'}</span>
                    ${isEdit ? 'Guardar cambios' : 'Crear grupo'}
                </button>
            </div>
        </div>
    </div>`);
}

function saveGroup(groupId) {
    const label = document.getElementById('mgLabel')?.value?.trim() || '';
    const desc = document.getElementById('mgDesc')?.value?.trim() || '';
    const icon = document.querySelector('input[name="groupIcon"]:checked')?.value || 'group';
    const color = document.querySelector('input[name="groupColor"]:checked')?.value || 'blue';
    const errEl = document.getElementById('esModalError');
    const btn = document.getElementById('esMgSaveBtn');

    if (!label) {
        if (errEl) { errEl.style.display = 'block'; errEl.textContent = 'El nombre es requerido.'; }
        return;
    }

    btn.disabled = true;
    btn.innerHTML = '<span class="material-icons es-spin">autorenew</span> Guardando...';

    if (groupId) {
        esCall('UpdateGroup', { groupId, label, description: desc, icon, color }, (resp) => {
            if (resp.success) { esCloseModal(); esToast('Grupo actualizado.', 'success'); loadConfig(); }
            else {
                btn.disabled = false; btn.innerHTML = '<span class="material-icons">save</span> Guardar cambios';
                if (errEl) { errEl.style.display = 'block'; errEl.textContent = resp.message; }
            }
        });
    } else {
        const key = document.getElementById('mgKey')?.value?.trim() || '';
        if (!key) {
            if (errEl) { errEl.style.display = 'block'; errEl.textContent = 'La clave es requerida.'; }
            btn.disabled = false;
            btn.innerHTML = '<span class="material-icons">add</span> Crear grupo';
            return;
        }
        esCall('CreateGroup', { groupKey: key, label, description: desc, icon, color }, (resp) => {
            if (resp.success) { esCloseModal(); esToast('Grupo creado.', 'success'); loadConfig(); }
            else {
                btn.disabled = false; btn.innerHTML = '<span class="material-icons">add</span> Crear grupo';
                if (errEl) { errEl.style.display = 'block'; errEl.textContent = resp.message; }
            }
        });
    }
}

function confirmDeleteGroup(groupId, label) {
    if (!confirm(`¿Eliminar el grupo "${label}"? Esta acción no se puede deshacer.`)) return;
    esCall('DeleteGroup', { groupId }, (resp) => {
        if (resp.success) { esToast(`Grupo "${label}" eliminado.`, 'info'); loadConfig(); }
        else esToast(resp.message, 'error');
    });
}

// ─── ALERTS ────────────────────────────────────────────────
function renderAlerts(alerts, availableGroups) {
    const tbody = document.getElementById('esAlertsBody');
    if (!alerts || alerts.length === 0) {
        tbody.innerHTML = '<tr><td colspan="4" style="text-align:center;color:var(--text-muted);padding:24px;">Sin alertas configuradas.</td></tr>';
        return;
    }

    const groupOptions = (availableGroups || []).map(g =>
        `<option value="${escH(g.groupKey)}">${escH(g.label)}</option>`
    ).join('');

    tbody.innerHTML = alerts.map(a => {
        // ── Celda de destinatarios ──────────────────────────
        let recipientCell;
        if (a.configurableRecipient) {
            // Dropdown editable — selecciona qué grupo recibe esta alerta
            const options = (availableGroups || []).map(g =>
                `<option value="${escH(g.groupKey)}" ${g.groupKey === a.groupKey ? 'selected' : ''}>${escH(g.label)}</option>`
            ).join('');
            recipientCell = `
                <div class="es-recipient-select-wrap">
                    <span class="material-icons es-recipient-icon">group</span>
                    <select class="es-recipient-select"
                            id="recipient-${escH(a.key)}"
                            onchange="saveAlertGroupKey('${escH(a.key)}', this)">
                        <option value="">— Sin grupo asignado —</option>
                        ${options}
                    </select>
                </div>`;
        } else {
            // Texto fijo informativo — no editable
            recipientCell = `
                <div class="es-recipient-fixed">
                    <span class="material-icons es-recipient-icon">info</span>
                    <span>${escH(a.fixedRecipientDesc || '—')}</span>
                </div>`;
        }

        // ── Celda de threshold ──────────────────────────────
        const hasThreshold = a.thresholdMinutes != null;
        const currentHours = hasThreshold ? Math.round(a.thresholdMinutes / 60) : null;
        const thresholdCell = hasThreshold ? `
            <div class="es-threshold-block">
                <div class="es-threshold-current">
                    <span class="material-icons">schedule</span>
                    Intervalo actual: <strong id="threshold-display-${escH(a.key)}">${currentHours} hora${currentHours !== 1 ? 's' : ''}</strong>
                </div>
                <div class="es-threshold-editor">
                    <span class="es-threshold-label">Cambiar a:</span>
                    <input type="number" class="es-threshold-input"
                           id="threshold-${escH(a.key)}"
                           value="${currentHours}" min="1" max="720" />
                    <span class="es-threshold-unit">hrs</span>
                    <button type="button" class="es-btn-threshold-save"
                            onclick="saveThreshold('${escH(a.key)}')">
                        <span class="material-icons">save</span> Guardar
                    </button>
                </div>
            </div>` : '';

        return `
        <tr class="es-alert-row ${a.enabled ? '' : 'es-alert-disabled'}">
            <td>
                <div class="es-alert-name">
                    <span class="material-icons es-alert-icon">${escH(a.icon)}</span>
                    <div>
                        <div>${escH(a.label)}</div>
                        ${thresholdCell}
                    </div>
                </div>
            </td>
            <td>${recipientCell}</td>
            <td>
                <label class="es-toggle es-toggle-sm">
                    <input type="checkbox" ${a.enabled ? 'checked' : ''}
                           onchange="setAlertEnabled('${escH(a.key)}', this.checked)" />
                    <span class="es-toggle-track"><span class="es-toggle-thumb"></span></span>
                    <span class="es-toggle-label">${a.enabled ? 'Activa' : 'Inactiva'}</span>
                </label>
            </td>
            <td>
                <button type="button" class="es-btn-test"
                        onclick="sendTestEmail('${escH(a.key)}', '${escH(a.label)}')">
                    <span class="material-icons">send</span> Probar
                </button>
            </td>
        </tr>`;
    }).join('');
}

function saveAlertGroupKey(alertKey, selectEl) {
    const groupKey = selectEl.value;
    esCall('SetAlertGroupKey', { alertKey, groupKey }, (resp) => {
        if (resp.success)
            esToast(`Destinatario actualizado.`, 'success');
        else {
            esToast(resp.message || 'Error al guardar.', 'error');
            loadConfig(); // revertir
        }
    });
}

function saveThreshold(alertKey) {
    const input = document.getElementById(`threshold-${alertKey}`);
    if (!input) return;
    const hours = parseInt(input.value, 10);
    if (isNaN(hours) || hours < 1 || hours > 720) {
        esToast('El intervalo debe ser entre 1 y 720 horas.', 'error');
        return;
    }
    esCall('SetAlertThreshold', { alertKey, thresholdHours: hours }, (resp) => {
        if (resp.success) {
            const display = document.getElementById(`threshold-display-${alertKey}`);
            if (display) display.textContent = `${hours} hora${hours !== 1 ? 's' : ''}`;
            esToast(`Intervalo guardado: cada ${hours} hora${hours !== 1 ? 's' : ''}.`, 'success');
        } else {
            esToast(resp.message || 'Error al guardar el intervalo.', 'error');
        }
    });
}

function setAlertEnabled(key, enabled) {
    esCall('SetAlertEnabled', { alertKey: key, enabled }, (resp) => {
        if (resp.success) {
            esToast(enabled ? `Alerta activada.` : `Alerta desactivada.`, 'info');
            loadConfig();
        } else esToast(resp.message, 'error');
    });
}

function setBulkAlerts(enabled) {
    esCall('SetBulkAlerts', { enabled }, (resp) => {
        if (resp.success) {
            esToast(enabled ? 'Todas activadas.' : 'Todas desactivadas.', 'info');
            loadConfig();
        } else esToast(resp.message, 'error');
    });
}

// ─── TEST EMAIL ────────────────────────────────────────────
function sendTestEmail(alertKey, alertLabel) {
    const recipient = document.getElementById('esTestEmail')?.value?.trim() || '';
    if (!recipient) {
        esToast('Activa el modo prueba e ingresa un correo antes de probar.', 'error'); return;
    }
    esToast(`Enviando prueba de "${alertLabel}"...`, 'info');
    esCall('SendTestEmail', { alertKey, overrideRecipient: recipient }, (resp) => {
        if (resp.success) esToast(`Prueba enviada a ${resp.sentTo}`, 'success');
        else esToast(resp.message || 'Error al enviar la prueba.', 'error');
    });
}

// ─── SMTP ──────────────────────────────────────────────────
function renderSmtp(smtp) {
    const grid = document.getElementById('esSmtpGrid');
    if (!smtp) return;
    const fields = [
        { label: 'Host', value: smtp.host, icon: 'dns' },
        { label: 'Puerto', value: smtp.port, icon: 'settings_ethernet' },
        { label: 'Usuario', value: smtp.user, icon: 'person' },
        { label: 'Remitente', value: smtp.from, icon: 'alternate_email' },
        { label: 'SSL', value: smtp.ssl, icon: 'lock' },
    ];
    grid.innerHTML = fields.map(f => `
        <div class="es-smtp-field">
            <span class="material-icons es-smtp-icon">${escH(f.icon)}</span>
            <div>
                <div class="es-smtp-label">${escH(f.label)}</div>
                <div class="es-smtp-value">${escH(f.value || '—')}</div>
            </div>
        </div>`).join('');
}

// ─── MODAL HELPERS ─────────────────────────────────────────
function esCloseModal(e) {
    if (e && e.target?.id !== 'esOverlay') return;
    const el = document.getElementById('esOverlay');
    if (el) el.remove();
}

// ─── TOAST ─────────────────────────────────────────────────
function esToast(msg, type = 'info') {
    const el = document.getElementById('estoast');
    if (el) el.remove();
    const icon = type === 'success' ? 'check_circle' : type === 'error' ? 'error' : 'info';
    document.body.insertAdjacentHTML('beforeend', `
    <div id="estoast" class="es-toast ${type}">
        <span class="material-icons" style="font-size:18px;">${icon}</span>
        ${escH(msg)}
    </div>`);
    setTimeout(() => { document.getElementById('estoast')?.remove(); }, 4000);
}

// ─── UTILS ─────────────────────────────────────────────────
function escH(s) {
    if (!s && s !== 0) return '';
    return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;')
        .replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}