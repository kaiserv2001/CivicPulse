window.themeInterop = {
    init: function () {
        const t = localStorage.getItem('cp_theme') || 'light';
        document.documentElement.setAttribute('data-bs-theme', t);
        return t;
    },
    set: function (theme) {
        localStorage.setItem('cp_theme', theme);
        document.documentElement.setAttribute('data-bs-theme', theme);
    }
};

window.authStorage = {
    save: function (token, email) {
        localStorage.setItem('cp_token', token);
        localStorage.setItem('cp_email', email);
        // Stamp the session with the current server's boot id. If the server later
        // restarts (or wakes from idle as a fresh process) the id changes, which
        // tells us the in-memory account behind this token has been wiped.
        localStorage.setItem('cp_server', window.cpServerId || '');
    },
    load: function () {
        var token = localStorage.getItem('cp_token');
        var savedServer = localStorage.getItem('cp_server');
        // Server restarted since login -> account no longer exists -> force logout.
        if (token && window.cpServerId && savedServer !== window.cpServerId) {
            this.clear();
            return { token: null, email: null };
        }
        return { token: token, email: localStorage.getItem('cp_email') };
    },
    clear: function () {
        localStorage.removeItem('cp_token');
        localStorage.removeItem('cp_email');
        localStorage.removeItem('cp_server');
    }
};

window.searchBox = {
    setValue: function (el, value) { if (el) el.value = value || ''; }
};

window.chartInterop = {
    _charts: {},

    createForecastChart: function (canvasId, labels, precip, maxTemp) {
        if (this._charts[canvasId]) {
            this._charts[canvasId].destroy();
            delete this._charts[canvasId];
        }
        const ctx = document.getElementById(canvasId);
        if (!ctx) return;

        this._charts[canvasId] = new Chart(ctx, {
            data: {
                labels: labels,
                datasets: [
                    {
                        type: 'bar',
                        label: 'Precipitation (mm)',
                        data: precip,
                        backgroundColor: 'rgba(99,179,237,0.55)',
                        borderColor: 'rgba(99,179,237,0.9)',
                        borderWidth: 1,
                        yAxisID: 'y1'
                    },
                    {
                        type: 'line',
                        label: 'Max Temp (°C)',
                        data: maxTemp,
                        borderColor: 'rgba(239,68,68,0.9)',
                        backgroundColor: 'transparent',
                        pointBackgroundColor: 'rgba(239,68,68,0.9)',
                        borderWidth: 2,
                        tension: 0.4,
                        yAxisID: 'y2'
                    }
                ]
            },
            options: {
                responsive: true,
                interaction: { mode: 'index', intersect: false },
                scales: {
                    y1: {
                        type: 'linear',
                        position: 'left',
                        beginAtZero: true,
                        title: { display: true, text: 'Precip (mm)' }
                    },
                    y2: {
                        type: 'linear',
                        position: 'right',
                        grid: { drawOnChartArea: false },
                        title: { display: true, text: 'Temp (°C)' }
                    }
                },
                plugins: { legend: { position: 'top' } }
            }
        });
    },

    createAqiTrendChart: function (canvasId, labels, values) {
        if (this._charts[canvasId]) {
            this._charts[canvasId].destroy();
            delete this._charts[canvasId];
        }
        const ctx = document.getElementById(canvasId);
        if (!ctx) return;

        const aqiColor = v => v <= 50 ? 'rgba(34,197,94,0.9)'
            : v <= 100 ? 'rgba(234,179,8,0.9)'
            : v <= 150 ? 'rgba(249,115,22,0.9)'
            : 'rgba(239,68,68,0.9)';

        this._charts[canvasId] = new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [{
                    label: 'AQI',
                    data: values,
                    borderColor: 'rgba(99,102,241,0.8)',
                    backgroundColor: 'rgba(99,102,241,0.08)',
                    fill: true,
                    tension: 0.4,
                    pointBackgroundColor: values.map(aqiColor),
                    pointRadius: 5,
                    pointHoverRadius: 7,
                    segment: { borderColor: c => aqiColor(c.p1.parsed.y) }
                }]
            },
            options: {
                responsive: true,
                plugins: { legend: { display: false } },
                scales: {
                    y: { beginAtZero: true, title: { display: true, text: 'AQI' } }
                }
            }
        });
    },

    destroyChart: function (canvasId) {
        if (this._charts[canvasId]) {
            this._charts[canvasId].destroy();
            delete this._charts[canvasId];
        }
    }
};
