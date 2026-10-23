namespace ScreenRecorder.Core.Models;

/// <summary>
/// User preferences persisted between runs.
/// </summary>
public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public static readonly IReadOnlyList<int> SupportedFrameRates = [30, 60];

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public string OutputFolder { get; init; } = DefaultOutputFolder;

    public bool CaptureSystemAudio { get; init; } = true;

    public bool CaptureMicrophone { get; init; }

    public bool ShowCursor { get; init; } = true;

    public int FrameRate { get; init; } = 30;

    public VideoQuality Quality { get; init; } = VideoQuality.Medium;

    public bool CountdownEnabled { get; init; } = true;

    public int CountdownSeconds { get; init; } = 3;

    public bool StartMinimized { get; init; }

    public Hotkey StartStopHotkey { get; init; } =
        new(HotkeyModifiers.Control | HotkeyModifiers.Shift, 'R');

    public Hotkey PauseResumeHotkey { get; init; } =
        new(HotkeyModifiers.Control | HotkeyModifiers.Shift, 'P');

    public static string DefaultOutputFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "ScreenRecorder");

    /// <summary>The countdown to apply before recording, honouring <see cref="CountdownEnabled"/>.</summary>
    public TimeSpan EffectiveCountdown => CountdownEnabled ? TimeSpan.FromSeconds(CountdownSeconds) : TimeSpan.Zero;

    /// <summary>
    /// Repairs values that may come from an older or hand-edited file.
    /// </summary>
    public AppSettings Normalize()
    {
        var defaults = new AppSettings();
        return this with
        {
            SchemaVersion = CurrentSchemaVersion,
            OutputFolder = string.IsNullOrWhiteSpace(OutputFolder) ? defaults.OutputFolder : OutputFolder,
            FrameRate = SupportedFrameRates.Contains(FrameRate) ? FrameRate : defaults.FrameRate,
            Quality = Enum.IsDefined(Quality) ? Quality : defaults.Quality,
            CountdownSeconds = Math.Clamp(CountdownSeconds, 1, 10),
            StartStopHotkey = StartStopHotkey is { IsValid: true } ? StartStopHotkey : defaults.StartStopHotkey,
            PauseResumeHotkey = PauseResumeHotkey is { IsValid: true } ? PauseResumeHotkey : defaults.PauseResumeHotkey,
        };
    }
}
