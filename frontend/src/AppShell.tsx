import { Route, Routes } from 'react-router-dom'
import { Navbar } from './components/Navbar/Navbar.tsx'
import { ConnectionStatusNotice } from './components/ConnectionStatusNotice.tsx'
import { ActionFailureNotice } from './components/ActionFailureNotice.tsx'
import { ScoreVisibilityProvider } from './context/ScoreVisibilityContext.tsx'
import { EntryEditorProvider } from './context/EntryEditorContext.tsx'
import { ActionFailureProvider } from './context/ActionFailureContext.tsx'
import { AppStatusProvider } from './context/AppStatusContext.tsx'
import { CompletionPromptProvider } from './context/CompletionPromptContext.tsx'
import { AnimeRankProvider } from './context/AnimeRankContext.tsx'
import { ContentFilterProvider } from './context/ContentFilterContext.tsx'
import { PageStateProvider } from './state/PageStateContext.tsx'
import { useScrollRestoration } from './hooks/useScrollRestoration.ts'
import { HomePage } from './pages/HomePage.tsx'
import { SeasonPage } from './pages/SeasonPage.tsx'
import { YearPage } from './pages/YearPage.tsx'
import { TopAnimePage } from './pages/TopAnimePage.tsx'
import { AiringPage } from './pages/AiringPage.tsx'
import { MyListPage } from './pages/MyListPage.tsx'
import { RecapPage } from './pages/RecapPage.tsx'
import { ProfilePage } from './pages/ProfilePage.tsx'
import { SettingsPage } from './pages/SettingsPage.tsx'
import { AnimeDetailPage } from './pages/AnimeDetailPage.tsx'
import { SearchPage } from './pages/SearchPage.tsx'
import { SeriesBrowserPage } from './pages/SeriesBrowserPage.tsx'
import { SeriesPage } from './pages/SeriesPage.tsx'

// Mounted once the "Connect to MAL" gate in App.tsx confirms a token is on
// file. Providers here (score visibility, entry editor, content filter) are
// app-wide, not per-page, since the navbar toggle and the editor overlay are
// used from anywhere in the routed content below. AppStatusProvider wraps
// only the navbar and the routes, not the whole tree, since it's the one
// poll the navbar's Settings control and the Settings page share
// (navbar-settings-status-indicator design.md D7) — App.tsx's connect gate
// above this component has no navbar to read it.
export function AppShell() {
  return (
    <ScoreVisibilityProvider>
      <ContentFilterProvider>
        <AnimeRankProvider>
          <EntryEditorProvider>
            <ActionFailureProvider>
              <CompletionPromptProvider>
                <PageStateProvider>
                  <ScrollRestorationMount />
                  <AppStatusProvider>
                    <Navbar />
                    <main className="page-content">
                      <Routes>
                        <Route path="/" element={<HomePage />} />
                        <Route path="/season" element={<SeasonPage />} />
                        <Route path="/year" element={<YearPage />} />
                        <Route path="/top" element={<TopAnimePage />} />
                        <Route path="/airing" element={<AiringPage />} />
                        <Route path="/my-list" element={<MyListPage />} />
                        <Route path="/recap" element={<RecapPage />} />
                        <Route path="/profile" element={<ProfilePage />} />
                        <Route path="/settings" element={<SettingsPage />} />
                        <Route path="/anime/:id" element={<AnimeDetailPage />} />
                        <Route path="/search" element={<SearchPage />} />
                        <Route path="/series" element={<SeriesBrowserPage />} />
                        <Route path="/series/:animeId" element={<SeriesPage />} />
                        <Route path="*" element={<HomePage />} />
                      </Routes>
                    </main>
                  </AppStatusProvider>
                  <ConnectionStatusNotice />
                  <ActionFailureNotice />
                </PageStateProvider>
              </CompletionPromptProvider>
            </ActionFailureProvider>
          </EntryEditorProvider>
        </AnimeRankProvider>
      </ContentFilterProvider>
    </ScoreVisibilityProvider>
  )
}

// A mount point rather than a call inside AppShell itself: the hook reads
// page-state context via usePageState, which requires a component rendered
// *inside* PageStateProvider, not the component that renders the provider.
function ScrollRestorationMount() {
  useScrollRestoration()
  return null
}
