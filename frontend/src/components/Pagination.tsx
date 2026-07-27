import './Pagination.css'

type PaginationProps = {
  currentPage: number
  totalPages: number
  onPageChange: (page: number) => void
  /** 'arrows' renders just the prev/next controls (for a compact top-right placement); 'full' also renders page-number buttons. */
  variant?: 'full' | 'arrows'
  className?: string
}

const WINDOW_SIZE = 1

// Page-number buttons around the current page, with an ellipsis-truncated
// window plus the first/last page always shown.
function pageNumbers(currentPage: number, totalPages: number): (number | 'ellipsis')[] {
  const pages: (number | 'ellipsis')[] = []
  for (let page = 1; page <= totalPages; page++) {
    const isEdge = page === 1 || page === totalPages
    const isNearCurrent = Math.abs(page - currentPage) <= WINDOW_SIZE
    if (isEdge || isNearCurrent) {
      pages.push(page)
    } else if (pages[pages.length - 1] !== 'ellipsis') {
      pages.push('ellipsis')
    }
  }
  return pages
}

// Reusable pagination control: page-number buttons plus left/right arrows
// (`variant="full"`, the default), or just the arrows (`variant="arrows"`,
// for a compact placement alongside a heading).
export function Pagination({ currentPage, totalPages, onPageChange, variant = 'full', className }: PaginationProps) {
  if (totalPages <= 1) return null

  function go(page: number) {
    if (page >= 1 && page <= totalPages && page !== currentPage) onPageChange(page)
  }

  return (
    <nav className={className ? `pagination ${className}` : 'pagination'} aria-label="Pagination">
      <button
        type="button"
        className="pagination__arrow"
        onClick={() => go(currentPage - 1)}
        disabled={currentPage === 1}
        aria-label="Previous page"
      >
        &lsaquo;
      </button>

      {variant === 'full' &&
        pageNumbers(currentPage, totalPages).map((page, index) =>
          page === 'ellipsis' ? (
            <span key={`ellipsis-${index}`} className="pagination__ellipsis">
              …
            </span>
          ) : (
            <button
              key={page}
              type="button"
              className={page === currentPage ? 'pagination__page pagination__page--active' : 'pagination__page'}
              aria-current={page === currentPage ? 'page' : undefined}
              onClick={() => go(page)}
            >
              {page}
            </button>
          ),
        )}

      <button
        type="button"
        className="pagination__arrow"
        onClick={() => go(currentPage + 1)}
        disabled={currentPage === totalPages}
        aria-label="Next page"
      >
        &rsaquo;
      </button>
    </nav>
  )
}
