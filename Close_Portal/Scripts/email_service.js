/* =========================================================
   email_service.js — Módulo de administración de Email Service
   Prefijo: es-
   ========================================================= */

// ─── INIT ──────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => loadConfig());

// ─── AJAX HELPER ───────────────────────────────────────────
function esCall(method, data, onSuccess) {
    $.ajax({
        type: 'POST',
        url: 'EmailService.aspx/' + method,
        data: JSON.stringify(data),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: (resp) => onSuccess(resp.d !== undefined ? resp.d : resp),
        error: (xhr) => {
            console.error('[email_service.js]', method, xhr.responseText);
            esShowToast('Error de comunicación con el servidor.', 'error');
        }
    });
}

// ─── LOAD CONFIG ───────────────────────────────────────────
function loadConfig() {
    esCall('GetEmailConfig', {}, (resp) => {
        if (!resp.success) { esShowToast(resp.message, 'error'); return; }
        renderServiceControl(resp);
        renderGroups(resp.groups || []);
        renderAlerts(resp.alerts || []);
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
    const mainText = document.getElementById('esMainToggleText');
    const testToggle = document.getElementById('esTestToggle');
    const testText = document.getElementById('esTestToggleText');
    const testWrap = document.getElementById('esTestEmailWrap');
    const testInput = document.getElementById('esTestEmail');

    mainToggle.checked = resp.notificationsEnabled;
    mainText.textContent = resp.notificationsEnabled ? 'Activado' : 'Desactivado';

    testToggle.checked = resp.testMode;
    testText.textContent = resp.testMode ? 'Activo' : 'Desactivado';

    if (resp.testMode) testWrap.style.display = 'flex';
    if (resp.testRecipient) testInput.value = resp.testRecipient;

    updateMainCard(resp.notificationsEnabled);
    updateTestCard(resp.testMode);
}

function updateMainCard(enabled) {
    const card = document.getElementById('esMainCard');
    if (card) card.className = 'es-control-card' + (enabled ? ' es-card-on' : ' es-card-off');
    const text = document.getElementById('esMainToggleText');
    if (text) text.textContent = enabled ? 'Activado' : 'Desactivado';
}

function updateTestCard(enabled) {
    const card = document.getElementById('esTestCard');
    if (card) card.className = 'es-control-card' + (enabled ? ' es-card-test' : '');
    const text = document.getElementById('esTestToggleText');
    if (text) text.textContent = enabled ? 'Activo' : 'Desactivado';
    const wrap = document.getElementById('esTestEmailWrap');
    if (wrap) wrap.style.display = enabled ? 'flex' : 'none';
}

// ─── TOGGLE NOTIFICATIONS ──────────────────────────────────
function toggleNotifications(enabled) {
    esCall('SetNotificationsEnabled', { enabled }, (resp) => {
        if (resp.success) {
            updateMainCard(enabled);
            esShowToast(enabled ? 'Notificaciones activadas.' : 'Notificaciones desactivadas.', enabled ? 'success' : 'info');
            loadConfig();
        } else {
            esShowToast(resp.message, 'error');
            document.getElementById('esMainToggle').checked = !enabled;
        }
    });
}

// ─── TOGGLE TEST MODE ──────────────────────────────────────
function toggleTestMode(enabled) {
    const recipient = document.getElementById('esTestEmail')?.value || '';
    esCall('SetTestMode', { enabled, testRecipient: recipient }, (resp) => {
        if (resp.success) {
            updateTestCard(enabled);
            esShowToast(enabled ? 'Modo prueba activado.' : 'Modo prueba desactivado.', 'info');
            loadConfig();
        } else {
            esShowToast(resp.message, 'error');
            document.getElementById('esTestToggle').checked = !enabled;
        }
    });
}

// ─── DEBOUNCE SAVE TEST EMAIL ──────────────────────────────
let _testEmailTimer = null;
function debounceSaveTestEmail(value) {
    clearTimeout(_testEmailTimer);
    _testEmailTimer = setTimeout(() => {
        const enabled = document.getElementById('esTestToggle')?.checked || false;
        esCall('SetTestMode', { enabled, testRecipient: value }, (resp) => {
            const icon = document.getElementById('esTestEmailIcon');
            if (icon) icon.style.display = resp.success ? 'block' : 'none';
        });
    }, 800);
}

// ─── RENDER GROUPS ─────────────────────────────────────────
function renderGroups(groups) {
    const grid = document.getElementById('esGroupsGrid');
    if (!groups || groups.length === 0) {
        grid.innerHTML = '<p style="color:var(--text-muted);font-size:13px;">Sin grupos configurados.</p>';
        return;
    }

    const colorMap = { red: '#dc2626', blue: '#2563eb', green: '#16a34a', amber: '#d97706' };

    grid.innerHTML = groups.map(g => {
        const color = colorMap[g.color] || 'var(--gd-accent)';
        const pills = (g.emails || []).length > 0
            ? g.emails.map(e => `<span class="es-email-pill">${escapeHtml(e)}</span>`).join('')
            : '<span style="color:var(--text-muted);font-size:12px;">Sin correos configurados</span>';

        return `
        <div class="es-group-card">
            <div class="es-group-header">
                <span class="material-icons es-group-icon" style="color:${color}">${escapeHtml(g.icon)}</span>
                <div>
                    <div class="es-group-label">${escapeHtml(g.label)}</div>
                    <div class="es-group-desc">${escapeHtml(g.desc)}</div>
                </div>
                <span class="es-group-count">${(g.emails || []).length}</span>
            </div>
            <div class="es-email-pills">${pills}</div>
        </div>`;
    }).join('');
}

// ─── RENDER ALERTS ─────────────────────────────────────────
function renderAlerts(alerts) {
    const tbody = document.getElementById('esAlertsBody');
    if (!alerts || alerts.length === 0) {
        tbody.innerHTML = '<tr><td colspan="4" style="text-align:center;color:var(--text-muted);padding:24px;">Sin alertas configuradas.</td></tr>';
        return;
    }

    tbody.innerHTML = alerts.map(a => `
        <tr class="es-alert-row ${a.enabled ? '' : 'es-alert-disabled'}">
            <td>
                <div class="es-alert-name">
                    <span class="material-icons es-alert-icon">${escapeHtml(a.icon)}</span>
                    ${escapeHtml(a.label)}
                </div>
            </td>
            <td class="es-alert-recipients">${escapeHtml(a.recipients)}</td>
            <td>
                <label class="es-toggle es-toggle-sm">
                    <input type="checkbox" ${a.enabled ? 'checked' : ''}
                           onchange="setAlertEnabled('${escapeHtml(a.key)}', this.checked)" />
                    <span class="es-toggle-track">
                        <span class="es-toggle-thumb"></span>
                    </span>
                    <span class="es-toggle-label">${a.enabled ? 'Activa' : 'Inactiva'}</span>
                </label>
            </td>
            <td>
                <button type="button" class="es-btn-test"
                        onclick="sendTestEmail('${escapeHtml(a.key)}', '${escapeHtml(a.label)}')">
                    <span class="material-icons">send</span>
                    Probar
                </button>
            </td>
        </tr>`).join('');
}

// ─── RENDER SMTP ───────────────────────────────────────────
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
            <span class="material-icons es-smtp-icon">${escapeHtml(f.icon)}</span>
            <div>
                <div class="es-smtp-label">${escapeHtml(f.label)}</div>
                <div class="es-smtp-value">${escapeHtml(f.value || '—')}</div>
            </div>
        </div>`).join('');
}

// ─── ALERT TOGGLE ──────────────────────────────────────────
function setAlertEnabled(key, enabled) {
    esCall('SetAlertEnabled', { alertKey: key, enabled }, (resp) => {
        if (resp.success) {
            esShowToast(enabled ? `Alerta "${key}" activada.` : `Alerta "${key}" desactivada.`, 'info');
            loadConfig();
        } else {
            esShowToast(resp.message, 'error');
        }
    });
}

function setBulkAlerts(enabled) {
    esCall('SetBulkAlerts', { enabled }, (resp) => {
        if (resp.success) {
            esShowToast(enabled ? 'Todas las alertas activadas.' : 'Todas las alertas desactivadas.', 'info');
            loadConfig();
        } else {
            esShowToast(resp.message, 'error');
        }
    });
}

// ─── SEND TEST EMAIL ───────────────────────────────────────
function sendTestEmail(alertKey, alertLabel) {
    const recipient = document.getElementById('esTestEmail')?.value?.trim() || '';

    if (!recipient) {
        esShowToast('Activa el modo prueba e ingresa un correo de destino antes de probar.', 'error');
        return;
    }

    esShowToast(`Enviando prueba de "${alertLabel}"...`, 'info');

    esCall('SendTestEmail', { alertKey, overrideRecipient: recipient }, (resp) => {
        if (resp.success) {
            esShowToast(`Prueba enviada a ${resp.sentTo}`, 'success');
        } else {
            esShowToast(resp.message || 'Error al enviar la prueba.', 'error');
        }
    });
}

// ─── TOAST ─────────────────────────────────────────────────
function esShowToast(message, type = 'info') {
    const existing = document.getElementById('estoast');
    if (existing) existing.remove();
    const icon = type === 'success' ? 'check_circle' : type === 'error' ? 'error' : 'info';
    document.body.insertAdjacentHTML('beforeend', `
    <div id="estoast" class="es-toast ${type}">
        <span class="material-icons" style="font-size:18px;">${icon}</span>
        ${escapeHtml(message)}
    </div>`);
    setTimeout(() => { const el = document.getElementById('estoast'); if (el) el.remove(); }, 4000);
}

// ─── UTILS ─────────────────────────────────────────────────
function escapeHtml(str) {
    if (!str && str !== 0) return '';
    return String(str)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;')
        .replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}