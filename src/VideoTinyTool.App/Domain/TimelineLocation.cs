namespace VideoTinyTool.Domain;

public readonly record struct TimelineLocation(Clip Clip, int Index, TimeSpan SourceOffset);
