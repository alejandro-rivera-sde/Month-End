// ============================================================================
// validate_request.js
// ============================================================================

function vrT(key) {
    var lang = document.documentElement.getAttribute('data-language') || 'es';
    return (typeof translations !== 'undefined' && translations[lang] && translations[lang][key])
        ? translations[lang][key]
        : key;
}

var vr_allRequests = [];
var vr_statusFilter = 'Pending';
var vr_locationFilter = '';
var vr_closedRequests = [];

// ── Init ──────────────────────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', function () {
    loadRequests();
});

// ── Cargar solicitudes ────────────────────────────────────────────────────────
function loadRequests() {
    document.getElementById('vrRequestList').innerHTML =
        '<div class="vr-loading"><span class="material-icons vr-spin">autorenew</span> ' +
        vrT('vr.loading') + '</div>';

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
        error: function () { showListError(vrT('common.error')); }
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

// ── Contar por estado ─────────────────────────────────────────────────────────
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
        return r.status === vr_statusFilter &&
            (!vr_locationFilter || String(r.locationId) === String(vr_locationFilter));
    });

    var container = document.getElementById('vrRequestList');

    if (filtered.length === 0) {
        var labels = {
            Pending: vrT('vr.empty.pending'),
            Approved: vrT('vr.empty.approved'),
            Rejected: vrT('vr.empty.rejected')
        };
        container.innerHTML =
            '<div class="vr-empty"><span class="material-icons">inbox</span>' +
            '<p>' + (labels[vr_statusFilter] || vrT('vr.empty')) + '</p></div>';
        return;
    }

    container.innerHTML = '<div class="vr-request-list">' +
        filtered.map(function (r) { return renderRequestItem(r); }).join('') +
        '</div>';
}

function renderRequestItem(r) {
    var isPending = r.status === 'Pending';
    var statusLabels = {
        Pending: vrT('vr.status.pending'),
        Approved: vrT('vr.status.approved'),
        Rejected: vrT('vr.status.rejected')
    };
    var statusLabel = statusLabels[r.status] || r.status;

    var omsHtml = r.omsLabel && r.omsLabel !== '—'
        ? '<div class="vr-oms-row">' +
        r.omsLabel.split(',').map(function (c) {
            return '<span class="vr-oms-pill">' + escHtml(c.trim()) + '</span>';
        }).join('') + '</div>'
        : '';

    var notesHtml = r.notes
        ? '<div class="vr-request-notes">' + escHtml(r.notes) + '</div>' : '';

    var reviewHtml = '';
    if (!isPending) {
        var icon = r.status === 'Approved' ? 'check_circle' : 'cancel';
        var cssType = r.status === 'Approved' ? 'approved' : 'rejected';
        var who = r.reviewedByName ? escHtml(r.reviewedByName) : 'Manager';
        var when = r.reviewedAt ? ' · ' + r.reviewedAt : '';
        var rnotes = r.reviewNotes ? ' — ' + escHtml(r.reviewNotes) : '';
        reviewHtml = '<div class="vr-review-result ' + cssType + '">' +
            '<span class="material-icons">' + icon + '</span>' +
            '<span>' + who + when + rnotes + '</span></div>';
    }

    // Solo Pending tiene botones de acción — Approved y Rejected son solo lectura aquí
    var actionsHtml = isPending
        ? '<div class="vr-request-actions">' +
        '<button type="button" class="vr-btn-approve" onclick="openReviewModal(' + r.requestId + ',\'Approved\')">' +
        '<span class="material-icons">check_circle</span>' + vrT('vr.btn.approve') + '</button>' +
        '<button type="button" class="vr-btn-reject" onclick="openReviewModal(' + r.requestId + ',\'Rejected\')">' +
        '<span class="material-icons">cancel</span>' + vrT('vr.btn.reject') + '</button>' +
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
        '<div class="vr-requester"><span class="material-icons">person</span>' +
        escHtml(r.requesterName) +
        '<span class="vr-requester-email">· ' + escHtml(r.requesterEmail) + '</span></div>' +
        notesHtml + reviewHtml +
        '</div>' + actionsHtml + '</div>';
}

// ── Modal de revisión (Approve/Reject) ────────────────────────────────────────
function openReviewModal(requestId, action) {
    var req = vr_allRequests.find(function (r) { return r.requestId === requestId; });
    if (!req) return;

    var isApprove = action === 'Approved';
    var headerCss = isApprove ? 'approve' : 'reject';
    var headerIcon = isApprove ? 'check_circle' : 'cancel';
    var headerText = isApprove ? vrT('vr.modal.approve_title') : vrT('vr.modal.reject_title');
    var btnCss = isApprove ? 'approve' : 'reject';
    var btnIcon = isApprove ? 'check_circle' : 'cancel';
    var btnText = isApprove ? vrT('vr.modal.confirm_approve') : vrT('vr.modal.confirm_reject');

    var omsLine = req.omsLabel && req.omsLabel !== '—'
        ? '<br/><strong>' + vrT('vr.card.oms') + '</strong> ' + escHtml(req.omsLabel) : '';

    var html =
        '<div class="vr-overlay" id="vrOverlay" onclick="closeReviewModalOnBg(event)">' +
        '<div class="vr-modal">' +
        '<div class="vr-modal-header ' + headerCss + '">' +
        '<span class="material-icons">' + headerIcon + '</span>' +
        '<span class="vr-modal-title">' + headerText + '</span>' +
        '<button type="button" class="vr-modal-close" onclick="closeReviewModal()">' +
        '<span class="material-icons">close</span></button></div>' +
        '<div class="vr-modal-body">' +
        '<div class="vr-modal-summary">' +
        '<strong>' + vrT('vr.modal.request') + ' #' + req.requestId + '</strong><br/>' +
        '<strong>' + vrT('vr.card.location') + '</strong> ' + escHtml(req.locationName) +
        omsLine + '<br/>' +
        '<strong>' + vrT('vr.card.requested_by') + '</strong> ' + escHtml(req.requesterName) +
        ' (' + escHtml(req.requesterEmail) + ')' +
        (req.notes ? '<br/><strong>' + vrT('vr.card.notes') + '</strong> ' + escHtml(req.notes) : '') +
        '</div>' +
        '<label class="vr-modal-label">' + vrT('vr.modal.notes_label') + ' ' +
        (isApprove
            ? '<span>(' + vrT('vr.modal.optional') + ')</span>'
            : '<span class="vr-required">* ' + vrT('vr.modal.required_reject') + '</span>') +
        '</label>' +
        '<textarea id="vrReviewNotes" class="vr-modal-textarea" rows="3" maxlength="500" placeholder="' +
        (isApprove ? vrT('vr.modal.notes_placeholder_approve') : vrT('vr.modal.notes_placeholder_reject')) +
        '"></textarea></div>' +
        '<div class="vr-modal-footer">' +
        '<span class="vr-modal-msg" id="vrModalMsg"></span>' +
        '<button type="button" class="vr-btn-cancel" onclick="closeReviewModal()">' + vrT('vr.modal.cancel') + '</button>' +
        '<button type="button" class="vr-btn-confirm ' + btnCss + '" id="vrBtnConfirm" ' +
        'onclick="submitReview(' + requestId + ',\'' + action + '\')">' +
        '<span class="material-icons">' + btnIcon + '</span>' + btnText + '</button>' +
        '</div></div></div>';

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

    if (action === 'Rejected' && !notes) {
        msgEl.textContent = vrT('vr.modal.reject_required');
        msgEl.className = 'vr-modal-msg error';
        msgEl.style.display = 'block';
        document.getElementById('vrReviewNotes').focus();
        return;
    }

    btnConf.disabled = true;
    btnConf.innerHTML = '<span class="material-icons vr-spin">autorenew</span> ' + vrT('vr.modal.processing');
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
                $.ajax({
                    type: 'POST',
                    url: window.AppRoot + 'Pages/Main/Live.aspx/MarkAsRead',
                    data: JSON.stringify({ referenceId: requestId, type: 'new_request' }),
                    contentType: 'application/json; charset=utf-8', dataType: 'json',
                    complete: function () { refreshBadge(); }
                });
            } else {
                btnConf.disabled = false;
                btnConf.innerHTML = '<span class="material-icons">' +
                    (action === 'Approved' ? 'check_circle' : 'cancel') + '</span>' +
                    (action === 'Approved' ? vrT('vr.modal.confirm_approve') : vrT('vr.modal.confirm_reject'));
                msgEl.textContent = d.message || vrT('common.error');
                msgEl.className = 'vr-modal-msg error';
                msgEl.style.display = 'block';
            }
        },
        error: function () {
            btnConf.disabled = false;
            msgEl.textContent = vrT('common.error');
            msgEl.className = 'vr-modal-msg error';
            msgEl.style.display = 'block';
        }
    });
}

// ══════════════════════════════════════════════════════════════════════════════
// PANEL DE SOLICITUDES CERRADAS
// ══════════════════════════════════════════════════════════════════════════════

function openClosedPanel() {
    if (document.getElementById('vrClosedOverlay')) {
        document.getElementById('vrClosedOverlay').classList.add('active');
        loadClosedRequests();
        return;
    }

    document.body.insertAdjacentHTML('beforeend',
        '<div class="vr-closed-overlay" id="vrClosedOverlay">' +
        '<div class="vr-closed-panel">' +
        '<div class="vr-closed-header">' +
        '<span class="material-icons">lock</span>' +
        '<h3>' + vrT('vr.closed.title') + '</h3>' +
        '<button type="button" class="vr-modal-close" onclick="closeClosedPanel()">' +
        '<span class="material-icons">close</span></button></div>' +
        '<p class="vr-closed-subtitle">' + vrT('vr.closed.subtitle') + '</p>' +
        '<div class="vr-closed-list" id="vrClosedList">' +
        '<div class="vr-loading"><span class="material-icons vr-spin">autorenew</span> ' + vrT('vr.loading') + '</div>' +
        '</div></div></div>'
    );

    document.getElementById('vrClosedOverlay').addEventListener('click', function (e) {
        if (e.target === this) closeClosedPanel();
    });

    document.getElementById('vrClosedOverlay').classList.add('active');
    loadClosedRequests();
}

function closeClosedPanel() {
    var overlay = document.getElementById('vrClosedOverlay');
    if (overlay) overlay.classList.remove('active');
}

function loadClosedRequests() {
    var list = document.getElementById('vrClosedList');
    list.innerHTML = '<div class="vr-loading"><span class="material-icons vr-spin">autorenew</span> ' +
        vrT('vr.loading') + '</div>';

    $.ajax({
        type: 'POST', url: 'ValidateRequest.aspx/GetClosedRequests', data: '{}',
        contentType: 'application/json; charset=utf-8', dataType: 'json',
        success: function (resp) {
            var d = resp.d;
            if (d.success) {
                vr_closedRequests = d.data;
                renderClosedRequests();
            } else {
                list.innerHTML = '<div class="vr-empty"><span class="material-icons">error_outline</span>' +
                    '<p>' + escHtml(d.message) + '</p></div>';
            }
        },
        error: function () {
            list.innerHTML = '<div class="vr-empty"><span class="material-icons">error_outline</span>' +
                '<p>' + vrT('common.error') + '</p></div>';
        }
    });
}

function renderClosedRequests() {
    var list = document.getElementById('vrClosedList');

    if (vr_closedRequests.length === 0) {
        list.innerHTML = '<div class="vr-empty"><span class="material-icons">lock_open</span>' +
            '<p>' + vrT('vr.closed.empty') + '</p></div>';
        return;
    }

    list.innerHTML = vr_closedRequests.map(function (r) {
        var reviewLine = r.reviewedByName
            ? '<div class="vr-review-result approved"><span class="material-icons">check_circle</span>' +
            '<span>' + escHtml(r.reviewedByName) +
            (r.reviewedAt ? ' · ' + r.reviewedAt : '') +
            (r.reviewNotes ? ' — ' + escHtml(r.reviewNotes) : '') +
            '</span></div>' : '';

        return '<div class="vr-closed-item">' +
            '<div class="vr-request-info">' +
            '<div class="vr-request-top">' +
            '<span class="vr-request-id">#' + r.requestId + '</span>' +
            '<span class="vr-location-tag">' + escHtml(r.locationName) + '</span>' +
            '<span class="vr-status-badge Approved">' + vrT('vr.status.approved') + '</span>' +
            '<span class="vr-request-date">' + r.createdAt + '</span></div>' +
            '<div class="vr-requester"><span class="material-icons">person</span>' +
            escHtml(r.requesterName) +
            '<span class="vr-requester-email">· ' + escHtml(r.requesterEmail) + '</span></div>' +
            (r.notes ? '<div class="vr-request-notes">' + escHtml(r.notes) + '</div>' : '') +
            reviewLine + '</div>' +
            '<div class="vr-request-actions">' +
            '<button type="button" class="vr-btn-revert" onclick="openRevertModal(' + r.requestId + ')">' +
            '<span class="material-icons">undo</span>' + vrT('vr.closed.btn_revert') + '</button>' +
            '</div></div>';
    }).join('');
}

// ── Modal de confirmación de reversión ────────────────────────────────────────
function openRevertModal(requestId) {
    var req = vr_closedRequests.find(function (r) { return r.requestId === requestId; });
    if (!req) return;

    var prev = document.getElementById('vrRevertOverlay');
    if (prev) prev.remove();

    document.body.insertAdjacentHTML('beforeend',
        '<div class="vr-overlay" id="vrRevertOverlay" onclick="closeRevertModalOnBg(event)">' +
        '<div class="vr-modal">' +
        '<div class="vr-modal-header revert">' +
        '<span class="material-icons">undo</span>' +
        '<span class="vr-modal-title">' + vrT('vr.closed.modal_title') + '</span>' +
        '<button type="button" class="vr-modal-close" onclick="closeRevertModal()">' +
        '<span class="material-icons">close</span></button></div>' +
        '<div class="vr-modal-body">' +
        '<div class="vr-modal-summary">' +
        '<strong>' + vrT('vr.card.location') + '</strong> ' + escHtml(req.locationName) + '<br/>' +
        '<strong>' + vrT('vr.card.requested_by') + '</strong> ' + escHtml(req.requesterName) + '</div>' +
        '<div class="vr-revert-hint"><span class="material-icons">info</span>' +
        '<span>' + vrT('vr.closed.modal_hint') + '</span></div>' +
        '<label class="vr-modal-label">' + vrT('vr.closed.reason_label') +
        '<span class="vr-required"> * ' + vrT('vr.modal.required') + '</span></label>' +
        '<textarea id="vrRevertReason" class="vr-modal-textarea" rows="3" maxlength="500" placeholder="' +
        vrT('vr.closed.reason_placeholder') + '"></textarea></div>' +
        '<div class="vr-modal-footer">' +
        '<span class="vr-modal-msg" id="vrRevertMsg"></span>' +
        '<button type="button" class="vr-btn-cancel" onclick="closeRevertModal()">' + vrT('vr.modal.cancel') + '</button>' +
        '<button type="button" class="vr-btn-confirm revert" id="vrBtnRevert" onclick="submitRevert(' + requestId + ')">' +
        '<span class="material-icons">undo</span>' + vrT('vr.closed.btn_confirm_revert') + '</button>' +
        '</div></div></div>'
    );

    document.getElementById('vrRevertReason').focus();
}

function closeRevertModal() {
    var overlay = document.getElementById('vrRevertOverlay');
    if (overlay) overlay.remove();
}

function closeRevertModalOnBg(e) {
    if (e.target.id === 'vrRevertOverlay') closeRevertModal();
}

function submitRevert(requestId) {
    var reason = document.getElementById('vrRevertReason').value.trim();
    var btnConf = document.getElementById('vrBtnRevert');
    var msgEl = document.getElementById('vrRevertMsg');

    if (!reason) {
        msgEl.textContent = vrT('vr.closed.reason_required');
        msgEl.className = 'vr-modal-msg error';
        msgEl.style.display = 'block';
        document.getElementById('vrRevertReason').focus();
        return;
    }

    btnConf.disabled = true;
    btnConf.innerHTML = '<span class="material-icons vr-spin">autorenew</span> ' + vrT('vr.modal.processing');
    msgEl.style.display = 'none';

    $.ajax({
        type: 'POST', url: 'ValidateRequest.aspx/RevertLocation',
        data: JSON.stringify({ requestId: requestId, reason: reason }),
        contentType: 'application/json; charset=utf-8', dataType: 'json',
        success: function (resp) {
            var d = resp.d;
            if (d.success) {
                closeRevertModal();
                loadClosedRequests();
                loadRequests();
            } else {
                btnConf.disabled = false;
                btnConf.innerHTML = '<span class="material-icons">undo</span>' + vrT('vr.closed.btn_confirm_revert');
                msgEl.textContent = d.message || vrT('common.error');
                msgEl.className = 'vr-modal-msg error';
                msgEl.style.display = 'block';
            }
        },
        error: function () {
            btnConf.disabled = false;
            btnConf.innerHTML = '<span class="material-icons">undo</span>' + vrT('vr.closed.btn_confirm_revert');
            msgEl.textContent = vrT('common.error');
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
        .replace(/&/g, '&amp;').replace(/</g, '&lt;')
        .replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

// SignalR
(function () {
    if (typeof $.connection === 'undefined' || typeof $.connection.locationHub === 'undefined') return;
    var hub = $.connection.locationHub;
    var _base = hub.client.newRequest;
    hub.client.newRequest = function (data) {
        if (_base) _base(data);
        loadRequests();
    };
})();