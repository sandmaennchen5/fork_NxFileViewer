#pragma once

inline const wchar_t* ExplorerTitle(unsigned action, const wchar_t* culture) {
    static const wchar_t* titles[4][13] = {
        { L"NxFileViewer Fork", L"Open in Batch", L"Verify in Batch", L"Open in File", L"Verify in File", L"Compress", L"Decompress", L"Convert to NSP", L"Convert to XCI", L"Merge to NSP", L"Merge to XCI", L"Split to NSP", L"Split to XCI" },
        { L"NxFileViewer Fork", L"Im Batch \u00f6ffnen", L"Im Batch pr\u00fcfen", L"Im File \u00f6ffnen", L"Im File pr\u00fcfen", L"Komprimieren", L"Dekomprimieren", L"Konvertieren zu NSP", L"Konvertieren zu XCI", L"Zusammenf\u00fchren zu NSP", L"Zusammenf\u00fchren zu XCI", L"Aufteilen zu NSP", L"Aufteilen zu XCI" },
        { L"NxFileViewer Fork", L"Ouvrir dans Batch", L"V\u00e9rifier dans Batch", L"Ouvrir dans File", L"V\u00e9rifier dans File", L"Compresser", L"D\u00e9compresser", L"Convertir en NSP", L"Convertir en XCI", L"Fusionner en NSP", L"Fusionner en XCI", L"Diviser en NSP", L"Diviser en XCI" },
        { L"NxFileViewer Fork", L"Abrir en Batch", L"Verificar en Batch", L"Abrir en File", L"Verificar en File", L"Comprimir", L"Descomprimir", L"Convertir a NSP", L"Convertir a XCI", L"Combinar en NSP", L"Combinar en XCI", L"Dividir en NSP", L"Dividir en XCI" }
    };
    auto lower = [](wchar_t c) { return c >= L'A' && c <= L'Z' ? c + (L'a' - L'A') : c; };
    unsigned language = 0;
    if (culture && culture[0] && culture[1]) {
        auto a = lower(culture[0]), b = lower(culture[1]);
        if (a == L'd' && b == L'e') language = 1;
        else if (a == L'f' && b == L'r') language = 2;
        else if (a == L'e' && b == L's') language = 3;
    }
    return titles[language][action <= 12 ? action : 0];
}
