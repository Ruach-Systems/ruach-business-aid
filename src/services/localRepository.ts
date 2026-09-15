import type { AppData } from '@/types/models'

const emptyData = (): AppData => ({
  business: null,
  inventoryItems: [],
  products: [],
  sales: [],
  expenses: [],
  movements: [],
  receipts: [],
  batches: []
})

const keyFor = (uid: string) => `mashal:v1:${uid}`

export function loadLocalData(uid: string): AppData {
  const raw = localStorage.getItem(keyFor(uid))
  if (!raw) return emptyData()
  try {
    return { ...emptyData(), ...JSON.parse(raw) } as AppData
  } catch {
    return emptyData()
  }
}

export function saveLocalData(uid: string, data: AppData): void {
  localStorage.setItem(keyFor(uid), JSON.stringify(data))
}

export function clearLocalData(uid: string): void {
  localStorage.removeItem(keyFor(uid))
}
