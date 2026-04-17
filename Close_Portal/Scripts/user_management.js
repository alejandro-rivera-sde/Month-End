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

    // Devolver inputs de password a su estado inicial (type=text oculto)
    // para que Chrome no los detecte como campos de credenciales
    ['editPassword1', 'editPassword2'].forEach(function (id) {
        var el = document.getElementById(id);
        if (el) {
            el.type = 'text';
            el.value = '';
            el.style.display = 'none';
        }
    });
}

function filterTable() {
    const search = document.getElementById('searchInput').value.toLowerCase().trim();
    const filterRole = document.getElementById('filterRole').value.trim();
    const filterStat = document.getElementById('filterStatus').value.trim();
    const filterLocEl = document.getElementById('filterLocation');
    const filterLoc = filterLocEl ? filterLocEl.value.trim() : '';
    const filterDeptEl = document.getElementById('filterDepartment');
    const filterDept = filterDeptEl ? filterDeptEl.value.toUpperCase().trim() : '';

    document.querySelectorAll('#usersTable tbody tr').forEach(row => {
        const text = row.innerText.toLowerCase();
        const roleId = parseInt(row.dataset.roleid || '0');
        const status = (row.dataset.status || '').trim();
        const locs = (row.dataset.location || '').split(',').map(s => s.trim());
        const dept = (row.dataset.department || '').toUpperCase().trim();

        const matchSearch = !search || text.includes(search);
        const matchRole = !filterRole || roleId === parseInt(filterRole);
        const matchStatus = !filterStat || status === filterStat;
        const matchLoc = !filterLoc || locs.includes(filterLoc);
        const matchDept = !filterDept || dept === filterDept;

        row.style.display = (matchSearch && matchRole && matchStatus && matchLoc && matchDept) ? '' : 'none';
    });
}

// Limpiar el buscador al cargar — evita que el browser inyecte
// valores cacheados de autocomplete antes de que el JS arranque
document.addEventListener('DOMContentLoaded', function () {
    var s = document.getElementById('searchInput');
    if (s) s.value = '';
});

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

    // Activar campos de password — en el DOM inicial son type="text" ocultos
    // para que Chrome no active autofill de credenciales en la página
    ['editPassword1', 'editPassword2'].forEach(function (id) {
        var el = document.getElementById(id);
        if (el) {
            el.type = 'password';
            el.value = '';
            el.style.display = '';
        }
    });
    document.getElementById('pwMatchMsg').style.display = 'none';

    const loadingHtml = `<div style="color:var(--text-muted);font-size:13px;padding:8px">${getTranslation('common.loading')}</div>`;
    document.getElementById('omsChecklist').innerHTML = loadingHtml;
    document.getElementById('locationsChecklist').innerHTML = loadingHtml;

    showOverlay();

    $.ajax({
        type: 'POST',
        url: window.PageWebMethodBase + 'GetUserDetail',
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

    document.getElementById('newEmail').value     = '';
    document.getElementById('newFirstName').value = '';
    document.getElementById('newLastName').value  = '';
    var newList = document.getElementById('newPhoneList');
    if (newList) { newList.innerHTML = ''; addPhoneRow('newPhoneList'); }

    // Restaurar no aplica en nuevo usuario
    const restoreBtn = document.getElementById('btnRestoreLocs');
    if (restoreBtn) restoreBtn.style.display = 'none';
    _savedLocIds = new Set();

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

function autoFillName() {
    var prefix = (document.getElementById('newEmail').value || '').trim();
    var dotIdx = prefix.indexOf('.');
    var first = '', last = '';
    if (dotIdx > 0) {
        first = _capitalize(prefix.substring(0, dotIdx));
        last  = _capitalize(prefix.substring(dotIdx + 1));
    } else {
        first = _capitalize(prefix);
    }
    document.getElementById('newFirstName').value = first;
    document.getElementById('newLastName').value  = last;
}

function _capitalize(s) {
    if (!s) return '';
    return s.charAt(0).toUpperCase() + s.slice(1).toLowerCase();
}

// ── PHONE LIST HELPERS ───────────────────────────────────────────
function _updatePhoneRemoveBtns(list) {
    var entries = list.querySelectorAll('.um-phone-entry');
    entries.forEach(function(e) {
        var btn = e.querySelector('.um-phone-remove-btn');
        if (btn) btn.style.visibility = entries.length > 1 ? 'visible' : 'hidden';
    });
}

function addPhoneRow(listId, phone, ext) {
    var list = document.getElementById(listId);
    if (!list) return;
    var entry = document.createElement('div');
    entry.className = 'um-phone-entry';
    entry.innerHTML =
        '<span class="material-icons um-phone-icon">phone</span>' +
        '<div class="um-phone-row">' +
            '<input type="text" class="um-phone-input" placeholder="(664) 000-0000" maxlength="20" autocomplete="off" />' +
            '<span class="um-phone-sep">Ext.</span>' +
            '<input type="text" class="um-phone-ext" placeholder="000" maxlength="10" autocomplete="off" />' +
        '</div>' +
        '<button type="button" class="um-phone-remove-btn" onclick="removePhoneRow(this)" title="Quitar">' +
            '<span class="material-icons">remove_circle_outline</span>' +
        '</button>';
    list.appendChild(entry);

    // Solo números, espacios, guiones, paréntesis y + para el teléfono
    entry.querySelector('.um-phone-input').addEventListener('input', function() {
        this.value = this.value.replace(/[^\d\s\-\(\)\+\.]/g, '');
    });
    // Solo dígitos para la extensión
    entry.querySelector('.um-phone-ext').addEventListener('input', function() {
        this.value = this.value.replace(/\D/g, '');
    });

    if (phone) entry.querySelector('.um-phone-input').value = phone;
    if (ext)   entry.querySelector('.um-phone-ext').value   = ext;

    _updatePhoneRemoveBtns(list);
}

function removePhoneRow(btn) {
    var entry = btn.closest('.um-phone-entry');
    var list  = entry && entry.parentElement;
    if (!entry || !list) return;
    // No eliminar si es la única fila
    if (list.querySelectorAll('.um-phone-entry').length <= 1) return;
    entry.remove();
    _updatePhoneRemoveBtns(list);
}

function getPhonesFromList(listId) {
    var list = document.getElementById(listId);
    var phones = [], extensions = [];
    if (!list) return { phones: phones, extensions: extensions };
    list.querySelectorAll('.um-phone-entry').forEach(function(entry) {
        var p = (entry.querySelector('.um-phone-input').value || '').trim();
        var e = (entry.querySelector('.um-phone-ext').value   || '').trim();
        if (p) { phones.push(p); extensions.push(e); }
    });
    return { phones: phones, extensions: extensions };
}

function togglePasswordField() {
    const v = document.getElementById('newLoginType').value;
    document.getElementById('passwordField').style.display = v === 'Standard' ? 'block' : 'none';
}

// ============================================================
// POBLAR MODAL (modo edición)
// ============================================================
function populateModal(data) {
    document.getElementById('modalAvatar').innerText   = data.Initials;
    document.getElementById('modalUserName').innerText  = data.FullName || data.Email;
    document.getElementById('modalUserEmail').innerText = data.Email;
    document.getElementById('editFirstName').value = data.FirstName || '';
    document.getElementById('editLastName').value  = data.LastName  || '';
    var editList = document.getElementById('editPhoneList');
    if (editList) {
        editList.innerHTML = '';
        var phones = data.Phones || [];
        if (phones.length === 0) {
            addPhoneRow('editPhoneList');
        } else {
            phones.forEach(function(p) { addPhoneRow('editPhoneList', p.Phone, p.Extension); });
        }
    }
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

    // Establecer modo antes de construir checklists (buildLocationChecklist lo lee)
    document.getElementById('modalOverlay').dataset.userId = data.UserId;
    document.getElementById('modalOverlay').dataset.mode = 'edit';

    // Primero WMS, luego locaciones
    buildWmsChecklist(data.WmsList);
    buildLocationChecklist(data.LocationList);

    // Mostrar/ocultar sección de contraseña según tipo de login
    const isGoogle = (data.LoginType || '').toLowerCase() === 'google';
    const pwSection = document.getElementById('editExtraFields');
    if (pwSection) pwSection.style.display = isGoogle ? 'none' : 'block';
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
var _savedLocIds = new Set(); // estado inicial al abrir modal de edición

function buildLocationChecklist(locationList) {
    const container = document.getElementById('locationsChecklist');
    container.innerHTML = '';

    if (!locationList || locationList.length === 0) {
        container.innerHTML =
            `<div style="color:var(--text-muted);font-size:13px;padding:8px">Sin locaciones disponibles</div>`;
        return;
    }

    const isEdit = document.getElementById('modalOverlay').dataset.mode === 'edit';

    // Guardar estado inicial solo en modo edición
    if (isEdit) {
        _savedLocIds = new Set(
            locationList.filter(l => l.Assigned).map(l => l.LocationId)
        );
        const btn = document.getElementById('btnRestoreLocs');
        if (btn) btn.style.display = '';
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
        url: window.PageWebMethodBase + 'GetAllWms',
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
        url: window.PageWebMethodBase + 'GetAllWms',
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
        url: window.PageWebMethodBase + 'GetAllLocations',
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

function toggleAllLocations(selectAll) {
    // Solo afecta los items visibles (respeta el filtro de búsqueda activo)
    document.querySelectorAll('#locationsChecklist .wms-check-item').forEach(function (item) {
        if (item.style.display === 'none') return;
        const cb = item.querySelector('input[type="checkbox"]');
        if (cb) {
            cb.checked = selectAll;
            item.classList.toggle('checked', selectAll);
        }
    });
}

function restoreLocations() {
    // Restaura al estado que tenían las locaciones al abrir el modal
    document.querySelectorAll('#locationsChecklist .wms-check-item').forEach(function (item) {
        const cb = item.querySelector('input[type="checkbox"]');
        if (!cb) return;
        const wasChecked = _savedLocIds.has(parseInt(cb.value));
        cb.checked = wasChecked;
        item.classList.toggle('checked', wasChecked);
    });
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
    const firstName = (document.getElementById('editFirstName').value || '').trim();
    const lastName  = (document.getElementById('editLastName').value  || '').trim();
    const { phones, extensions } = getPhonesFromList('editPhoneList');
    const password1 = document.getElementById('editPassword1').value;
    const password2 = document.getElementById('editPassword2').value;

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
        url: window.PageWebMethodBase + 'SaveUserChanges',
        data: JSON.stringify({
            userId,
            roleId,
            active,
            locked,
            wmsIds,
            locationIds,
            firstName,
            lastName,
            phones,
            phoneExtensions: extensions,
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
                if (result.Row) updateUserRow(result.Row);
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
    const emailPrefix = (document.getElementById('newEmail').value    || '').trim();
    const email       = emailPrefix ? emailPrefix + '@novamex.com' : '';
    const firstName   = (document.getElementById('newFirstName').value || '').trim();
    const lastName    = (document.getElementById('newLastName').value  || '').trim();
    const { phones, extensions } = getPhonesFromList('newPhoneList');

    if (!emailPrefix) {
        showToast('El email es requerido.', 'error');
        return;
    }
    if (emailPrefix.includes('@')) {
        showToast('Escribe solo la parte antes del @.', 'error');
        return;
    }
    if (!roleId) {
        showToast('Selecciona un rol.', 'error');
        return;
    }

    setBtnLoading(true);

    $.ajax({
        type: 'POST',
        url: window.PageWebMethodBase + 'CreateUser',
        // Always Google OAuth — no password needed
        data: JSON.stringify({ email, firstName, lastName, phones, phoneExtensions: extensions, roleId, wmsIds: wmsIds || [], locationIds: locationIds || [], departmentId: departmentId || 0 }),
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
            url: window.PageWebMethodBase + 'ToggleUserActive',
            data: JSON.stringify({ userId, active: !isActive }),
            contentType: 'application/json; charset=utf-8',
            dataType: 'json',
            success: function (response) {
                const result = response.d;
                showToast(result.Message, result.Success ? 'success' : 'error');
                if (result.Success && result.Row) updateUserRow(result.Row);
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

// ============================================================
// ACTUALIZAR FILA EN LA TABLA SIN RELOAD
// ============================================================
function updateUserRow(row) {
    const tr = document.querySelector('tr[data-userid="' + row.UserId + '"]');
    if (!tr) return;

    // Data-attributes para filtros del toolbar
    tr.dataset.role       = row.RoleName;
    tr.dataset.roleid     = row.RoleId;
    tr.dataset.status     = row.StatusLabel;
    tr.dataset.wms        = row.WmsCodes || '';
    tr.dataset.location   = row.LocationNames || '';
    tr.dataset.department = row.DepartmentCode || '';

    const cells = tr.querySelectorAll('td');

    // [0] Avatar + nombre
    cells[0].querySelector('.avatar').textContent         = row.Initials;
    cells[0].querySelector('.user-name-text').textContent = row.FullName;

    // [1] Badge de rol
    const roleBadge = cells[1].querySelector('.badge');
    roleBadge.className   = 'badge badge-' + row.RoleBadge;
    roleBadge.textContent = row.RoleName;

    // [2] Departamento
    if (row.DepartmentCode) {
        cells[2].innerHTML =
            "<span class='dept-badge'>" + escHtml(row.DepartmentCode) + "</span>" +
            "<span class='dept-name'> "  + escHtml(row.DepartmentName) + "</span>";
    } else {
        cells[2].innerHTML = "<span style='color:var(--text-muted);font-size:11px'>—</span>";
    }

    // [3] Tags WMS/locaciones
    cells[3].querySelector('.wms-tags').innerHTML = row.WmsTagsHtml || '';

    // [4] Login type — no editable, sin cambios

    // [5] Badge de estado
    const statusBadge = cells[5].querySelector('.badge');
    statusBadge.className   = 'badge badge-' + row.StatusBadge;
    statusBadge.textContent = row.StatusLabel;

    // [6] Acciones — reconstruir botones con el estado actual
    if (row.RoleId < row.CurrentRoleId || row.UserId === parseInt(window.CurrentUserId)) {
        const toggleTitle = row.Active ? 'Desactivar usuario' : 'Activar usuario';
        const toggleIcon  = row.Active ? 'person_off' : 'person';
        cells[6].innerHTML =
            "<div class='actions'>" +
            "<button type='button' class='btn-icon edit' onclick='openModalEdit(" + row.UserId + ")' title='Editar usuario'>" +
            "<span class='material-icons'>edit</span></button>" +
            "<button type='button' class='btn-icon delete' " +
            "onclick='confirmToggleActive(" + row.UserId + ", " + row.Active + ")' " +
            "title='" + toggleTitle + "'>" +
            "<span class='material-icons'>" + toggleIcon + "</span></button>" +
            "</div>";
    } else {
        cells[6].innerHTML =
            "<div class='actions'><span class='um-no-action' title='Sin permisos para editar'>" +
            "<span class='material-icons'>lock</span></span></div>";
    }
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