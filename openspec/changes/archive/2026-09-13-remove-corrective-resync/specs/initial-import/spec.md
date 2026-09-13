## REMOVED Requirements

### Requirement: Corrective full re-sync upsert
**Reason**: The corrective re-sync ("Correct imported data") is removed. Two existing jobs cover it. Run full reconciliation reads the same MyAnimeList list and applies status, episodes watched, score and dates once I accept its diff. The scheduled tiered refresh fetches full detail for every anime on my list, so cached metadata is refreshed, staggered by tier. The re-sync also re-created anime whose removal from my list hadn't reached MyAnimeList yet, and removing it ends that.
**Migration**: For list values, run full reconciliation from the Settings page and accept its diff. Like the re-sync, it leaves entries with unsent local edits alone. Cached metadata needs nothing: the tiered refresh reaches every anime on my list within its tier, which is at most 28 days for a show that finished long ago. A single anime can be refreshed straight away from its detail page, or with the Settings page's force-refresh.
