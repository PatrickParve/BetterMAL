import { useEffect, useState } from 'react'
import './DateField.css'

const MONTH_LABELS = [
  'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
]

type Parts = { year: number | ''; month: number | ''; day: number | '' }

const EMPTY_PARTS: Parts = { year: '', month: '', day: '' }

function parseValue(value: string): Parts {
  if (!value) return EMPTY_PARTS
  const [year, month, day] = value.split('-').map(Number)
  return { year, month, day }
}

function daysInMonth(year: Parts['year'], month: Parts['month']): number {
  if (year === '' || month === '') return 31
  return new Date(year, month, 0).getDate()
}

function today(): { year: number; month: number; day: number } {
  const now = new Date()
  return { year: now.getFullYear(), month: now.getMonth() + 1, day: now.getDate() }
}

function formatValue(parts: Parts): string {
  const year = String(parts.year).padStart(4, '0')
  const month = String(parts.month).padStart(2, '0')
  const day = String(parts.day).padStart(2, '0')
  return `${year}-${month}-${day}`
}

type DateFieldProps = {
  value: string
  onChange: (next: string) => void
  label?: string
  idPrefix: string
}

// design.md D3: holds { year, month, day } itself rather than deriving them
// from `value` on every render — a field being filled in passes through
// states ('year picked, month not') that map to no date at all, and a purely
// derived field would throw the user's first pick away the moment it
// emitted ''. Re-seeds from `value` only when it changes from outside.
export function DateField({ value, onChange, label, idPrefix }: DateFieldProps) {
  const [parts, setParts] = useState<Parts>(() => parseValue(value))

  useEffect(() => {
    setParts(parseValue(value))
  }, [value])

  const fieldLabel = label ?? 'date'
  const { year: todayYear, month: todayMonth, day: todayDay } = today()
  const isEmpty = parts.year === '' && parts.month === '' && parts.day === ''

  // A date being entered may not land in the future (list-editing: dates
  // can't be set ahead of today), so the year/month/day lists themselves
  // only ever offer choices that keep the field at or behind today — there's
  // nothing to reject at commit time because there's nothing future left to
  // pick from any of the three.
  const years: number[] = []
  for (let year = todayYear; year >= 1970; year--) years.push(year)
  const externalYear = parseValue(value).year
  if (externalYear !== '' && !years.includes(externalYear)) {
    years.push(externalYear)
    years.sort((a, b) => b - a)
  }
  const monthCount = parts.year === todayYear ? todayMonth : 12
  const daysInSelectedMonth = daysInMonth(parts.year, parts.month)
  const dayCount = parts.year === todayYear && parts.month === todayMonth
    ? Math.min(daysInSelectedMonth, todayDay)
    : daysInSelectedMonth

  function commit(next: Parts) {
    setParts(next)
    onChange(next.year !== '' && next.month !== '' && next.day !== '' ? formatValue(next) : '')
  }

  // Selecting the blank option in any one dropdown clears the whole field
  // (design.md D3), not just the part that was changed.
  function handlePartChange(part: keyof Parts, raw: string) {
    if (raw === '') {
      commit(EMPTY_PARTS)
      return
    }
    const next: Parts = { ...parts, [part]: Number(raw) }
    if (part === 'year' || part === 'month') {
      const limit = daysInMonth(next.year, next.month)
      if (next.day !== '' && next.day > limit) next.day = limit
    }
    // Changing the year or month out from under a later-in-the-day choice
    // can leave the field in the future (e.g. May 31 -> September while
    // today is September 15 is fine, but May 31 -> the current month isn't)
    // — clamp month then day back onto today rather than emit a future date.
    if (next.year === todayYear && next.month !== '' && next.month > todayMonth) next.month = todayMonth
    if (next.year === todayYear && next.month === todayMonth && next.day !== '' && next.day > todayDay) next.day = todayDay
    commit(next)
  }

  function handleToday() {
    commit(today())
  }

  return (
    <div className="date-field">
      <select
        id={`${idPrefix}-year`}
        className="date-field__part date-field__part--year"
        aria-label={`${fieldLabel} year`}
        value={parts.year}
        onChange={(event) => handlePartChange('year', event.target.value)}
      >
        <option value="">-</option>
        {years.map((year) => (
          <option key={year} value={year}>
            {year}
          </option>
        ))}
      </select>
      <select
        id={`${idPrefix}-month`}
        className="date-field__part date-field__part--month"
        aria-label={`${fieldLabel} month`}
        value={parts.month}
        onChange={(event) => handlePartChange('month', event.target.value)}
      >
        <option value="">-</option>
        {MONTH_LABELS.slice(0, monthCount).map((monthLabel, index) => (
          <option key={monthLabel} value={index + 1}>
            {monthLabel}
          </option>
        ))}
      </select>
      <select
        id={`${idPrefix}-day`}
        className="date-field__part date-field__part--day"
        aria-label={`${fieldLabel} day`}
        value={parts.day}
        onChange={(event) => handlePartChange('day', event.target.value)}
      >
        <option value="">-</option>
        {Array.from({ length: dayCount }, (_, i) => i + 1).map((day) => (
          <option key={day} value={day}>
            {day}
          </option>
        ))}
      </select>
      <button
        type="button"
        className="date-field__today"
        aria-label={`Set ${fieldLabel} to today`}
        onClick={handleToday}
      >
        Today
      </button>
      <button
        type="button"
        className="date-field__clear"
        aria-label={`Clear ${fieldLabel}`}
        disabled={isEmpty}
        onClick={() => commit(EMPTY_PARTS)}
      >
        Clear
      </button>
    </div>
  )
}
