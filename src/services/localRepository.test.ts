import { beforeEach, describe, expect, it } from 'vitest'
import { loadLocalData, saveLocalData } from './localRepository'
import type { AppData } from '@/types/models'

class MemoryStorage implements Storage {
  private readonly values = new Map<string, string>()

  get length(): number { return this.values.size }
  clear(): void { this.values.clear() }
  getItem(key: string): string | null { return this.values.get(key) ?? null }
  key(index: number): string | null { return [...this.values.keys()][index] ?? null }
  removeItem(key: string): void { this.values.delete(key) }
  setItem(key: string, value: string): void { this.values.set(key, value) }
}

describe('local business data', () => {
  beforeEach(() => {
    Object.defineProperty(globalThis, 'localStorage', {
      value: new MemoryStorage(),
      configurable: true
    })
  })

  it('survives independent application-shell cache updates', () => {
    const data: AppData = {
      business: {
        id: 'owner-1',
        ownerUid: 'owner-1',
        name: 'MASHAL Test Business',
        defaultLocation: 'Main Location',
        currency: 'PHP',
        timezone: 'Asia/Manila',
        createdAt: '2026-09-12T00:00:00.000Z',
        updatedAt: '2026-09-12T00:00:00.000Z'
      },
      inventoryItems: [],
      products: [],
      sales: [],
      expenses: [],
      movements: [],
      receipts: [],
      batches: []
    }

    saveLocalData('owner-1', data)

    expect(loadLocalData('owner-1')).toEqual(data)
    expect(localStorage.getItem('mashal:v1:owner-1')).not.toBeNull()
  })
})
