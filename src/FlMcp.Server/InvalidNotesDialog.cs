namespace FlMcp.Server;

/// <summary>Exact prompt/button policy, deliberately separate from process/window mechanics.</summary>
public static class InvalidNotesDialog
{
    public const string LoadPrompt = "There are invalid notes in this project. Leaving them in the project may lead to unexpected behavior and crashes. Do you want to delete these notes?";
    public const string SavePrompt = "There are invalid notes in this project. These notes will be deleted when the project is saved. Are you sure you want to continue?";

    public static StudioDialogButton? ResponseButton(StudioDialog dialog, string phase = "Launch")
    {
        // Exact loader branch: Yes (6) deletes invalid notes. Startup save confirmation:
        // No (7) aborts that pending save, whose destination has not been established here.
        var body = Prompt(dialog);
        if (!SupportedDialog(dialog) || Normalize(dialog.Title) != "Invalid data" ||
            (body != LoadPrompt && body != SavePrompt) || dialog.Buttons.Count != 2)
            return null;
        var yes = dialog.Buttons.FirstOrDefault(button => button.Id == 6 && Label(button.Caption) == "Yes");
        var no = dialog.Buttons.FirstOrDefault(button => button.Id == 7 && Label(button.Caption) == "No");
        if (yes is null || no is null) return null;
        if (body == SavePrompt) return phase == "Launch" ? no : null;
        return yes;
    }

    public static string Prompt(StudioDialog dialog) => Normalize(string.Join(" ", dialog.Text));

    private static bool SupportedDialog(StudioDialog dialog) => dialog.WindowClass == "#32770" ||
        (dialog.WindowClass == "TMsgForm" && dialog.VerifiedNativeChoices);

    private static string Label(string value) => Normalize(value.Replace("&", "", StringComparison.Ordinal));
    private static string Normalize(string value) => string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
