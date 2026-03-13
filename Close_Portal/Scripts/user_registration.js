// ============================================================
// user_registration.js
// Alta de usuario — Close Portal
// ============================================================

document.addEventListener('DOMContentLoaded', function () {
    loadAvailableRoles();
    loadAvailableOms();
});

// ============================================================
// CARGAR ROLES DISPONIBLES
// ============================================================
function loadAvailableRoles() {
    callMethod('UserRegistration.aspx/GetAvailableRoles', {}, function (data) {
        if (!data.Success) {
            showMessage('Error al cargar roles: ' + data.Message, 'error');
            return;
        }

        const select = document.getElementById('ddlRole');
        select.innerHTML = '<option value="">-- Selecciona un rol --</option>';

        data.Roles.forEach(function (r) {
            const opt = document.createElement('option');
            opt.value = r.RoleId;
            opt.textContent = r.RoleName;
            select.appendChild(opt);
        });
    });
}

// ============================================================
// CARGAR OMS DISPONIBLES
// Agrupa por WMS para legibilidad, igual que el modal de UserManagement
// ============================================================
function loadAvailableOms() {
    callMethod('UserRegistration.aspx/GetAvailableOms', {}, function (data) {
        const container = document.getElementById('omsChecklist');

        if (!data.Success) {
            container.innerHTML =
                '<p style="color:var(--error-text);font-size:13px;">Error al cargar OMS</p>';
            return;
        }

        if (!data.OmsList || data.OmsList.length === 0) {
            container.innerHTML =
                '<p style="color:var(--text-muted);font-size:13px;">No hay OMS disponibles</p>';
            return;
        }

        container.innerHTML = '';

        // Agrupar por WmsId
        const groups = {};
        data.OmsList.forEach(function (oms) {
            if (!groups[oms.WmsId])
                groups[oms.WmsId] = { code: oms.WmsCode, items: [] };
            groups[oms.WmsId].items.push(oms);
        });

        Object.values(groups).forEach(function (group) {
            // Cada grupo WMS es una columna
            const col = document.createElement('div');
            col.className = 'ur-oms-group';

            const header = document.createElement('div');
            header.className = 'um-oms-group-header';
            header.textContent = group.code;
            col.appendChild(header);

            group.items.forEach(function (oms) {
                const label = document.createElement('label');
                label.className = 'wms-check-item';
                label.innerHTML = `
                    <input type="checkbox" value="${oms.OmsId}" onchange="toggleOmsItem(this)" />
                    <span class="wms-check-oms">${escHtml(oms.OmsName)}</span>`;
                col.appendChild(label);
            });

            container.appendChild(col);
        });
    });
}

// ============================================================
// TOGGLE VISUAL DEL ÍTEM
// ============================================================
function toggleOmsItem(checkbox) {
    checkbox.closest('.wms-check-item').classList.toggle('checked', checkbox.checked);
}

// ============================================================
// CREAR USUARIO
// ============================================================
function createUser() {
    const email = document.getElementById('newEmail').value.trim();
    const roleId = parseInt(document.getElementById('ddlRole').value);

    const omsIds = Array.from(
        document.querySelectorAll('#omsChecklist input[type="checkbox"]:checked')
    ).map(cb => parseInt(cb.value));

    // ── Validaciones frontend ────────────────────────────────
    if (!email) {
        showMessage('El email es obligatorio', 'error');
        return;
    }
    if (!email.endsWith('@novamex.com')) {
        showMessage('El email debe ser @novamex.com', 'error');
        return;
    }
    if (!roleId) {
        showMessage('Debes seleccionar un rol', 'error');
        return;
    }

    const btn = document.getElementById('btnCreate');
    btn.disabled = true;
    btn.innerHTML = '<span class="material-icons spin">sync</span> Creando...';

    const usernameEl = document.getElementById('inputUsername');
    const username = (usernameEl && usernameEl.value.trim())
        ? usernameEl.value.trim()
        : email.split('@')[0];

    callMethod('UserRegistration.aspx/CreateUser', {
        email,
        username,
        roleId,
        omsIds
    }, function (data) {
        btn.disabled = false;
        btn.innerHTML = '<span class="material-icons">person_add</span> Crear Usuario';

        if (data.Success) {
            showMessage('Usuario creado correctamente', 'success');
            // Limpiar formulario tras éxito
            document.getElementById('newEmail').value = '';
            document.getElementById('ddlRole').value = '';
            document.querySelectorAll('#omsChecklist input[type="checkbox"]')
                .forEach(function (cb) {
                    cb.checked = false;
                    cb.closest('.wms-check-item').classList.remove('checked');
                });
        } else {
            showMessage(data.Message || 'Error al crear el usuario', 'error');
        }
    });
}

// ============================================================
// HELPERS
// ============================================================
function callMethod(url, params, callback) {
    $.ajax({
        type: 'POST',
        url: url,
        data: JSON.stringify(params),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: function (response) { callback(response.d); },
        error: function (xhr) {
            console.error('Error AJAX:', xhr.responseText);
            showMessage('Error de comunicación con el servidor', 'error');
        }
    });
}

function showMessage(text, type) {
    const div = document.getElementById('formMessage');
    div.textContent = text;
    div.className = 'form-message ' + type;
    div.style.display = 'block';
    if (type === 'error') {
        setTimeout(function () { div.style.display = 'none'; }, 5000);
    }
}

function escHtml(str) {
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}