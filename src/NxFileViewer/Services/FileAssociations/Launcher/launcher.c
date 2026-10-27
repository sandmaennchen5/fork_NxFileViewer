// Native x86 Windows launcher; no C runtime, works with x86 and x64 viewers.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shellapi.h>
// Zig/MinGW links the PE TLS index even though this launcher has no TLS data.
DWORD _tls_index;
static WCHAR module[32768], command[32768], reference[32768], target[512];
static char targetBytes[1024];
static unsigned used;
static int put(WCHAR c) { if (used >= 32767) return 0; command[used++] = c; return 1; }
static int quote(const WCHAR *s) {
    if (!put(L'"')) return 0;
    while (*s) {
        unsigned slashes = 0;
        while (*s == L'\\') { ++slashes; ++s; }
        unsigned count = (*s == L'"' || !*s) ? slashes * 2 : slashes;
        while (count--) if (!put(L'\\')) return 0;
        if (!*s) break;
        if (*s == L'"' && !put(L'\\')) return 0;
        if (!put(*s++)) return 0;
    }
    return put(L'"');
}
static void fail(void) {
    MessageBoxW(0, L"Could not start NxFileViewer Fork. Add the file association again in the viewer settings. The command line may also be too long.",
        L"NxFileViewer Fork", MB_OK | MB_ICONERROR);
    ExitProcess(1);
}
void WINAPI LauncherEntry(void) {
    DWORD n = GetModuleFileNameW(0, module, 32768);
    if (!n || n >= 32768) fail();
    unsigned end = n;
    while (end && module[end-1] != L'\\' && module[end-1] != L'/') --end;
    if (!end || end + 512 >= 32768) fail();
    unsigned i;
    for (i=0; i<end; ++i) reference[i]=module[i];
    const WCHAR *referenceName=L"NxFileViewerFork.Open.target";
    while (*referenceName) reference[i++]=*referenceName++;
    reference[i]=0;
    const WCHAR *name=L"NxFileViewer.exe";
    HANDLE file=CreateFileW(reference,GENERIC_READ,FILE_SHARE_READ|FILE_SHARE_WRITE|FILE_SHARE_DELETE,0,OPEN_EXISTING,FILE_ATTRIBUTE_NORMAL,0);
    if (file!=INVALID_HANDLE_VALUE) {
        DWORD read=0;
        if (!ReadFile(file,targetBytes,sizeof(targetBytes),&read,0)) { CloseHandle(file); fail(); }
        CloseHandle(file);
        if (!read || read>=sizeof(targetBytes)) fail();
        int chars=MultiByteToWideChar(CP_UTF8,MB_ERR_INVALID_CHARS,targetBytes,(int)read,target,511);
        if (chars<=4) fail();
        target[chars]=0;
        for (int n=0;n<chars;++n) if (target[n]<32 || target[n]==L'\\' || target[n]==L'/' || target[n]==L':' || target[n]==L'"' || target[n]==L'<' || target[n]==L'>' || target[n]==L'|' || target[n]==L'?' || target[n]==L'*') fail();
        if (lstrcmpiW(target+chars-4,L".exe") || !lstrcmpiW(target,L"NxFileViewerFork.Open.exe")) fail();
        name=target;
    } else if (GetLastError()!=ERROR_FILE_NOT_FOUND) fail();
    i=end;
    while (*name) module[i++]=*name++;
    module[i]=0;
    int argc = 0;
    LPWSTR *argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    if (!argv || !quote(module)) fail();
    for (int arg=1; arg<argc; ++arg) if (!put(L' ') || !quote(argv[arg])) fail();
    command[used] = 0;
    LocalFree(argv);
    STARTUPINFOW startup = {0}; PROCESS_INFORMATION process = {0};
    startup.cb = sizeof(startup);
    // Derive the working directory from our own path, never the document path.
    static WCHAR directory[32768];
    for (i=0; i<end; ++i) directory[i] = module[i];
    directory[end] = 0;
    if (!CreateProcessW(module, command, 0, 0, FALSE, 0, 0, directory, &startup, &process)) fail();
    CloseHandle(process.hThread); CloseHandle(process.hProcess);
    ExitProcess(0);
}
