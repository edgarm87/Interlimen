window.arrestsMap = {
    _map: null,
    _markers: [],

    init: function (dotNetRef, aors) {
        if (this._map) {
            this._map.remove();
            this._map = null;
        }

        this._map = L.map('arrests-map', {
            center: [39.5, -98.35],
            zoom: 4,
            zoomControl: true
        });

        L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
            maxZoom: 19
        }).addTo(this._map);

        this._markers = [];
        const maxArrests = Math.max(...aors.map(a => a.totalArrests));

        aors.forEach(aor => {
            const radius = Math.max(8, Math.sqrt(aor.totalArrests / maxArrests) * 60);
            const circle = L.circleMarker([aor.lat, aor.lng], {
                radius: radius,
                fillColor: '#1B4F72',
                color: '#fff',
                weight: 1.5,
                opacity: 0.9,
                fillOpacity: 0.65
            }).addTo(this._map);

            circle.on('click', () => {
                dotNetRef.invokeMethodAsync('OnAorClicked', aor.name);
            });

            circle.bindTooltip(aor.name, { permanent: false, direction: 'top' });
            this._markers.push(circle);
        });
    },

    destroy: function () {
        if (this._map) {
            this._map.remove();
            this._map = null;
        }
    }
};
