import { useEffect } from 'react'
import type { AnimeUpdateDto } from '../../api/types.ts'
import { UpdateCard } from './UpdateCard.tsx'
import { useCappedCardHeight } from './useCappedCardHeight.ts'
import { useUpdateLook } from './useUpdateLook.ts'
import { useSeenTracking } from './useSeenTracking.ts'
import { useUpdatesSeen, flushSeenReports } from './updatesSeenStore.ts'

type UpdatesDropdownProps = {
  items: AnimeUpdateDto[]
  onNavigate: () => void
  onOpenHistory: () => void
}

// The dropdown panel, split out of UpdatesMenu so it mounts fresh on every
// open (store-seen-updates-on-server design.md D6): its look, its tracker
// and its height measurement all start over by construction rather than
// needing to be reset by hand. Keeps the `updates-menu__*` classes, so
// UpdatesMenu.css needs no change.
export function UpdatesDropdown({ items, onNavigate, onOpenHistory }: UpdatesDropdownProps) {
  const { isSeen, reportSeen } = useUpdatesSeen()
  const { listRef, maxHeight: listMaxHeight, listNode } = useCappedCardHeight(items.length)
  const newIds = useUpdateLook(items, isSeen)
  useSeenTracking(listNode, items, isSeen, reportSeen)

  useEffect(() => {
    return () => flushSeenReports()
  }, [])

  return (
    <div className="updates-menu__dropdown">
      <div className="updates-menu__header">
        <span className="updates-menu__title">Updates</span>
        <button type="button" className="updates-menu__history-button" onClick={onOpenHistory}>
          History
        </button>
      </div>

      {items.length === 0 ? (
        <p className="updates-menu__empty">No recent updates.</p>
      ) : (
        <ul
          className="updates-menu__list"
          ref={listRef}
          style={listMaxHeight !== undefined ? { maxHeight: listMaxHeight } : undefined}
        >
          {items.map((item) => (
            <li key={item.id} data-update-id={item.id}>
              <UpdateCard item={item} variant="menu" onNavigate={onNavigate} isNew={newIds.has(item.id)} />
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
