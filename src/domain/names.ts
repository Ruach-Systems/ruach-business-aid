interface NamedEntity {
  id: string
  name: string
  businessId: string
  deletedAt?: string
}

export const cleanEntityName = (name: string): string =>
  name.normalize('NFKC').trim().replace(/\s+/g, ' ')

const entityNameKey = (name: string): string => cleanEntityName(name).toLocaleLowerCase('en')

export function isEntityNameTaken(
  name: string,
  entities: NamedEntity[],
  businessId: string,
  currentId?: string
): boolean {
  const key = entityNameKey(name)
  return entities.some((entity) =>
    !entity.deletedAt
    && entity.businessId === businessId
    && entity.id !== currentId
    && entityNameKey(entity.name) === key
  )
}
