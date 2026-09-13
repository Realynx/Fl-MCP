# Managed FL dialogs and invalid-note recovery

Managed launch and render inspect enabled, process-owned dialog windows while waiting. An unrecognized actionable dialog fails with its available text and a diagnostic path instead of consuming the entire render deadline. A custom FL message form gets one second to finish initializing before it is classified. No dialog inspection or automated input runs against an attached user session.

Before either managed process starts, the server copies its input FLP to a fresh `recovery/<id>/original.flp` in the workspace. That copy is separate from the template, supplied source project, working project and render snapshot. The server records SHA-256 and requires the working input to remain unchanged before any automatic response. This preserves the original bytes; it does not promise that FL can load a corrupt project without repairs.

Only these complete English messages, under the exact title **Invalid data**, have an automatic response:

| Message | During launch | During render |
| --- | --- | --- |
| “There are invalid notes in this project. Leaving them in the project may lead to unexpected behavior and crashes. Do you want to delete these notes?” | Yes: allow FL to remove invalid notes while loading the disposable copy | Same bounded recovery |
| “There are invalid notes in this project. These notes will be deleted when the project is saved. Are you sure you want to continue?” | **No**: abort the pending startup save, whose destination has not been established | Refuse and report the dialog |

Whitespace is normalized; additional or different text is refused. Each recognized message may receive one response per process. A posted response must make the dialog disappear within five seconds; it is never repeatedly clicked. Unknown, localized, changed, repeated or unsupported prompts remain unanswered.

FL uses custom `TMsgForm`, `TQuickMemo` and `TQuickFocusBtn` controls. The framework's read-only `FlDialogInspector` supports exact builds 25.2.5.5319 and 26.1.3.5570. It verifies the process, window ancestry, Delphi class identity, cached HWNDs, text bounds, and button callback/owner/modal-result fields before returning semantic Yes/No choices. No remote memory is written and no FL function pointer is called. MCP re-inspects the same dialog immediately before sending mouse messages to the identified button's own HWND; it does not send global keys or click screen coordinates. Standard Win32 message boxes require the matching text and Yes/No IDs and labels.

Successful recovery adds a `warnings` array to launch, status, save, close and render results. Each entry identifies the action, preserved original and JSON diagnostic. Invalid-note removal is a content change: inspect the remaining notes before continuing. A startup save declined with No is reported separately. Diagnostic files contain bounded dialog metadata and hashes, and redact token-shaped strings. They never contain the private bridge session token.

The shared SDK separately validates note channel references before saving, so new saves can fail with an actionable note/pattern error while leaving the editor open. This adapter does not parse undocumented FLP events or automatically repair notes in attached projects. Back up and inspect a separate copy when manual recovery is required.

## Verification

On FL Studio 26.1.3.5570, a disposable fixture with 24 notes and one deliberately invalid channel reference successfully passed the exact load prompt, retained 23 notes across two channels, saved a recovery snapshot and rendered a 1,536,184-byte WAV. The corrupted source hash was unchanged. This used the policy above; the startup save prompt did not occur in that final run. Startup No semantics are verified from the native save branch and covered by tests. FL 2025 has exact-build binary verification and decoder fixtures, but this recovery workflow has not been live-tested on FL 2025.
