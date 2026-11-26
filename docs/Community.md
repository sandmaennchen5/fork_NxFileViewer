# Feedback, polls and system reports

[Documentation index](README.md) · [Logging](Logging.md) · [Application updates](Application-updates.md)

The Viewer provides these features through a compatible HTTPS companion service configured in the application settings.

## Bugs and feature requests

Open **Info → Report a bug / request a feature**. Describe the problem and steps to
reproduce it, or explain the desired feature. The published issue is public: remove
personal information from its title and description.

Choose GitHub authorization in the browser or report without a GitHub account.
Submitting a report from the app requires a reachable reporting service. Reports with or without GitHub sign-in include system details in the protected admin archive, independently of the system reports setting. You can also create an issue manually on GitHub outside the app.

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

## System reports

The first-start explanation appears when no valid choice is saved. It describes
which system details are collected for compatibility testing and update planning.
The statistics show which operating systems, architectures, .NET runtimes and
app versions are in use. The app does not track time spent using it. The initial
dialog waits for an explicit **Enable** or **Disable** response, without a timer.
Closing or pressing Esc does not select an answer. Once a choice is saved, the
dialog does not appear again, including after updates. The optional report setting
remains editable under Settings → Program through **Disable system report**.
When system reports are enabled, an additional report is sent at the first startup
after seven days. Changing the option is reflected in the next report. Failed
reports are retried on a subsequent startup; reporting does not prevent startup.
Windows uses its existing installation identity, Linux uses its machine ID and a
persistent user identity is available as a fallback (also for macOS). Only a hash
is transmitted. The stable identity links reports to the same Support ID.
Names, file paths/content and session logs are not part of statistics. New issue
submissions contain the participant identity instead of repeating system details;
the admin archive shows the latest system record linked through that Support ID.

The service aggregates active identities over a configurable period (for example
30, 60, 90 or 120 days). Public badges and charts show totals, distributions and
history. Individual Support IDs, names, reports, private logs and usage mappings
remain behind admin authentication. The admin UI distinguishes standard reports, additional system reports and the
reported preference. Missing reports may mean the app was offline or unused;
preferences of older clients are shown as unknown.

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
