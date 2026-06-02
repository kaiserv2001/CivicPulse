window.pushInterop = {
    async isSupported() {
        return 'serviceWorker' in navigator && 'PushManager' in window;
    },

    async getPermission() {
        return Notification.permission;
    },

    async requestPermission() {
        return await Notification.requestPermission();
    },

    async subscribe(vapidPublicKey) {
        const reg = await navigator.serviceWorker.register('/sw.js');
        await navigator.serviceWorker.ready;

        const existing = await reg.pushManager.getSubscription();
        if (existing) return JSON.stringify(existing);

        const sub = await reg.pushManager.subscribe({
            userVisibleOnly: true,
            applicationServerKey: urlBase64ToUint8Array(vapidPublicKey)
        });
        return JSON.stringify(sub);
    },

    async unsubscribe() {
        const reg = await navigator.serviceWorker.getRegistration('/sw.js');
        if (!reg) return null;
        const sub = await reg.pushManager.getSubscription();
        if (!sub) return null;
        const endpoint = sub.endpoint;
        await sub.unsubscribe();
        return endpoint;
    },

    async getCurrentEndpoint() {
        const reg = await navigator.serviceWorker.getRegistration('/sw.js');
        if (!reg) return null;
        const sub = await reg.pushManager.getSubscription();
        return sub ? sub.endpoint : null;
    }
};

function urlBase64ToUint8Array(base64String) {
    const padding = '='.repeat((4 - base64String.length % 4) % 4);
    const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
    const rawData = atob(base64);
    return Uint8Array.from([...rawData].map(c => c.charCodeAt(0)));
}
