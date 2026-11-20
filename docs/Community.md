# Feedback, polls and usage statistics

[Documentation index](README.md) · [Logging](Logging.md) · [Application updates](Application-updates.md)

These features belong to the WPF viewer. They require a compatible, separately
deployed HTTPS companion service configured in the application settings. The
experimental Avalonia viewer does not yet provide these community features.

## Bugs and feature requests

Open **Info → Report a bug / request a feature**. Describe the problem and steps to
reproduce it, or explain the desired feature. The published issue is public: remove
personal information from its title and description.

Choose GitHub authorization in the browser or report without a GitHub account.
An optional name for unsigned reports is public. The app shows its stable public
**Support ID**; you can copy it or search your GitHub issues by that ID. Both report
methods include it. Browser authorization confirms each report; the app does not
keep a GitHub login token locally.

Two independent log options are available:

- **Public excerpt:** locally anonymizes paths/filenames and provides an editable
  preview for the GitHub issue. Review the preview before sending.
- **Private original:** uploads the unchanged current session log, at most 5 MiB,
  for protected admin download. It may contain personal paths/names. It is not
  attached publicly to GitHub and can be deleted in the admin interface.

Both options, either one or neither can be selected. Versions use the application's
informational version such as `4.1.1-beta3`, rather than only its numeric assembly
version. The session header also records the OS, architectures, UI and runtime.

The updated companion service stores accepted reports before contacting GitHub.
Queued, successful and review-needed reports remain readable in its private admin
archive, independently of GitHub. A receipt confirms acceptance by the service,
not necessarily GitHub issue creation. Safe retries use a cron job; an uncertain
POST is not blindly repeated. GitHub-authored issues still require the user's
authorization and are not silently published as the bot. If the companion service
itself cannot be reached, the app cannot confirm server-side storage.

## Polls

Open **Info → Polls**. The selected app language controls the question/answer
language. German and English share the same poll/answer IDs and votes. Missing
English text falls back to German; user-written content is not machine-translated.

Choice polls allow one vote per participant identity. Free-text polls accept one
private response of up to 2000 characters, visible only in the protected admin area;
public results contain counts, not other users' text.

An unanswered open poll may appear on Start. **Not participating** saves a refusal
to the server and removes that poll from unanswered reminders, including after
restarting the app. **Participate after all** reverses the refusal. Refusal is not
a vote and does not erase an existing answer. Optional polls can omit the startup
reminder while remaining available in the poll window. There is no web-overview
button in that window.

Administrators can edit bilingual questions/labels, append choices and retire
existing ones. Retired choices cannot receive new votes; their IDs and previous
votes remain in the results and can be reactivated. Two to twenty active choices
are required. Change labels only when their original meaning remains valid.
Participation in the app, result display and startup reminders have separate flags.
Turning both app-display flags off hides the poll from the app; the public website
is independent. Hidden app results are masked in the API response as well.

The app uses its stable installation-derived identity; browsers use a separate
session identity, so they do not share a vote or refusal. Changing language does
not change either identity.

## Usage statistics

The first-start explanation appears when no valid choice is saved. It describes
what is collected and its purpose. Reporting is enabled by default: **Keep enabled**,
closing the dialog, Esc and expiry of the visible ten-second countdown all save the
enabled choice. **Disable** saves a disabled choice. The setting remains editable
under Settings → Program; a saved choice is not replaced on later starts.

At startup, enabled reporting sends an installation-derived identity, app/build
version, OS/platform, process and OS architecture, WPF/Avalonia identifier, runtime
distribution/version and runtime ID. It sends at most once per UTC day and immediately
after a build-version change. An offline service does not prevent normal startup.
The WPF client currently uses a locally derived Windows-installation identity;
no raw Windows ID is uploaded. The stable identity is technically pseudonymous and
can be linked internally to support reports. Names, file paths/content and session
logs are not part of usage reporting.

The service aggregates active identities over a configurable period (for example
30, 60, 90 or 120 days). Public badges and charts show totals, distributions and
history. Individual Support IDs, names, reports, private logs and usage mappings
remain behind admin authentication. No report is not proof that a user disabled
statistics: the app may simply have been offline or unused.

## Companion administration and deployment

The companion service has German/English public/admin interfaces, per-user
permissions, optional/required authenticator 2FA, recovery codes, login throttling
and idle/absolute session limits. SHOW views data; poll management, deletion,
private logs, report delivery and user management are individually selectable.

The service starts with a new empty SQLite or MariaDB database. Old test databases
are not upgraded automatically. Keep the private store outside the public webroot;
never publish its configuration, keys, database, logs or backups. The full single-file
service guide is maintained in [the companion-service README](../tools/feedback/README.md).
App version updates do not upload or deploy that website automatically.
