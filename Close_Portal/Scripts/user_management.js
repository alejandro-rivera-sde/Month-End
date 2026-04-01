// ============================================================
// user_management.js - Gestión de Usuarios
// ============================================================

function getTranslation(key) {
    const lang = document.documentElement.getAttribute('data-language') || 'es';
    return (typeof translations !== 'undefined' && translations[lang]?.[key])
        ? translations[lang][key]
        : key;
}

// ============================================================
// TAB SWITCHING
// ============================================================
function switchTab(tabName) {
    document.querySelectorAll('.um-tab').forEach(t =>
        t.classList.toggle('active', t.dataset.tab === tabName));
    document.querySelectorAll('.um-tab-panel').forEach(p =>
        p.classList.toggle('active', p.id === 'tab-' + tabName));
    // Reset location search when leaving/entering that tab
    if (tabName !== 'locations') {
        const s = document.getElementById('locSearch');
        if (s) { s.value = ''; filterLocationChecklist(''); }
    }
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
    switchTab('general');
}

function filterTable() {
    const search = document.getElementById('searchInput').value.toLowerCase().trim();
    const filterRole = document.getElementById('filterRole').value.trim();
    const filterStat = document.getElementById('filterStatus').value.trim();
    const filterWms = document.getElementById('filterWms').value.toUpperCase().trim();
    const filterDeptEl = document.getElementById('filterDepartment');
    const filterDept = filterDeptEl ? filterDeptEl.value.toUpperCase().trim() : '';

    document.querySelectorAll('#usersTable tbody tr').forEach(row => {
        const text = row.innerText.toLowerCase();
        const roleId = parseInt(row.dataset.roleid || '0');
        const status = (row.dataset.status || '').trim();
        const wms = (row.dataset.wms || '').toUpperCase();
        const dept = (row.dataset.department || '').toUpperCase().trim();

        const matchSearch = !search || text.includes(search);
        const matchRole = !filterRole || roleId === parseInt(filterRole);
        const matchStatus = !filterStat || status === filterStat;
        const matchWms = !filterWms || wms.split(',').map(s => s.trim()).includes(filterWms);
        const matchDept = !filterDept || dept === filterDept;

        row.style.display = (matchSearch && matchRole && matchStatus && matchWms && matchDept) ? '' : 'none';
    });
}

// ============================================================
// ABRIR MODAL EDICIÓN
// ============================================================
function openModalEdit(userId) {
    createModal();
    switchTab('general');

    document.getElementById('modalTitle').style.display = 'none';
    document.getElementById('userInfoCard').style.display = 'flex';
    document.getElementById('newUserFields').style.display = 'none';
    document.getElementById('editModeFields').style.display = 'block';

    document.getElementById('editPassword1').value = '';
    document.getElementById('editPassword2').value = '';
    document.getElementById('pwMatchMsg').style.display = 'none';

    const loadingHtml = `<div style="color:var(--text-muted);font-size:13px;padding:8px">${getTranslation('common.loading')}</div>`;
    document.getElementById('omsChecklist').innerHTML = loadingHtml;
    document.getElementById('locationsChecklist').innerHTML = loadingHtml;

    showOverlay();

    $.ajax({
        type: 'POST',
        url: 'UserManagement.aspx/GetUserDetail',
        data: JSON.stringify({ userId }),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: function (response) {
            const data = response.d;
            if (!data.Success) { showToast(getTranslation('common.error'), 'error'); closeModal(); return; }
            populateModal(data);
        },
        error: function () { showToast(getTranslation('common.error'), 'error'); closeModal(); }
    });
}

function openModalNew() {
    createModal();
    switchTab('general');

    const titleEl = document.getElementById('modalTitle');
    titleEl.style.display = 'flex';
    titleEl.innerText = getTranslation('um.modal.new_title');
    document.getElementById('userInfoCard').style.display = 'none';
    document.getElementById('newUserFields').style.display = 'block';
    document.getElementById('editModeFields').style.display = 'none';

    document.getElementById('newEmail').value = '';
    document.getElementById('newUsername').value = '';

    const newRole = document.getElementById('newModalRole');
    if (newRole) newRole.selectedIndex = 0;
    const newDept = document.getElementById('newModalDepartment');
    if (newDept) newDept.selectedIndex = 0;

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

    // Primero WMS, luego locaciones
    buildWmsChecklist(data.WmsList);
    buildLocationChecklist(data.LocationList);

    document.getElementById('modalOverlay').dataset.userId = data.UserId;
    document.getElementById('modalOverlay').dataset.mode = 'edit';
}

// ============================================================
// WMS CHECKLIST
// Lista plana de WMS — cada usuario puede tener N WMS asignados.
// ============================================================
function buildWmsChecklist(wmsList) {
    const container = document.getElementById('omsChecklist');
    container.innerHTML = '';

    if (!wmsList || wmsList.length === 0) {
        container.innerHTML =
            `<div style="color:var(--text-muted);font-size:13px;padding:8px">Sin WMS disponibles</div>`;
        return;
    }

    wmsList.forEach(wms => {
        const label = document.createElement('label');
        label.className = `wms-check-item${wms.Assigned ? ' checked' : ''}`;
        label.innerHTML = `
            <input type="checkbox"
                   value="${wms.WmsId}"
                   ${wms.Assigned ? 'checked' : ''}
                   onchange="toggleWmsItem(this)" />
            <span class="wms-check-code">${escHtml(wms.WmsCode)}</span>
            <span class="wms-check-oms">${escHtml(wms.WmsName)}</span>`;
        container.appendChild(label);
    });
}

// ============================================================
// LOCATION CHECKLIST
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
        label.innerHTML = `
            <input type="checkbox"
                   value="${loc.LocationId}"
                   ${loc.Assigned ? 'checked' : ''}
                   onchange="toggleWmsItem(this)" />
            <span class="wms-check-loc">${escHtml(loc.LocationName)}</span>`;
        container.appendChild(label);
    });
}

// ============================================================
// CARGAR WMS PARA NUEVO / EDITAR USUARIO
// ============================================================
function loadAllOms() {
    $.ajax({
        type: 'POST',
        url: 'UserManagement.aspx/GetAllWms',
        data: JSON.stringify({}),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: function (response) {
            const result = response.d;
            if (result && result.Success) {
                buildWmsChecklist(result.Data);
                loadAllLocations();
            } else {
                showToast(getTranslation('common.error'), 'error');
            }
        },
        error: function () { showToast(getTranslation('common.error'), 'error'); }
    });
}

function loadAllOmsForNew() {
    $.ajax({
        type: 'POST',
        url: 'UserManagement.aspx/GetAllWms',
        data: JSON.stringify({}),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: function (response) {
            const result = response.d;
            if (result && result.Success) {
                buildWmsChecklistInto('omsChecklistNew', result.Data, []);
            } else {
                document.getElementById('omsChecklistNew').innerHTML =
                    `<div style="color:var(--text-muted);font-size:13px;padding:8px">Sin WMS disponibles</div>`;
            }
        },
        error: function () {
            document.getElementById('omsChecklistNew').innerHTML =
                `<div style="color:var(--text-muted);font-size:13px;padding:8px">Error al cargar WMS</div>`;
        }
    });
}

// Generic WMS checklist builder — targets a given container id, marks assignedIds as checked
function buildWmsChecklistInto(containerId, wmsList, assignedIds) {
    const container = document.getElementById(containerId);
    container.innerHTML = '';

    if (!wmsList || wmsList.length === 0) {
        container.innerHTML = `<div style="color:var(--text-muted);font-size:13px;padding:8px">Sin WMS disponibles</div>`;
        return;
    }

    const assigned = new Set(assignedIds || []);

    wmsList.forEach(wms => {
        const isAssigned = assigned.has(wms.WmsId);
        const label = document.createElement('label');
        label.className = `wms-check-item${isAssigned ? ' checked' : ''}`;
        label.innerHTML = `
            <input type="checkbox" value="${wms.WmsId}" ${isAssigned ? 'checked' : ''}
                   onchange="toggleWmsItem(this)" />
            <span>${escHtml(wms.WmsCode)}</span>
            <span style="color:var(--text-muted);font-size:12px;margin-left:4px">${escHtml(wms.WmsName)}</span>`;
        container.appendChild(label);
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

function filterLocationChecklist(query) {
    const q = query.toLowerCase().trim();
    document.querySelectorAll('#locationsChecklist .wms-check-item').forEach(function (item) {
        const name = item.querySelector('.wms-item-name, span:not(.material-icons)');
        const text = (name ? name.textContent : item.textContent).toLowerCase();
        item.style.display = (!q || text.includes(q)) ? '' : 'none';
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

    const roleId = parseInt(document.getElementById('modalRole').value) || 0;
    const active = document.getElementById('modalActive').checked;
    const locked = document.getElementById('modalLocked').checked;
    const deptEl = document.getElementById('modalDepartment');
    const departmentId = deptEl && deptEl.value ? parseInt(deptEl.value) : null;

    const wmsIds = Array.from(
        document.querySelectorAll('#omsChecklist input[type=checkbox]:checked')
    ).map(cb => parseInt(cb.value));

    const locationIds = Array.from(
        document.querySelectorAll('#locationsChecklist input[type=checkbox]:checked')
    ).map(cb => parseInt(cb.value));

    if (mode === 'new') {
        const newRoleId = parseInt(document.getElementById('newModalRole').value) || 0;
        const newDeptEl = document.getElementById('newModalDepartment');
        const newDeptId = newDeptEl && newDeptEl.value ? parseInt(newDeptEl.value) : null;
        const newWmsIds = Array.from(document.querySelectorAll('#omsChecklist input[type=checkbox]:checked')).map(cb => parseInt(cb.value));
        const newLocIds = Array.from(document.querySelectorAll('#locationsChecklist input[type=checkbox]:checked')).map(cb => parseInt(cb.value));
        saveNewUser(newRoleId, newWmsIds, newLocIds, newDeptId);
    } else {
        const userId = parseInt(overlay.dataset.userId);
        saveEditChanges(userId, roleId, active, locked, wmsIds, locationIds, departmentId);
    }
}

function saveEditChanges(userId, roleId, active, locked, wmsIds, locationIds, departmentId) {
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
            wmsIds,
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

function saveNewUser(roleId, wmsIds, locationIds, departmentId) {
    const email = (document.getElementById('newEmail').value || '').trim();
    const username = (document.getElementById('newUsername').value || '').trim();

    if (!email) {
        showToast('El email es requerido.', 'error');
        return;
    }
    if (!email.toLowerCase().endsWith('@novamex.com')) {
        showToast('El email debe ser @novamex.com', 'error');
        return;
    }
    if (!username) {
        showToast(getTranslation('um.modal.username_required'), 'error');
        return;
    }
    if (!roleId) {
        showToast('Selecciona un rol.', 'error');
        return;
    }

    setBtnLoading(true);

    $.ajax({
        type: 'POST',
        url: 'UserManagement.aspx/CreateUser',
        // Always Google OAuth — no password needed
        data: JSON.stringify({ email, username, roleId, wmsIds: wmsIds || [], locationIds: locationIds || [], departmentId: departmentId || 0 }),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: function (response) {
            setBtnLoading(false);
            const result = response.d;
            if (result.Success) {
                showToast(result.Message || 'Usuario creado correctamente.', 'success');
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