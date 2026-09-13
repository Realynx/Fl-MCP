using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FruityLink.Core.Diagnostics;
using FruityLink.Core.Hosting;

namespace FlMcp.Server;

/// <summary>Reads only visible modals belonging to one owned process; never uses foreground input.</summary>
internal static class WindowsStudioDialogs
{
    private const uint GetText = 0x000D, ButtonClick = 0x00F5;
    private const uint AbortIfHung = 0x0002, OwnerWindow = 4;
    private static readonly HashSet<string> ButtonClasses = new(StringComparer.OrdinalIgnoreCase)
        { "Button", "TButton", "TNewButton", "TBitBtn" };
    private static readonly HashSet<string> TextClasses = new(StringComparer.OrdinalIgnoreCase)
        { "Static", "TLabel", "TStaticText", "TNewLabel", "TPanel" };

    internal static IReadOnlyList<StudioDialog> Read(int processId, CancellationToken ct) =>
        Read(processId, null, ct);

    internal static IReadOnlyList<StudioDialog> Read(int processId,
        IReadOnlyList<FlStudioWindowSnapshot>? snapshots, CancellationToken ct)
    {
        var windows = new List<nint>();
        if (snapshots is not null)
        {
            windows.AddRange(snapshots.Where(window => IsModal(window, processId)).Take(16).Select(window => window.Window));
        }
        else
        {
            EnumWindows((window, _) =>
            {
                if (windows.Count >= 16) return false;
                if (IsModal(window, processId)) windows.Add(window);
                return true;
            }, 0);
        }
        var result = new List<StudioDialog>();
        var elapsed = Stopwatch.StartNew();
        foreach (var window in windows)
        {
            ct.ThrowIfCancellationRequested();
            if (elapsed.ElapsedMilliseconds >= 1000) break;
            var dialog = ReadOne(window, processId, elapsed, ct);
            // FL can keep plugin frames enabled while their owner is disabled by a real
            // message form. A frame without dialog actions is not itself an actionable modal.
            if (dialog.Buttons.Count > 0 || dialog.WindowClass is "#32770" or "TMsgForm") result.Add(dialog);
        }
        return result;
    }

    private static StudioDialog ReadOne(nint window, int processId, Stopwatch elapsed, CancellationToken ct)
    {
        var text = new List<string>();
        var buttons = new List<StudioDialogButton>();
        var classes = new List<string>();
        var isFlMessage = ClassName(window) == "TMsgForm";
        var children = new List<nint>();
        EnumChildWindows(window, (child, _) =>
        {
            if (children.Count >= 64) return false;
            children.Add(child);
            return true;
        }, 0);
        foreach (var child in children)
        {
            ct.ThrowIfCancellationRequested();
            if (elapsed.ElapsedMilliseconds >= 1000) break;
            if (!BelongsTo(child, processId) || !IsWindowVisible(child)) continue;
            var kind = ClassName(child);
            classes.Add(kind);
            if ((ButtonClasses.Contains(kind) || (isFlMessage && kind == "TQuickFocusBtn")) && IsWindowEnabled(child))
                buttons.Add(new(child, GetDlgCtrlID(child), ReadText(child)));
            else if (TextClasses.Contains(kind) || (isFlMessage && kind == "TQuickMemo"))
                text.Add(ReadText(child));
        }
        return WithNativeInspection(new(window, processId, ClassName(window), ReadText(window), text, buttons) { ChildClasses = classes });
    }

    private static StudioDialog WithNativeInspection(StudioDialog dialog)
    {
        var native = dialog.WindowClass == "TMsgForm" ? FlDialogInspector.TryInspect(dialog.ProcessId, dialog.Window) : null;
        if (native is null) return dialog;
        return dialog with { Text = [native.Body], VerifiedNativeChoices = true,
            Buttons = native.Choices.Select(choice => new StudioDialogButton(choice.Window, choice.Result,
                choice.Result == 6 ? "Yes" : "No") { ClientClick = true }).ToArray() };
    }


    internal static bool TryClick(int processId, StudioDialog expected, StudioDialogButton button,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!IsModal(expected.Window, processId) || !BelongsTo(button.Window, processId) ||
            !IsChild(expected.Window, button.Window) || !IsWindowVisible(button.Window) || !IsWindowEnabled(button.Window))
            return false;
        var current = ReadOne(expected.Window, processId, Stopwatch.StartNew(), ct);
        if (!SameDialog(expected, current) || !current.Buttons.Contains(button)) return false;
        // Posting to this verified button is bounded and does not steal focus or send global keystrokes.
        // A successful post is not confirmation of completion; the monitor waits for the modal to disappear.
        return button.ClientClick ? PostClientClick(button.Window) : PostMessage(button.Window, ButtonClick, 0, 0);
    }

    private static bool PostClientClick(nint button)
    {
        if (!GetClientRect(button, out var rect) || rect.Right is < 1 or > 4096 || rect.Bottom is < 1 or > 4096) return false;
        var point = (nint)((rect.Bottom / 2 << 16) | (rect.Right / 2));
        return PostMessage(button, 0x0201, 1, point) && PostMessage(button, 0x0202, 0, point);
    }

    private static bool SameDialog(StudioDialog left, StudioDialog right) =>
        left.Window == right.Window && left.ProcessId == right.ProcessId &&
        left.WindowClass == right.WindowClass && left.Title == right.Title &&
        left.VerifiedNativeChoices == right.VerifiedNativeChoices && left.Text.SequenceEqual(right.Text) && left.Buttons.SequenceEqual(right.Buttons);

    private static bool IsModal(nint window, int processId)
    {
        if (!BelongsTo(window, processId) || !IsWindowVisible(window) || !IsWindowEnabled(window)) return false;
        var owner = GetWindow(window, OwnerWindow);
        if (owner != 0 && BelongsTo(owner, processId) && !IsWindowEnabled(owner)) return true;
        return ClassName(window) == "#32770";
    }

    private static bool IsModal(FlStudioWindowSnapshot window, int processId) =>
        window.ProcessId == processId && window.Visible && window.Enabled &&
        ((window.Owner != 0 && !window.OwnerEnabled) || window.ClassName == "#32770");

    private static bool BelongsTo(nint window, int processId) =>
        IsWindow(window) && GetWindowThreadProcessId(window, out var pid) != 0 && pid == processId;

    private static string ClassName(nint window)
    {
        var buffer = new StringBuilder(128);
        GetClassName(window, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static string ReadText(nint window)
    {
        var buffer = new StringBuilder(2048);
        return SendMessageTimeout(window, GetText, (nuint)buffer.Capacity, buffer,
            AbortIfHung, 50, out _) == 0 ? "" : buffer.ToString();
    }

    private delegate bool EnumWindow(nint window, nint parameter);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint window, out Rect rect);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindow callback, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(nint parent, EnumWindow callback, nint parameter);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool IsWindowEnabled(nint window);
    [DllImport("user32.dll")] private static extern bool IsChild(nint parent, nint child);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll")] private static extern int GetDlgCtrlID(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder text, int maximum);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint SendMessageTimeout(nint window, uint message,
        nuint wParam, StringBuilder text, uint flags, uint timeout, out nuint result);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool PostMessage(nint window, uint message, nuint wParam, nint lParam);
}
