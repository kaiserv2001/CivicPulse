self.addEventListener('push', event => {
    if (!event.data) return;
    const data = event.data.json();
    event.waitUntil(
        self.registration.showNotification(data.title, {
            body: data.body,
            icon: '/favicon.ico',
            data: { locationId: data.locationId }
        })
    );
});

self.addEventListener('notificationclick', event => {
    event.notification.close();
    const locationId = event.notification.data?.locationId;
    if (locationId) {
        event.waitUntil(
            clients.openWindow(`/dashboard/${locationId}`)
        );
    }
});
