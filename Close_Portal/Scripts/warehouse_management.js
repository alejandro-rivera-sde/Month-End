// ============================================================
// warehouse_management.js — Gestión de Bodegas
// Modal inyectado en document.body (position:fixed seguro)
// ============================================================

// ── Cache de catálogo OMS ────────────────────────────────────
let _wmOmsList = [];   // { OmsId, OmsCode, OmsName, WmsId, WmsCode, WmsName }

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
                        <span id="wmModalTitle">Locación</span>
                    </h3>
                    <button type="button" class="btn-icon" onclick="closeWmModal()" title="Cerrar">
                        <span class="material-icons">close</span>
                    </button>
                </div>

                <div class="wm-body">

                    <!-- Nombre -->
                    <div class="field-group">
                        <label data-translate-key="wm.modal.name">Nombre de la Locación</label>
                        <input type="text" id="wmLocName" maxlength="150"
                               placeholder="Ej. Bodega Norte" />
                    </div>

                    <div class="wm-divider"></div>

                    <!-- OMS checklist -->
                    <div class="field-group">
                        <label data-translate-key="wm.modal.oms">OMS que pueden ver esta locación</label>
                        <div id="wmOmsChecklist" class="wm-oms-checklist">
                            <div class="wm-checklist-loading">Cargando...</div>
                        </div>
                    </div>

                    <div class="wm-divider"></div>

                    <!-- Estado (solo edición) -->
                    <div id="wmStatusRow" style="display:none">
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
    const overlay = document.getElementById('wmOverlay');
    if (overlay) overlay.classList.remove('active');
}

function showWmOverlay() {
    document.getElementById('wmOverlay').classList.add('active');
}

// ============================================================
// ABRIR MODAL — NUEVO
// ============================================================
function openModalNew() {
    createWmModal();

    document.getElementById('wmModalTitle').textContent = 'Nueva Locación';
    document.getElementById('wmLocName').value = '';
    document.getElementById('wmStatusRow').style.display = 'none';
    document.getElementById('wmOverlay').dataset.mode = 'new';
    document.getElementById('wmOverlay').dataset.locationId = '0';

    loadOmsCatalog().then(() => buildOmsChecklist([]));
    showWmOverlay();
}

// ============================================================
// ABRIR MODAL — EDITAR
// ============================================================
function openModalEdit(locationId) {
    createWmModal();

    document.getElementById('wmModalTitle').textContent = 'Editar Locación';
    document.getElementById('wmStatusRow').style.display = 'block';
    document.getElementById('wmOmsChecklist').innerHTML =
        '<div class="wm-checklist-loading">Cargando...</div>';
    document.getElementById('wmOverlay').dataset.mode = 'edit';
    document.getElementById('wmOverlay').dataset.locationId = locationId;

    showWmOverlay();

    Promise.all([
        loadOmsCatalog(),
        fetchLocationDetail(locationId)
    ]).then(([, detail]) => {
        if (!detail.Success) {
            showWmToast(detail.Message || 'Error al cargar', 'error');
            closeWmModal();
            return;
        }
        document.getElementById('wmLocName').value = detail.LocationName || '';
        document.getElementById('wmActive').checked = detail.Active;
        buildOmsChecklist(detail.OmsIds || []);
    }).catch(() => {
        showWmToast('Error al cargar la locación', 'error');
        closeWmModal();
    });
}

// ============================================================
// CARGAR CATÁLOGO OMS (con caché)
// ============================================================
function loadOmsCatalog() {
    if (_wmOmsList.length > 0) return Promise.resolve();

    return new Promise((resolve, reject) => {
        $.ajax({
            type: 'POST',
            url: 'WarehouseManagement.aspx/GetOmsList',
            data: JSON.stringify({}),
            contentType: 'application/json; charset=utf-8',
            dataType: 'json',
            success: function (r) {
                if (r.d && r.d.Success) _wmOmsList = r.d.Data;
                resolve();
            },
            error: reject
        });
    });
}

function fetchLocationDetail(locationId) {
    return new Promise((resolve, reject) => {
        $.ajax({
            type: 'POST',
            url: 'WarehouseManagement.aspx/GetLocationDetail',
            data: JSON.stringify({ locationId }),
            contentType: 'application/json; charset=utf-8',
            dataType: 'json',
            success: function (r) { resolve(r.d); },
            error: reject
        });
    });
}

// ============================================================
// CONSTRUIR OMS CHECKLIST (agrupado por WMS para legibilidad)
// ============================================================
function buildOmsChecklist(assignedIds) {
    const container = document.getElementById('wmOmsChecklist');
    container.innerHTML = '';

    if (_wmOmsList.length === 0) {
        container.innerHTML = '<div class="wm-checklist-empty">Sin OMS disponibles</div>';
        return;
    }

    // Agrupar por WMS
    const groups = {};
    _wmOmsList.forEach(o => {
        if (!groups[o.WmsId]) {
            groups[o.WmsId] = { wmsCode: o.WmsCode, wmsName: o.WmsName, items: [] };
        }
        groups[o.WmsId].items.push(o);
    });

    Object.values(groups).forEach(group => {
        const header = document.createElement('div');
        header.className = 'wm-oms-group-header';
        header.innerHTML = `<span class="code-tag wms">${group.wmsCode}</span>${group.wmsName}`;
        container.appendChild(header);

        group.items.forEach(o => {
            const assigned = assignedIds.includes(o.OmsId);
            const label = document.createElement('label');
            label.className = `wm-oms-item${assigned ? ' checked' : ''}`;
            label.innerHTML = `
                <input type="checkbox" value="${o.OmsId}" ${assigned ? 'checked' : ''}
                       onchange="toggleOmsItem(this)" />
                <span class="wm-oms-code">${o.OmsCode}</span>
                <span class="wm-oms-name">${o.OmsName}</span>
            `;
            container.appendChild(label);
        });
    });
}

function toggleOmsItem(checkbox) {
    checkbox.closest('label').classList.toggle('checked', checkbox.checked);
}

// ============================================================
// GUARDAR
// ============================================================
function saveWmLocation() {
    const overlay = document.getElementById('wmOverlay');
    const mode = overlay.dataset.mode;
    const locationId = parseInt(overlay.dataset.locationId) || 0;

    const locationName = (document.getElementById('wmLocName').value || '').trim();
    const active = document.getElementById('wmActive').checked;

    const omsIds = Array.from(
        document.querySelectorAll('#wmOmsChecklist input[type=checkbox]:checked')
    ).map(cb => parseInt(cb.value));

    if (!locationName) {
        showWmToast('El nombre de la locación es requerido', 'error');
        return;
    }

    setWmBtnLoading(true);

    $.ajax({
        type: 'POST',
        url: 'WarehouseManagement.aspx/SaveLocation',
        data: JSON.stringify({
            locationId: mode === 'edit' ? locationId : 0,
            locationName,
            active: mode === 'edit' ? active : true,
            omsIds
        }),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: function (response) {
            setWmBtnLoading(false);
            const result = response.d;
            if (result.Success) {
                showWmToast(result.Message, 'success');
                closeWmModal();
                setTimeout(() => location.reload(), 800);
            } else {
                showWmToast(result.Message || 'Error al guardar', 'error');
            }
        },
        error: function () {
            setWmBtnLoading(false);
            showWmToast('Error de conexión', 'error');
        }
    });
}

// ============================================================
// TOGGLE ACTIVO / INACTIVO
// ============================================================
function confirmToggleActive(locationId, isActive, locationName) {
    const action = isActive ? 'desactivar' : 'activar';
    if (!confirm(`¿Deseas ${action} la locación "${locationName}"?`)) return;

    $.ajax({
        type: 'POST',
        url: 'WarehouseManagement.aspx/ToggleLocationActive',
        data: JSON.stringify({ locationId, active: !isActive }),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: function (response) {
            const result = response.d;
            showWmToast(result.Message, result.Success ? 'success' : 'error');
            if (result.Success) setTimeout(() => location.reload(), 800);
        },
        error: function () { showWmToast('Error de conexión', 'error'); }
    });
}

// ============================================================
// FILTRO DE TABLA
// ============================================================
function filterTable() {
    const search = document.getElementById('searchInput').value.toLowerCase();
    const filterStat = document.getElementById('filterStatus').value;

    document.querySelectorAll('#warehouseTable tbody tr').forEach(row => {
        const text = row.innerText.toLowerCase();
        const status = (row.dataset.status || '');

        const ok = (!search || text.includes(search))
            && (!filterStat || status === filterStat);

        row.style.display = ok ? '' : 'none';
    });
}

// ============================================================
// HELPERS
// ============================================================
function setWmBtnLoading(loading) {
    const btn = document.getElementById('wmBtnSave');
    if (!btn) return;
    btn.disabled = loading;
    btn.textContent = loading ? 'Guardando...' : 'Guardar';
}

function showWmToast(message, type) {
    const existing = document.getElementById('wm-toast');
    if (existing) existing.remove();

    const colors = {
        success: { bg: 'rgba(16,185,129,0.95)', icon: 'check_circle' },
        error: { bg: 'rgba(239,68,68,0.95)', icon: 'error' },
        info: { bg: 'rgba(99,102,241,0.95)', icon: 'info' }
    };
    const c = colors[type] || colors.info;

    const toast = document.createElement('div');
    toast.id = 'wm-toast';
    toast.className = 'wm-toast';
    toast.innerHTML = `<span class="material-icons" style="font-size:18px">${c.icon}</span>${message}`;
    toast.style.background = c.bg;
    document.body.appendChild(toast);
    setTimeout(() => { if (toast.parentNode) toast.remove(); }, 3000);
}

document.addEventListener('DOMContentLoaded', function () {
    createWmModal();
});