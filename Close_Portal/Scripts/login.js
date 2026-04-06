// ============================================================================
// LOGIN.JS
// ============================================================================

// AppRoot y LoginWebMethodBase son inyectados por Login.aspx desde el servidor
// usando ResolveUrl — siempre correctos independientemente de la ruta amigable.
// window.AppRoot            = resuelto por servidor
// window.LoginWebMethodBase = resuelto por servidor

const GOOGLE_CLIENT_ID = "529272784814-e2o5s7m1fscqhssu78s5efb4feg6h2em.apps.googleusercontent.com";

let isGoogleMode = true;

// ============================================================================
// Inicialización
// ============================================================================
window.onload = function () {
    initializeGoogleSignIn();
    initializeTabToggle();
};

function initializeGoogleSignIn() {
    google.accounts.id.initialize({
        client_id: GOOGLE_CLIENT_ID,
        callback: handleGoogleResponse,
        auto_select: false,
        cancel_on_tap_outside: true
    });

    google.accounts.id.renderButton(
        document.getElementById("google-btn-container"),
        {
            theme: "outline",
            size: "large",
            text: "continue_with",
            shape: "rectangular",
            logo_alignment: "left",
            width: 340
        }
    );
}

// ============================================================================
// Toggle del Tab
// ============================================================================
function initializeTabToggle() {
    var toggleBtn = document.getElementById('toggleLoginMode');
    var tabText = document.getElementById('tabText');
    var googleSection = document.getElementById('googleLoginSection');
    var standardSection = document.getElementById('standardLoginSection');

    if (toggleBtn) {
        toggleBtn.addEventListener('dblclick', function () {
            isGoogleMode = !isGoogleMode;
            googleSection.style.display = isGoogleMode ? 'block' : 'none';
            standardSection.style.display = isGoogleMode ? 'none' : 'block';
            tabText.textContent = isGoogleMode ? 'Login' : 'Google';
            hideMessages();
        });
    }

    var btnStandardLogin = document.getElementById('btnStandardLogin');
    if (btnStandardLogin)
        btnStandardLogin.addEventListener('click', validateStandardLogin);

    var passwordInput = document.getElementById('passwordStandard');
    if (passwordInput)
        passwordInput.addEventListener('keypress', function (e) {
            if (e.key === 'Enter') validateStandardLogin();
        });
}

// ============================================================================
// Login Estándar
// ============================================================================
function validateStandardLogin() {
    var email = document.getElementById('emailStandard').value;
    var password = document.getElementById('passwordStandard').value;

    if (!email || !password) { showError("Por favor ingresa email y contraseña"); return; }
    if (!email.endsWith('@novamex.com')) { showError("Por favor usa tu email corporativo @novamex.com"); return; }

    showLoading(true);
    hideMessages();

    $.ajax({
        type: "POST",
        url: window.LoginWebMethodBase + 'ValidarLoginEstandar',
        data: JSON.stringify({ request: { Email: email, Password: password } }),
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (response) {
            showLoading(false);
            var result = response.d;
            if (result && result.Result) result = result.Result;
            handleLoginResponse(result);
        },
        error: function () {
            showLoading(false);
            showError("Error de comunicación. Intente nuevamente.");
        }
    });
}

// ============================================================================
// Callback de Google
// ============================================================================
function handleGoogleResponse(response) {
    if (!response.credential) { showError("No se recibió token de Google"); return; }
    validateGoogleLogin(response.credential);
}

function validateGoogleLogin(idToken) {
    showLoading(true);
    hideMessages();

    $.ajax({
        type: "POST",
        url: window.LoginWebMethodBase + 'ValidarLoginGoogle',
        data: JSON.stringify({ request: { IdToken: idToken } }),
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (response) {
            showLoading(false);
            var result = response.d;
            if (result && result.Result) result = result.Result;
            handleLoginResponse(result);
        },
        error: function () {
            showLoading(false);
            showError("Error de comunicación. Intente nuevamente.");
        }
    });
}

function handleLoginResponse(result) {
    if (result && result.Success) {
        showSuccess('¡Bienvenido! ' + (result.Email || result.Username || ''));
        setTimeout(function () {
            window.location.href = window.AppRoot + 'live';
        }, 1000);
    } else {
        showError((result && result.Message) || "Error al iniciar sesión");
    }
}

// ============================================================================
// UI
// ============================================================================
function showError(message) {
    var el = document.getElementById("errorMessage");
    el.textContent = message;
    el.style.display = "block";
}

function showSuccess(message) {
    var el = document.getElementById("successMessage");
    el.textContent = message;
    el.style.display = "block";
}

function hideMessages() {
    document.getElementById("errorMessage").style.display = "none";
    document.getElementById("successMessage").style.display = "none";
}

function showLoading(show) {
    var el = document.getElementById("loading");
    if (el) el.style.display = show ? "block" : "none";
}