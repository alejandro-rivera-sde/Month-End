// assign_roles.js

let ar_allUsers = [];
let ar_roles = [];
let ar_roleFilter = 'all';
let ar_search = '';

document.addEventListener('DOMContentLoaded', function () {
    // Cargar roles PRIMERO, luego usuarios dentro del callback
    // Si se cargan en paralelo, ar_roles puede estar vacío cuando renderUsers() corre
    loadRolesFirst();
});

function loadRolesFirst() {
    $.ajax({
        type: 'POST',
        url: 'AssignRoles.aspx/GetAssignableRoles',
        data: '{}',
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: function (resp) {
            var d = resp.d;
            if (d.success) {
                ar_roles = d.data;
                console.log('[assign_roles] Roles cargados:', ar_roles.length);
            }
            // Cargar usuarios siempre, aunque roles falle
            loadUsers();
        },
        error: function () {
            console.warn('[assign_roles] No se pudieron cargar roles');
            loadUsers();
        }
    });
}

function loadUsers() {
    document.getElementById('arUserList').innerHTML =
        '<div class="ar-loading"><span class="material-icons ar-spin">autorenew</span> Cargando usuarios...</div>';

    $.ajax({
        type: 'POST',
        url: 'AssignRoles.aspx/GetUsers',
        data: '{}',
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: function (resp) {
            var d = resp.d;
            if (d.success) {
                ar_allUsers = d.data;
                console.log('[assign_roles] Usuarios cargados:', ar_allUsers.length, '| Roles disponibles:', ar_roles.length);
                renderUsers();
            } else {
                showListError(d.message);
            }
        },
        error: function () {
            showListError('Error de comunicación.');
        }
    });
}

function renderUsers() {
    var filtered = ar_allUsers.filter(function (u) {
        var matchRole = ar_roleFilter === 'all' || String(u.roleId) === ar_roleFilter;
        var matchSearch = ar_search === '' ||
            u.username.toLowerCase().includes(ar_search) ||
            u.email.toLowerCase().includes(ar_search);
        return matchRole && matchSearch;
    });

    var list = document.getElementById('arUserList');

    if (filtered.length === 0) {
        list.innerHTML =
            '<div class="ar-empty"><span class="material-icons">manage_accounts</span>No hay usuarios que coincidan</div>';
        return;
    }

    list.innerHTML = filtered.map(function (u) {
        var initials = getInitials(u.username || u.email);
        var roleClass = 'role-' + u.roleId;

        var options = ar_roles.map(function (r) {
            var sel = r.roleId === u.roleId ? ' selected' : '';
            return '<option value="' + r.roleId + '"' + sel + '>' + escHtml(r.roleName) + '</option>';
        }).join('');

        // Si no hay roles asignables (ej. Admin viendo a otro Admin), no mostrar select
        if (!options) {
            options = '<option value="' + u.roleId + '" selected>' + escHtml(u.roleName) + '</option>';
        }

        return '<div class="ar-user-card' + (u.active ? '' : ' inactive') + '" data-role="' + u.roleId + '">' +
            '<div class="ar-avatar ' + roleClass + '">' + initials + '</div>' +
            '<div class="ar-user-info">' +
            '<div class="ar-user-name">' + escHtml(u.username || u.email) + '</div>' +
            '<div class="ar-user-email">' + escHtml(u.email) + '</div>' +
            (u.wmsCodes ? '<div class="ar-user-wms"><span class="material-icons" style="font-size:12px;vertical-align:middle">warehouse</span> ' + escHtml(u.wmsCodes) + '</div>' : '') +
            '</div>' +
            '<span class="ar-role-badge ' + roleClass + '">' + escHtml(u.roleName) + '</span>' +
            '<select class="ar-role-select" id="sel_' + u.userId + '" onchange="onRoleSelectChange(' + u.userId + ', this)">' +
            options +
            '</select>' +
            '<button type="button" class="ar-btn-save" id="btn_' + u.userId + '" disabled onclick="saveRoleChange(' + u.userId + ')">' +
            '<span class="material-icons">save</span> Guardar' +
            '</button>' +
            '</div>';
    }).join('');
}

function filterByRole(filter) {
    ar_roleFilter = filter;
    document.querySelectorAll('.ar-tab').forEach(function (t) {
        t.classList.toggle('active', t.getAttribute('data-filter') === filter);
    });
    renderUsers();
}

function filterUsers(value) {
    ar_search = value.toLowerCase().trim();
    renderUsers();
}

function onRoleSelectChange(userId, select) {
    var card = select.closest('.ar-user-card');
    var originalId = parseInt(card.getAttribute('data-role'));
    var newId = parseInt(select.value);
    document.getElementById('btn_' + userId).disabled = (newId === originalId);
}

function saveRoleChange(userId) {
    var select = document.getElementById('sel_' + userId);
    var btn = document.getElementById('btn_' + userId);
    var newId = parseInt(select.value);

    btn.disabled = true;
    btn.innerHTML = '<span class="material-icons ar-spin">autorenew</span> Guardando...';

    $.ajax({
        type: 'POST',
        url: 'AssignRoles.aspx/SaveRoleChange',
        data: JSON.stringify({ targetUserId: userId, newRoleId: newId }),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: function (resp) {
            var d = resp.d;
            if (d.success) {
                showToast('Rol actualizado correctamente', 'success');
                loadRolesFirst(); // refresca todo desde el inicio
            } else {
                showToast(d.message || 'Error al guardar', 'error');
                btn.disabled = false;
                btn.innerHTML = '<span class="material-icons">save</span> Guardar';
            }
        },
        error: function () {
            showToast('Error de comunicación', 'error');
            btn.disabled = false;
            btn.innerHTML = '<span class="material-icons">save</span> Guardar';
        }
    });
}

function showToast(message, type) {
    var existing = document.getElementById('arToast');
    if (existing) existing.remove();
    var icon = type === 'success' ? 'check_circle' : 'error_outline';
    var toast = document.createElement('div');
    toast.id = 'arToast';
    toast.className = 'ar-toast ' + type + ' show';
    toast.innerHTML = '<span class="material-icons">' + icon + '</span>' + escHtml(message);
    document.body.appendChild(toast);
    setTimeout(function () { if (toast.parentNode) toast.remove(); }, 3000);
}

function showListError(msg) {
    document.getElementById('arUserList').innerHTML =
        '<div class="ar-empty"><span class="material-icons">error_outline</span>' + escHtml(msg) + '</div>';
}

function getInitials(name) {
    var parts = name.trim().split(/[\s@.]+/);
    if (parts.length >= 2) return (parts[0][0] + parts[1][0]).toUpperCase();
    return name.substring(0, 2).toUpperCase();
}

function escHtml(str) {
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}