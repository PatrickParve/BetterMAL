using AnimeTracker.Api.Services.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

// SeriesYearSpan is the one home of the year-span rule the series page's header
// and the Series browser's card share (polish-series-header-and-completion-
// prompt design.md D1). It is handed the main line's air dates only, so these
// tests pin the arithmetic itself: which year opens the span, which closes it,
// and what an undated entry does. Which entries reach it is SeriesListFigures-
// Tests' and SeriesServiceYearSpanTests' subject.
public class SeriesYearSpanTests
{
    private static (DateOnly? AiredFrom, DateOnly? AiredTo) Aired(int fromYear, int? toYear = null) =>
        (new DateOnly(fromYear, 4, 1), toYear is { } to ? new DateOnly(to, 9, 1) : null);

    [Fact]
    public void ASingleEntryGivesItsOwnYears()
    {
        var (first, last) = SeriesYearSpan.Of([Aired(2013, 2015)]);

        Assert.Equal(2013, first);
        Assert.Equal(2015, last);
    }

    [Fact]
    public void SeveralEntriesRunFromTheEarliestStartToTheLatestFinish()
    {
        // Deliberately out of order, and the latest finish belongs to the middle entry.
        var (first, last) = SeriesYearSpan.Of([Aired(2016, 2017), Aired(2013, 2013), Aired(2014, 2019)]);

        Assert.Equal(2013, first);
        Assert.Equal(2019, last);
    }

    [Fact]
    public void AnEntryWithNoFinishDateEndsTheSpanAtItsStartYear()
    {
        // Still airing, or not aired yet: AiredTo isn't known.
        var (first, last) = SeriesYearSpan.Of([Aired(2013, 2013), Aired(2024)]);

        Assert.Equal(2013, first);
        Assert.Equal(2024, last);
    }

    [Fact]
    public void AnUndatedEntryIsIgnored()
    {
        // No start date, though it carries a finish date: it must not count at
        // all, or a stray AiredTo would stretch the span.
        var undated = ((DateOnly?)null, (DateOnly?)new DateOnly(2030, 1, 1));

        var (first, last) = SeriesYearSpan.Of([Aired(2013, 2015), undated]);

        Assert.Equal(2013, first);
        Assert.Equal(2015, last);
    }

    [Fact]
    public void NoDatedEntryGivesNoSpan()
    {
        var (first, last) = SeriesYearSpan.Of([((DateOnly?)null, (DateOnly?)null), ((DateOnly?)null, (DateOnly?)null)]);

        Assert.Null(first);
        Assert.Null(last);
    }

    [Fact]
    public void AnEmptyInputGivesNoSpan()
    {
        var (first, last) = SeriesYearSpan.Of([]);

        Assert.Null(first);
        Assert.Null(last);
    }
}
