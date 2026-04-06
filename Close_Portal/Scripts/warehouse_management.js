// ============================================================
// warehouse_management.js — Gestión de Bodegas
// Modal inyectado en document.body (position:fixed seguro)
// ============================================================

function wmT(key) {
    var lang = document.documentElement.getAttribute('data-language') || 'es';
    return (typeof translations !== 'undefined' && translations[lang] && translations[lang][key])
        ? translations[lang][key]
        : key;
}

// ============================================================
// CREAR MODAL (una sola vez en body)
// ============================================================
function createWmModal() {
    if (document.getElementById('wmOverlay')) return;

    document.body.insertAdjacentHTML('beforeend', `
        <div class="wm-overlay" id="wmOverlay">
            <div class="wm-modal">

                <div class="wm-header">
                    <h3>
                        <span class="material-icons">warehouse</span>
                        <span id="wmModalTitle"></span>
                    </h3>
                    <button type="button" class="btn-icon" onclick="closeWmModal()" title="Cerrar">
                        <span class="material-icons">close</span>
                    </button>
                </div>

                <div class="wm-body">

                    <!-- Nombre -->
                    <div class="field-group">
                        <label for="wmLocName" data-translate-key="wm.modal.name">Nombre de la Locación</label>
                        <input type="text" id="wmLocName" maxlength="150"
                               data-translate-key="wm.modal.name_placeholder"
                               placeholder="Ej. Bodega Norte" />
                    </div>

                    <!-- Estado (solo edición) -->
                    <div id="wmStatusRow" style="display:none">
                        <div class="wm-divider"></div>
                        <div class="toggle-row">
                            <span class="toggle-label" data-translate-key="wm.modal.active">Activa</span>
                            <label class="toggle">
                                <input type="checkbox" id="wmActive" checked />
                                <span class="slider"></span>
                            </label>
                        </div>
                    </div>

                </div>

                <div class="wm-footer">
                    <button type="button" class="btn-cancel" onclick="closeWmModal()"
                            data-translate-key="common.cancel">Cancelar</button>
                    <button type="button" class="btn-save" id="wmBtnSave" onclick="saveWmLocation()"
                            data-translate-key="wm.modal.save">Guardar</button>
                </div>

            </div>
        </div>
    `);

    document.getElementById('wmOverlay').addEventListener('click', function (e) {
        if (e.target === this) closeWmModal();
    });
}

function closeWmModal() {
    var overlay = document.getElementById('wmOverlay');
    if (overlay) overlay.classList.remove('active');
}

function showWmOverlay() {
    document.getElementById('wmOverlay').classList.add('active');
}

function refreshWmModalTitle() {
    var overlay = document.getElementById('wmOverlay');
    if (!overlay) return;
    var key = overlay.dataset.mode === 'edit' ? 'wm.modal.edit_title' : 'wm.modal.new_title';
    document.getElementById('wmModalTitle').textContent = wmT(key);
}

// ============================================================
// ABRIR MODAL — NUEVO
// ============================================================
function openModalNew() {
    createWmModal();

    var overlay = document.getElementById('wmOverlay');
    overlay.dataset.mode = 'new';
    overlay.dataset.locationId = '0';

    document.getElementById('wmLocName').value = '';
    document.getElementById('wmStatusRow').style.display = 'none';
    refreshWmModalTitle();

    showWmOverlay();
    document.getElementById('wmLocName').focus();
}

// ============================================================
// ABRIR MODAL — EDITAR
// ============================================================
function openModalEdit(locationId) {
    createWmModal();

    var overlay = document.getElementById('wmOverlay');
    overlay.dataset.mode = 'edit';
    overlay.dataset.locationId = locationId;

    document.getElementById('wmLocName').value = '';
    document.getElementById('wmStatusRow').style.display = 'none';
    refreshWmModalTitle();

    showWmOverlay();

    $.ajax({
        type: 'POST',
        url: window.PageWebMethodBase + 'GetLocationDetail',
        data: JSON.stringify({ locationId: locationId }),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: function (r) {
            var d = r.d;
            if (!d.Success) {
                showWmToast(d.Message || wmT('common.error'), 'error');
                closeWmModal();
                return;
            }
            document.getElementById('wmLocName').value = d.LocationName || '';
            document.getElementById('wmActive').checked = d.Active;
            document.getElementById('wmStatusRow').style.display = 'block';
        },
        error: function () {
            showWmToast(wmT('common.error'), 'error');
            closeWmModal();
        }
    });
}

// ============================================================
// GUARDAR
// ============================================================
function saveWmLocation() {
    var overlay = document.getElementById('wmOverlay');
    var mode = overlay.dataset.mode;
    var locationId = parseInt(overlay.dataset.locationId) || 0;

    var locationName = (document.getElementById('wmLocName').value || '').trim();
    var active = mode === 'edit' ? document.getElementById('wmActive').checked : true;

    if (!locationName) {
        showWmToast(wmT('wm.modal.name_required'), 'error');
        return;
    }

    setWmBtnLoading(true);

    $.ajax({
        type: 'POST',
        url: window.PageWebMethodBase + 'SaveLocation',
        data: JSON.stringify({
            locationId: mode === 'edit' ? locationId : 0,
            locationName: locationName,
            active: active
        }),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: function (response) {
            setWmBtnLoading(false);
            var result = response.d;
            if (result.Success) {
                showWmToast(result.Message, 'success');
                closeWmModal();
                setTimeout(function () { location.reload(); }, 800);
            } else {
                showWmToast(result.Message || wmT('common.error'), 'error');
            }
        },
        error: function () {
            setWmBtnLoading(false);
            showWmToast(wmT('common.error'), 'error');
        }
    });
}

// ============================================================
// TOGGLE ACTIVO / INACTIVO
// ============================================================
function confirmToggleActive(locationId, isActive, locationName) {
    var action = isActive ? 'desactivar' : 'activar';
    if (!confirm('¿Deseas ' + action + ' la locación "' + locationName + '"?')) return;

    $.ajax({
        type: 'POST',
        url: window.PageWebMethodBase + 'ToggleLocationActive',
        data: JSON.stringify({ locationId: locationId, active: !isActive }),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: function (response) {
            var result = response.d;
            showWmToast(result.Message, result.Success ? 'success' : 'error');
            if (result.Success) setTimeout(function () { location.reload(); }, 800);
        },
        error: function () { showWmToast(wmT('common.error'), 'error'); }
    });
}

// ============================================================
// FILTRO DE TABLA
// ============================================================
function filterTable() {
    var search = document.getElementById('searchInput').value.toLowerCase();
    var filterStat = document.getElementById('filterStatus').value;

    document.querySelectorAll('#warehouseTable tbody tr').forEach(function (row) {
        var text = row.innerText.toLowerCase();
        var status = (row.dataset.status || '');
        var ok = (!search || text.includes(search)) && (!filterStat || status === filterStat);
        row.style.display = ok ? '' : 'none';
    });
}

// ============================================================
// HELPERS
// ============================================================
function setWmBtnLoading(loading) {
    var btn = document.getElementById('wmBtnSave');
    if (!btn) return;
    btn.disabled = loading;
    btn.textContent = loading ? wmT('wm.modal.saving') : wmT('wm.modal.save');
}

function showWmToast(message, type) {
    var existing = document.getElementById('wm-toast');
    if (existing) existing.remove();

    var colors = {
        success: { bg: 'rgba(16,185,129,0.95)', icon: 'check_circle' },
        error: { bg: 'rgba(239,68,68,0.95)', icon: 'error' },
        info: { bg: 'rgba(99,102,241,0.95)', icon: 'info' }
    };
    var c = colors[type] || colors.info;

    var toast = document.createElement('div');
    toast.id = 'wm-toast';
    toast.className = 'wm-toast';
    toast.innerHTML = '<span class="material-icons" style="font-size:18px">' + c.icon + '</span>' + message;
    toast.style.background = c.bg;
    document.body.appendChild(toast);
    setTimeout(function () { if (toast.parentNode) toast.remove(); }, 3000);
}

document.addEventListener('DOMContentLoaded', function () {
    createWmModal();

    // Re-traducir el modal al cambiar idioma
    window.onLanguageChange = function () {
        refreshWmModalTitle();
    };
});