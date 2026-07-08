type PagePlaceholderProps = {
  title: string
}

// Stand-in for pages whose real content lands in later sections; keeps
// routing/navigation fully wired and testable ahead of that work.
export function PagePlaceholder({ title }: PagePlaceholderProps) {
  return (
    <section className="page-placeholder">
      <h1>{title}</h1>
    </section>
  )
}
