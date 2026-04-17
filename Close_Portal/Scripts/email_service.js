/* =========================================================
   email_service.js — Email Service IT module
   ========================================================= */

if (!window.AppRoot) {
    (function () {
        var path = window.location.pathname;
        var idx = path.toLowerCase().indexOf('/pages/');
        window.AppRoot = idx !== -1 ? path.substring(0, idx + 1) : '/';
    })();
}

function esT(key) { return (window.I18n && window.I18n.t) ? window.I18n.t(key) : key; }
function esTP(key, p0) { return esT(key).replace('{0}', p0); }

var _esLastConfig = null;

document.addEventListener('DOMContentLoaded', () => loadConfig());

document.addEventListener('i18n:applied', () => {
    if (!_esLastConfig) return;
    renderServiceControl(_esLastConfig);
    renderGroups(_esLastConfig.groups || []);
    renderAlerts(_esLastConfig.alerts || [], _esLastConfig.availableGroups || []);
    renderSmtp(_esLastConfig.smtp);
    updateStatusBadge(_esLastConfig);
});

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
            esToast(esT('common.error'), 'error');
        }
    });
}

// ─── LOAD CONFIG ───────────────────────────────────────────
function loadConfig() {
    esCall('GetEmailConfig', {}, (resp) => {
        if (!resp.success) { esToast(resp.message, 'error'); return; }
        _esLastConfig = resp;
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
    const text  = document.getElementById('esStatusText');
    if (!resp.notificationsEnabled) {
        badge.className  = 'es-header-badge es-badge-off';
        text.textContent = esT('email.status_off');
    } else if (resp.testMode) {
        badge.className  = 'es-header-badge es-badge-test';
        text.textContent = esT('email.status_test');
    } else {
        badge.className  = 'es-header-badge es-badge-on';
        text.textContent = esT('email.status_on');
    }
}

// ─── SERVICE CONTROL ───────────────────────────────────────
function renderServiceControl(resp) {
    const mainToggle = document.getElementById('esMainToggle');
    const testToggle = document.getElementById('esTestToggle');
    const testWrap   = document.getElementById('esTestEmailWrap');
    const testInput  = document.getElementById('esTestEmail');

    mainToggle.checked = resp.notificationsEnabled;
    document.getElementById('esMainToggleText').textContent =
        resp.notificationsEnabled ? esT('email.toggle_on') : esT('email.toggle_off');

    testToggle.checked = resp.testMode;
    document.getElementById('esTestToggleText').textContent =
        resp.testMode ? esT('email.toggle_active') : esT('email.toggle_off');

    testWrap.style.display = resp.testMode ? 'flex' : 'none';
    if (resp.testRecipient) testInput.value = resp.testRecipient;

    updateMainCard(resp.notificationsEnabled);
    updateTestCard(resp.testMode);
}

function updateMainCard(on) {
    const c = document.getElementById('esMainCard');
    if (c) c.className = 'es-control-card' + (on ? ' es-card-on' : ' es-card-off');
    const t = document.getElementById('esMainToggleText');
    if (t) t.textContent = on ? esT('email.toggle_on') : esT('email.toggle_off');
}

function updateTestCard(on) {
    const c = document.getElementById('esTestCard');
    if (c) c.className = 'es-control-card' + (on ? ' es-card-test' : '');
    const t = document.getElementById('esTestToggleText');
    if (t) t.textContent = on ? esT('email.toggle_active') : esT('email.toggle_off');
    const w = document.getElementById('esTestEmailWrap');
    if (w) w.style.display = on ? 'flex' : 'none';
}

function toggleNotifications(enabled) {
    esCall('SetNotificationsEnabled', { enabled }, (resp) => {
        if (resp.success) {
            updateMainCard(enabled);
            esToast(enabled ? esT('email.toast_notif_on') : esT('email.toast_notif_off'),
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
            esToast(enabled ? esT('email.toast_test_mode_on') : esT('email.toast_test_mode_off'), 'info');
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

function getDynamicTypeLabel(type) {
    const map = { role: esT('email.group_dynamic_role'), spot: esT('email.group_dynamic_spot') };
    return map[type] || type;
}

function renderGroups(groups) {
    const grid = document.getElementById('esGroupsGrid');

    if (!groups || groups.length === 0) {
        grid.innerHTML = `
        <div class="es-empty-groups">
            <span class="material-icons">inbox</span>
            <p>${esT('email.groups_empty')}</p>
        </div>
        <button type="button" class="es-btn-add-group" onclick="openGroupModal()">
            <span class="material-icons">add</span> ${esT('email.group_create_first')}
        </button>`;
        return;
    }

    grid.innerHTML = groups.map(g => {
        const color = COLOR_MAP[g.color] || '#6366f1';

        if (g.isDynamic) {
            const typeLabel = getDynamicTypeLabel(g.groupType);
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
                ${esT('email.group_dynamic_note')}
            </div>
        </div>`;
        }

        const memberPills = (g.members || []).length > 0
            ? g.members.map(m => `
                <span class="es-member-pill">
                    <span class="es-member-email">${escH(m.email)}</span>
                    ${m.displayName ? `<span class="es-member-name">${escH(m.displayName)}</span>` : ''}
                    <button type="button" class="es-member-remove"
                            onclick="removeMember(${m.memberId}, ${g.groupId})"
                            title="${esT('email.group_remove_title')}">
                        <span class="material-icons">close</span>
                    </button>
                </span>`).join('')
            : `<span class="es-members-empty">${esT('email.group_no_members')}</span>`;

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
                            title="${esT('email.group_edit_title')}">
                        <span class="material-icons">edit</span>
                    </button>
                    <button type="button" class="es-btn-icon es-btn-icon-danger"
                            onclick="confirmDeleteGroup(${g.groupId}, '${escH(g.label)}')"
                            title="${esT('email.group_remove_title')}">
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
                           placeholder="${esT('email.group_field_name')} (${esT('email.group_field_desc').toLowerCase()})" />
                    <button type="button" class="es-btn-add-member"
                            onclick="addMember(${g.groupId})">
                        <span class="material-icons">person_add</span>
                        ${esT('email.group_add_member')}
                    </button>
                </div>
            </div>
        </div>`;
    }).join('') + `
    <button type="button" class="es-btn-add-group" onclick="openGroupModal()">
        <span class="material-icons">add</span> ${esT('email.group_new')}
    </button>`;
}

// ─── ADD MEMBER ────────────────────────────────────────────
function addMember(groupId) {
    const emailEl = document.getElementById(`newEmail-${groupId}`);
    const nameEl  = document.getElementById(`newName-${groupId}`);
    const email   = emailEl?.value?.trim() || '';
    const name    = nameEl?.value?.trim()  || '';

    if (!email) { emailEl.focus(); return; }

    esCall('AddMember', { groupId, email, displayName: name }, (resp) => {
        if (resp.success) {
            emailEl.value = '';
            if (nameEl) nameEl.value = '';
            esToast(`${email} ${esT('email.toast_member_added')}`, 'success');
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
            esToast(esT('email.toast_member_removed'), 'info');
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
    const icons  = ['group', 'emergency', 'computer', 'support_agent', 'notifications', 'business',
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
                <h3>${isEdit ? esT('email.group_modal_edit') : esT('email.group_modal_new')}</h3>
                <button type="button" class="es-modal-close" onclick="esCloseModal()">
                    <span class="material-icons">close</span>
                </button>
            </div>
            <div class="es-modal-body">
                <div class="es-modal-error" id="esModalError" style="display:none;"></div>
                ${!isEdit ? `
                <div class="es-field">
                    <label>${esT('email.group_field_key')} <span class="es-field-hint">${esT('email.group_field_key_hint')}</span></label>
                    <input type="text" id="mgKey" class="es-input" placeholder="GroupKey" />
                </div>` : ''}
                <div class="es-field">
                    <label>${esT('email.group_field_name')}</label>
                    <input type="text" id="mgLabel" class="es-input"
                           value="${escH(label || '')}" placeholder="Ej: Equipo Finanzas" />
                </div>
                <div class="es-field">
                    <label>${esT('email.group_field_desc')}</label>
                    <input type="text" id="mgDesc" class="es-input"
                           value="${escH(description || '')}" placeholder="Cuándo se usa este grupo" />
                </div>
                <div class="es-field">
                    <label>${esT('email.group_field_color')}</label>
                    <div class="es-color-picker">${colorOptions}</div>
                </div>
                <div class="es-field">
                    <label>${esT('email.group_field_icon')}</label>
                    <div class="es-icon-picker">${iconOptions}</div>
                </div>
            </div>
            <div class="es-modal-footer">
                <button type="button" class="es-btn-cancel" onclick="esCloseModal()">${esT('common.cancel')}</button>
                <button type="button" class="es-btn-confirm" id="esMgSaveBtn"
                        onclick="saveGroup(${groupId || 'null'})">
                    <span class="material-icons">${isEdit ? 'save' : 'add'}</span>
                    ${isEdit ? esT('email.group_btn_save') : esT('email.group_btn_create')}
                </button>
            </div>
        </div>
    </div>`);
}

function saveGroup(groupId) {
    const label  = document.getElementById('mgLabel')?.value?.trim() || '';
    const desc   = document.getElementById('mgDesc')?.value?.trim()  || '';
    const icon   = document.querySelector('input[name="groupIcon"]:checked')?.value  || 'group';
    const color  = document.querySelector('input[name="groupColor"]:checked')?.value || 'blue';
    const errEl  = document.getElementById('esModalError');
    const btn    = document.getElementById('esMgSaveBtn');

    if (!label) {
        if (errEl) { errEl.style.display = 'block'; errEl.textContent = esT('email.group_err_name'); }
        return;
    }

    btn.disabled = true;
    btn.innerHTML = `<span class="material-icons es-spin">autorenew</span> ${esT('email.group_btn_saving')}`;

    if (groupId) {
        esCall('UpdateGroup', { groupId, label, description: desc, icon, color }, (resp) => {
            if (resp.success) { esCloseModal(); esToast(esT('email.toast_group_updated'), 'success'); loadConfig(); }
            else {
                btn.disabled = false;
                btn.innerHTML = `<span class="material-icons">save</span> ${esT('email.group_btn_save')}`;
                if (errEl) { errEl.style.display = 'block'; errEl.textContent = resp.message; }
            }
        });
    } else {
        const key = document.getElementById('mgKey')?.value?.trim() || '';
        if (!key) {
            if (errEl) { errEl.style.display = 'block'; errEl.textContent = esT('email.group_err_key'); }
            btn.disabled = false;
            btn.innerHTML = `<span class="material-icons">add</span> ${esT('email.group_btn_create')}`;
            return;
        }
        esCall('CreateGroup', { groupKey: key, label, description: desc, icon, color }, (resp) => {
            if (resp.success) { esCloseModal(); esToast(esT('email.toast_group_created'), 'success'); loadConfig(); }
            else {
                btn.disabled = false;
                btn.innerHTML = `<span class="material-icons">add</span> ${esT('email.group_btn_create')}`;
                if (errEl) { errEl.style.display = 'block'; errEl.textContent = resp.message; }
            }
        });
    }
}

function confirmDeleteGroup(groupId, label) {
    if (!confirm(esTP('email.confirm_delete_group', label))) return;
    esCall('DeleteGroup', { groupId }, (resp) => {
        if (resp.success) { esToast(esTP('email.toast_group_deleted', label), 'info'); loadConfig(); }
        else esToast(resp.message, 'error');
    });
}

// ─── ALERTS ────────────────────────────────────────────────
function renderAlerts(alerts, availableGroups) {
    const tbody = document.getElementById('esAlertsBody');
    if (!alerts || alerts.length === 0) {
        tbody.innerHTML = `<tr><td colspan="4" style="text-align:center;color:var(--text-muted);padding:24px;">${esT('email.alerts_empty')}</td></tr>`;
        return;
    }

    tbody.innerHTML = alerts.map(a => {
        let recipientCell;
        if (a.configurableRecipient) {
            const options = (availableGroups || []).map(g =>
                `<option value="${escH(g.groupKey)}" ${g.groupKey === a.groupKey ? 'selected' : ''}>${escH(g.label)}</option>`
            ).join('');
            recipientCell = `
                <div class="es-recipient-select-wrap">
                    <span class="material-icons es-recipient-icon">group</span>
                    <select class="es-recipient-select"
                            id="recipient-${escH(a.key)}"
                            onchange="saveAlertGroupKey('${escH(a.key)}', this)">
                        <option value="">${esT('email.alert_no_group')}</option>
                        ${options}
                    </select>
                </div>`;
        } else {
            recipientCell = `
                <div class="es-recipient-fixed">
                    <span class="material-icons es-recipient-icon">info</span>
                    <span>${escH(a.fixedRecipientDesc || '—')}</span>
                </div>`;
        }

        const hasThreshold   = a.thresholdMinutes != null;
        const currentHours   = hasThreshold ? Math.round(a.thresholdMinutes / 60) : null;
        const hLabel         = (h) => h === 1 ? esT('email.alert_threshold_hour') : esT('email.alert_threshold_hours');
        const thresholdCell  = hasThreshold ? `
            <div class="es-threshold-block">
                <div class="es-threshold-current">
                    <span class="material-icons">schedule</span>
                    ${esT('email.alert_threshold_current')} <strong id="threshold-display-${escH(a.key)}">${currentHours} ${hLabel(currentHours)}</strong>
                </div>
                <div class="es-threshold-editor">
                    <span class="es-threshold-label">${esT('email.alert_threshold_change')}</span>
                    <input type="number" class="es-threshold-input"
                           id="threshold-${escH(a.key)}"
                           value="${currentHours}" min="1" max="720" />
                    <span class="es-threshold-unit">${esT('email.alert_threshold_unit')}</span>
                    <button type="button" class="es-btn-threshold-save"
                            onclick="saveThreshold('${escH(a.key)}')">
                        <span class="material-icons">save</span> ${esT('email.alert_threshold_save')}
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
                    <span class="es-toggle-label">${a.enabled ? esT('email.alert_active') : esT('email.alert_inactive')}</span>
                </label>
            </td>
            <td>
                <button type="button" class="es-btn-test"
                        onclick="sendTestEmail('${escH(a.key)}', '${escH(a.label)}')">
                    <span class="material-icons">send</span> ${esT('email.alert_test_btn')}
                </button>
            </td>
        </tr>`;
    }).join('');
}

function saveAlertGroupKey(alertKey, selectEl) {
    const groupKey = selectEl.value;
    esCall('SetAlertGroupKey', { alertKey, groupKey }, (resp) => {
        if (resp.success)
            esToast(esT('email.toast_recipient_updated'), 'success');
        else {
            esToast(resp.message || esT('common.error'), 'error');
            loadConfig();
        }
    });
}

function saveThreshold(alertKey) {
    const input = document.getElementById(`threshold-${alertKey}`);
    if (!input) return;
    const hours = parseInt(input.value, 10);
    if (isNaN(hours) || hours < 1 || hours > 720) {
        esToast(esT('email.toast_threshold_error'), 'error');
        return;
    }
    esCall('SetAlertThreshold', { alertKey, thresholdHours: hours }, (resp) => {
        if (resp.success) {
            const hLabel  = hours === 1 ? esT('email.alert_threshold_hour') : esT('email.alert_threshold_hours');
            const display = document.getElementById(`threshold-display-${alertKey}`);
            if (display) display.textContent = `${hours} ${hLabel}`;
            esToast(esTP('email.toast_threshold_saved', `${hours} ${hLabel}`), 'success');
        } else {
            esToast(resp.message || esT('common.error'), 'error');
        }
    });
}

function setAlertEnabled(key, enabled) {
    esCall('SetAlertEnabled', { alertKey: key, enabled }, (resp) => {
        if (resp.success) {
            esToast(enabled ? esT('email.toast_alert_enabled') : esT('email.toast_alert_disabled'), 'info');
            loadConfig();
        } else esToast(resp.message, 'error');
    });
}

function setBulkAlerts(enabled) {
    esCall('SetBulkAlerts', { enabled }, (resp) => {
        if (resp.success) {
            esToast(enabled ? esT('email.toast_bulk_on') : esT('email.toast_bulk_off'), 'info');
            loadConfig();
        } else esToast(resp.message, 'error');
    });
}

// ─── TEST EMAIL ────────────────────────────────────────────
function sendTestEmail(alertKey, alertLabel) {
    const recipient = document.getElementById('esTestEmail')?.value?.trim() || '';
    if (!recipient) {
        esToast(esT('email.toast_test_no_email'), 'error'); return;
    }
    esToast(esTP('email.toast_test_sending', alertLabel), 'info');
    esCall('SendTestEmail', { alertKey, overrideRecipient: recipient }, (resp) => {
        if (resp.success) esToast(esTP('email.toast_test_sent', resp.sentTo), 'success');
        else esToast(resp.message || esT('email.toast_test_error'), 'error');
    });
}

// ─── SMTP ──────────────────────────────────────────────────
function renderSmtp(smtp) {
    const grid = document.getElementById('esSmtpGrid');
    if (!smtp) return;
    const fields = [
        { label: esT('email.smtp_host'), value: smtp.host, icon: 'dns' },
        { label: esT('email.smtp_port'), value: smtp.port, icon: 'settings_ethernet' },
        { label: esT('email.smtp_user'), value: smtp.user, icon: 'person' },
        { label: esT('email.smtp_from'), value: smtp.from, icon: 'alternate_email' },
        { label: esT('email.smtp_ssl'),  value: smtp.ssl,  icon: 'lock' },
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
