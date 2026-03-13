// ============================================================================
// validate_request.js
// ============================================================================

let vr_allRequests = [];
let vr_statusFilter = 'Pending';
let vr_locationFilter = '';

// ── Init ──────────────────────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', function () {
    loadRequests();
});

// ── Cargar solicitudes ────────────────────────────────────────────────────────
function loadRequests() {
    document.getElementById('vrRequestList').innerHTML =
        '<div class="vr-loading"><span class="material-icons vr-spin">autorenew</span> Cargando solicitudes...</div>';

    $.ajax({
        type: 'POST', url: 'ValidateRequest.aspx/GetRequests', data: '{}',
        contentType: 'application/json; charset=utf-8', dataType: 'json',
        success: function (resp) {
            var d = resp.d;
            if (d.success) {
                vr_allRequests = d.data;
                populateLocationFilter();
                updateTabCounts();
                renderRequests();
            } else {
                showListError(d.message);
            }
        },
        error: function () { showListError('Error de comunicación.'); }
    });
}

// ── Poblar selector de locaciones ─────────────────────────────────────────────
function populateLocationFilter() {
    var seen = {};
    var sel = document.getElementById('vrLocationFilter');
    while (sel.options.length > 1) sel.remove(1);

    vr_allRequests.forEach(function (r) {
        if (!seen[r.locationId]) {
            seen[r.locationId] = true;
            var opt = document.createElement('option');
            opt.value = r.locationId;
            opt.textContent = r.locationName;
            sel.appendChild(opt);
        }
    });
}

// ── Contar por estado para los tabs ──────────────────────────────────────────
function updateTabCounts() {
    var counts = { Pending: 0, Approved: 0, Rejected: 0 };
    vr_allRequests.forEach(function (r) {
        if (counts[r.status] !== undefined) counts[r.status]++;
    });
    document.getElementById('vrCountPending').textContent = counts.Pending;
    document.getElementById('vrCountApproved').textContent = counts.Approved;
    document.getElementById('vrCountRejected').textContent = counts.Rejected;

    document.getElementById('vrCount').textContent = counts.Pending > 0
        ? counts.Pending + ' pendiente' + (counts.Pending > 1 ? 's' : '')
        : vr_allRequests.length;
}

// ── Filtros ───────────────────────────────────────────────────────────────────
function setStatusFilter(status) {
    vr_statusFilter = status;
    document.querySelectorAll('.vr-tab').forEach(function (t) {
        t.classList.toggle('active', t.getAttribute('data-filter') === status);
    });
    renderRequests();
}

function applyFilters() {
    vr_locationFilter = document.getElementById('vrLocationFilter').value;
    renderRequests();
}

// ── Renderizar lista ──────────────────────────────────────────────────────────
function renderRequests() {
    var filtered = vr_allRequests.filter(function (r) {
        var matchStatus = r.status === vr_statusFilter;
        var matchLocation = !vr_locationFilter || String(r.locationId) === String(vr_locationFilter);
        return matchStatus && matchLocation;
    });

    var container = document.getElementById('vrRequestList');

    if (filtered.length === 0) {
        var label = {
            Pending: 'solicitudes pendientes',
            Approved: 'solicitudes aprobadas',
            Rejected: 'solicitudes rechazadas'
        };
        container.innerHTML =
            '<div class="vr-empty">' +
            '<span class="material-icons">inbox</span>' +
            '<p>Sin ' + (label[vr_statusFilter] || 'resultados') + '</p>' +
            '</div>';
        return;
    }

    container.innerHTML = '<div class="vr-request-list">' +
        filtered.map(function (r) { return renderRequestItem(r); }).join('') +
        '</div>';
}

function renderRequestItem(r) {
    var isPending = r.status === 'Pending';

    var statusLabels = { Pending: 'Pendiente', Approved: 'Aprobada', Rejected: 'Rechazada' };
    var statusLabel = statusLabels[r.status] || r.status;

    // OMS codes como pills
    var omsHtml = r.omsLabel && r.omsLabel !== '—'
        ? '<div class="vr-oms-row">' +
        r.omsLabel.split(',').map(function (c) {
            return '<span class="vr-oms-pill">' + escHtml(c.trim()) + '</span>';
        }).join('') +
        '</div>'
        : '';

    // Notas del solicitante
    var notesHtml = r.notes
        ? '<div class="vr-request-notes">' + escHtml(r.notes) + '</div>'
        : '';

    // Resultado de revisión (para Approved/Rejected)
    var reviewHtml = '';
    if (!isPending) {
        var icon = r.status === 'Approved' ? 'check_circle' : 'cancel';
        var cssType = r.status === 'Approved' ? 'approved' : 'rejected';
        var who = r.reviewedByName ? escHtml(r.reviewedByName) : 'Manager';
        var when = r.reviewedAt ? ' · ' + r.reviewedAt : '';
        var rnotes = r.reviewNotes ? ' — ' + escHtml(r.reviewNotes) : '';
        reviewHtml =
            '<div class="vr-review-result ' + cssType + '">' +
            '<span class="material-icons">' + icon + '</span>' +
            '<span>' + who + when + rnotes + '</span>' +
            '</div>';
    }

    // Botones solo para Pending
    var actionsHtml = isPending
        ? '<div class="vr-request-actions">' +
        '  <button type="button" class="vr-btn-approve" onclick="openReviewModal(' + r.requestId + ',\'Approved\')">' +
        '    <span class="material-icons">check_circle</span>Aprobar' +
        '  </button>' +
        '  <button type="button" class="vr-btn-reject" onclick="openReviewModal(' + r.requestId + ',\'Rejected\')">' +
        '    <span class="material-icons">cancel</span>Rechazar' +
        '  </button>' +
        '</div>'
        : '';

    return '<div class="vr-request-item" id="vr-item-' + r.requestId + '">' +
        '<div class="vr-request-info">' +
        '<div class="vr-request-top">' +
        '<span class="vr-request-id">#' + r.requestId + '</span>' +
        '<span class="vr-location-tag">' + escHtml(r.locationName) + '</span>' +
        '<span class="vr-status-badge ' + r.status + '">' + statusLabel + '</span>' +
        '<span class="vr-request-date">' + r.createdAt + '</span>' +
        '</div>' +
        omsHtml +
        '<div class="vr-requester">' +
        '<span class="material-icons">person</span>' +
        escHtml(r.requesterName) +
        '<span class="vr-requester-email">· ' + escHtml(r.requesterEmail) + '</span>' +
        '</div>' +
        notesHtml +
        reviewHtml +
        '</div>' +
        actionsHtml +
        '</div>';
}

// ── Modal de revisión ─────────────────────────────────────────────────────────
function openReviewModal(requestId, action) {
    var req = vr_allRequests.find(function (r) { return r.requestId === requestId; });
    if (!req) return;

    var isApprove = action === 'Approved';
    var headerCss = isApprove ? 'approve' : 'reject';
    var headerIcon = isApprove ? 'check_circle' : 'cancel';
    var headerText = isApprove ? 'Aprobar solicitud' : 'Rechazar solicitud';
    var btnCss = isApprove ? 'approve' : 'reject';
    var btnIcon = isApprove ? 'check_circle' : 'cancel';
    var btnText = isApprove ? 'Confirmar aprobación' : 'Confirmar rechazo';

    var omsLine = req.omsLabel && req.omsLabel !== '—'
        ? '<br/><strong>OMS:</strong> ' + escHtml(req.omsLabel)
        : '';

    var html =
        '<div class="vr-overlay" id="vrOverlay" onclick="closeReviewModalOnBg(event)">' +
        '<div class="vr-modal">' +
        '<div class="vr-modal-header ' + headerCss + '">' +
        '<span class="material-icons">' + headerIcon + '</span>' +
        '<span class="vr-modal-title">' + headerText + '</span>' +
        '<button type="button" class="vr-modal-close" onclick="closeReviewModal()">' +
        '<span class="material-icons">close</span>' +
        '</button>' +
        '</div>' +
        '<div class="vr-modal-body">' +
        '<div class="vr-modal-summary">' +
        '<strong>Solicitud #' + req.requestId + '</strong><br/>' +
        '<strong>Locación:</strong> ' + escHtml(req.locationName) +
        omsLine + '<br/>' +
        '<strong>Solicitante:</strong> ' + escHtml(req.requesterName) + ' (' + escHtml(req.requesterEmail) + ')' +
        (req.notes ? '<br/><strong>Notas:</strong> ' + escHtml(req.notes) : '') +
        '</div>' +
        '<label class="vr-modal-label">' +
        'Comentarios de revisión <span>(opcional)</span>' +
        '</label>' +
        '<textarea id="vrReviewNotes" class="vr-modal-textarea" rows="3" maxlength="500" ' +
        'placeholder="Agrega observaciones o instrucciones para el solicitante..."></textarea>' +
        '</div>' +
        '<div class="vr-modal-footer">' +
        '<span class="vr-modal-msg" id="vrModalMsg"></span>' +
        '<button type="button" class="vr-btn-cancel" onclick="closeReviewModal()">Cancelar</button>' +
        '<button type="button" class="vr-btn-confirm ' + btnCss + '" id="vrBtnConfirm" ' +
        'onclick="submitReview(' + requestId + ',\'' + action + '\')">' +
        '<span class="material-icons">' + btnIcon + '</span>' +
        btnText +
        '</button>' +
        '</div>' +
        '</div>' +
        '</div>';

    document.body.insertAdjacentHTML('beforeend', html);
    document.getElementById('vrReviewNotes').focus();
}

function closeReviewModal() {
    var overlay = document.getElementById('vrOverlay');
    if (overlay) overlay.remove();
}

function closeReviewModalOnBg(e) {
    if (e.target.id === 'vrOverlay') closeReviewModal();
}

// ── Enviar revisión ───────────────────────────────────────────────────────────
function submitReview(requestId, action) {
    var notes = document.getElementById('vrReviewNotes').value.trim();
    var btnConf = document.getElementById('vrBtnConfirm');
    var msgEl = document.getElementById('vrModalMsg');

    btnConf.disabled = true;
    btnConf.innerHTML = '<span class="material-icons vr-spin">autorenew</span> Procesando...';
    msgEl.className = 'vr-modal-msg';
    msgEl.style.display = 'none';

    $.ajax({
        type: 'POST', url: 'ValidateRequest.aspx/ReviewRequest',
        data: JSON.stringify({ requestId: requestId, action: action, reviewNotes: notes }),
        contentType: 'application/json; charset=utf-8', dataType: 'json',
        success: function (resp) {
            var d = resp.d;
            if (d.success) {
                closeReviewModal();
                loadRequests();
            } else {
                btnConf.disabled = false;
                var isApprove = action === 'Approved';
                btnConf.innerHTML =
                    '<span class="material-icons">' + (isApprove ? 'check_circle' : 'cancel') + '</span>' +
                    (isApprove ? 'Confirmar aprobación' : 'Confirmar rechazo');
                msgEl.textContent = d.message || 'Error al procesar.';
                msgEl.className = 'vr-modal-msg error';
                msgEl.style.display = 'block';
            }
        },
        error: function () {
            btnConf.disabled = false;
            msgEl.textContent = 'Error de comunicación. Intenta nuevamente.';
            msgEl.className = 'vr-modal-msg error';
            msgEl.style.display = 'block';
        }
    });
}

// ── Helpers ───────────────────────────────────────────────────────────────────
function showListError(msg) {
    document.getElementById('vrRequestList').innerHTML =
        '<div class="vr-empty"><span class="material-icons">error_outline</span><p>' + escHtml(msg) + '</p></div>';
}

function escHtml(str) {
    return String(str || '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}