namespace FlMcp.Server;

/// <summary>Bounded, process-owned modal metadata. Handles are private implementation identifiers.</summary>
public sealed record StudioDialog(nint Window, int ProcessId, string WindowClass, string Title,
    IReadOnlyList<string> Text, IReadOnlyList<StudioDialogButton> Buttons)
{
    public IReadOnlyList<string> ChildClasses { get; init; } = [];
    public bool VerifiedNativeChoices { get; init; }
}

public sealed record StudioDialogButton(nint Window, int Id, string Caption)
{
    public bool ClientClick { get; init; }
}
