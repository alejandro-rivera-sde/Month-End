// ============================================================================
// request_closure.js
// ============================================================================

let rc_allHistory = [];
let rc_histFilter = 'all';
let rc_managerId = 0;    // Manager activo de la locación seleccionada

// ── Init ─────────────────────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', function () {
    loadMyLocations();
    loadHistory();
    initCharCounter();
});

// ── Cargar locaciones del usuario ─────────────────────────────────────────────
function loadMyLocations() {
    $.ajax({
        type: 'POST', url: 'RequestClosure.aspx/GetMyLocations', data: '{}',
        contentType: 'application/json; charset=utf-8', dataType: 'json',
        success: function (resp) {
            var d = resp.d;
            var sel = document.getElementById('rcLocation');
            sel.innerHTML = '<option value="">-- Selecciona una locación --</option>';

            if (d.success && d.data && d.data.length > 0) {
                d.data.forEach(function (loc) {
                    var opt = document.createElement('option');
                    opt.value = loc.locationId;
                    opt.textContent = loc.locationName + '  (' + loc.omsLabel + ')';
                    sel.appendChild(opt);
                });
                sel.onchange = function () { onLocationChange(this.value); };
            } else {
                sel.innerHTML = '<option value="">Sin locaciones asignadas</option>';
            }
        },
        error: function () {
            document.getElementById('rcLocation').innerHTML =
                '<option value="">Error al cargar locaciones</option>';
        }
    });
}

// ── Cambio de locación seleccionada ───────────────────────────────────────────
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
        type: 'POST', url: 'RequestClosure.aspx/GetManagerForLocation',
        data: JSON.stringify({ locationId: parseInt(locationId) }),
        contentType: 'application/json; charset=utf-8', dataType: 'json',
        success: function (resp) {
            var d = resp.d;
            if (!d.success) { noMgr.style.display = 'flex'; return; }

            if (d.data) {
                rc_managerId = d.data.managerId;
                document.getElementById('rcManagerName').textContent = d.data.managerName || '(sin nombre)';
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
        showFormMsg('Selecciona una locación primero.', 'error');
        return;
    }
    if (!rc_managerId) {
        showFormMsg('Esta locación no tiene un Manager asignado.', 'error');
        return;
    }

    var btn = document.getElementById('rcBtnSend');
    btn.disabled = true;
    btn.innerHTML = '<span class="material-icons rc-spin">autorenew</span> Enviando...';
    hideFormMsg();

    $.ajax({
        type: 'POST', url: 'RequestClosure.aspx/SubmitRequest',
        data: JSON.stringify({ locationId: locationId, notes: notes }),
        contentType: 'application/json; charset=utf-8', dataType: 'json',
        success: function (resp) {
            var d = resp.d;
            btn.disabled = false;
            btn.innerHTML = '<span class="material-icons">send</span> Enviar solicitud';

            if (d.success) {
                showFormMsg(d.message, 'success');
                // Reset form
                document.getElementById('rcLocation').value = '';
                document.getElementById('rcNotes').value = '';
                document.getElementById('rcCharCount').textContent = '0';
                document.getElementById('rcManagerCard').style.display = 'none';
                document.getElementById('rcNoManager').style.display = 'none';
                btn.disabled = true;
                rc_managerId = 0;
                loadHistory();
            } else {
                showFormMsg(d.message || 'Error al enviar la solicitud.', 'error');
            }
        },
        error: function () {
            btn.disabled = false;
            btn.innerHTML = '<span class="material-icons">send</span> Enviar solicitud';
            showFormMsg('Error de comunicación. Intenta nuevamente.', 'error');
        }
    });
}

// ── Historial ─────────────────────────────────────────────────────────────────
function loadHistory() {
    document.getElementById('rcHistoryList').innerHTML =
        '<div class="rc-loading"><span class="material-icons rc-spin">autorenew</span> Cargando...</div>';

    $.ajax({
        type: 'POST', url: 'RequestClosure.aspx/GetMyHistory', data: '{}',
        contentType: 'application/json; charset=utf-8', dataType: 'json',
        success: function (resp) {
            var d = resp.d;
            if (d.success) {
                rc_allHistory = d.data;
                renderHistory();
            } else {
                document.getElementById('rcHistoryList').innerHTML =
                    '<div class="rc-empty"><span class="material-icons">error_outline</span>' +
                    escHtml(d.message) + '</div>';
            }
        },
        error: function () {
            document.getElementById('rcHistoryList').innerHTML =
                '<div class="rc-empty"><span class="material-icons">error_outline</span>' +
                'Error al cargar historial.</div>';
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
            '<div class="rc-empty"><span class="material-icons">inbox</span>Sin solicitudes</div>';
        return;
    }

    var statusLabels = { Pending: 'Pendiente', Approved: 'Aprobada', Rejected: 'Rechazada' };

    container.innerHTML = '<div class="rc-history-list">' +
        filtered.map(function (r) {
            var label = statusLabels[r.status] || r.status;

            var notesHtml = r.notes
                ? '<div class="rc-history-notes">' + escHtml(r.notes) + '</div>'
                : '';

            var reviewHtml = (r.reviewNotes || r.reviewedByName)
                ? '<div class="rc-review-notes"><strong>' + escHtml(r.reviewedByName || 'Manager') + ':</strong> ' +
                escHtml(r.reviewNotes || '—') +
                (r.reviewedAt ? ' <span style="color:var(--text-muted)">· ' + r.reviewedAt + '</span>' : '') +
                '</div>'
                : '';

            // OMS label como pills
            var omsHtml = r.omsLabel && r.omsLabel !== '—'
                ? r.omsLabel.split(',').map(function (c) {
                    return '<span class="rc-oms-pill">' + escHtml(c.trim()) + '</span>';
                }).join('')
                : '';

            return '<div class="rc-history-item">' +
                '<div class="rc-history-top">' +
                '<span class="rc-location-tag">' + escHtml(r.locationName) + '</span>' +
                '<span class="rc-status-badge ' + r.status + '">' + label + '</span>' +
                '<span class="rc-history-date">' + r.createdAt + '</span>' +
                '</div>' +
                (omsHtml ? '<div class="rc-oms-row">' + omsHtml + '</div>' : '') +
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