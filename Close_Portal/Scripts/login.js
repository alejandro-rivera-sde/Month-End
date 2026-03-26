// ============================================================================
// LOGIN.JS - Cliente JavaScript para Google OAuth y Login Estándar
// ============================================================================

// AppRoot: raíz de la app derivada desde la URL actual
if (!window.AppRoot) {
    (function () {
        var path = window.location.pathname;
        var idx = path.toLowerCase().indexOf('/pages/');
        window.AppRoot = idx !== -1 ? path.substring(0, idx + 1) : '/';
    })();
}

const GOOGLE_CLIENT_ID = "529272784814-e2o5s7m1fscqhssu78s5efb4feg6h2em.apps.googleusercontent.com";

// Estado del toggle
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

    console.log("Google Sign-In inicializado");
}

// ============================================================================
// Toggle del Tab
// ============================================================================

function initializeTabToggle() {
    const toggleBtn = document.getElementById('toggleLoginMode');
    const tabText = document.getElementById('tabText');
    const googleSection = document.getElementById('googleLoginSection');
    const standardSection = document.getElementById('standardLoginSection');

    if (toggleBtn) {
        // DOBLE CLICK para activar toggle
        toggleBtn.addEventListener('dblclick', function () {
            isGoogleMode = !isGoogleMode;

            if (isGoogleMode) {
                // Mostrar Google
                googleSection.style.display = 'block';
                standardSection.style.display = 'none';
                tabText.textContent = 'Login';
                hideMessages();
            } else {
                // Mostrar campos estándar
                googleSection.style.display = 'none';
                standardSection.style.display = 'block';
                tabText.textContent = 'Google';
                hideMessages();
            }

            console.log('Modo cambiado a:', isGoogleMode ? 'Google' : 'Estándar');
        });
    }

    // Login estándar
    const btnStandardLogin = document.getElementById('btnStandardLogin');
    if (btnStandardLogin) {
        btnStandardLogin.addEventListener('click', function () {
            validateStandardLogin();
        });
    }

    // Enter en password para submit
    const passwordInput = document.getElementById('passwordStandard');
    if (passwordInput) {
        passwordInput.addEventListener('keypress', function (e) {
            if (e.key === 'Enter') {
                validateStandardLogin();
            }
        });
    }
}

// ============================================================================
// Login Estándar
// ============================================================================

function validateStandardLogin() {
    const email = document.getElementById('emailStandard').value;
    const password = document.getElementById('passwordStandard').value;

    console.log('=== validateStandardLogin EJECUTADA ===');

    // Validaciones básicas
    if (!email || !password) {
        showError("Por favor ingresa email y contraseña");
        return;
    }

    if (!email.endsWith('@novamex.com')) {
        showError("Por favor usa tu email corporativo @novamex.com");
        return;
    }

    // Mostrar loading
    showLoading(true);
    hideMessages();

    // Preparar request
    const requestData = {
        request: {
            Email: email,
            Password: password
        }
    };

    // AJAX al WebMethod
    $.ajax({
        type: "POST",
        url: "Login.aspx/ValidarLoginEstandar",
        data: JSON.stringify(requestData),
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (response) {
            console.log("=== RESPUESTA LOGIN ESTÁNDAR ===");
            console.log(response);

            showLoading(false);

            let result = response.d;
            if (result && result.Result) {
                result = result.Result;
            }

            handleLoginResponse(result);
        },
        error: function (xhr, status, error) {
            showLoading(false);
            console.error("Error en login estándar:", error);
            console.error("Response:", xhr.responseText);
            showError("Error de comunicación. Intente nuevamente.");
        }
    });
}

// ============================================================================
// Callback de Google
// ============================================================================

function handleGoogleResponse(response) {
    console.log("=== Google response recibida ===");
    const idToken = response.credential;

    if (!idToken) {
        showError("No se recibió token de Google");
        return;
    }

    validateGoogleLogin(idToken);
}

// ============================================================================
// Comunicación con Backend
// ============================================================================

function validateGoogleLogin(idToken) {
    console.log("=== Validando con backend ===");
    showLoading(true);
    hideMessages();

    const requestData = {
        request: {
            IdToken: idToken
        }
    };

    $.ajax({
        type: "POST",
        url: "Login.aspx/ValidarLoginGoogle",
        data: JSON.stringify(requestData),
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (response) {
            showLoading(false);
            let result = response.d;
            if (result && result.Result) {
                result = result.Result;
            }
            handleLoginResponse(result);
        },
        error: function (xhr, status, error) {
            showLoading(false);
            console.error("Error AJAX:", error);
            showError("Error de comunicación. Intente nuevamente.");
        }
    });
}

function handleLoginResponse(result) {
    console.log("========================================");
    console.log("=== handleLoginResponse ===");
    console.log("Result:", result);
    console.log("Success:", result.Success);
    console.log("Message:", result.Message);
    console.log("UserId:", result.UserId);
    console.log("Email:", result.Email);
    console.log("RoleName:", result.RoleName);
    console.log("========================================");

    if (result.Success) {
        // Login exitoso
        console.log("✓ Login exitoso - Guardando en sessionStorage...");

        showSuccess(`¡Bienvenido! ${result.Email || result.Username || ''}`);

        // Guardar datos del usuario en sessionStorage
        try {
            sessionStorage.setItem("userId", result.UserId);
            sessionStorage.setItem("email", result.Email);
            sessionStorage.setItem("username", result.Username || result.Email);
            sessionStorage.setItem("roleName", result.RoleName);

            console.log("SessionStorage guardado:");
            console.log("- userId:", sessionStorage.getItem("userId"));
            console.log("- email:", sessionStorage.getItem("email"));
            console.log("- username:", sessionStorage.getItem("username"));
            console.log("- roleName:", sessionStorage.getItem("roleName"));
        } catch (e) {
            console.error("Error guardando en sessionStorage:", e);
        }

        // Redirigir al dashboard después de 1 segundo
        console.log("Preparando redirección al dashboard...");

        setTimeout(function () {
            var dashboardUrl = window.AppRoot + 'Pages/Main/Live.aspx';
            console.log("Redirigiendo a:", dashboardUrl);
            window.location.href = dashboardUrl;
        }, 1000);

    } else {
        // Login fallido
        console.log("✗ Login fallido");
        showError(result.Message || "Error al iniciar sesión");
    }
}

// ============================================================================
// Funciones de UI
// ============================================================================

function showError(message) {
    const errorDiv = document.getElementById("errorMessage");
    errorDiv.textContent = message;
    errorDiv.style.display = "block";
}

function showSuccess(message) {
    const successDiv = document.getElementById("successMessage");
    successDiv.textContent = message;
    successDiv.style.display = "block";
}

function hideMessages() {
    document.getElementById("errorMessage").style.display = "none";
    document.getElementById("successMessage").style.display = "none";
}

function showLoading(show) {
    const loadingDiv = document.getElementById("loading");
    if (loadingDiv) {
        loadingDiv.style.display = show ? "block" : "none";
    }
}