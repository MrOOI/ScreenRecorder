namespace ScreenRecorder.Core.Abstractions;

/// <summary>
/// Chooses where the next recording is written.
/// </summary>
public interface IOutputPathResolver
{
    /// <summary>
    /// Ensures <paramref name="folder"/> exists and returns a new, unused file path inside it.
    /// </summary>
    /// <exception cref="OutputFolderUnavailableException">The folder cannot be created or written to.</exception>
    string ResolveNewFilePath(string folder);
}

/// <summary>
/// The output folder cannot be used. <see cref="Exception.Message"/> is suitable for the user.
/// </summary>
public sealed class OutputFolderUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
