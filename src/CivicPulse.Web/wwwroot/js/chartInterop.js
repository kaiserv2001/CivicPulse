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
    },
    load: function () {
        return { token: localStorage.getItem('cp_token'), email: localStorage.getItem('cp_email') };
    },
    clear: function () {
        localStorage.removeItem('cp_token');
        localStorage.removeItem('cp_email');
    }
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

    destroyChart: function (canvasId) {
        if (this._charts[canvasId]) {
            this._charts[canvasId].destroy();
            delete this._charts[canvasId];
        }
    }
};
