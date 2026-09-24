import { useId } from 'react'
import { Modal } from './Modal.tsx'
import {
  CURRENT_GROUP_KEY,
  type PickerGroup,
  type PickerOption,
  type PickerSection,
} from './picturePickerSections.ts'
import { TmdbAttribution } from './TmdbAttribution.tsx'
import './PicturePickerOverlay.css'

type PicturePickerOverlayProps = {
  title: string
  sections: PickerSection[]
  // Which groups are open, by PickerGroup.key. Owned by the page rather than
  // by this overlay so that closing the picker and reopening it shows what was
  // last open (see usePickerOpenGroups); this component only reads it and
  // reports a toggle.
  openGroups: ReadonlySet<string>
  onToggleGroup: (key: string) => void
  // The current selection is marked wherever it appears, and is always shown:
  // when no section lists it (a choice its source has since dropped) it gets a
  // "Current picture" group of its own ahead of the sections, so a choice
  // stays visible and replaceable rather than disappearing (design D9 /
  // artwork-selection "A stored choice is never re-validated away").
  current: string | null
  onPick: (url: string) => void
  onClose: () => void
  // Quiet notes shown below the sections rather than errors — "TMDB has no
  // match", "N members not yet fetched" (design D6, D12).
  notes?: string[]
  // True when any section holds TMDB images, which obliges TMDB's logo and
  // notice (tmdb-artwork "TMDB is credited with its logo and notice").
  showTmdbAttribution?: boolean
  // Supplied only when a choice is stored — its presence is the client's
  // only signal that there is anything to clear (spec artwork-selection
  // "The picture picker").
  onClear?: () => void
}

// Shared by the anime detail page and the series page (design D14): the
// picture options inside the existing Modal, divided into labelled sections
// by source, click-to-choose with no separate confirm step, following
// TopAnimeSelectionOverlay's interaction and RelatedAnimeOverlay's grid/list
// structure. Picking closes the overlay immediately (spec anime-detail "A
// chosen picture applies immediately"); the caller updates its own state and
// fires the save, optimistically or otherwise — this component does not wait
// on it.
//
// Every group opens and closes under its own heading, and a closed group
// renders no <img> at all — never a hidden one, since a browser still
// downloads a display:none image. TMDB's `original` images are heavy, so what
// stays closed is never fetched. Opening a group only mounts its images, and
// those come straight from the image CDN: no request to the backend or to the
// TMDB API is involved.
export function PicturePickerOverlay({
  title,
  sections,
  openGroups,
  onToggleGroup,
  current,
  onPick,
  onClose,
  notes,
  showTmdbAttribution,
  onClear,
}: PicturePickerOverlayProps) {
  const idPrefix = useId()

  function handlePick(url: string) {
    onPick(url)
    onClose()
  }

  function handleClear() {
    onClear?.()
    onClose()
  }

  const currentIsListed =
    current !== null &&
    sections.some((section) => section.groups.some((group) => group.options.some((option) => option.url === current)))
  const shownSections: PickerSection[] =
    current !== null && !currentIsListed
      ? [
          {
            key: CURRENT_GROUP_KEY,
            heading: 'Current picture',
            groups: [{ key: CURRENT_GROUP_KEY, options: [{ url: current }] }],
          },
          ...sections,
        ]
      : sections

  return (
    <Modal onClose={onClose} labelledBy="picture-picker-overlay-title" className="modal--wide">
      <div className="picture-picker-overlay">
        <div className="picture-picker-overlay__header">
          <h2 id="picture-picker-overlay-title" className="picture-picker-overlay__title">
            {title}
          </h2>
          <div className="picture-picker-overlay__actions">
            {onClear && (
              <button type="button" className="picture-picker-overlay__clear" onClick={handleClear}>
                Default
              </button>
            )}
            <button type="button" className="picture-picker-overlay__close" aria-label="Close" onClick={onClose}>
              <CloseIcon />
            </button>
          </div>
        </div>

        <div className="picture-picker-overlay__sections">
          {shownSections.map((section) => {
            // A section whose only group has no heading of its own (the
            // MyAnimeList section, the "Current picture" one) is that group:
            // its heading is the group's toggle. Any other section's heading
            // is a plain label, and each of its groups carries a toggle.
            const [onlyGroup] = section.groups
            const headingIsToggle = section.groups.length === 1 && onlyGroup.heading === undefined

            return (
              <section key={section.key} className="picture-picker-overlay__section">
                {headingIsToggle ? (
                  <PickerGroupBlock
                    group={onlyGroup}
                    heading={section.heading}
                    asSectionHeading
                    idPrefix={idPrefix}
                    open={openGroups.has(onlyGroup.key)}
                    onToggle={onToggleGroup}
                    current={current}
                    onPick={handlePick}
                  />
                ) : (
                  <>
                    <h3 className="picture-picker-overlay__section-heading">{section.heading}</h3>
                    {section.groups.map((group) => (
                      <PickerGroupBlock
                        key={group.key}
                        group={group}
                        heading={group.heading ?? section.heading}
                        idPrefix={idPrefix}
                        open={openGroups.has(group.key)}
                        onToggle={onToggleGroup}
                        current={current}
                        onPick={handlePick}
                      />
                    ))}
                  </>
                )}
              </section>
            )
          })}
        </div>

        {notes?.map((note) => (
          <p key={note} className="picture-picker-overlay__note">
            {note}
          </p>
        ))}
        {showTmdbAttribution && <TmdbAttribution compact />}
      </div>
    </Modal>
  )
}

type PickerGroupBlockProps = {
  group: PickerGroup
  heading: string
  // The group's toggle is its section's heading (a section that is one
  // headingless group), and is drawn as one.
  asSectionHeading?: boolean
  idPrefix: string
  open: boolean
  onToggle: (key: string) => void
  current: string | null
  onPick: (url: string) => void
}

function PickerGroupBlock({
  group,
  heading,
  asSectionHeading,
  idPrefix,
  open,
  onToggle,
  current,
  onPick,
}: PickerGroupBlockProps) {
  const panelId = `${idPrefix}-${group.key}`
  const Heading = asSectionHeading ? 'h3' : 'h4'

  return (
    <div className="picture-picker-overlay__group">
      <Heading className="picture-picker-overlay__group-heading">
        <button
          type="button"
          className={
            asSectionHeading
              ? 'picture-picker-overlay__toggle picture-picker-overlay__toggle--section'
              : 'picture-picker-overlay__toggle'
          }
          aria-expanded={open}
          aria-controls={open ? panelId : undefined}
          onClick={() => onToggle(group.key)}
        >
          <span className="picture-picker-overlay__caret" aria-hidden="true">
            {open ? '▾' : '▸'}
          </span>
          {heading} · {group.options.length}
        </button>
      </Heading>
      {open && (
        <div id={panelId} className="picture-picker-overlay__grid">
          {group.options.map((option) => (
            <PickerOptionButton key={option.url} option={option} selected={option.url === current} onPick={onPick} />
          ))}
        </div>
      )}
    </div>
  )
}

function PickerOptionButton({
  option,
  selected,
  onPick,
}: {
  option: PickerOption
  selected: boolean
  onPick: (url: string) => void
}) {
  return (
    <button
      type="button"
      className={
        selected
          ? 'picture-picker-overlay__option picture-picker-overlay__option--selected'
          : 'picture-picker-overlay__option'
      }
      onClick={() => onPick(option.url)}
      aria-pressed={selected}
    >
      {/* width/height are set only when TMDB gave them: the browser derives
          the image's aspect ratio from them, so a backdrop reserves its
          landscape footprint before it decodes. Lazy and async because an
          opened group can still hold a hundred `original` images. */}
      <img
        src={option.url}
        alt=""
        width={option.width}
        height={option.height}
        loading="lazy"
        decoding="async"
        className="picture-picker-overlay__image"
      />
      {selected && <span className="picture-picker-overlay__badge">Current</span>}
    </button>
  )
}

function CloseIcon() {
  return (
    <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <line x1="6" y1="6" x2="18" y2="18" />
      <line x1="18" y1="6" x2="6" y2="18" />
    </svg>
  )
}
