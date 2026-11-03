using System.Globalization;
using TV_Player;
using TV_Player.Presentation;
using Xunit;

namespace TV_Player.Tests;

public class ProgramGuideProjectionTests
{
    private static readonly DateTime Noon = new(2026, 11, 3, 12, 0, 0);
    private static ProgramInfo Program(string title, double startMinutes, double endMinutes) => new()
        { Title = title, StartTime = Noon.AddMinutes(startMinutes), EndTime = Noon.AddMinutes(endMinutes) };
    private static ProgramGuide Guide(params ProgramInfo[] programs) => new() { Programs = programs.ToList() };

    [Fact]
    public void EmptyAndMissingGuidesHaveNoPanelOrCards()
    {
        foreach (var guide in new ProgramGuide?[] { null, Guide(), new() { Programs = null! } })
        {
            var state = ProgramGuideProjection.Project(guide, Noon);
            Assert.Null(state.Current);
            Assert.Null(state.Next);
            Assert.Empty(state.Window);
        }
    }

    [Fact]
    public void StartIsInclusiveAndEndIsExclusiveAtExactProgramBoundary()
    {
        var state = ProgramGuideProjection.Project(Guide(Program("A", -60, 0), Program("B", 0, 60)), Noon);
        Assert.Equal("B", state.Current!.Title);
        Assert.Equal(0, state.Current.Progress);
        Assert.Equal("B", state.Window[0].Title);
        Assert.Null(state.Next);
    }

    [Fact]
    public void CurrentAndNextAreIndependentAndGuideIsChronologicallyOrdered()
    {
        var state = ProgramGuideProjection.Project(Guide(Program("Next", 30, 90), Program("Past", -90, -30), Program("Now", -30, 30)), Noon);
        Assert.Equal("Now", state.Current!.Title);
        Assert.Equal(0.5, state.Current.Progress);
        Assert.Equal("Next", state.Next!.Title);
        Assert.Equal(new[] { "Now", "Next" }, state.Window.Select(item => item.Title));
    }

    [Fact]
    public void BeforeFirstProgramProjectsUpcomingWithZeroProgress()
    {
        var state = ProgramGuideProjection.Project(Guide(Program("Future", 60, 120)), Noon);
        Assert.Null(state.Current);
        Assert.Equal("Future", state.Next!.Title);
        Assert.False(state.Next.IsCurrent);
        Assert.Equal(0, state.Next.Progress);
        Assert.Same(state.Next, state.Window[0]);
    }

    [Theory]
    [InlineData(GuideWindowOrigin.CurrentOrNextOrFirst, "Future")]
    [InlineData(GuideWindowOrigin.CurrentOrFirst, "Past")]
    public void GapRetainsEachClientsExplicitWindowPolicy(GuideWindowOrigin origin, string first)
    {
        var state = ProgramGuideProjection.Project(Guide(Program("Past", -90, -30), Program("Future", 30, 90)), Noon, origin: origin);
        Assert.Null(state.Current);
        Assert.Equal("Future", state.Next!.Title);
        Assert.Equal(first, state.Window[0].Title);
    }

    [Fact]
    public void OverlapSelectsFirstChronologicalCurrentAndMarksEachActiveCard()
    {
        var state = ProgramGuideProjection.Project(Guide(Program("Later start", -30, 30), Program("Earlier start", -60, 60)), Noon);
        Assert.Equal("Earlier start", state.Current!.Title);
        Assert.All(state.Window, item => Assert.True(item.IsCurrent));
        Assert.Null(state.Next);
    }

    [Fact]
    public void RepeatedTitlesDoNotJumpBackToAnExpiredProgram()
    {
        var state = ProgramGuideProjection.Project(Guide(Program("News", -120, -60), Program("News", -30, 30), Program("News", 30, 90)), Noon);
        Assert.Equal(Noon.AddMinutes(-30), state.Current!.StartTime);
        Assert.Equal(Noon.AddMinutes(-30), state.Window[0].StartTime);
        Assert.Equal(2, state.Window.Count);
    }

    [Fact]
    public void ExpiredGuideRetainsFirstCardsWithoutCurrentOrNext()
    {
        var state = ProgramGuideProjection.Project(Guide(Program("A", -120, -60), Program("B", -60, 0)), Noon);
        Assert.Null(state.Current);
        Assert.Null(state.Next);
        Assert.Equal(new[] { "A", "B" }, state.Window.Select(item => item.Title));
        Assert.All(state.Window, item => { Assert.False(item.IsCurrent); Assert.Equal(1, item.Progress); });
    }

    [Fact]
    public void WindowIsLimitedAndProjectionDoesNotMutateInputOrder()
    {
        var programs = Enumerable.Range(0, 12).Select(i => Program(i.ToString(), i * 60, (i + 1) * 60)).Reverse().ToArray();
        var guide = Guide(programs);
        var originalOrder = guide.Programs.ToArray();
        var state = ProgramGuideProjection.Project(guide, Noon.AddMinutes(90));
        Assert.Equal(7, state.Window.Count);
        Assert.Equal(Enumerable.Range(1, 7).Select(i => i.ToString()), state.Window.Select(item => item.Title));
        Assert.Equal(originalOrder, guide.Programs);
    }

    [Fact]
    public void ProjectionIsASnapshotOfMutableGuideRecords()
    {
        var program = Program("Original", -30, 30);
        var guide = Guide(program);
        var state = ProgramGuideProjection.Project(guide, Noon);
        program.Title = "Changed";
        program.EndTime = Noon.AddDays(1);
        guide.Programs.Clear();
        Assert.Equal("Original", state.Current!.Title);
        Assert.Equal(0.5, state.Current.Progress);
        Assert.Single(state.Window);
    }

    [Fact]
    public void InvalidIntervalsAreExcludedAndCannotProduceNaNProgress()
    {
        var state = ProgramGuideProjection.Project(Guide(null!, Program("Zero", 0, 0), Program("Negative", 30, 0)), Noon);
        Assert.Null(state.Current);
        Assert.Null(state.Next);
        Assert.Empty(state.Window);
        Assert.Equal(0, ProgramGuideProjection.Progress(Noon, Noon, Noon));
        Assert.Equal(0, ProgramGuideProjection.Progress(Noon.AddHours(1), Noon, Noon));
    }

    [Theory]
    [InlineData(-30, 0)]
    [InlineData(0, 0)]
    [InlineData(15, 0.25)]
    [InlineData(30, 0.5)]
    [InlineData(60, 1)]
    [InlineData(90, 1)]
    public void ProgressIsNormalizedAndClamped(double elapsedMinutes, double progress)
        => Assert.Equal(progress, ProgramGuideProjection.Progress(Noon, Noon.AddHours(1), Noon.AddMinutes(elapsedMinutes)));

    [Fact]
    public void MidnightAndSubsecondIntervalsUseExactProvidedTime()
    {
        var start = new DateTime(2026, 11, 3, 23, 30, 0);
        var end = start.AddHours(1);
        Assert.Equal(0.5, ProgramGuideProjection.Progress(start, end, start.AddMinutes(30)));
        Assert.Equal(0.5, ProgramGuideProjection.Progress(Noon, Noon.AddTicks(10), Noon.AddTicks(5)));
        Assert.Equal(0.9, ProgramGuideProjection.Progress(Noon, Noon.AddTicks(10), Noon.AddTicks(9)));
    }

    [Fact]
    public void SharedLabelsPreserveFixedCarouselAndLocalizedDesktopFormats()
    {
        var state = ProgramGuideProjection.Project(Guide(Program("A", 60, 120)), Noon.AddMinutes(90), formatProvider: CultureInfo.GetCultureInfo("en-US"));
        Assert.Equal("13:00", state.Current!.TimeLabels.Start);
        Assert.Equal(Noon.AddHours(1).ToString("t", CultureInfo.GetCultureInfo("en-US")), state.Current.ShortTimeLabels.Start);
        Assert.Equal("14:00", state.Current.TimeLabels.End);
    }

    [Fact]
    public void ZeroWindowStillProvidesCurrentAndInvalidWindowIsRejected()
    {
        var guide = Guide(Program("A", -30, 30));
        var state = ProgramGuideProjection.Project(guide, Noon, windowSize: 0);
        Assert.NotNull(state.Current);
        Assert.Empty(state.Window);
        Assert.Throws<ArgumentOutOfRangeException>(() => ProgramGuideProjection.Project(guide, Noon, windowSize: -1));
    }

    [Fact]
    public void AdvancingExplicitClockMovesWindowAndNeverKeepsExpiredCurrent()
    {
        var guide = Guide(Program("A", -30, 30), Program("B", 30, 90));
        var before = ProgramGuideProjection.Project(guide, Noon);
        var after = ProgramGuideProjection.Project(guide, Noon.AddMinutes(30));
        Assert.Equal("A", before.Current!.Title);
        Assert.Equal("B", after.Current!.Title);
        Assert.Equal("B", after.Window[0].Title);
    }
}
