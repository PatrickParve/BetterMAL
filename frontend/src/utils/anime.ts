// Prefer the English title wherever an anime title is displayed, falling
// back to the default (usually romaji/native) title when MAL has none.
export function pickDisplayTitle(title: string, englishTitle: string | null | undefined): string {
  return englishTitle && englishTitle.trim().length > 0 ? englishTitle : title
}
