#define WIN32_LEAN_AND_MEAN
#define UNICODE
#include <windows.h>
#include <string>

// Hidden package application. The context-menu DLL starts the viewer directly;
// this entry point also makes explicit activation of the integration package useful.
int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int) {
    wchar_t executable[32768]; DWORD bytes = sizeof(executable);
    auto result = RegGetValueW(HKEY_CURRENT_USER, L"Software\\NxFileViewerFork\\ExplorerIntegration", L"Executable", RRF_RT_REG_SZ, nullptr, executable, &bytes);
    if (result != ERROR_SUCCESS) return 1;
    std::wstring command = L"\"" + std::wstring(executable) + L"\"";
    STARTUPINFOW startup = {}; startup.cb = sizeof(startup);
    PROCESS_INFORMATION process = {};
    if (!CreateProcessW(executable, command.data(), nullptr, nullptr, FALSE, 0, nullptr, nullptr, &startup, &process)) return 1;
    CloseHandle(process.hThread); CloseHandle(process.hProcess);
    return 0;
}
