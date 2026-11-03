using System.Globalization;

namespace TV_Player.Presentation;

public enum GuideWindowOrigin { CurrentOrNextOrFirst, CurrentOrFirst }
public sealed record ProgramTimeLabels(string Start, string End);
public sealed record EpgProgramItem(string Title, DateTime StartTime, DateTime EndTime, bool IsCurrent,
    double Progress, ProgramTimeLabels TimeLabels, ProgramTimeLabels ShortTimeLabels);
public sealed record ProgramGuideState(EpgProgramItem? Current, EpgProgramItem? Next, IReadOnlyList<EpgProgramItem> Window);

/// <summary>Pure guide projection. Times must share the clock's time domain; no clock or UI is owned here.</summary>
public static class ProgramGuideProjection
{
    public static ProgramGuideState Project(ProgramGuide? guide, DateTime now, int windowSize = 7,
        GuideWindowOrigin origin = GuideWindowOrigin.CurrentOrNextOrFirst, IFormatProvider? formatProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(windowSize);
        if (!Enum.IsDefined(origin)) throw new ArgumentOutOfRangeException(nameof(origin));
        var format = formatProvider ?? CultureInfo.InvariantCulture;
        var items = (guide?.Programs ?? Enumerable.Empty<ProgramInfo>())
            .Where(program => program != null && program.EndTime > program.StartTime)
            .OrderBy(program => program.StartTime)
            .Select(program => new EpgProgramItem(program.Title ?? string.Empty, program.StartTime, program.EndTime,
                program.StartTime <= now && now < program.EndTime, Progress(program.StartTime, program.EndTime, now),
                new(program.StartTime.ToString("HH:mm", format), program.EndTime.ToString("HH:mm", format)),
                new(program.StartTime.ToString("t", format), program.EndTime.ToString("t", format))))
            .ToArray();
        var current = Array.FindIndex(items, item => item.IsCurrent);
        var next = Array.FindIndex(items, item => item.StartTime > now);
        var first = current >= 0 ? current : origin == GuideWindowOrigin.CurrentOrNextOrFirst && next >= 0 ? next : 0;
        return new(current >= 0 ? items[current] : null, next >= 0 ? items[next] : null,
            Array.AsReadOnly(items.Skip(first).Take(windowSize).ToArray()));
    }

    public static double Progress(DateTime start, DateTime end, DateTime now)
    {
        if (end <= start || now <= start) return 0;
        if (now >= end) return 1;
        return (double)(now.Ticks - start.Ticks) / (end.Ticks - start.Ticks);
    }
}
