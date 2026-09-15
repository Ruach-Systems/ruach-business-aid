import { initializeApp } from 'firebase/app'
import {
  browserLocalPersistence,
  getAuth,
  getRedirectResult,
  GoogleAuthProvider,
  onAuthStateChanged,
  setPersistence,
  signInWithPopup,
  signInWithRedirect,
  signOut,
  type Auth,
  type User
} from 'firebase/auth'
import {
  collection,
  deleteDoc,
  doc,
  getDoc,
  getDocs,
  initializeFirestore,
  onSnapshot,
  persistentLocalCache,
  persistentMultipleTabManager,
  setDoc,
  writeBatch,
  type Firestore
} from 'firebase/firestore'
import { prefersGoogleRedirect, shouldRetryWithGoogleRedirect } from '@/services/authFlow'
import type { AppData, AppUser, AuditedEntity, Business, CollectionName } from '@/types/models'

const firebaseConfig = {
  apiKey: import.meta.env.VITE_FIREBASE_API_KEY,
  authDomain: import.meta.env.VITE_FIREBASE_AUTH_DOMAIN,
  projectId: import.meta.env.VITE_FIREBASE_PROJECT_ID,
  storageBucket: import.meta.env.VITE_FIREBASE_STORAGE_BUCKET,
  messagingSenderId: import.meta.env.VITE_FIREBASE_MESSAGING_SENDER_ID,
  appId: import.meta.env.VITE_FIREBASE_APP_ID,
  measurementId: import.meta.env.VITE_FIREBASE_MEASUREMENT_ID
}

export const isFirebaseConfigured = Boolean(firebaseConfig.apiKey && firebaseConfig.projectId && firebaseConfig.appId)

let auth: Auth | null = null
let db: Firestore | null = null
let authPersistenceReady: Promise<void> = Promise.resolve()

if (isFirebaseConfigured) {
  const firebaseApp = initializeApp(firebaseConfig)
  auth = getAuth(firebaseApp)
  authPersistenceReady = setPersistence(auth, browserLocalPersistence)
  db = initializeFirestore(firebaseApp, {
    localCache: persistentLocalCache({ tabManager: persistentMultipleTabManager() })
  })
  if (import.meta.env.PROD && firebaseConfig.measurementId) {
    void import('firebase/analytics').then(async ({ getAnalytics, isSupported }) => {
      if (await isSupported()) getAnalytics(firebaseApp)
    }).catch(() => undefined)
  }
}

const toAppUser = (user: User): AppUser => ({
  uid: user.uid,
  displayName: user.displayName || user.email?.split('@')[0] || 'Business owner',
  email: user.email || '',
  photoURL: user.photoURL || undefined
})

export function observeAuth(callback: (user: AppUser | null) => void): () => void {
  if (!auth) {
    callback(null)
    return () => undefined
  }
  return onAuthStateChanged(auth, (user) => callback(user ? toAppUser(user) : null))
}

export async function signInGoogle(): Promise<AppUser | null> {
  if (!auth) return null
  await authPersistenceReady
  const provider = new GoogleAuthProvider()
  provider.setCustomParameters({ prompt: 'select_account' })

  const environment = {
    hostname: window.location.hostname,
    authDomain: firebaseConfig.authDomain,
    userAgent: navigator.userAgent,
    standalone: window.matchMedia('(display-mode: standalone)').matches,
    viewportWidth: window.innerWidth
  }
  if (prefersGoogleRedirect(environment)) {
    await signInWithRedirect(auth, provider)
    return null
  }

  try {
    const credential = await signInWithPopup(auth, provider)
    return toAppUser(credential.user)
  } catch (error) {
    const code = (error as { code?: string }).code
    if (shouldRetryWithGoogleRedirect(code, environment)) {
      await signInWithRedirect(auth, provider)
      return null
    }
    throw error
  }
}

export async function completeGoogleRedirect(): Promise<void> {
  if (!auth) return
  await authPersistenceReady
  await getRedirectResult(auth)
}

export async function signOutFirebase(): Promise<void> {
  if (auth) await signOut(auth)
}

export async function loadCloudData(uid: string): Promise<Partial<AppData>> {
  if (!db) return {}
  const businessSnapshot = await getDoc(doc(db, 'businesses', uid))
  const result: Partial<AppData> = {
    business: businessSnapshot.exists() ? businessSnapshot.data() as Business : null
  }
  const names: CollectionName[] = ['inventoryItems', 'products', 'sales', 'expenses', 'movements', 'receipts', 'batches']
  await Promise.all(names.map(async (name) => {
    const snapshot = await getDocs(collection(db!, 'businesses', uid, name))
    ;(result as Record<string, unknown>)[name] = snapshot.docs.map((entry) => entry.data())
  }))
  return result
}

export async function saveBusinessCloud(business: Business): Promise<void> {
  if (!db) return
  await setDoc(doc(db, 'businesses', business.id), business, { merge: true })
}

export async function saveEntityCloud(
  uid: string,
  collectionName: CollectionName,
  entity: { id: string; [key: string]: unknown }
): Promise<void> {
  if (!db) return
  await setDoc(doc(db, 'businesses', uid, collectionName, entity.id), entity, { merge: true })
}

export async function deleteEntityCloud(uid: string, collectionName: CollectionName, id: string): Promise<void> {
  if (!db) return
  await deleteDoc(doc(db, 'businesses', uid, collectionName, id))
}

export async function saveEntitiesCloud(
  uid: string,
  changes: Array<{ collectionName: CollectionName; entity: AuditedEntity }>
): Promise<void> {
  if (!db || changes.length === 0) return
  const batch = writeBatch(db)
  changes.forEach(({ collectionName, entity }) => {
    batch.set(doc(db!, 'businesses', uid, collectionName, entity.id), entity as unknown as Record<string, unknown>, { merge: true })
  })
  await batch.commit()
}

export function subscribeCloudData(
  uid: string,
  onBusiness: (business: Business | null) => void,
  onCollection: (name: CollectionName, entities: AuditedEntity[]) => void,
  onError: (error: Error) => void
): () => void {
  if (!db) return () => undefined
  const unsubscribers = [
    onSnapshot(doc(db, 'businesses', uid), { includeMetadataChanges: true },
      (snapshot) => onBusiness(snapshot.exists() ? snapshot.data() as Business : null), onError)
  ]
  const names: CollectionName[] = ['inventoryItems', 'products', 'sales', 'expenses', 'movements', 'receipts', 'batches']
  names.forEach((name) => {
    unsubscribers.push(onSnapshot(collection(db!, 'businesses', uid, name), { includeMetadataChanges: true },
      (snapshot) => onCollection(name, snapshot.docs.map((entry) => entry.data() as AuditedEntity)), onError))
  })
  return () => unsubscribers.forEach((unsubscribe) => unsubscribe())
}
