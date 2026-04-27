// ===================================
// processes.js — Módulo IT / Procesos
// ===================================

// ─── AJAX helper ────────────────────────────────────────────
function prCall(method, data, cb) {
    $.ajax({
        type: 'POST',
        url: window.PageWebMethodBase + method,
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        data: JSON.stringify(data),
        success: function (res) { cb(null, res.d || res); },
        error:   function ()    { cb('err'); }
    });
}

// ─── INIT ────────────────────────────────────────────────────
$(function () {
    loadConfig();
});

function loadConfig() {
    prCall('GetProcessesConfig', {}, function (err, data) {
        if (err || !data.success) {
            showBadge(false);
            return;
        }
        applyConfirmacionCierre(data.confirmacionCierreEnabled);
        applyRecipient('ConfirmacionCierre', data.confirmacionCierreRecipient || '');
        refreshBadge();
    });
}

// ─── TOGGLE ─────────────────────────────────────────────────
function setProcessEnabled(processKey, enabled) {
    prCall('SetProcessEnabled', { processKey: processKey, enabled: enabled }, function (err, data) {
        if (err || !data.success) {
            showToast(window.I18n ? window.I18n.t('pr.toast_error') : 'Error.', 'error');
            if (processKey === 'ConfirmacionCierre')
                document.getElementById('prToggleConfirmacionCierre').checked = !enabled;
            return;
        }
        if (processKey === 'ConfirmacionCierre') applyConfirmacionCierre(enabled);
        refreshBadge();
        var key = enabled ? 'pr.toast_enabled' : 'pr.toast_disabled';
        showToast(window.I18n ? window.I18n.t(key) : (enabled ? 'Activado.' : 'Desactivado.'), 'success');
    });
}

// ─── RECIPIENT ───────────────────────────────────────────────
var _recipientTimers = {};

function debounceSetRecipient(processKey, value) {
    clearTimeout(_recipientTimers[processKey]);
    var okIcon = document.getElementById('prRecipientOkIcon');
    if (okIcon) okIcon.style.display = 'none';
    _recipientTimers[processKey] = setTimeout(function () {
        prCall('SetProcessRecipient', { processKey: processKey, recipient: value }, function (err, data) {
            if (!err && data.success && okIcon) {
                okIcon.style.display = value ? 'inline' : 'none';
            }
        });
    }, 700);
}

function applyRecipient(processKey, value) {
    if (processKey === 'ConfirmacionCierre') {
        var input  = document.getElementById('prRecipientConfirmacionCierre');
        var okIcon = document.getElementById('prRecipientOkIcon');
        if (input)  input.value = value;
        if (okIcon) okIcon.style.display = value ? 'inline' : 'none';
    }
}

// ─── TEST ────────────────────────────────────────────────────
function testProcess(processKey) {
    var btn = event && event.target ? event.target.closest('button') : null;
    if (btn) btn.disabled = true;

    prCall('TestProcess', { processKey: processKey }, function (err, data) {
        if (btn) btn.disabled = false;
        if (err || !data.success) {
            var msg = (data && data.message) || (window.I18n ? window.I18n.t('pr.toast_test_error') : 'Error al ejecutar.');
            showToast(msg, 'error');
            return;
        }
        showToast(window.I18n ? window.I18n.t('pr.toast_test_sent') : 'Prueba ejecutada.', 'success');
    });
}

// ─── RENDER HELPERS ─────────────────────────────────────────
function applyConfirmacionCierre(enabled) {
    var checkbox = document.getElementById('prToggleConfirmacionCierre');
    var label    = document.getElementById('prToggleConfirmacionCierreText');
    if (!checkbox) return;
    checkbox.checked = enabled;
    var onKey  = 'pr.toggle_on';
    var offKey = 'pr.toggle_off';
    label.textContent = window.I18n
        ? window.I18n.t(enabled ? onKey : offKey)
        : (enabled ? 'Activo' : 'Inactivo');
    if (window.I18n) label.setAttribute('data-translate-key', enabled ? onKey : offKey);
}

function refreshBadge() {
    var badge = document.getElementById('prStatusBadge');
    var text  = document.getElementById('prStatusText');
    if (!badge || !text) return;

    var anyEnabled = document.getElementById('prToggleConfirmacionCierre').checked;
    badge.className = 'pr-header-badge ' + (anyEnabled ? 'pr-badge-on' : 'pr-badge-off');

    var key = anyEnabled ? 'pr.badge_active' : 'pr.badge_inactive';
    text.textContent = window.I18n ? window.I18n.t(key) : (anyEnabled ? 'Procesos activos' : 'Sin procesos activos');
    if (window.I18n) text.setAttribute('data-translate-key', key);
}

function showBadge(ok) {
    var badge = document.getElementById('prStatusBadge');
    if (badge) badge.className = 'pr-header-badge ' + (ok ? 'pr-badge-on' : 'pr-badge-off');
}

// ─── TOAST ───────────────────────────────────────────────────
function showToast(msg, type) {
    if (window.showDashboardToast) { window.showDashboardToast(msg, type); return; }
    var el = document.createElement('div');
    el.style.cssText = 'position:fixed;bottom:24px;right:24px;padding:12px 20px;border-radius:8px;' +
        'background:' + (type === 'error' ? '#ef4444' : '#16a34a') + ';color:#fff;font-size:14px;z-index:9999;';
    el.textContent = msg;
    document.body.appendChild(el);
    setTimeout(function () { el.remove(); }, 3000);
}
