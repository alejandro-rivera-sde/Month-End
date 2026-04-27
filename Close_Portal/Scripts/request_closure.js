// ============================================================================
// request_closure.js
// ============================================================================

var rc_allHistory = [];
var rc_histFilter = 'all';
var rc_managerId = 0;

function rcT(key) { return (window.I18n && window.I18n.t(key)) || key; }

// ── Init ──────────────────────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', function () {
    loadMyLocations();
    loadHistory();
    initCharCounter();
});

// ── Cargar locaciones del usuario ─────────────────────────────────────────────
function loadMyLocations() {
    $.ajax({
        type: 'POST', url: window.PageWebMethodBase + 'GetMyLocations', data: '{}',
        contentType: 'application/json; charset=utf-8', dataType: 'json',
        success: function (resp) {
            var d = resp.d;
            var sel = document.getElementById('rcLocation');
            sel.innerHTML = '<option value="" data-translate-key="rc.location.select">' +
                rcT('rc.location.select') + '</option>';

            if (d.success && d.data && d.data.length > 0) {
                d.data.forEach(function (loc) {
                    var opt = document.createElement('option');
                    opt.value = loc.locationId;
                    opt.textContent = loc.locationName;
                    sel.appendChild(opt);
                });
                sel.onchange = function () { onLocationChange(this.value); };
            } else {
                sel.innerHTML = '<option value="" data-translate-key="rc.location.none">' +
                    rcT('rc.location.none') + '</option>';
            }
        },
        error: function () {
            document.getElementById('rcLocation').innerHTML =
                '<option value="" data-translate-key="rc.location.load_error">' +
                rcT('rc.location.load_error') + '</option>';
        }
    });
}

// ── Cambio de locacion seleccionada ──────────────────────────────────────────
function onLocationChange(locationId) {
    var card = document.getElementById('rcManagerCard');
    var noMgr = document.getElementById('rcNoManager');
    var btnSend = document.getElementById('rcBtnSend');

    card.style.display = 'none';
    noMgr.style.display = 'none';
    btnSend.disabled = true;
    rc_managerId = 0;

    if (!locationId) return;

    $.ajax({
        type: 'POST', url: window.PageWebMethodBase + 'GetManagerForLocation',
        data: JSON.stringify({ locationId: parseInt(locationId) }),
        contentType: 'application/json; charset=utf-8', dataType: 'json',
        success: function (resp) {
            var d = resp.d;
            if (!d.success) { noMgr.style.display = 'flex'; return; }

            if (d.data) {
                rc_managerId = d.data.managerId;
                document.getElementById('rcManagerName').textContent = d.data.managerName || '';
                document.getElementById('rcManagerEmail').textContent = d.data.managerEmail || '';
                card.style.display = 'flex';
                btnSend.disabled = false;
            } else {
                noMgr.style.display = 'flex';
            }
        },
        error: function () { noMgr.style.display = 'flex'; }
    });
}

// ── Enviar solicitud ──────────────────────────────────────────────────────────
function sendRequest() {
    var locationId = parseInt(document.getElementById('rcLocation').value);
    var notes = document.getElementById('rcNotes').value.trim();

    if (!locationId) {
        showFormMsg(rcT('rc.err.location_required'), 'error');
        return;
    }
    if (!rc_managerId) {
        showFormMsg(rcT('rc.err.no_manager'), 'error');
        return;
    }

    var btn = document.getElementById('rcBtnSend');
    btn.disabled = true;
    btn.innerHTML = '<span class="material-icons rc-spin">autorenew</span> ' + rcT('rc.btn.sending');
    hideFormMsg();

    $.ajax({
        type: 'POST', url: window.PageWebMethodBase + 'SubmitRequest',
        data: JSON.stringify({ locationId: locationId, notes: notes }),
        contentType: 'application/json; charset=utf-8', dataType: 'json',
        success: function (resp) {
            var d = resp.d;
            btn.disabled = false;
            btn.innerHTML = '<span class="material-icons">send</span> ' + rcT('rc.btn.send');

            if (d.success) {
                showFormMsg(d.message, 'success');
                document.getElementById('rcLocation').value = '';
                document.getElementById('rcNotes').value = '';
                document.getElementById('rcCharCount').textContent = '0';
                document.getElementById('rcManagerCard').style.display = 'none';
                document.getElementById('rcNoManager').style.display = 'none';
                btn.disabled = true;
                rc_managerId = 0;
                loadMyLocations();
                loadHistory();
            } else {
                showFormMsg(d.message || rcT('rc.toast.error'), 'error');
            }
        },
        error: function () {
            btn.disabled = false;
            btn.innerHTML = '<span class="material-icons">send</span> ' + rcT('rc.btn.send');
            showFormMsg(rcT('rc.err.comm_error'), 'error');
        }
    });
}

// ── Historial ─────────────────────────────────────────────────────────────────
function loadHistory() {
    document.getElementById('rcHistoryList').innerHTML =
        '<div class="rc-loading"><span class="material-icons rc-spin">autorenew</span>' +
        ' <span data-translate-key="common.loading">' + rcT('common.loading') + '</span></div>';

    $.ajax({
        type: 'POST', url: window.PageWebMethodBase + 'GetMyHistory', data: '{}',
        contentType: 'application/json; charset=utf-8', dataType: 'json',
        success: function (resp) {
            var d = resp.d;
            if (d.success) {
                rc_allHistory = d.data;
                renderHistory();
            } else {
                document.getElementById('rcHistoryList').innerHTML =
                    '<div class="rc-empty"><span class="material-icons">error_outline</span>' +
                    '<span>' + escHtml(d.message) + '</span></div>';
            }
        },
        error: function () {
            document.getElementById('rcHistoryList').innerHTML =
                '<div class="rc-empty"><span class="material-icons">error_outline</span>' +
                '<span data-translate-key="rc.err.history_error">' + rcT('rc.err.history_error') + '</span></div>';
        }
    });
}

function renderHistory() {
    var filtered = rc_histFilter === 'all'
        ? rc_allHistory
        : rc_allHistory.filter(function (r) { return r.status === rc_histFilter; });

    document.getElementById('rcCount').textContent = rc_allHistory.length;

    var container = document.getElementById('rcHistoryList');

    if (filtered.length === 0) {
        container.innerHTML =
            '<div class="rc-empty"><span class="material-icons">inbox</span>' +
            '<span data-translate-key="rc.history.empty">' + rcT('rc.history.empty') + '</span></div>';
        return;
    }

    var statusKeys = { Pending: 'rc.status.pending', Approved: 'rc.status.approved', Rejected: 'rc.status.rejected' };
    var statusFallback = { Pending: 'Pendiente', Approved: 'Aprobada', Rejected: 'Rechazada' };

    container.innerHTML = '<div class="rc-history-list">' +
        filtered.map(function (r) {
            var key = statusKeys[r.status] || '';
            var label = key ? rcT(key) : (statusFallback[r.status] || r.status);

            var notesHtml = r.notes
                ? '<div class="rc-history-notes">' + escHtml(r.notes) + '</div>'
                : '';

            var reviewHtml = (r.reviewNotes || r.reviewedByName)
                ? '<div class="rc-review-notes"><strong>' + escHtml(r.reviewedByName || 'Administrador') + ':</strong> ' +
                escHtml(r.reviewNotes || '-') +
                (r.reviewedAt ? ' <span style="color:var(--text-muted)">- ' + r.reviewedAt + '</span>' : '') +
                '</div>'
                : '';

            return '<div class="rc-history-item">' +
                '<div class="rc-history-top">' +
                '<span class="rc-location-tag">' + escHtml(r.locationName) + '</span>' +
                '<span class="rc-status-badge ' + r.status + '"' + (key ? ' data-translate-key="' + key + '"' : '') + '>' + label + '</span>' +
                '<span class="rc-history-date">' + r.createdAt + '</span>' +
                '</div>' +
                notesHtml +
                reviewHtml +
                '</div>';
        }).join('') +
        '</div>';
}

function filterHistory(filter) {
    rc_histFilter = filter;
    document.querySelectorAll('.rc-tab').forEach(function (t) {
        t.classList.toggle('active', t.getAttribute('data-filter') === filter);
    });
    renderHistory();
}

// ── Char counter ──────────────────────────────────────────────────────────────
function initCharCounter() {
    var textarea = document.getElementById('rcNotes');
    var counter = document.getElementById('rcCharCount');
    if (textarea && counter) {
        textarea.addEventListener('input', function () {
            counter.textContent = this.value.length;
        });
    }
}

// ── Form messages ─────────────────────────────────────────────────────────────
function showFormMsg(msg, type) {
    var el = document.getElementById('rcFormMessage');
    el.textContent = msg;
    el.className = 'rc-form-message ' + type;
    el.style.display = 'block';
}

function hideFormMsg() {
    document.getElementById('rcFormMessage').style.display = 'none';
}

// ── Helpers ───────────────────────────────────────────────────────────────────
function escHtml(str) {
    return String(str || '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

// ── SignalR ───────────────────────────────────────────────────────────────────
// Registrar ANTES de que dashboard_layout.js llame a hub.start()
(function () {
    if (typeof $.connection === 'undefined') return;
    if (typeof $.connection.locationHub === 'undefined') return;

    var hub = $.connection.locationHub;

    hub.client.requestReviewed = function (data) {
        var statusKey = { Approved: 'rc.status.approved', Rejected: 'rc.status.rejected', Reopened: 'rc.status.pending' };
        var label = statusKey[data.newStatus] ? rcT(statusKey[data.newStatus]) : data.newStatus;
        if (typeof showOsNotification === 'function') {
            showOsNotification(rcT('rc.history.title') + ' — ' + label, data.locationName + ' - ' + data.reviewedBy);
        }
        if (typeof refreshBadge === 'function') {
            refreshBadge();
        }
        loadHistory();
        loadMyLocations();
    };
}());
