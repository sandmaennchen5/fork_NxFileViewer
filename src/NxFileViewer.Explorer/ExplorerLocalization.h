#pragma once

inline const wchar_t* ExplorerTitle(unsigned action, const wchar_t* culture) {
    static const wchar_t* titles[4][5] = {
        { L"NxFileViewer Fork", L"Open in Batch", L"Verify in Batch", L"Open in File", L"Verify in File" },
        { L"NxFileViewer Fork", L"Im Batch \u00f6ffnen", L"Im Batch pr\u00fcfen", L"Im File \u00f6ffnen", L"Im File pr\u00fcfen" },
        { L"NxFileViewer Fork", L"Ouvrir dans Batch", L"V\u00e9rifier dans Batch", L"Ouvrir dans File", L"V\u00e9rifier dans File" },
        { L"NxFileViewer Fork", L"Abrir en Batch", L"Verificar en Batch", L"Abrir en File", L"Verificar en File" }
    };
    auto lower = [](wchar_t c) { return c >= L'A' && c <= L'Z' ? c + (L'a' - L'A') : c; };
    unsigned language = 0;
    if (culture && culture[0] && culture[1]) {
        auto a = lower(culture[0]), b = lower(culture[1]);
        if (a == L'd' && b == L'e') language = 1;
        else if (a == L'f' && b == L'r') language = 2;
        else if (a == L'e' && b == L's') language = 3;
    }
    return titles[language][action <= 4 ? action : 0];
}
