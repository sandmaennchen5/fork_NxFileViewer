# NxFileViewer

## Description

View and browse content of Nintendo Switch files.

Download latest version [here](https://github.com/Myster-Tee/NxFileViewer/releases).

## Features

- Based on [LibHac](https://github.com/Thealexbarney/LibHac)
- Supported files: NSP, NSZ, XCI, XCZ
- Supports Super NSP/XCI
- Browse files content structure
- Export files
- Save or copy title images
- Specify your own keys location
- Searches keys in commonly used locations
- Automatically download keys from an URL defined in the settings
- Supports drag and drop
- Checks real files type (XCI or NSP)
- Detailed log
- User-friendly and responsive interface
- Single executable file
- Do not write anything outside of the program directory
- Verify integrity (hash and signature)
- Batch integrity check for complete folders containing NSP, NSZ, XCI, and XCZ files
- Live batch results with file type, package type, compression, integrity status, and error details
- Preview the overview of the currently checked file directly in the batch window
- Filter the batch list to show faulty files only and export the results as CSV
- Move successfully verified files to a selected destination while preserving subdirectories
- Existing destination files are never overwritten and faulty files are never deleted automatically
- Displays missing keys
- Opens title URL
- Configurable Tinfoil title page and API URLs
- Multiple interface languages (English, French, German, and Spanish)
- Supports compressed NACP title blocks and up to 32 NACP title languages
- Advanced files renaming
- Improved online title normalization when renaming files
- Dark, light, and system themes, including themed window title bars
- Remembers the main window size, position, and the last batch directory
- Full support of NSZ and XCZ files (compressed with [NSZ](https://github.com/nicoboss/nsz/) tool from **nicoboss**).

### Batch integrity check

Open **Tools → Check folder integrity** to verify all supported Switch files in a directory. Subdirectories can be included optionally. Results are added to the table as soon as each file has been checked, while the overview panel displays information about the file currently being processed.

The batch window supports cancellation, live progress reporting, faulty-file filtering, and CSV export. After the check, files reported as original and valid can be moved to a selected destination. The original folder structure is retained, existing destination files are skipped, and invalid files remain untouched.

## Screenshots

![Overview](./screenshots/Overview.png)

![Content](./screenshots/Content.png)

![Content](./screenshots/Rename.png)

![Settings](./screenshots/Settings.png)

## Requirements

If application doesn't start, please install the *.NET Desktop Runtime 8* which can be downloaded from the official Microsoft website [here](https://dotnet.microsoft.com/download/dotnet/8.0).

## Contribute

Feel free to contribute to this project to make this program better.

I designed the application so that it can be easily localized in several languages.  
If you want this app in your language, send me your translations ;).

## Development

### Requirements

 - Microsoft Visual Studio 2022+

### Publishing

Run the PowerShell script below.

```PowerShell
.\Publish.ps1
```

## Credits

- Special thanks to [Thealexbarney](https://github.com/Thealexbarney) for his powerful and easy to use [LibHac](https://github.com/Thealexbarney/LibHac) library.
- Special thanks to [nicoboss](https://github.com/nicoboss/) who took a lot of time to explain me the [NSZ](https://github.com/nicoboss/nsz) format and many other things.
- Thanks to all the Switch scene :)
