import { useParams } from 'react-router-dom'
import { PagePlaceholder } from './PagePlaceholder.tsx'

export function AnimeDetailPage() {
  const { id } = useParams()
  return <PagePlaceholder title={`Anime #${id}`} />
}
