import { useLayoutEffect, useRef, useState, type PointerEvent } from 'react'
import { createPortal } from 'react-dom'
import './TruncatedTitle.css'

type TruncatedTitleProps = {
  title: string
  lines: 1 | 2
  className?: string
}

const POINTER_OFFSET = 14
const VIEWPORT_MARGIN = 8

// Shared truncation + hover tooltip for any anime title that might be cut
// off. `lines` picks the truncation shape (single-line ellipsis vs. a
// two-line clamp); the tooltip only appears when the text is actually
// overflowing, so a title that fits shows nothing extra on hover.
export function TruncatedTitle({ title, lines, className }: TruncatedTitleProps) {
  const textRef = useRef<HTMLSpanElement>(null)
  const tooltipRef = useRef<HTMLDivElement>(null)
  const [pointer, setPointer] = useState<{ x: number; y: number } | null>(null)
  const [position, setPosition] = useState<{ left: number; top: number } | null>(null)

  // Runs before paint so the tooltip's first visible frame is already
  // clamped to the viewport, using its just-rendered size — there is no
  // reliable width/height to clamp against before it has rendered once.
  useLayoutEffect(() => {
    if (!pointer) {
      setPosition(null)
      return
    }
    const tooltip = tooltipRef.current
    const width = tooltip?.offsetWidth ?? 0
    const height = tooltip?.offsetHeight ?? 0
    // A 2-line clamped title is tall enough that "pointer + a small offset"
    // can still land inside the title's own box (e.g. hovering its top edge),
    // putting the tooltip right over the text it's meant to reveal. Floor the
    // top position at the title element's own bottom edge so the tooltip
    // never overlaps the row it describes, however tall that row is.
    const elementBottom = textRef.current?.getBoundingClientRect().bottom ?? pointer.y
    const preferredTop = Math.max(pointer.y + POINTER_OFFSET, elementBottom + POINTER_OFFSET)
    const left = Math.min(pointer.x + POINTER_OFFSET, window.innerWidth - width - VIEWPORT_MARGIN)
    const top = Math.min(preferredTop, window.innerHeight - height - VIEWPORT_MARGIN)
    setPosition({ left: Math.max(VIEWPORT_MARGIN, left), top: Math.max(VIEWPORT_MARGIN, top) })
  }, [pointer])

  function updatePointerIfOverflowing(event: PointerEvent<HTMLSpanElement>) {
    const el = textRef.current
    if (!el) return
    const isOverflowing = lines === 1 ? el.scrollWidth > el.clientWidth : el.scrollHeight > el.clientHeight
    if (!isOverflowing) {
      setPointer(null)
      return
    }
    setPointer({ x: event.clientX, y: event.clientY })
  }

  return (
    <>
      <span
        ref={textRef}
        className={[className, lines === 1 ? 'truncated-title--single' : 'truncated-title--clamped']
          .filter(Boolean)
          .join(' ')}
        onPointerEnter={updatePointerIfOverflowing}
        onPointerMove={updatePointerIfOverflowing}
        onPointerLeave={() => setPointer(null)}
      >
        {title}
      </span>
      {pointer &&
        createPortal(
          <div
            ref={tooltipRef}
            className="truncated-title__tooltip"
            style={
              position
                ? { left: position.left, top: position.top }
                : { left: pointer.x + POINTER_OFFSET, top: pointer.y + POINTER_OFFSET, visibility: 'hidden' }
            }
          >
            {title}
          </div>,
          document.body,
        )}
    </>
  )
}
