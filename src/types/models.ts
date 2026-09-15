export type SyncStatus = 'local' | 'syncing' | 'synced' | 'error'
export type ProductInventoryMode = 'prepared' | 'untracked'
export type MovementType =
  | 'purchase'
  | 'sale_consumption'
  | 'batch_consumption'
  | 'batch_output'
  | 'adjustment'
  | 'waste'

export interface AuditedEntity {
  id: string
  businessId: string
  createdAt: string
  updatedAt: string
  deletedAt?: string
}

export interface AppUser {
  uid: string
  displayName: string
  email: string
  photoURL?: string
  isDemo?: boolean
}

export interface Business {
  id: string
  ownerUid: string
  name: string
  defaultLocation: string
  currency: 'PHP'
  timezone: 'Asia/Manila'
  createdAt: string
  updatedAt: string
}

export type UnitKind = 'mass' | 'volume' | 'count' | 'custom'

export interface UnitDefinition {
  code: string
  label: string
  kind: UnitKind
  toBase: number
  baseCode: string
}

export interface InventoryItem extends AuditedEntity {
  name: string
  baseUnit: string
  unitKind: UnitKind
  currentQuantity: number
  averageCostCentavos: number
  minimumQuantity: number
  isActive: boolean
}

export interface RecipeLine {
  inventoryItemId: string
  quantity: number
}

export interface Product extends AuditedEntity {
  name: string
  sellingPriceCentavos: number
  manualCostCentavos: number
  inventoryMode: ProductInventoryMode
  finishedInventoryItemId?: string
  recipe: RecipeLine[]
  isActive: boolean
}

export interface SaleLine {
  id: string
  productId: string
  productName: string
  quantity: number
  unitPriceCentavos: number
  unitCostCentavos: number
  lineRevenueCentavos: number
  lineCostCentavos: number
}

export interface Sale extends AuditedEntity {
  saleDate: string
  location: string
  lines: SaleLine[]
  totalItems: number
  totalRevenueCentavos: number
  totalCostCentavos: number
  totalProfitCentavos: number
  deductInventory: boolean
}

export interface Expense extends AuditedEntity {
  description: string
  category: string
  amountCentavos: number
  expenseDate: string
}

export interface InventoryMovement extends AuditedEntity {
  inventoryItemId: string
  itemName: string
  quantityDelta: number
  balanceAfter: number
  unitCostCentavos: number
  movementType: MovementType
  referenceType: 'sale' | 'stock_receipt' | 'production_batch' | 'adjustment'
  referenceId: string
  note?: string
}

export interface StockReceipt extends AuditedEntity {
  inventoryItemId: string
  itemName: string
  quantity: number
  unit: string
  quantityInBaseUnit: number
  totalCostCentavos: number
  resultingAverageCostCentavos: number
  receivedAt: string
  note?: string
}

export interface BatchIngredient {
  inventoryItemId: string
  itemName: string
  plannedQuantity: number
  actualQuantity: number
  unitCostCentavos: number
}

export interface ProductionBatch extends AuditedEntity {
  productId: string
  productName: string
  status: 'draft' | 'completed'
  plannedYield: number
  actualYield?: number
  ingredients: BatchIngredient[]
  totalCostCentavos?: number
  costPerUnitCentavos?: number
  completedAt?: string
  note?: string
}

export interface AppData {
  business: Business | null
  inventoryItems: InventoryItem[]
  products: Product[]
  sales: Sale[]
  expenses: Expense[]
  movements: InventoryMovement[]
  receipts: StockReceipt[]
  batches: ProductionBatch[]
}

export type CollectionName = Exclude<keyof AppData, 'business'>

export const unitDefinitions: UnitDefinition[] = [
  { code: 'g', label: 'gram', kind: 'mass', toBase: 1, baseCode: 'g' },
  { code: 'kg', label: 'kilogram', kind: 'mass', toBase: 1000, baseCode: 'g' },
  { code: 'ml', label: 'milliliter', kind: 'volume', toBase: 1, baseCode: 'ml' },
  { code: 'L', label: 'liter', kind: 'volume', toBase: 1000, baseCode: 'ml' },
  { code: 'pc', label: 'piece', kind: 'count', toBase: 1, baseCode: 'pc' },
  { code: 'pack', label: 'pack', kind: 'custom', toBase: 1, baseCode: 'pack' },
  { code: 'serving', label: 'serving', kind: 'custom', toBase: 1, baseCode: 'serving' }
]
