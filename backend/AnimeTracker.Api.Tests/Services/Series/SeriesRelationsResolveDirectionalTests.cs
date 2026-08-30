using AnimeTracker.Api.Services.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

// SeriesRelations.ResolveDirectional (split-series-by-version design.md
// decision 4, tasks 2.2/2.4): an extra's relation group depends on which end
// of the edge it sits on. Mirrors FindRecapIds/FindSideContentIds's own
// directional reading of the same relation pairs.
public class SeriesRelationsResolveDirectionalTests
{
    [Fact]
    public void MainLineOwningSummaryMakesTheExtraASummary()
    {
        // M --summary--> X: X recaps M, so X is a Summary of the main line.
        Assert.Equal(RelationGroup.Summary, SeriesRelations.ResolveDirectional("summary", extraIsOwner: false));
    }

    [Fact]
    public void ExtraOwningSummaryMakesItTheFullStory()
    {
        // X --summary--> M: M recaps X, so X is the fuller, original story.
        Assert.Equal(RelationGroup.FullStory, SeriesRelations.ResolveDirectional("summary", extraIsOwner: true));
    }

    [Fact]
    public void ExtraOwningFullStoryMakesItASummary()
    {
        // X --full_story--> M: X recaps M.
        Assert.Equal(RelationGroup.Summary, SeriesRelations.ResolveDirectional("full_story", extraIsOwner: true));
    }

    [Fact]
    public void MainLineOwningFullStoryMakesTheExtraTheFullStory()
    {
        // M --full_story--> X: M recaps X, so X is the fuller story.
        Assert.Equal(RelationGroup.FullStory, SeriesRelations.ResolveDirectional("full_story", extraIsOwner: false));
    }

    [Fact]
    public void MainLineOwningSideStoryMakesTheExtraASideStory()
    {
        // M --side_story--> X: X is a side story of M.
        Assert.Equal(RelationGroup.SideStory, SeriesRelations.ResolveDirectional("side_story", extraIsOwner: false));
    }

    [Fact]
    public void ExtraOwningSideStoryMakesItTheParentStory()
    {
        // X --side_story--> M: M is a side story of X, so X is the parent.
        Assert.Equal(RelationGroup.ParentStory, SeriesRelations.ResolveDirectional("side_story", extraIsOwner: true));
    }

    [Fact]
    public void ExtraOwningParentStoryMakesItASideStory()
    {
        // X --parent_story--> M: X is side content of M.
        Assert.Equal(RelationGroup.SideStory, SeriesRelations.ResolveDirectional("parent_story", extraIsOwner: true));
    }

    [Fact]
    public void MainLineOwningParentStoryMakesTheExtraTheParentStory()
    {
        // M --parent_story--> X: M is side content of X, so X is the parent.
        Assert.Equal(RelationGroup.ParentStory, SeriesRelations.ResolveDirectional("parent_story", extraIsOwner: false));
    }

    [Fact]
    public void MainLineOwningSequelMakesTheExtraASequel()
    {
        // M --sequel--> X: X follows M.
        Assert.Equal(RelationGroup.Sequel, SeriesRelations.ResolveDirectional("sequel", extraIsOwner: false));
    }

    [Fact]
    public void ExtraOwningSequelMakesItThePrequel()
    {
        // X --sequel--> M: M follows X, so X precedes M.
        Assert.Equal(RelationGroup.Prequel, SeriesRelations.ResolveDirectional("sequel", extraIsOwner: true));
    }

    [Fact]
    public void MainLineOwningPrequelMakesTheExtraThePrequel()
    {
        // M --prequel--> X: X precedes M.
        Assert.Equal(RelationGroup.Prequel, SeriesRelations.ResolveDirectional("prequel", extraIsOwner: false));
    }

    [Fact]
    public void ExtraOwningPrequelMakesItASequel()
    {
        // X --prequel--> M: M precedes X, so X follows M.
        Assert.Equal(RelationGroup.Sequel, SeriesRelations.ResolveDirectional("prequel", extraIsOwner: true));
    }

    [Theory]
    [InlineData("alternative_version", true)]
    [InlineData("alternative_version", false)]
    public void AlternativeVersionIsDirectionIndependent(string relationType, bool extraIsOwner) =>
        Assert.Equal(RelationGroup.AlternativeVersion, SeriesRelations.ResolveDirectional(relationType, extraIsOwner));

    [Theory]
    [InlineData("alternative_setting", true)]
    [InlineData("alternative_setting", false)]
    public void AlternativeSettingIsDirectionIndependent(string relationType, bool extraIsOwner) =>
        Assert.Equal(RelationGroup.AlternativeSetting, SeriesRelations.ResolveDirectional(relationType, extraIsOwner));

    [Fact]
    public void UnrecognisedRelationResolvesToOther() =>
        Assert.Equal(RelationGroup.Other, SeriesRelations.ResolveDirectional("cm", extraIsOwner: false));
}
