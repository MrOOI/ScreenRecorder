namespace ScreenRecorder.Infrastructure.Recording;

/// <summary>
/// A failure reported by the native capture library through its error callback.
/// </summary>
public sealed class RecordingEngineException : Exception
{
    public RecordingEngineException(string nativeError, string? filePath)
        : base($"The capture library reported an error: {nativeError}")
    {
        NativeError = nativeError;
        FilePath = filePath;
        Data["NativeError"] = nativeError;
        Data["OutputFile"] = filePath;
    }

    /// <summary>The error text as reported by the library.</summary>
    public string NativeError { get; }

    /// <summary>The file that was being written, if known.</summary>
    public string? FilePath { get; }
}
