(() => {
let database;
async function open() {
  if (database) return database;
  database = await new Promise((resolve, reject) => {
    const request = indexedDB.open('mashal-sql-v2', 1);
    request.onupgradeneeded = () => { request.result.createObjectStore('accounts'); request.result.createObjectStore('meta'); };
    request.onsuccess = () => { request.result.onversionchange = () => { request.result.close(); database = null; }; resolve(request.result); };
    request.onerror = () => reject(request.error);
    request.onblocked = () => reject(new Error('Close other Business Aid tabs to upgrade local storage.'));
  });
  return database;
}
const releases = new Map();
window.mashalStorage = {
  async read(store, key) {
    const db = await open();
    return new Promise((resolve, reject) => {
      const request = db.transaction(store).objectStore(store).get(key);
      request.onsuccess = () => resolve(request.result == null ? null : JSON.stringify(request.result));
      request.onerror = () => reject(request.error);
    });
  },
  async write(store, key, json) {
    const db = await open();
    return new Promise((resolve, reject) => {
      const tx = db.transaction(store, 'readwrite');
      tx.objectStore(store).put(JSON.parse(json), key);
      tx.oncomplete = () => resolve();
      tx.onerror = tx.onabort = () => reject(tx.error || new Error('Local save was interrupted.'));
    });
  },
  async delete(store, key) {
    const db = await open();
    return new Promise((resolve, reject) => {
      const tx = db.transaction(store, 'readwrite');
      tx.objectStore(store).delete(key);
      tx.oncomplete = () => resolve();
      tx.onerror = tx.onabort = () => reject(tx.error || new Error('Local reset was interrupted.'));
    });
  },
  async compareAndSwap(key, revision, json) {
    const db = await open();
    return new Promise((resolve, reject) => {
      const tx = db.transaction('accounts', 'readwrite'), store = tx.objectStore('accounts');
      let saved = false;
      const request = store.get(key);
      request.onsuccess = () => {
        if ((request.result?.revision || 0) !== revision) return;
        store.put(JSON.parse(json), key); saved = true;
      };
      tx.oncomplete = () => resolve(saved);
      tx.onerror = tx.onabort = () => reject(tx.error || new Error('Local save was interrupted.'));
    });
  },
  async acquire(name) {
    if (!navigator.locks) return;
    return new Promise((resolve, reject) => {
      navigator.locks.request(name, async () => {
        await new Promise(release => { releases.set(name, release); resolve(); });
      }).catch(reject);
    });
  },
  release(name) { releases.get(name)?.(); releases.delete(name); },
  listen(dotnet) {
    const changed = () => dotnet.invokeMethodAsync('ConnectivityChanged', navigator.onLine).catch(() => {});
    addEventListener('online', changed); addEventListener('offline', changed); addEventListener('focus', changed);
    setInterval(() => { if (document.visibilityState === 'visible') changed(); }, 30000);
    return navigator.onLine;
  }
};
})();
