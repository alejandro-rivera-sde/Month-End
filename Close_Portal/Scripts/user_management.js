// ============================================================
// user_management.js - Gestión de Usuarios
// ============================================================

function getTranslation(key) {
    const lang = document.documentElement.getAttribute('data-language') || 'es';
    return (typeof translations !== 'undefined' && translations[lang]?.[key])
        ? translations[lang][key]
        : key;
}

function createModal() {
    const overlay = document.getElementById('modalOverlay');
    if (!overlay || overlay.dataset.initialized) return;
    overlay.addEventListener('click', function (e) {
        if (e.target === this) closeModal();
    });
    overlay.dataset.initialized = 'true';
}

function showOverlay() {
    document.getElementById('modalOverlay').classList.add('active');
}

function closeModal() {
    const overlay = document.getElementById('modalOverlay');
    if (overlay) overlay.classList.remove('active');
}

function filterTable() {
    const search = document.getElementById('searchInput').value.toLowerCase();
    const filterRole = document.getElementById('filterRole').value.toLowerCase();
    const filterStat = document.getElementById('filterStatus').value;
    const filterWms = document.getElementById('filterWms').value.toUpperCase();
    const filterDeptEl = document.getElementById('filterDepartment');
    const filterDept = filterDeptEl ? filterDeptEl.value.toUpperCase() : '';

    document.querySelectorAll('#usersTable tbody tr').forEach(row => {
        const text = row.innerText.toLowerCase();
        const role = (row.dataset.role || '').toLowerCase();
        const status = (row.dataset.status || '');
        const wms = (row.dataset.wms || '').toUpperCase();
        const dept = (row.dataset.department || '').toUpperCase();

        const matchSearch = !search || text.includes(search);
        const matchRole = !filterRole || role === filterRole;
        const matchStatus = !filterStat || status === filterStat;
        const matchWms = !filterWms || wms.split(',').includes(filterWms);
        const matchDept = !filterDept || dept === filterDept;

        row.style.display = (matchSearch && matchRole && matchStatus && matchWms && matchDept) ? '' : 'none';
    });
}

// ============================================================
// ABRIR MODAL EDICIÓN
// ============================================================
function openModalEdit(userId) {
    createModal();

    document.getElementById('modalTitle').innerText = getTranslation('um.modal.edit_title');
    document.getElementById('newUserFields').style.display = 'none';
    document.getElementById('statusToggles').style.display = 'block';
    document.getElementById('userInfoCard').style.display = 'flex';
    document.getElementById('editExtraFields').style.display = 'flex';

    const loadingHtml = `<div style="color:var(--text-muted);font-size:13px;padding:8px">${getTranslation('common.loading')}</div>`;
    document.getElementById('omsChecklist').innerHTML = loadingHtml;
    document.getElementById('locationsChecklist').innerHTML = loadingHtml;

    document.getElementById('editPassword1').value = '';
    document.getElementById('editPassword2').value = '';
    document.getElementById('pwMatchMsg').style.display = 'none';

    showOverlay();

    $.ajax({
        type: 'POST',
        url: 'UserManagement.aspx/GetUserDetail',
        data: JSON.stringify({ userId }),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: function (response) {
            const data = response.d;
            if (!data.Success) {
                showToast(getTranslation('common.error'), 'error');
                closeModal();
                return;
            }
            populateModal(data);
        },
        error: function () { showToast(getTranslation('common.error'), 'error'); closeModal(); }
    });
}

// ============================================================
// ABRIR MODAL NUEVO USUARIO
// ============================================================
function openModalNew() {
    createModal();

    document.getElementById('modalTitle').innerText = getTranslation('um.modal.new_title');
    document.getElementById('newUserFields').style.display = 'block';
    document.getElementById('statusToggles').style.display = 'none';
    document.getElementById('userInfoCard').style.display = 'none';
    document.getElementById('editExtraFields').style.display = 'none';

    document.getElementById('newEmail').value = '';
    document.getElementById('newUsername').value = '';
    document.getElementById('newPassword').value = '';
    document.getElementById('modalActive').checked = true;
    document.getElementById('modalLocked').checked = false;

    const deptNew = document.getElementById('modalDepartment');
    if (deptNew) deptNew.selectedIndex = 0;

    document.getElementById('modalOverlay').dataset.mode = 'new';
    document.getElementById('modalOverlay').dataset.userId = '0';

    const loadingHtml = `<div style="color:var(--text-muted);font-size:13px;padding:8px">${getTranslation('common.loading')}</div>`;
    document.getElementById('omsChecklist').innerHTML = loadingHtml;
    document.getElementById('locationsChecklist').innerHTML = loadingHtml;

    loadAllOms();
    showOverlay();
}

function togglePasswordField() {
    const v = document.getElementById('newLoginType').value;
    document.getElementById('passwordField').style.display = v === 'Standard' ? 'block' : 'none';
}

// ============================================================
// POBLAR MODAL (modo edición)
// ============================================================
function populateModal(data) {
    document.getElementById('modalAvatar').innerText = data.Initials;
    document.getElementById('modalUserName').innerText = data.Username;
    document.getElementById('modalUserEmail').innerText = data.Email;
    document.getElementById('editUsername').value = data.Username || '';

    const roleSelect = document.getElementById('modalRole');
    for (let i = 0; i < roleSelect.options.length; i++) {
        if (parseInt(roleSelect.options[i].value) === data.RoleId) {
            roleSelect.selectedIndex = i;
            break;
        }
    }

    document.getElementById('modalActive').checked = data.Active;
    document.getElementById('modalLocked').checked = data.Locked;

    // Departamento
    const deptSelect = document.getElementById('modalDepartment');
    if (deptSelect) {
        for (let i = 0; i < deptSelect.options.length; i++) {
            if (parseInt(deptSelect.options[i].value) === data.DepartmentId) {
                deptSelect.selectedIndex = i;
                break;
            }
        }
    }

    // Primero OMS (define el scope), luego locaciones filtradas por OMS asignados
    buildOmsChecklist(data.OmsList);
    buildLocationChecklist(data.LocationList);

    document.getElementById('modalOverlay').dataset.userId = data.UserId;
    document.getElementById('modalOverlay').dataset.mode = 'edit';
}

// ============================================================
// OMS CHECKLIST
// Agrupa por WMS para legibilidad.
// Al cambiar cualquier OMS, filtra dinámicamente las locaciones visibles.
// ============================================================
function buildOmsChecklist(omsList) {
    const container = document.getElementById('omsChecklist');
    container.innerHTML = '';

    if (!omsList || omsList.length === 0) {
        container.innerHTML =
            `<div style="color:var(--text-muted);font-size:13px;padding:8px">Sin OMS disponibles</div>`;
        return;
    }

    // Agrupar por WmsId
    const groups = {};
    omsList.forEach(oms => {
        if (!groups[oms.WmsId])
            groups[oms.WmsId] = { code: oms.WmsCode, items: [] };
        groups[oms.WmsId].items.push(oms);
    });

    Object.values(groups).forEach(group => {
        const header = document.createElement('div');
        header.className = 'um-oms-group-header';
        header.textContent = group.code;
        container.appendChild(header);

        group.items.forEach(oms => {
            const label = document.createElement('label');
            label.className = `wms-check-item${oms.Assigned ? ' checked' : ''}`;
            label.innerHTML = `
                <input type="checkbox"
                       value="${oms.OmsId}"
                       ${oms.Assigned ? 'checked' : ''}
                       onchange="toggleWmsItem(this); filterLocationsByOms()" />
                <span class="wms-check-code">${escHtml(oms.OmsCode)}</span>
                <span class="wms-check-oms">${escHtml(oms.OmsName)}</span>`;
            container.appendChild(label);
        });
    });

    // Aplicar filtro inicial con los OMS ya marcados
    filterLocationsByOms();
}

// ============================================================
// LOCATION CHECKLIST
// Cada ítem guarda data-oms-ids para filtrado dinámico.
// ============================================================
function buildLocationChecklist(locationList) {
    const container = document.getElementById('locationsChecklist');
    container.innerHTML = '';

    if (!locationList || locationList.length === 0) {
        container.innerHTML =
            `<div style="color:var(--text-muted);font-size:13px;padding:8px">Sin locaciones disponibles</div>`;
        return;
    }

    locationList.forEach(loc => {
        const label = document.createElement('label');
        label.className = `wms-check-item${loc.Assigned ? ' checked' : ''}`;
        label.dataset.omsIds = JSON.stringify(loc.OmsIds || []);
        label.innerHTML = `
            <input type="checkbox"
                   value="${loc.LocationId}"
                   ${loc.Assigned ? 'checked' : ''}
                   onchange="toggleWmsItem(this)" />
            <span class="wms-check-loc">${escHtml(loc.LocationName)}</span>
            <span class="wms-check-oms">${escHtml(loc.OmsLabel)}</span>`;
        container.appendChild(label);
    });

    // Sincronizar visibilidad con OMS actualmente seleccionados
    filterLocationsByOms();
}

// ============================================================
// FILTRAR LOCACIONES POR OMS SELECCIONADOS
// Muestra solo locaciones que tienen al menos un OMS marcado.
// Si ningún OMS está marcado → muestra todas (modo sin filtro).
// Desmarca y oculta locaciones que queden fuera del filtro.
// ============================================================
function filterLocationsByOms() {
    const checkedOmsIds = Array.from(
        document.querySelectorAll('#omsChecklist input[type=checkbox]:checked')
    ).map(cb => parseInt(cb.value));

    document.querySelectorAll('#locationsChecklist .wms-check-item').forEach(item => {
        const itemOmsIds = JSON.parse(item.dataset.omsIds || '[]');
        const visible = checkedOmsIds.length === 0 ||
            itemOmsIds.some(id => checkedOmsIds.includes(id));

        item.style.display = visible ? '' : 'none';

        // Desmarcar locaciones que ya no están en scope
        if (!visible) {
            const cb = item.querySelector('input[type=checkbox]');
            if (cb && cb.checked) {
                cb.checked = false;
                item.classList.remove('checked');
            }
        }
    });
}

// ============================================================
// CARGAR OMS PARA NUEVO USUARIO
// Después de cargar OMS, carga locaciones en callback
// ============================================================
function loadAllOms() {
    $.ajax({
        type: 'POST',
        url: 'UserManagement.aspx/GetAllOms',
        data: JSON.stringify({}),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: function (response) {
            const result = response.d;
            if (result && result.Success) {
                buildOmsChecklist(result.Data);
                // Cargar locaciones después de OMS para que filterLocationsByOms funcione
                loadAllLocations();
            } else {
                showToast(getTranslation('common.error'), 'error');
            }
        },
        error: function () { showToast(getTranslation('common.error'), 'error'); }
    });
}

function loadAllLocations() {
    $.ajax({
        type: 'POST',
        url: 'UserManagement.aspx/GetAllLocations',
        data: JSON.stringify({}),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: function (response) {
            const result = response.d;
            if (result && result.Success) {
                buildLocationChecklist(result.Data);
            } else {
                showToast(getTranslation('common.error'), 'error');
            }
        },
        error: function () { showToast(getTranslation('common.error'), 'error'); }
    });
}

function toggleWmsItem(checkbox) {
    checkbox.closest('label').classList.toggle('checked', checkbox.checked);
}

// ============================================================
// GUARDAR CAMBIOS
// ============================================================
function saveChanges() {
    const overlay = document.getElementById('modalOverlay');
    const mode = overlay.dataset.mode;
    const userId = parseInt(overlay.dataset.userId);
    const roleId = parseInt(document.getElementById('modalRole').value);
    const active = document.getElementById('modalActive').checked;
    const locked = document.getElementById('modalLocked').checked;
    const deptEl = document.getElementById('modalDepartment');
    const departmentId = deptEl && deptEl.value ? parseInt(deptEl.value) : null;

    const omsIds = Array.from(
        document.querySelectorAll('#omsChecklist input[type=checkbox]:checked')
    ).map(cb => parseInt(cb.value));

    const locationIds = Array.from(
        document.querySelectorAll('#locationsChecklist input[type=checkbox]:checked')
    ).map(cb => parseInt(cb.value));

    if (mode === 'edit') {
        saveEditChanges(userId, roleId, active, locked, omsIds, locationIds, departmentId);
    } else {
        saveNewUser(roleId, omsIds, locationIds, departmentId);
    }
}

function saveEditChanges(userId, roleId, active, locked, omsIds, locationIds, departmentId) {
    const username = (document.getElementById('editUsername').value || '').trim();
    const password1 = document.getElementById('editPassword1').value;
    const password2 = document.getElementById('editPassword2').value;

    if (!username) {
        showToast(getTranslation('um.modal.username_required'), 'error');
        return;
    }

    if (password1 || password2) {
        if (!PW_ALLOWED.test(password1)) {
            showToast(getTranslation('um.modal.pw_invalid_chars'), 'error');
            return;
        }
        if (password1.length < 8) {
            showToast(getTranslation('um.modal.pw_too_short'), 'error');
            return;
        }
        if (password1 !== password2) {
            showToast(getTranslation('um.modal.pw_nomatch'), 'error');
            return;
        }
    }

    setBtnLoading(true);

    $.ajax({
        type: 'POST',
        url: 'UserManagement.aspx/SaveUserChanges',
        data: JSON.stringify({
            userId,
            roleId,
            active,
            locked,
            omsIds,
            locationIds,
            username,
            newPassword: password1 || null,
            departmentId: departmentId || 0
        }),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: function (response) {
            setBtnLoading(false);
            const result = response.d;
            if (result.Success) {
                showToast(result.Message, 'success');
                closeModal();
                setTimeout(() => location.reload(), 800);
            } else {
                showToast(result.Message || getTranslation('common.error'), 'error');
            }
        },
        error: function () {
            setBtnLoading(false);
            showToast(getTranslation('common.error'), 'error');
        }
    });
}

function saveNewUser(roleId, omsIds, locationIds, departmentId) {
    showToast('Alta de usuario en desarrollo', 'info');
}

// ============================================================
// TOGGLE ACTIVO/INACTIVO
// ============================================================
function confirmToggleActive(userId, isActive) {
    if (confirm(`¿Deseas ${isActive ? 'desactivar' : 'activar'} este usuario?`)) {
        $.ajax({
            type: 'POST',
            url: 'UserManagement.aspx/ToggleUserActive',
            data: JSON.stringify({ userId, active: !isActive }),
            contentType: 'application/json; charset=utf-8',
            dataType: 'json',
            success: function (response) {
                const result = response.d;
                showToast(result.Message, result.Success ? 'success' : 'error');
                if (result.Success) setTimeout(() => location.reload(), 800);
            },
            error: function () { showToast(getTranslation('common.error'), 'error'); }
        });
    }
}

// ============================================================
// PASSWORD HELPERS
// ============================================================
const PW_ALLOWED = /^[a-zA-ZñÑ0-9!"#$%&/()=?¡]+$/;

function validatePwChar(e) {
    const char = String.fromCharCode(e.which || e.keyCode);
    if (e.which < 32) return true;
    if (!PW_ALLOWED.test(char)) { e.preventDefault(); return false; }
    return true;
}

function togglePwVisibility(inputId, btn) {
    const input = document.getElementById(inputId);
    const icon = btn.querySelector('.material-icons');
    if (input.type === 'password') {
        input.type = 'text';
        icon.textContent = 'visibility_off';
    } else {
        input.type = 'password';
        icon.textContent = 'visibility';
    }
}

function checkPwMatch() {
    const p1 = document.getElementById('editPassword1').value;
    const p2 = document.getElementById('editPassword2').value;
    const msg = document.getElementById('pwMatchMsg');
    if (!p2) { msg.style.display = 'none'; return; }
    msg.style.display = 'flex';
    if (p1 === p2) {
        msg.className = 'pw-match-msg match';
        msg.innerHTML = `<span class="material-icons" style="font-size:14px">check_circle</span>${getTranslation('um.modal.pw_match')}`;
    } else {
        msg.className = 'pw-match-msg nomatch';
        msg.innerHTML = `<span class="material-icons" style="font-size:14px">cancel</span>${getTranslation('um.modal.pw_nomatch')}`;
    }
}

function setBtnLoading(loading) {
    const btn = document.getElementById('btnSave');
    if (!btn) return;
    btn.disabled = loading;
    btn.innerText = loading ? getTranslation('common.loading') : getTranslation('um.modal.save');
}

function showToast(message, type) {
    const existing = document.getElementById('um-toast');
    if (existing) existing.remove();

    const colors = {
        success: { bg: 'rgba(16,185,129,0.95)', icon: 'check_circle' },
        error: { bg: 'rgba(239,68,68,0.95)', icon: 'error' },
        info: { bg: 'rgba(99,102,241,0.95)', icon: 'info' }
    };

    const c = colors[type] || colors.info;
    const toast = document.createElement('div');
    toast.id = 'um-toast';
    toast.className = 'um-toast';
    toast.innerHTML = `<span class="material-icons" style="font-size:18px">${c.icon}</span>${message}`;
    toast.style.cssText = `
        position:fixed;bottom:24px;left:50%;transform:translateX(-50%);
        background:${c.bg};color:white;padding:12px 20px;border-radius:8px;
        display:flex;align-items:center;gap:8px;
        font-size:13px;font-weight:600;font-family:inherit;
        box-shadow:0 4px 12px rgba(0,0,0,0.2);z-index:999999;`;
    document.body.appendChild(toast);
    setTimeout(() => { if (toast.parentNode) toast.remove(); }, 3000);
}

function escHtml(str) {
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

document.addEventListener('DOMContentLoaded', function () {
    createModal();
});