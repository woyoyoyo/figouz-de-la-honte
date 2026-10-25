// Petits helpers JS pour l'interop Blazor.
window.hobby = {
    isOnline: () => navigator.onLine,

    // Vide le cache service worker et force le rechargement de l'app
    hardRefresh: async () => {
        if ('serviceWorker' in navigator) {
            const regs = await navigator.serviceWorker.getRegistrations();
            await Promise.all(regs.map(r => r.unregister()));
        }
        if ('caches' in window) {
            const keys = await caches.keys();
            await Promise.all(keys.map(k => caches.delete(k)));
        }
        window.location.reload();
    },

    copyText: (text) => navigator.clipboard?.writeText(text) ?? Promise.resolve(),

    scrollToId: (id) => {
        const el = document.getElementById(id);
        if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
};
