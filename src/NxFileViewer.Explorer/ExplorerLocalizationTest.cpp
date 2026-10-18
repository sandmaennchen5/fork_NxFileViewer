#include "ExplorerLocalization.h"
#include <cwchar>

int main() {
    const wchar_t* cultures[] = { L"en-US", L"de-DE", L"fr-FR", L"es-ES" };
    const wchar_t* expected[4][4] = {
        { L"Open in Batch", L"Verify in Batch", L"Open in File", L"Verify in File" },
        { L"Im Batch \u00f6ffnen", L"Im Batch pr\u00fcfen", L"Im File \u00f6ffnen", L"Im File pr\u00fcfen" },
        { L"Ouvrir dans Batch", L"V\u00e9rifier dans Batch", L"Ouvrir dans File", L"V\u00e9rifier dans File" },
        { L"Abrir en Batch", L"Verificar en Batch", L"Abrir en File", L"Verificar en File" }
    };
    for (unsigned language = 0; language < 4; ++language) {
        if (std::wcscmp(ExplorerTitle(0, cultures[language]), L"NxFileViewer Fork")) return 1;
        for (unsigned action = 1; action <= 4; ++action)
            if (std::wcscmp(ExplorerTitle(action, cultures[language]), expected[language][action - 1])) return 2;
    }
    if (std::wcscmp(ExplorerTitle(1, L"DE-at"), expected[1][0])) return 3;
    if (std::wcscmp(ExplorerTitle(1, L"unknown"), expected[0][0])) return 4;
    if (std::wcscmp(ExplorerTitle(1, nullptr), expected[0][0])) return 5;
    return 0;
}
