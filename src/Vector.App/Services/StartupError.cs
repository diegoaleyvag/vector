namespace Vector.App.Services;

/// <summary>
/// Registered instead of the studio services when the decision content JSON could not be fetched or
/// parsed at startup. Its presence tells <see cref="App"/> to render a static error screen instead of
/// the studio, satisfying fail-closed handling of missing/incompatible data files.
/// </summary>
/// <param name="Message">A human-readable description of what went wrong.</param>
public sealed record StartupError(string Message);
