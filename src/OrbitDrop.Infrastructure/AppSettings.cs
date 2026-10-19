using OrbitDrop.Core;

namespace OrbitDrop.Infrastructure;

public enum AppTheme { System, Light, Dark }
public enum WheelSize { Compact, Standard, Large }
public enum TriggerKey { Shift, Ctrl }

/// <summary>
/// User preferences. Every property here is wired to real behaviour; nothing is decorative.
/// Bump <see cref="CurrentSchemaVersion"/> and extend <see cref="SettingsStore"/> migration when the shape changes.
/// </summary>
public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;
    public const int MaxRecentFolders = 4;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    // General
    public bool ShowCompletionNotifications { get; set; } = true;
    public bool RevealWhenDone { get; set; }
    public bool OnboardingCompleted { get; set; }

    // Appearance
    public AppTheme Theme { get; set; } = AppTheme.System;
    public bool UseWindowsAccent { get; set; }
    public WheelSize WheelSize { get; set; } = WheelSize.Standard;
    public bool ReduceMotion { get; set; }

    // Shortcuts
    public TriggerKey TriggerKey { get; set; } = TriggerKey.Shift;

    // Conversions
    public OutputLocation OutputLocation { get; set; } = OutputLocation.BesideOriginal;
    public string? OutputFolder { get; set; }
    public int JpegQuality { get; set; } = 92;

    // Advanced
    public bool ExtendedDragDetection { get; set; }
    public bool DetailedLogging { get; set; }

    // Updates: one anonymous request to GitHub Releases at most once a day, only while this is on.
    public bool CheckForUpdates { get; set; } = true;
    public DateTimeOffset? LastUpdateCheck { get; set; }

    // Actions: the last few folders used with Copy to / Move to (most recent first).
    public string[] RecentFolders { get; set; } = [];

    public AppSettings Clone() => (AppSettings)MemberwiseClone();

    /// <summary>Clamps values that may have been hand-edited into an invalid range.</summary>
    public void Normalize()
    {
        JpegQuality = Math.Clamp(JpegQuality, 50, 100);
        if (!Enum.IsDefined(Theme)) Theme = AppTheme.System;
        if (!Enum.IsDefined(WheelSize)) WheelSize = WheelSize.Standard;
        if (!Enum.IsDefined(TriggerKey)) TriggerKey = TriggerKey.Shift;
        if (OutputLocation == OutputLocation.Folder && string.IsNullOrWhiteSpace(OutputFolder))
            OutputLocation = OutputLocation.BesideOriginal;
        RecentFolders = (RecentFolders ?? []).Where(f => !string.IsNullOrWhiteSpace(f)).Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxRecentFolders).ToArray();
    }
}
