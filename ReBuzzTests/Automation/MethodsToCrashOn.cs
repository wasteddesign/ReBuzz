namespace ReBuzzTests.Automation;

public class MethodsToCrashOn
{
    public const string Constructor = "constructor";
    public const string GetEnvelopeInfos = nameof(GetEnvelopeInfos);
    public const string Init = nameof(Init);
    public const string AttributesChanged = nameof(AttributesChanged);
    public const string SetNumTracks = nameof(SetNumTracks);
    public const string Tick = nameof(Tick);
    public const string WorkMonoToStereo = nameof(WorkMonoToStereo);
    public const string Work = nameof(Work);
    public const string Stop = nameof(Stop);
}