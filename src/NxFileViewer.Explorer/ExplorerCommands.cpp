// Native Explorer extension. No managed runtime is loaded into Explorer.
#define WIN32_LEAN_AND_MEAN
#define UNICODE
#define _UNICODE
#include <windows.h>
#include <shlobj.h>
#include <shobjidl.h>
#include <shlwapi.h>
#include <string>
#include <vector>
#include <new>
#include "ExplorerLocalization.h"

static const CLSID CLSID_NxExplorer = {0x872f5f52,0xbc53,0x4a69,{0xb1,0xd1,0x85,0x83,0xb2,0x0c,0xa4,0xb1}};
static LONG objects = 0;
enum class Action { Root, BatchOpen, BatchVerify, FileOpen, FileVerify, Compress, Decompress, ConvertNsp, ConvertXci, MergeNsp, MergeXci, SplitNsp, SplitXci };
static const wchar_t* Modes[] = { L"", L"batch-open", L"batch-verify", L"file-open", L"file-verify", L"compress", L"decompress", L"convert-nsp", L"convert-xci", L"merge-nsp", L"merge-xci", L"split-nsp", L"split-xci" };

static const wchar_t* Title(Action action) {
    wchar_t culture[32] = {}; DWORD bytes = sizeof(culture);
    auto result = RegGetValueW(HKEY_CURRENT_USER, L"Software\\NxFileViewerFork\\ExplorerIntegration", L"Language", RRF_RT_REG_SZ, nullptr, culture, &bytes);
    if (result != ERROR_SUCCESS || !culture[0] || _wcsicmp(culture, L"Auto") == 0) {
        const wchar_t* system = L"en";
        switch (PRIMARYLANGID(GetUserDefaultUILanguage())) {
            case LANG_GERMAN: system = L"de"; break;
            case LANG_FRENCH: system = L"fr"; break;
            case LANG_SPANISH: system = L"es"; break;
        }
        return ExplorerTitle(static_cast<unsigned>(action), system);
    }
    return ExplorerTitle(static_cast<unsigned>(action), culture);
}

static HRESULT Executable(std::wstring& value) {
    DWORD bytes = 0;
    auto result = RegGetValueW(HKEY_CURRENT_USER, L"Software\\NxFileViewerFork\\ExplorerIntegration", L"Executable", RRF_RT_REG_SZ, nullptr, nullptr, &bytes);
    if (result != ERROR_SUCCESS || bytes < sizeof(wchar_t) || bytes > 65536) return HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND);
    std::vector<wchar_t> buffer(bytes / sizeof(wchar_t));
    result = RegGetValueW(HKEY_CURRENT_USER, L"Software\\NxFileViewerFork\\ExplorerIntegration", L"Executable", RRF_RT_REG_SZ, nullptr, buffer.data(), &bytes);
    if (result != ERROR_SUCCESS) return HRESULT_FROM_WIN32(result);
    value = buffer.data();
    return value.empty() ? E_FAIL : S_OK;
}

static bool Supported(const std::wstring& path) {
    auto ext = PathFindExtensionW(path.c_str());
    for (auto candidate : { L".nsp", L".nsz", L".xci", L".xcz", L".nro", L".nca", L".nax0", L".zip", L".7z" })
        if (_wcsicmp(ext, candidate) == 0) return true;
    return false;
}

static bool FeatureEnabled(const wchar_t* name) {
    DWORD value = 1, bytes = sizeof(value);
    auto result = RegGetValueW(HKEY_CURRENT_USER, L"Software\\NxFileViewerFork\\ExplorerIntegration",
        name, RRF_RT_REG_DWORD, nullptr, &value, &bytes);
    // Existing installations have no flags until the updated viewer saves its configuration.
    return result != ERROR_SUCCESS || value != 0;
}

static bool SupportsAction(Action action, const std::vector<std::wstring>& paths, bool folder) {
    if ((action == Action::Compress || action == Action::Decompress) && !FeatureEnabled(L"CompressionEnabled")) return false;
    if (action >= Action::ConvertNsp && action <= Action::SplitXci) {
        if (!FeatureEnabled(L"PackageOperationsEnabled")) return false;
        if (!FeatureEnabled(L"CompressionEnabled")) {
            for (const auto& path : paths) {
                auto ext = PathFindExtensionW(path.c_str());
                if (_wcsicmp(ext, L".nsz") == 0 || _wcsicmp(ext, L".xcz") == 0) return false;
            }
        }
    }
    if (action == Action::FileOpen || action == Action::FileVerify) return paths.size() == 1 && !folder;
    if (action >= Action::ConvertNsp && action <= Action::SplitXci) {
        if (folder || paths.empty()) return false;
        if ((action == Action::MergeNsp || action == Action::MergeXci) && paths.size() < 2) return false;
        for (const auto& path : paths) {
            auto ext = PathFindExtensionW(path.c_str());
            if (_wcsicmp(ext, L".nsp") != 0 && _wcsicmp(ext, L".xci") != 0 &&
                _wcsicmp(ext, L".nsz") != 0 && _wcsicmp(ext, L".xcz") != 0) return false;
        }
        return true;
    }
    if (action != Action::Compress && action != Action::Decompress) return true;
    if (folder || paths.empty()) return false;
    for (const auto& path : paths) {
        auto ext = PathFindExtensionW(path.c_str());
        if (action == Action::Compress) {
            if (_wcsicmp(ext, L".nsp") != 0 && _wcsicmp(ext, L".xci") != 0) return false;
        } else if (_wcsicmp(ext, L".nsz") != 0 && _wcsicmp(ext, L".xcz") != 0) return false;
    }
    return true;
}

static HRESULT Selection(IShellItemArray* items, std::vector<std::wstring>& paths, bool& hasFolder) {
    hasFolder = false;
    if (!items) return E_INVALIDARG;
    DWORD count = 0;
    HRESULT hr = items->GetCount(&count);
    if (FAILED(hr) || count == 0 || count > 100000) return E_INVALIDARG;
    for (DWORD i = 0; i < count; ++i) {
        IShellItem* item = nullptr;
        hr = items->GetItemAt(i, &item);
        if (FAILED(hr)) return hr;
        PWSTR name = nullptr;
        hr = item->GetDisplayName(SIGDN_FILESYSPATH, &name);
        item->Release();
        if (FAILED(hr)) return hr;
        std::wstring path(name);
        CoTaskMemFree(name);
        DWORD attributes = GetFileAttributesW(path.c_str());
        if (attributes == INVALID_FILE_ATTRIBUTES) return HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND);
        bool folder = (attributes & FILE_ATTRIBUTE_DIRECTORY) != 0;
        if (!folder && !Supported(path)) return E_INVALIDARG;
        hasFolder |= folder;
        paths.push_back(std::move(path));
    }
    return S_OK;
}

static HRESULT Launch(Action action, const std::vector<std::wstring>& paths) {
    std::wstring exe;
    HRESULT hr = Executable(exe);
    if (FAILED(hr)) return hr;
    PWSTR local = nullptr;
    hr = SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, nullptr, &local);
    if (FAILED(hr)) return hr;
    std::wstring directory = std::wstring(local) + L"\\NxFileViewerFork\\ExplorerRequests";
    CoTaskMemFree(local);
    int created = SHCreateDirectoryExW(nullptr, directory.c_str(), nullptr);
    if (created != ERROR_SUCCESS && created != ERROR_ALREADY_EXISTS && created != ERROR_FILE_EXISTS) return HRESULT_FROM_WIN32(created);
    GUID id;
    hr = CoCreateGuid(&id);
    if (FAILED(hr)) return hr;
    wchar_t guid[40];
    StringFromGUID2(id, guid, 40);
    std::wstring compact;
    for (auto c : guid) { if (!c) break; if (c != L'{' && c != L'}' && c != L'-') compact += c; }
    std::wstring selectionFile = directory + L"\\" + compact + L".selection";
    std::wstring data(L"NXFV1", 5);
    data += L'\0'; data += Modes[static_cast<int>(action)]; data += L'\0';
    for (const auto& path : paths) { data += path; data += L'\0'; }
    if (data.size() * sizeof(wchar_t) > 8 * 1024 * 1024) return HRESULT_FROM_WIN32(ERROR_BUFFER_OVERFLOW);
    HANDLE file = CreateFileW(selectionFile.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (file == INVALID_HANDLE_VALUE) return HRESULT_FROM_WIN32(GetLastError());
    DWORD written = 0, size = static_cast<DWORD>(data.size() * sizeof(wchar_t));
    bool ok = WriteFile(file, data.data(), size, &written, nullptr) && written == size;
    DWORD error = ok ? ERROR_SUCCESS : GetLastError();
    CloseHandle(file);
    if (!ok) { DeleteFileW(selectionFile.c_str()); return HRESULT_FROM_WIN32(error ? error : ERROR_WRITE_FAULT); }
    // Only a short response-file argument goes on the command line, even for large selections.
    std::wstring command = L"\"" + exe + L"\" --explorer-request \"" + selectionFile + L"\"";
    std::wstring working = exe.substr(0, exe.find_last_of(L"\\/"));
    STARTUPINFOW startup = {}; startup.cb = sizeof(startup);
    PROCESS_INFORMATION process = {};
    if (!CreateProcessW(exe.c_str(), command.data(), nullptr, nullptr, FALSE, 0, nullptr, working.c_str(), &startup, &process)) {
        error = GetLastError(); DeleteFileW(selectionFile.c_str()); return HRESULT_FROM_WIN32(error);
    }
    CloseHandle(process.hThread); CloseHandle(process.hProcess);
    return S_OK;
}

class Command final : public IExplorerCommand {
    LONG refs = 1;
    Action action;
public:
    explicit Command(Action value) : action(value) { InterlockedIncrement(&objects); }
    ~Command() { InterlockedDecrement(&objects); }
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid, void** result) override {
        if (!result) return E_POINTER;
        *result = nullptr;
        if (IsEqualIID(iid, IID_IUnknown) || IsEqualIID(iid, IID_IExplorerCommand)) { *result = static_cast<IExplorerCommand*>(this); AddRef(); return S_OK; }
        return E_NOINTERFACE;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return InterlockedIncrement(&refs); }
    ULONG STDMETHODCALLTYPE Release() override { auto n = InterlockedDecrement(&refs); if (!n) delete this; return n; }
    HRESULT STDMETHODCALLTYPE GetTitle(IShellItemArray*, PWSTR* title) override { return SHStrDupW(Title(action), title); }
    HRESULT STDMETHODCALLTYPE GetIcon(IShellItemArray*, PWSTR* icon) override {
        *icon = nullptr;
        try { std::wstring exe; auto hr = Executable(exe); return FAILED(hr) ? hr : SHStrDupW((L"\"" + exe + L"\",0").c_str(), icon); }
        catch (...) { return E_OUTOFMEMORY; }
    }
    HRESULT STDMETHODCALLTYPE GetToolTip(IShellItemArray*, PWSTR* tip) override { *tip = nullptr; return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetCanonicalName(GUID* name) override { *name = CLSID_NxExplorer; name->Data1 += static_cast<int>(action); return S_OK; }
    HRESULT STDMETHODCALLTYPE GetState(IShellItemArray* items, BOOL, EXPCMDSTATE* state) override {
        *state = ECS_HIDDEN;
        try {
            std::vector<std::wstring> paths; bool folder;
            if (FAILED(Selection(items, paths, folder))) return S_OK;
            if (!SupportsAction(action, paths, folder)) return S_OK;
            *state = ECS_ENABLED; return S_OK;
        } catch (...) { return E_OUTOFMEMORY; }
    }
    HRESULT STDMETHODCALLTYPE Invoke(IShellItemArray* items, IBindCtx*) override {
        if (action == Action::Root) return E_NOTIMPL;
        HRESULT hr;
        try {
            std::vector<std::wstring> paths; bool folder;
            hr = Selection(items, paths, folder);
            if (SUCCEEDED(hr) && !SupportsAction(action, paths, folder)) hr = E_INVALIDARG;
            if (SUCCEEDED(hr)) hr = Launch(action, paths);
        } catch (...) { hr = E_OUTOFMEMORY; }
        if (FAILED(hr)) MessageBoxW(nullptr, L"NxFileViewer konnte nicht gestartet werden. Bitte die Explorer-Integration in den Einstellungen erneut hinzuf\u00fcgen.", L"NxFileViewer", MB_OK | MB_ICONERROR);
        return hr;
    }
    HRESULT STDMETHODCALLTYPE GetFlags(EXPCMDFLAGS* flags) override { *flags = action == Action::Root ? ECF_HASSUBCOMMANDS : ECF_DEFAULT; return S_OK; }
    HRESULT STDMETHODCALLTYPE EnumSubCommands(IEnumExplorerCommand** commands) override;
};

class Commands final : public IEnumExplorerCommand {
    LONG refs = 1;
    ULONG index = 1;
public:
    explicit Commands(ULONG start = 1) : index(start) { InterlockedIncrement(&objects); }
    ~Commands() { InterlockedDecrement(&objects); }
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid, void** result) override {
        if (!result) return E_POINTER;
        *result = nullptr;
        if (IsEqualIID(iid, IID_IUnknown) || IsEqualIID(iid, IID_IEnumExplorerCommand)) { *result = static_cast<IEnumExplorerCommand*>(this); AddRef(); return S_OK; }
        return E_NOINTERFACE;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return InterlockedIncrement(&refs); }
    ULONG STDMETHODCALLTYPE Release() override { auto n = InterlockedDecrement(&refs); if (!n) delete this; return n; }
    HRESULT STDMETHODCALLTYPE Next(ULONG count, IExplorerCommand** values, ULONG* fetched) override {
        if (!values || (!fetched && count != 1)) return E_POINTER;
        ULONG n = 0;
        while (n < count && index <= 12) {
            static constexpr Action order[] = { Action::FileOpen, Action::FileVerify, Action::BatchOpen, Action::BatchVerify, Action::Compress, Action::Decompress, Action::ConvertNsp, Action::ConvertXci, Action::MergeNsp, Action::MergeXci, Action::SplitNsp, Action::SplitXci };
            auto command = new(std::nothrow) Command(order[index - 1]);
            if (!command) { if (fetched) *fetched = n; return E_OUTOFMEMORY; }
            values[n++] = command; ++index;
        }
        if (fetched) *fetched = n;
        return n == count ? S_OK : S_FALSE;
    }
    HRESULT STDMETHODCALLTYPE Skip(ULONG count) override { ULONG remaining = 13 - index; index += count < remaining ? count : remaining; return count <= remaining ? S_OK : S_FALSE; }
    HRESULT STDMETHODCALLTYPE Reset() override { index = 1; return S_OK; }
    HRESULT STDMETHODCALLTYPE Clone(IEnumExplorerCommand** value) override { *value = new(std::nothrow) Commands(index); return *value ? S_OK : E_OUTOFMEMORY; }
};

HRESULT Command::EnumSubCommands(IEnumExplorerCommand** commands) {
    *commands = nullptr;
    if (action != Action::Root) return E_NOTIMPL;
    *commands = new(std::nothrow) Commands();
    return *commands ? S_OK : E_OUTOFMEMORY;
}

class Factory final : public IClassFactory {
    LONG refs = 1;
public:
    Factory() { InterlockedIncrement(&objects); }
    ~Factory() { InterlockedDecrement(&objects); }
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid, void** result) override {
        if (!result) return E_POINTER;
        *result = nullptr;
        if (IsEqualIID(iid, IID_IUnknown) || IsEqualIID(iid, IID_IClassFactory)) { *result = static_cast<IClassFactory*>(this); AddRef(); return S_OK; }
        return E_NOINTERFACE;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return InterlockedIncrement(&refs); }
    ULONG STDMETHODCALLTYPE Release() override { auto n = InterlockedDecrement(&refs); if (!n) delete this; return n; }
    HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown* outer, REFIID iid, void** value) override {
        if (!value) return E_POINTER;
        *value = nullptr;
        if (outer) return CLASS_E_NOAGGREGATION;
        auto command = new(std::nothrow) Command(Action::Root);
        if (!command) return E_OUTOFMEMORY;
        HRESULT hr = command->QueryInterface(iid, value); command->Release(); return hr;
    }
    HRESULT STDMETHODCALLTYPE LockServer(BOOL lock) override { if (lock) InterlockedIncrement(&objects); else InterlockedDecrement(&objects); return S_OK; }
};

extern "C" HRESULT __stdcall DllGetClassObject(REFCLSID clsid, REFIID iid, void** value) {
    if (!value) return E_POINTER;
    *value = nullptr;
    if (!IsEqualCLSID(clsid, CLSID_NxExplorer)) return CLASS_E_CLASSNOTAVAILABLE;
    auto factory = new(std::nothrow) Factory();
    if (!factory) return E_OUTOFMEMORY;
    HRESULT hr = factory->QueryInterface(iid, value); factory->Release(); return hr;
}
extern "C" HRESULT __stdcall DllCanUnloadNow() { return objects == 0 ? S_OK : S_FALSE; }
