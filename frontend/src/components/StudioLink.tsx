import { Fragment } from 'react'
import { studioLinkHref } from '../utils/anime.ts'

// A studio's name as a link to its page on AniList (studio-links, design.md D11).
// The link's address is the app's own, which sends the new tab on to the studio's
// page or to AniList's search for the name (studioLinkHref); it opens in a new tab
// as the app's other links to MyAnimeList and AniList do, so the page it was
// followed from stays where it was. The text is the name exactly as MyAnimeList
// credits it. Colours are the surrounding text's own: the info box and the series
// stats already colour a `dd a` as text at rest and the accent on hover.
export function StudioLink({ name, className }: { name: string; className?: string }) {
  return (
    <a href={studioLinkHref(name)} target="_blank" rel="noreferrer" className={className}>
      {name}
    </a>
  )
}

// Several studios named together: each its own link, and the commas between them
// outside every link, so a click on a separator follows nothing.
export function StudioLinks({ names }: { names: string[] }) {
  return (
    <>
      {names.map((name, index) => (
        <Fragment key={name}>
          {index > 0 && ', '}
          <StudioLink name={name} />
        </Fragment>
      ))}
    </>
  )
}
