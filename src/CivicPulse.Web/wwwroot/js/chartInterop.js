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
