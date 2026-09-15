import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import {
  makeId,
  migrateLegacyMadeToOrderProduct,
  nowIso,
  productCostCentavos,
  weightedAverageCost,
  localDateKey
} from '@/domain/calculations'
import {
  completeGoogleRedirect,
  isFirebaseConfigured,
  loadCloudData,
  observeAuth,
  saveBusinessCloud,
  saveEntitiesCloud,
  subscribeCloudData,
  signInGoogle,
  signOutFirebase
} from '@/services/firebase'
import { authErrorMessage } from '@/services/authErrors'
import { clearLocalData, loadLocalData, saveLocalData } from '@/services/localRepository'
import { clearSignInArtifacts } from '@/services/signinReset'
import { cleanEntityName, isEntityNameTaken } from '@/domain/names'
import type {
  AppData,
  AppUser,
  AuditedEntity,
  BatchIngredient,
  Business,
  CollectionName,
  Expense,
  InventoryItem,
  InventoryMovement,
  Product,
  ProductionBatch,
  Sale,
  SaleLine,
  StockReceipt
} from '@/types/models'

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

function mergeEntities<T extends AuditedEntity>(local: T[], cloud: T[]): T[] {
  const merged = new Map<string, T>()
  ;[...local, ...cloud].forEach((entity) => {
    const current = merged.get(entity.id)
    if (!current || entity.updatedAt >= current.updatedAt) merged.set(entity.id, entity)
  })
  return [...merged.values()]
}

export const useBusinessStore = defineStore('business', () => {
  const user = ref<AppUser | null>(null)
  const authReady = ref(false)
  const dataReady = ref(false)
  const data = ref<AppData>(emptyData())
  const syncStatus = ref<'local' | 'syncing' | 'synced' | 'error'>('local')
  const lastSyncedAt = ref<string>()
  const errorMessage = ref('')
  let stopCloudSubscription: (() => void) | undefined
  let latestCloudAttempt = 0
  let latestHydrationAttempt = 0

  const business = computed(() => data.value.business)
  const inventoryItems = computed(() => data.value.inventoryItems.filter((item) => !item.deletedAt && item.isActive))
  const products = computed(() => data.value.products.filter((product) => !product.deletedAt && product.isActive))
  const sales = computed(() => data.value.sales.filter((sale) => !sale.deletedAt))
  const expenses = computed(() => data.value.expenses.filter((expense) => !expense.deletedAt))
  const movements = computed(() => data.value.movements.filter((movement) => !movement.deletedAt))
  const batches = computed(() => data.value.batches.filter((batch) => !batch.deletedAt))
  const lowStockItems = computed(() => inventoryItems.value.filter((item) => item.currentQuantity <= item.minimumQuantity))

  const todayMetrics = computed(() => {
    const today = localDateKey()
    const todaysSales = sales.value.filter((sale) => sale.saleDate === today)
    const todaysExpenses = expenses.value.filter((expense) => expense.expenseDate === today)
    const revenueCentavos = todaysSales.reduce((sum, sale) => sum + sale.totalRevenueCentavos, 0)
    const costCentavos = todaysSales.reduce((sum, sale) => sum + sale.totalCostCentavos, 0)
    const expenseCentavos = todaysExpenses.reduce((sum, expense) => sum + expense.amountCentavos, 0)
    return {
      revenueCentavos,
      costCentavos,
      expenseCentavos,
      profitCentavos: revenueCentavos - costCentavos - expenseCentavos,
      itemCount: todaysSales.reduce((sum, sale) => sum + sale.totalItems, 0),
      lowStockCount: lowStockItems.value.length
    }
  })

  function persist(): void {
    if (user.value) saveLocalData(user.value.uid, data.value)
  }

  function replaceEntity<T extends AuditedEntity>(collectionName: CollectionName, entity: T): void {
    const list = data.value[collectionName] as T[]
    const index = list.findIndex((candidate) => candidate.id === entity.id)
    if (index >= 0) list[index] = entity
    else list.push(entity)
  }

  function runCloudWrite(operation: () => Promise<void>): void {
    if (!navigator.onLine) {
      syncStatus.value = 'local'
      return
    }

    const attempt = ++latestCloudAttempt
    syncStatus.value = 'syncing'
    const slowTimer = window.setTimeout(() => {
      if (attempt !== latestCloudAttempt) return
      syncStatus.value = 'error'
      errorMessage.value = 'Saved on this device. Cloud sync is taking longer than expected and will retry automatically.'
    }, 8000)

    void operation().then(() => {
      window.clearTimeout(slowTimer)
      if (attempt !== latestCloudAttempt) return
      syncStatus.value = navigator.onLine ? 'synced' : 'local'
      lastSyncedAt.value = nowIso()
      if (errorMessage.value.startsWith('Saved on this device.')) errorMessage.value = ''
    }).catch((error) => {
      window.clearTimeout(slowTimer)
      if (attempt !== latestCloudAttempt) return
      syncStatus.value = navigator.onLine ? 'error' : 'local'
      const detail = error instanceof Error ? error.message : 'Cloud sync failed.'
      errorMessage.value = `Saved on this device. Cloud sync will retry automatically. ${detail}`
    })
  }

  async function syncChanges(changes: Array<{ collectionName: CollectionName; entity: AuditedEntity }>): Promise<void> {
    persist()
    if (!user.value || user.value.isDemo || !isFirebaseConfigured) {
      syncStatus.value = 'local'
      return
    }
    const uid = user.value.uid
    runCloudWrite(() => saveEntitiesCloud(uid, changes))
  }

  async function hydrate(nextUser: AppUser): Promise<void> {
    const hydrationAttempt = ++latestHydrationAttempt
    dataReady.value = false
    user.value = nextUser
    data.value = loadLocalData(nextUser.uid)
    try {
      if (!nextUser.isDemo && isFirebaseConfigured) {
        syncStatus.value = 'syncing'
        const cloud = await loadCloudData(nextUser.uid)
        if (cloud.business && (!data.value.business || cloud.business.updatedAt >= data.value.business.updatedAt)) {
          data.value.business = cloud.business
        }
        const names: CollectionName[] = ['inventoryItems', 'products', 'sales', 'expenses', 'movements', 'receipts', 'batches']
        names.forEach((name) => {
          const local = data.value[name] as AuditedEntity[]
          const remote = (cloud[name] || []) as AuditedEntity[]
          ;(data.value as unknown as Record<string, AuditedEntity[]>)[name] = mergeEntities(local, remote)
        })
        persist()
        syncStatus.value = navigator.onLine ? 'synced' : 'local'
        lastSyncedAt.value = nowIso()
        stopCloudSubscription?.()
        stopCloudSubscription = subscribeCloudData(
          nextUser.uid,
          (remoteBusiness) => {
            if (remoteBusiness && (!data.value.business || remoteBusiness.updatedAt >= data.value.business.updatedAt)) {
              data.value.business = remoteBusiness
              persist()
            }
          },
          (name, remote) => {
            const local = data.value[name] as AuditedEntity[]
            ;(data.value as unknown as Record<string, AuditedEntity[]>)[name] = mergeEntities(local, remote)
            persist()
            syncStatus.value = navigator.onLine ? 'synced' : 'local'
            lastSyncedAt.value = nowIso()
          },
          (error) => {
            errorMessage.value = error.message
            syncStatus.value = navigator.onLine ? 'error' : 'local'
          }
        )
      }

      const originalProducts = data.value.products
      const migratedProducts = originalProducts.map((product) => migrateLegacyMadeToOrderProduct(product, data.value.inventoryItems))
      const changedProducts = migratedProducts.filter((product, index) => product !== originalProducts[index])
      if (changedProducts.length) {
        data.value.products = migratedProducts
        persist()
        if (!nextUser.isDemo && isFirebaseConfigured) {
          runCloudWrite(() => saveEntitiesCloud(nextUser.uid, changedProducts.map((entity) => ({ collectionName: 'products', entity }))))
        }
      }
    } catch (error) {
      if (hydrationAttempt === latestHydrationAttempt) {
        syncStatus.value = navigator.onLine ? 'error' : 'local'
        errorMessage.value = error instanceof Error ? error.message : 'Unable to load cloud data.'
      }
    } finally {
      if (hydrationAttempt === latestHydrationAttempt) dataReady.value = true
    }
  }

  async function bootstrap(): Promise<void> {
    window.addEventListener('online', () => { void syncAll() })
    if (!isFirebaseConfigured) {
      authReady.value = true
      dataReady.value = true
      return
    }
    const startupTimeout = window.setTimeout(() => {
      if (!authReady.value) {
        errorMessage.value = 'Google sign-in did not finish in this browser. Please try again, or open MASHAL in Chrome or Safari.'
        authReady.value = true
        if (!user.value) dataReady.value = true
      }
    }, 8000)
    const finishStartup = (): void => {
      window.clearTimeout(startupTimeout)
      authReady.value = true
    }

    observeAuth((nextUser) => {
      if (nextUser) {
        void hydrate(nextUser).finally(finishStartup)
      } else {
        latestHydrationAttempt += 1
        user.value = null
        data.value = emptyData()
        dataReady.value = true
        finishStartup()
      }
    })
    void completeGoogleRedirect().catch((error) => {
      errorMessage.value = authErrorMessage(error)
      finishStartup()
    })
  }

  async function loginWithGoogle(): Promise<void> {
    errorMessage.value = ''
    const nextUser = await signInGoogle()
    if (nextUser) await hydrate(nextUser)
  }

  async function continueLocally(): Promise<void> {
    await hydrate({
      uid: 'local-owner',
      displayName: 'Local business owner',
      email: 'local@mashal.app',
      isDemo: true
    })
    authReady.value = true
  }

  async function logout(): Promise<void> {
    stopCloudSubscription?.()
    stopCloudSubscription = undefined
    if (!user.value?.isDemo) await signOutFirebase()
    user.value = null
    data.value = emptyData()
    dataReady.value = true
    syncStatus.value = 'local'
  }

  async function resetSignedInUser(): Promise<void> {
    const currentUid = user.value?.uid
    stopCloudSubscription?.()
    stopCloudSubscription = undefined
    if (!user.value?.isDemo) await signOutFirebase()
    if (currentUid) clearLocalData(currentUid)
    await clearSignInArtifacts(currentUid)
    user.value = null
    data.value = emptyData()
    dataReady.value = true
    syncStatus.value = 'local'
    errorMessage.value = ''
  }

  async function createBusiness(name: string, defaultLocation: string, includeSamples = true): Promise<void> {
    if (!user.value) throw new Error('Sign in first.')
    const now = nowIso()
    const nextBusiness: Business = {
      id: user.value.uid,
      ownerUid: user.value.uid,
      name: name.trim(),
      defaultLocation: defaultLocation.trim() || 'Main Location',
      currency: 'PHP',
      timezone: 'Asia/Manila',
      createdAt: data.value.business?.createdAt || now,
      updatedAt: now
    }
    data.value.business = nextBusiness
    if (includeSamples && data.value.products.length === 0 && data.value.inventoryItems.length === 0) seedSamples(nextBusiness.id)
    persist()
    if (!user.value.isDemo && isFirebaseConfigured) {
      const uid = user.value.uid
      const changes: Array<{ collectionName: CollectionName; entity: AuditedEntity }> = []
      const names: CollectionName[] = ['inventoryItems', 'products', 'sales', 'expenses', 'movements', 'receipts', 'batches']
      names.forEach((collectionName) => {
        ;(data.value[collectionName] as AuditedEntity[]).forEach((entity) => changes.push({ collectionName, entity }))
      })
      runCloudWrite(async () => {
        await Promise.all([saveBusinessCloud(nextBusiness), saveEntitiesCloud(uid, changes)])
      })
    }
  }

  function seedSamples(businessId: string): void {
    const now = nowIso()
    const makeItem = (name: string, baseUnit: string, quantity: number, averageCostCentavos: number, minimumQuantity: number): InventoryItem => ({
      id: makeId(), businessId, name, baseUnit,
      unitKind: baseUnit === 'g' ? 'mass' : baseUnit === 'ml' ? 'volume' : baseUnit === 'pc' ? 'count' : 'custom',
      currentQuantity: quantity, averageCostCentavos, minimumQuantity, isActive: true, createdAt: now, updatedAt: now
    })
    const buko = makeItem('Buko', 'serving', 40, 800, 10)
    const milk = makeItem('Condensed Milk', 'ml', 2500, 12, 500)
    const sugar = makeItem('Sugar', 'g', 3200, 8, 500)
    const cup = makeItem('Plastic Cup', 'pc', 180, 200, 30)
    const ice = makeItem('Ice', 'serving', 60, 100, 15)
    const straw = makeItem('Straw', 'pc', 200, 50, 30)
    data.value.inventoryItems.push(buko, milk, sugar, cup, ice, straw)
    data.value.products.push({
      id: makeId(), businessId, name: 'Buko Juice', sellingPriceCentavos: 3500, manualCostCentavos: 1630,
      inventoryMode: 'untracked', recipe: [], isActive: true, createdAt: now, updatedAt: now
    })
    data.value.products.push({
      id: makeId(), businessId, name: 'Empanada', sellingPriceCentavos: 2500, manualCostCentavos: 1400,
      inventoryMode: 'untracked', recipe: [], isActive: true, createdAt: now, updatedAt: now
    })
    data.value.products.push({
      id: makeId(), businessId, name: 'Siomai', sellingPriceCentavos: 1000, manualCostCentavos: 550,
      inventoryMode: 'untracked', recipe: [], isActive: true, createdAt: now, updatedAt: now
    })
  }

  async function syncAll(): Promise<void> {
    if (!user.value || user.value.isDemo || !isFirebaseConfigured || !data.value.business) return
    const uid = user.value.uid
    const currentBusiness = data.value.business
    const changes: Array<{ collectionName: CollectionName; entity: AuditedEntity }> = []
    const names: CollectionName[] = ['inventoryItems', 'products', 'sales', 'expenses', 'movements', 'receipts', 'batches']
    names.forEach((name) => {
      ;(data.value[name] as AuditedEntity[]).forEach((entity) => changes.push({ collectionName: name, entity }))
    })
    runCloudWrite(async () => {
      await Promise.all([saveBusinessCloud(currentBusiness), saveEntitiesCloud(uid, changes)])
    })
  }

  async function saveInventoryItem(input: Partial<InventoryItem> & Pick<InventoryItem, 'name' | 'baseUnit' | 'unitKind' | 'minimumQuantity'>): Promise<InventoryItem> {
    if (!business.value) throw new Error('Create a business first.')
    const existing = input.id ? data.value.inventoryItems.find((item) => item.id === input.id) : undefined
    const name = cleanEntityName(input.name)
    if (!name) throw new Error('Enter an inventory item name.')
    if (isEntityNameTaken(name, data.value.inventoryItems, business.value.id, existing?.id)) {
      throw new Error(`An inventory item named “${name}” already exists.`)
    }
    const now = nowIso()
    const entity: InventoryItem = {
      id: existing?.id || makeId(), businessId: business.value.id, name, baseUnit: input.baseUnit,
      unitKind: input.unitKind, currentQuantity: existing?.currentQuantity || 0,
      averageCostCentavos: existing?.averageCostCentavos || 0, minimumQuantity: Number(input.minimumQuantity || 0),
      isActive: input.isActive ?? true, createdAt: existing?.createdAt || now, updatedAt: now
    }
    replaceEntity('inventoryItems', entity)
    await syncChanges([{ collectionName: 'inventoryItems', entity }])
    return entity
  }

  async function saveProduct(input: Partial<Product> & Pick<Product, 'name' | 'sellingPriceCentavos' | 'inventoryMode' | 'recipe'>): Promise<Product> {
    if (!business.value) throw new Error('Create a business first.')
    const existing = input.id ? data.value.products.find((product) => product.id === input.id) : undefined
    const name = cleanEntityName(input.name)
    if (!name) throw new Error('Enter a product name.')
    if (isEntityNameTaken(name, data.value.products, business.value.id, existing?.id)) {
      throw new Error(`A product named “${name}” already exists.`)
    }
    const now = nowIso()
    let finishedInventoryItemId = input.finishedInventoryItemId || existing?.finishedInventoryItemId
    const changes: Array<{ collectionName: CollectionName; entity: AuditedEntity }> = []
    if (input.inventoryMode === 'prepared' && !finishedInventoryItemId) {
      const finishedItem = await saveInventoryItem({
        name, baseUnit: 'pc', unitKind: 'count', minimumQuantity: 5, isActive: true
      })
      finishedInventoryItemId = finishedItem.id
    }
    const entity: Product = {
      id: existing?.id || makeId(), businessId: business.value.id, name,
      sellingPriceCentavos: Number(input.sellingPriceCentavos || 0), manualCostCentavos: Number(input.manualCostCentavos || 0),
      inventoryMode: input.inventoryMode, finishedInventoryItemId, recipe: input.recipe,
      isActive: input.isActive ?? true, createdAt: existing?.createdAt || now, updatedAt: now
    }
    replaceEntity('products', entity)
    changes.push({ collectionName: 'products', entity })
    await syncChanges(changes)
    return entity
  }

  async function addStock(input: { inventoryItemId: string; quantity: number; unit: string; toBase: number; totalCostCentavos: number; note?: string }): Promise<void> {
    if (!business.value) throw new Error('Create a business first.')
    const current = data.value.inventoryItems.find((item) => item.id === input.inventoryItemId)
    if (!current) throw new Error('Inventory item not found.')
    const now = nowIso()
    const baseQuantity = input.quantity * input.toBase
    const averageCostCentavos = weightedAverageCost(current.currentQuantity, current.averageCostCentavos, baseQuantity, input.totalCostCentavos)
    const item: InventoryItem = { ...current, currentQuantity: current.currentQuantity + baseQuantity, averageCostCentavos, updatedAt: now }
    const receipt: StockReceipt = {
      id: makeId(), businessId: business.value.id, inventoryItemId: item.id, itemName: item.name,
      quantity: input.quantity, unit: input.unit, quantityInBaseUnit: baseQuantity, totalCostCentavos: input.totalCostCentavos,
      resultingAverageCostCentavos: averageCostCentavos, receivedAt: now, note: input.note, createdAt: now, updatedAt: now
    }
    const movement: InventoryMovement = {
      id: makeId(), businessId: business.value.id, inventoryItemId: item.id, itemName: item.name,
      quantityDelta: baseQuantity, balanceAfter: item.currentQuantity, unitCostCentavos: averageCostCentavos,
      movementType: 'purchase', referenceType: 'stock_receipt', referenceId: receipt.id, note: input.note,
      createdAt: now, updatedAt: now
    }
    replaceEntity('inventoryItems', item)
    replaceEntity('receipts', receipt)
    replaceEntity('movements', movement)
    await syncChanges([
      { collectionName: 'inventoryItems', entity: item },
      { collectionName: 'receipts', entity: receipt },
      { collectionName: 'movements', entity: movement }
    ])
  }

  async function adjustStock(input: { inventoryItemId: string; countedQuantity: number; reason: string; note?: string }): Promise<void> {
    if (!business.value) throw new Error('Create a business first.')
    const current = data.value.inventoryItems.find((item) => item.id === input.inventoryItemId)
    if (!current) throw new Error('Inventory item not found.')
    const now = nowIso()
    const delta = input.countedQuantity - current.currentQuantity
    const item: InventoryItem = { ...current, currentQuantity: input.countedQuantity, updatedAt: now }
    const movement: InventoryMovement = {
      id: makeId(), businessId: business.value.id, inventoryItemId: item.id, itemName: item.name,
      quantityDelta: delta, balanceAfter: item.currentQuantity, unitCostCentavos: item.averageCostCentavos,
      movementType: input.reason === 'Waste / spoilage' ? 'waste' : 'adjustment', referenceType: 'adjustment', referenceId: makeId(),
      note: [input.reason, input.note].filter(Boolean).join(' · '), createdAt: now, updatedAt: now
    }
    replaceEntity('inventoryItems', item)
    replaceEntity('movements', movement)
    await syncChanges([
      { collectionName: 'inventoryItems', entity: item },
      { collectionName: 'movements', entity: movement }
    ])
  }

  async function recordSale(quantities: Record<string, number>, deductInventory: boolean, saleDate = localDateKey()): Promise<Sale> {
    if (!business.value) throw new Error('Create a business first.')
    const now = nowIso()
    const lines: SaleLine[] = products.value.flatMap((product) => {
      const quantity = Number(quantities[product.id] || 0)
      if (quantity <= 0) return []
      const unitCostCentavos = productCostCentavos(product, inventoryItems.value)
      return [{
        id: makeId(), productId: product.id, productName: product.name, quantity,
        unitPriceCentavos: product.sellingPriceCentavos, unitCostCentavos,
        lineRevenueCentavos: quantity * product.sellingPriceCentavos,
        lineCostCentavos: quantity * unitCostCentavos
      }]
    })
    if (lines.length === 0) throw new Error('Enter at least one quantity.')
    const sale: Sale = {
      id: makeId(), businessId: business.value.id, saleDate, location: business.value.defaultLocation,
      lines, totalItems: lines.reduce((sum, line) => sum + line.quantity, 0),
      totalRevenueCentavos: lines.reduce((sum, line) => sum + line.lineRevenueCentavos, 0),
      totalCostCentavos: lines.reduce((sum, line) => sum + line.lineCostCentavos, 0),
      totalProfitCentavos: lines.reduce((sum, line) => sum + line.lineRevenueCentavos - line.lineCostCentavos, 0),
      deductInventory, createdAt: now, updatedAt: now
    }
    const changes: Array<{ collectionName: CollectionName; entity: AuditedEntity }> = [{ collectionName: 'sales', entity: sale }]
    replaceEntity('sales', sale)
    if (deductInventory) {
      for (const line of lines) {
        const product = products.value.find((candidate) => candidate.id === line.productId)
        if (!product || product.inventoryMode === 'untracked') continue
        const deductions = product.finishedInventoryItemId
          ? [{ inventoryItemId: product.finishedInventoryItemId, quantity: line.quantity }]
          : []
        for (const deduction of deductions) {
          const current = data.value.inventoryItems.find((item) => item.id === deduction.inventoryItemId)
          if (!current) continue
          const item: InventoryItem = { ...current, currentQuantity: current.currentQuantity - deduction.quantity, updatedAt: now }
          const movement: InventoryMovement = {
            id: makeId(), businessId: business.value.id, inventoryItemId: item.id, itemName: item.name,
            quantityDelta: -deduction.quantity, balanceAfter: item.currentQuantity, unitCostCentavos: item.averageCostCentavos,
            movementType: 'sale_consumption', referenceType: 'sale', referenceId: sale.id,
            note: `${line.quantity} × ${product.name}`, createdAt: now, updatedAt: now
          }
          replaceEntity('inventoryItems', item)
          replaceEntity('movements', movement)
          changes.push({ collectionName: 'inventoryItems', entity: item }, { collectionName: 'movements', entity: movement })
        }
      }
    }
    await syncChanges(changes)
    return sale
  }

  async function saveExpense(input: Partial<Expense> & Pick<Expense, 'description' | 'category' | 'amountCentavos' | 'expenseDate'>): Promise<Expense> {
    if (!business.value) throw new Error('Create a business first.')
    const existing = input.id ? data.value.expenses.find((expense) => expense.id === input.id) : undefined
    const now = nowIso()
    const entity: Expense = {
      id: existing?.id || makeId(), businessId: business.value.id, description: input.description.trim(), category: input.category,
      amountCentavos: Number(input.amountCentavos), expenseDate: input.expenseDate,
      createdAt: existing?.createdAt || now, updatedAt: now
    }
    replaceEntity('expenses', entity)
    await syncChanges([{ collectionName: 'expenses', entity }])
    return entity
  }

  async function deleteExpense(id: string): Promise<void> {
    const existing = data.value.expenses.find((expense) => expense.id === id)
    if (!existing) return
    const entity: Expense = { ...existing, deletedAt: nowIso(), updatedAt: nowIso() }
    replaceEntity('expenses', entity)
    await syncChanges([{ collectionName: 'expenses', entity }])
  }

  async function saveBatch(input: { id?: string; productId: string; plannedYield: number; note?: string }): Promise<ProductionBatch> {
    if (!business.value) throw new Error('Create a business first.')
    const product = products.value.find((candidate) => candidate.id === input.productId)
    if (!product || product.inventoryMode !== 'prepared') throw new Error('Select a prepared-in-advance product.')
    const existing = input.id ? data.value.batches.find((batch) => batch.id === input.id) : undefined
    if (existing?.status === 'completed') throw new Error('Completed batches cannot be edited.')
    const now = nowIso()
    const ingredients: BatchIngredient[] = product.recipe.map((line) => {
      const item = inventoryItems.value.find((candidate) => candidate.id === line.inventoryItemId)
      const plannedQuantity = line.quantity * input.plannedYield
      const currentLine = existing?.ingredients.find((candidate) => candidate.inventoryItemId === line.inventoryItemId)
      return {
        inventoryItemId: line.inventoryItemId, itemName: item?.name || 'Unknown item', plannedQuantity,
        actualQuantity: currentLine?.actualQuantity ?? plannedQuantity, unitCostCentavos: item?.averageCostCentavos ?? 0
      }
    })
    const entity: ProductionBatch = {
      id: existing?.id || makeId(), businessId: business.value.id, productId: product.id, productName: product.name,
      status: 'draft', plannedYield: input.plannedYield, ingredients, note: input.note,
      createdAt: existing?.createdAt || now, updatedAt: now
    }
    replaceEntity('batches', entity)
    await syncChanges([{ collectionName: 'batches', entity }])
    return entity
  }

  async function completeBatch(input: { batchId: string; actualYield: number; actualQuantities: Record<string, number>; note?: string }): Promise<ProductionBatch> {
    if (!business.value) throw new Error('Create a business first.')
    const existing = data.value.batches.find((batch) => batch.id === input.batchId)
    if (!existing || existing.status !== 'draft') throw new Error('Draft batch not found.')
    const product = products.value.find((candidate) => candidate.id === existing.productId)
    if (!product?.finishedInventoryItemId) throw new Error('Finished inventory item is missing.')
    const now = nowIso()
    const ingredients = existing.ingredients.map((line) => ({ ...line, actualQuantity: Number(input.actualQuantities[line.inventoryItemId] ?? line.actualQuantity) }))
    const totalCostCentavos = ingredients.reduce((sum, line) => sum + Math.round(line.actualQuantity * line.unitCostCentavos), 0)
    const costPerUnitCentavos = input.actualYield > 0 ? Math.round(totalCostCentavos / input.actualYield) : 0
    const batch: ProductionBatch = {
      ...existing, status: 'completed', actualYield: input.actualYield, ingredients, totalCostCentavos, costPerUnitCentavos,
      completedAt: now, note: input.note || existing.note, updatedAt: now
    }
    const changes: Array<{ collectionName: CollectionName; entity: AuditedEntity }> = [{ collectionName: 'batches', entity: batch }]
    replaceEntity('batches', batch)
    for (const line of ingredients) {
      const current = data.value.inventoryItems.find((item) => item.id === line.inventoryItemId)
      if (!current) continue
      const item: InventoryItem = { ...current, currentQuantity: current.currentQuantity - line.actualQuantity, updatedAt: now }
      const movement: InventoryMovement = {
        id: makeId(), businessId: business.value.id, inventoryItemId: item.id, itemName: item.name,
        quantityDelta: -line.actualQuantity, balanceAfter: item.currentQuantity, unitCostCentavos: line.unitCostCentavos,
        movementType: 'batch_consumption', referenceType: 'production_batch', referenceId: batch.id,
        note: batch.productName, createdAt: now, updatedAt: now
      }
      replaceEntity('inventoryItems', item)
      replaceEntity('movements', movement)
      changes.push({ collectionName: 'inventoryItems', entity: item }, { collectionName: 'movements', entity: movement })
    }
    const currentFinished = data.value.inventoryItems.find((item) => item.id === product.finishedInventoryItemId)
    if (currentFinished) {
      const newAverage = weightedAverageCost(currentFinished.currentQuantity, currentFinished.averageCostCentavos, input.actualYield, totalCostCentavos)
      const finished: InventoryItem = {
        ...currentFinished, currentQuantity: currentFinished.currentQuantity + input.actualYield,
        averageCostCentavos: newAverage, updatedAt: now
      }
      const outputMovement: InventoryMovement = {
        id: makeId(), businessId: business.value.id, inventoryItemId: finished.id, itemName: finished.name,
        quantityDelta: input.actualYield, balanceAfter: finished.currentQuantity, unitCostCentavos: costPerUnitCentavos,
        movementType: 'batch_output', referenceType: 'production_batch', referenceId: batch.id,
        note: `Completed ${batch.productName}`, createdAt: now, updatedAt: now
      }
      replaceEntity('inventoryItems', finished)
      replaceEntity('movements', outputMovement)
      changes.push({ collectionName: 'inventoryItems', entity: finished }, { collectionName: 'movements', entity: outputMovement })
    }
    await syncChanges(changes)
    return batch
  }

  return {
    user, authReady, dataReady, data, syncStatus, lastSyncedAt, errorMessage, business, inventoryItems, products, sales,
    expenses, movements, batches, lowStockItems, todayMetrics, isFirebaseConfigured,
    bootstrap, loginWithGoogle, continueLocally, logout, resetSignedInUser, createBusiness, syncAll,
    saveInventoryItem, saveProduct, addStock, adjustStock, recordSale, saveExpense, deleteExpense,
    saveBatch, completeBatch
  }
})
