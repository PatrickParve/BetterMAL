import tmdbLogo from '../../assets/TMDB_Logo.svg'
import './TmdbAttribution.css'

// The attribution TMDB's API terms of use require (§3 "Attribution"): its
// logo, unaltered, to identify the app's use of TMDB, and this notice placed
// prominently in or on the app. The wording is TMDB's own — the bracketed noun
// is one of "website, program, service, application, product" — so it is not
// paraphrased. Both surfaces that credit TMDB render this one component: the
// picture picker (whenever it offers TMDB images) and the Settings page's
// Credits group (always).
const NOTICE = 'This application uses TMDB and the TMDB APIs but is not endorsed, certified, or otherwise approved by TMDB.'

type TmdbAttributionProps = {
  // The small form used inside the picker: the logo has to stay less prominent
  // than the app's own marks, and the picker's footer is quiet.
  compact?: boolean
}

export function TmdbAttribution({ compact }: TmdbAttributionProps) {
  return (
    <div className={compact ? 'tmdb-attribution tmdb-attribution--compact' : 'tmdb-attribution'}>
      <a
        href="https://www.themoviedb.org/"
        target="_blank"
        rel="noreferrer"
        className="tmdb-attribution__logo-link"
      >
        <img src={tmdbLogo} alt="The Movie Database (TMDB)" className="tmdb-attribution__logo" />
      </a>
      <p className="tmdb-attribution__notice">{NOTICE}</p>
    </div>
  )
}
