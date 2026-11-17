# NxFileViewer feedback and polls

The Info page opens the feedback form and the repository's GitHub Discussions.
Enable Discussions in the fork's settings and create polls in its Polls category.
Voting takes place on GitHub.

## Configure direct issue reporting on ALL-INKL

1. Register a GitHub App. Give it repository permission **Issues: Read and write**
   and install it on this fork only. Enable user authorization via the web flow
   and leave webhooks disabled. Set the callback URL to the exact public HTTPS URL
   of `report.php`. Generate a client secret and an App private key.
2. Use a PHP 8.1+ HTTPS site with cURL, OpenSSL and PDO SQLite, and allow outbound
   HTTPS to GitHub. Upload `report.php` into the site's document root.
3. Create `nxfileviewer-feedback` alongside that document root in a directory
   outside every public document root. Copy `config.example.php` there as
   `config.php`. Populate the settings and upload the private key into that
   private directory. The PHP user needs write access there for the SQLite database.
   Keep the config, database and key out of Git.
4. NxFileViewer defaults to `https://feedback.sandmaennchen5.de/report.php`.
   To use another URL, edit the top-level `reportEndpoint` property in
   `NxFileViewerFork.settings.json` beside the executable while the app is
   closed. Use the same HTTPS URL as the GitHub App callback.

5. Submit one test report per mode and check the GitHub authors. With GitHub
   login, the author is the authorized user. Without login, the author is the
   GitHub App bot. Check that both submissions return the issue number.

Set `reportEndpoint` to an empty string to use the prefilled GitHub form in the browser.
GitHub authorization is performed for each signed submission. The server uses
the user token for that submission and does not retain it.

The service allows three attempts per source IP and thirty total per clock hour.
The limiter uses a keyed hash of the source address. Hosting access logs may
contain visitor IP addresses and authorization callback URLs. Review the hosting
log settings and disclose the public nature of reports before deployment.
Expired authorization drafts are deleted on the next service request.
