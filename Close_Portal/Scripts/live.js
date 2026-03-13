/* ============================================================
   dashboard_locations.js
   Lógica del grid de locaciones del Dashboard.
   Patrón: WebMethod via $.ajax — prefijo db-
   ============================================================ */

// ─── Estado global ──────────────────────────────────────────
let db_allLocations = [];
let db_currentFilter = 'all';

// ─── INIT ───────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
    loadDashboard();

    // Stat cards también actúan como filtro
    document.querySelectorAll('.db-stat-card').forEach(card => {
        card.addEventListener('click', () => {
            filterLocations(card.dataset.filter);
        });
    });
});

// ─── LOAD ───────────────────────────────────────────────────
function loadDashboard() {
    const grid = document.getElementById('dbLocationGrid');
    const banner = document.getElementById('dbGuardBanner');
    const refresh = document.getElementById('dbBtnRefresh');

    if (refresh) { refresh.disabled = true; refresh.querySelector('.material-icons').classList.add('db-spin'); }

    grid.innerHTML = `<div class="db-loading">
        <span class="material-icons db-spin">autorenew</span>
        <span>${dbT('db.loading')}</span>
    </div>`;

    $.ajax({
        type: 'POST',
        url: window.DashboardPageUrl + '/GetDashboardData',
        data: '{}',
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: (resp) => {
            const d = resp.d !== undefined ? resp.d : resp;
            if (!d.success) {
                grid.innerHTML = `<div class="db-empty">
                    <span class="material-icons">error_outline</span>
                    <p>${escDb(d.message || dbT('common.error'))}</p>
                </div>`;
                return;
            }
            renderGuardBanner(d.guard);
            renderStats(d.summary);
            db_allLocations = d.locations || [];
            filterLocations(db_currentFilter);
        },
        error: () => {
            grid.innerHTML = `<div class="db-empty">
                <span class="material-icons">wifi_off</span>
                <p>${dbT('common.error')}</p>
            </div>`;
        },
        complete: () => {
            if (refresh) { refresh.disabled = false; refresh.querySelector('.material-icons').classList.remove('db-spin'); }
        }
    });
}

// ─── GUARD BANNER ───────────────────────────────────────────
function renderGuardBanner(guard) {
    const banner = document.getElementById('dbGuardBanner');

    if (!guard || !guard.isActive) {
        banner.className = 'db-guard-banner db-guard-inactive';
        banner.innerHTML = `
            <span class="material-icons">shield</span>
            <div class="db-guard-info">
                <span class="db-guard-title">${dbT('db.guard.inactive')}</span>
                <span class="db-guard-sub">${dbT('db.guard.inactive_hint')}</span>
            </div>`;
        return;
    }

    const start = formatDateTime(guard.startTime);
    const end = formatDateTime(guard.endTime);

    banner.className = 'db-guard-banner db-guard-active';
    banner.innerHTML = `
        <span class="material-icons">security</span>
        <div class="db-guard-info">
            <span class="db-guard-title">
                ${dbT('db.guard.active')} <strong>${escDb(guard.ownerName)}</strong>
            </span>
            <span class="db-guard-sub">${start} → ${end}</span>
        </div>
        <span class="db-guard-pill">${dbT('db.guard.live')}</span>`;
}

// ─── STATS ──────────────────────────────────────────────────
function renderStats(summary) {
    if (!summary) return;
    document.getElementById('dbStatTotal').textContent = summary.total ?? '—';
    document.getElementById('dbStatActive').textContent = summary.active ?? '—';
    document.getElementById('dbStatPending').textContent = summary.pending ?? '—';
    document.getElementById('dbStatRejected').textContent = summary.rejected ?? '—';
    document.getElementById('dbStatApproved').textContent = summary.approved ?? '—';
}

// ─── FILTER ─────────────────────────────────────────────────
function filterLocations(filter) {
    db_currentFilter = filter;

    // Tabs
    document.querySelectorAll('.db-tab').forEach(tab => {
        tab.classList.toggle('active', tab.dataset.filter === filter);
    });

    // Stat cards
    document.querySelectorAll('.db-stat-card').forEach(card => {
        card.classList.toggle('active', card.dataset.filter === filter);
    });

    const items = filter === 'all'
        ? db_allLocations
        : db_allLocations.filter(l => l.status === filter);

    renderGrid(items);
}

// ─── RENDER GRID ────────────────────────────────────────────
function renderGrid(items) {
    const grid = document.getElementById('dbLocationGrid');

    if (!items.length) {
        grid.innerHTML = `<div class="db-empty">
            <span class="material-icons">location_off</span>
            <p>${dbT('db.empty')}</p>
        </div>`;
        return;
    }

    grid.innerHTML = items.map(loc => buildCard(loc)).join('');
}

// ─── BUILD CARD ─────────────────────────────────────────────
function buildCard(loc) {
    const statusMeta = getStatusMeta(loc.status);

    // Línea de quién envió (si hay request)
    let submittedLine = '';
    if (loc.requestedBy) {
        submittedLine = `
        <div class="db-card-meta-row">
            <span class="material-icons">person</span>
            <span>${escDb(loc.requestedBy)}</span>
            <span class="db-card-time">${escDb(loc.requestedAt)}</span>
        </div>`;
    }

    // Línea de revisión (si fue aprobada o rechazada)
    let reviewLine = '';
    if (loc.reviewedBy && (loc.status === 'Approved' || loc.status === 'Rejected')) {
        const reviewIcon = loc.status === 'Approved' ? 'check_circle' : 'cancel';
        reviewLine = `
        <div class="db-card-meta-row db-meta-review">
            <span class="material-icons">${reviewIcon}</span>
            <span>${escDb(loc.reviewedBy)}</span>
            <span class="db-card-time">${escDb(loc.reviewedAt)}</span>
        </div>`;
    }

    // Notas de rechazo
    let rejectNotes = '';
    if (loc.status === 'Rejected' && loc.reviewNotes) {
        rejectNotes = `
        <div class="db-card-reject-notes">
            <span class="material-icons">comment</span>
            <span>${escDb(loc.reviewNotes)}</span>
        </div>`;
    }

    return `
    <div class="db-location-card db-card-${loc.status.toLowerCase()}">
        <div class="db-card-header">
            <div class="db-card-name-row">
                <span class="material-icons db-card-icon" style="color:${statusMeta.color}">${statusMeta.icon}</span>
                <span class="db-card-name">${escDb(loc.locationName)}</span>
            </div>
            <span class="db-status-pill db-pill-${loc.status.toLowerCase()}">${statusMeta.label}</span>
        </div>
        <div class="db-card-body">
            ${submittedLine}
            ${reviewLine}
            ${rejectNotes}
        </div>
    </div>`;
}

// ─── STATUS META ────────────────────────────────────────────
function getStatusMeta(status) {
    const map = {
        'Active': { icon: 'radio_button_unchecked', color: '#10b981', label: dbT('db.status.active') },
        'Pending': { icon: 'pending', color: '#f59e0b', label: dbT('db.status.pending') },
        'Rejected': { icon: 'cancel', color: '#ef4444', label: dbT('db.status.rejected') },
        'Approved': { icon: 'lock', color: '#6366f1', label: dbT('db.status.approved') }
    };
    return map[status] || map['Active'];
}

// ─── UTILS ──────────────────────────────────────────────────
function formatDateTime(val) {
    if (!val) return '—';
    // Manejar formato /Date(ms)/ de WebMethod ASP.NET
    if (typeof val === 'string' && val.startsWith('/Date(')) {
        const ms = parseInt(val.replace('/Date(', '').replace(')/', ''));
        val = new Date(ms);
    } else {
        val = new Date(val);
    }
    if (isNaN(val)) return '—';
    return val.toLocaleDateString('es-MX', { day: '2-digit', month: '2-digit' })
        + ' ' + val.toLocaleTimeString('es-MX', { hour: '2-digit', minute: '2-digit' });
}

function escDb(str) {
    if (!str) return '';
    return String(str)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;')
        .replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}

function dbT(key) {
    const lang = document.documentElement.getAttribute('data-language') || 'es';
    return (typeof translations !== 'undefined' && translations[lang] && translations[lang][key])
        ? translations[lang][key]
        : key;
}