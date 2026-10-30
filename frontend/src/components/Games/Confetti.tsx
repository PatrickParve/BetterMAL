import { useEffect, useRef, type RefObject } from 'react'
import { createPortal } from 'react-dom'
import './Confetti.css'

// How long the burst lasts.
const DURATION_MS = 1800
const MOST_PIECES = 160

// More at each milestone than the one before, up to a set most (design.md D17):
// 10 has 40, 100 has 100, and 1,000 and beyond have 160.
function confettiPieces(level: number): number {
  return Math.min(40 + 20 * level, MOST_PIECES)
}

type ConfettiProps = {
  /** The milestone's place in the sequence (10 is 0, 25 is 1, and so on). */
  level: number
  /** Where the burst starts: the centre of this element, or of the window when it's gone. */
  originRef: RefObject<HTMLElement | null>
  onDone: () => void
}

// A milestone's burst, drawn on one fixed, full-window canvas that takes no
// pointer, in its own requestAnimationFrame loop. Scaled by elapsed time so it
// runs the same at any refresh rate. A canvas rather than DOM pieces, because
// transformed pieces can extend the page's scrollable area, which the "no
// sideways scroll" rule forbids, and it is about 50 lines against a dependency.
// The host never mounts it under reduced motion or forced colours.
export function Confetti({ level, originRef, onDone }: ConfettiProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const onDoneRef = useRef(onDone)
  onDoneRef.current = onDone

  useEffect(() => {
    const canvas = canvasRef.current
    const ctx = canvas?.getContext('2d')
    if (!canvas || !ctx) {
      onDoneRef.current()
      return
    }

    const dpr = window.devicePixelRatio || 1
    canvas.width = window.innerWidth * dpr
    canvas.height = window.innerHeight * dpr
    ctx.scale(dpr, dpr)

    const rect = originRef.current?.getBoundingClientRect()
    const x0 = rect ? rect.left + rect.width / 2 : window.innerWidth / 2
    const y0 = rect ? rect.top + rect.height / 2 : window.innerHeight / 2

    // The app's own tokens, so the pieces suit both themes.
    const css = getComputedStyle(document.documentElement)
    const colors = ['--game-milestone', '--accent', '--game-correct', '--mal'].map((name) => css.getPropertyValue(name).trim())

    const pieces = Array.from({ length: confettiPieces(level) }, (_, i) => {
      const angle = Math.random() * Math.PI * 2
      const speed = 5 + Math.random() * 12
      return {
        x: x0,
        y: y0,
        vx: Math.cos(angle) * speed,
        vy: Math.sin(angle) * speed - 4,
        size: 6 + Math.random() * 6,
        rotation: Math.random() * 6,
        spin: Math.random() * 0.5 - 0.25,
        color: colors[i % colors.length],
      }
    })

    const start = performance.now()
    let last = start
    let frame = 0
    const draw = (now: number) => {
      const t = (now - start) / DURATION_MS
      // Steps in 60 Hz frames, whatever the real frame time is.
      const k = (now - last) / (1000 / 60)
      last = now
      ctx.clearRect(0, 0, window.innerWidth, window.innerHeight)
      ctx.globalAlpha = Math.max(0, Math.min(1, (1 - t) * 3))
      for (const p of pieces) {
        p.vx *= 0.985 ** k
        p.vy = p.vy * 0.985 ** k + 0.24 * k
        p.x += p.vx * k
        p.y += p.vy * k
        p.rotation += p.spin * k
        ctx.save()
        ctx.translate(p.x, p.y)
        ctx.rotate(p.rotation)
        ctx.fillStyle = p.color
        ctx.fillRect(-p.size / 2, -p.size / 3, p.size, p.size * 0.66)
        ctx.restore()
      }
      if (t < 1) frame = requestAnimationFrame(draw)
      else onDoneRef.current()
    }
    frame = requestAnimationFrame(draw)
    return () => cancelAnimationFrame(frame)
  }, [level, originRef])

  return createPortal(<canvas ref={canvasRef} className="confetti" aria-hidden="true" />, document.body)
}
