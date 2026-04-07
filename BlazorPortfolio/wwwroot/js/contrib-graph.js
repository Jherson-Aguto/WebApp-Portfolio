// ── GitHub contribution graph canvas renderer ─────────────────────────────
window.renderContribGraph = function (canvasId, weeks, darkMode) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;

    const cellSize = 11;
    const gap = 3;
    const step = cellSize + gap;
    const cols = weeks.length;
    const rows = 7;

    canvas.width = cols * step - gap;
    canvas.height = rows * step - gap;

    // Make it crisp on high-DPI screens
    const dpr = window.devicePixelRatio || 1;
    const cssW = canvas.width;
    const cssH = canvas.height;
    canvas.width = cssW * dpr;
    canvas.height = cssH * dpr;
    canvas.style.width = cssW + 'px';
    canvas.style.height = cssH + 'px';

    const ctx = canvas.getContext('2d');
    ctx.scale(dpr, dpr);

    weeks.forEach(function (week, wi) {
        for (var d = 0; d < 7; d++) {
            var day = week.days.find(function (x) { return x.dayOfWeek === d; });
            var x = wi * step;
            var y = d * step;
            var radius = 2;

            ctx.beginPath();
            ctx.roundRect(x, y, cellSize, cellSize, radius);
            ctx.fillStyle = day ? day.color : 'rgba(255,255,255,0.04)';
            ctx.fill();
        }
    });

    // Tooltip via mousemove on the canvas wrapper
    canvas.onmousemove = function (e) {
        var rect = canvas.getBoundingClientRect();
        var scaleX = cssW / rect.width;
        var scaleY = cssH / rect.height;
        var mx = (e.clientX - rect.left) * scaleX;
        var my = (e.clientY - rect.top) * scaleY;
        var wi = Math.floor(mx / step);
        var di = Math.floor(my / step);
        if (wi >= 0 && wi < weeks.length && di >= 0 && di < 7) {
            var day = weeks[wi].days.find(function (x) { return x.dayOfWeek === di; });
            canvas.title = day
                ? day.date + ' \u2014 ' + day.count + ' contribution' + (day.count === 1 ? '' : 's')
                : '';
        } else {
            canvas.title = '';
        }
    };
};
