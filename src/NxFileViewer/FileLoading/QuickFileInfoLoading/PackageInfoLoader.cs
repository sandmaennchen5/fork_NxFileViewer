using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emignatik.NxFileViewer.Services.KeysManagement;
using Emignatik.NxFileViewer.Utils;
using Emignatik.NxFileViewer.Utils.LibHacExtensions;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using ContentType = LibHac.Ncm.ContentType;

namespace Emignatik.NxFileViewer.FileLoading.QuickFileInfoLoading;

public class PackageInfoLoader : IPackageInfoLoader
{
    private readonly IPackageTypeAnalyzer _packageTypeAnalyzer;
    private readonly IKeySetProviderService _keySetProviderService;

    public PackageInfoLoader(IPackageTypeAnalyzer packageTypeAnalyzer, IKeySetProviderService keySetProviderService)
    {
        _packageTypeAnalyzer = packageTypeAnalyzer ?? throw new ArgumentNullException(nameof(packageTypeAnalyzer));
        _keySetProviderService = keySetProviderService ?? throw new ArgumentNullException(nameof(keySetProviderService));
    }

    public PackageInfo GetPackageInfo(string filePath)
    {
        var keySet = _keySetProviderService.GetKeySet();

        AccuratePackageType accuratePackageType;
        List<Content> contents;
        switch (_packageTypeAnalyzer.GetType(filePath))
        {
            case PackageType.UNKNOWN:
                throw new FileNotSupportedException(filePath);

            case PackageType.XCI:
                contents = LoadXciContents(filePath, keySet, out accuratePackageType);
                break;
            case PackageType.NSP:
                contents = LoadNspContents(filePath, keySet, out accuratePackageType);

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        var packageInfo = new PackageInfo
        {
            PackageType = _packageTypeAnalyzer.GetType(filePath),
            AccuratePackageType = accuratePackageType,
            Contents = contents
        };

        return packageInfo;

    }

    private static List<Content> LoadNspContents(string nspFilePath, KeySet keySet, out AccuratePackageType accuratePackageType)
    {
        using var localFile = new LocalStorage(nspFilePath, FileAccess.Read);
        using var nspPartition = localFile.LoadPartition();

        var contents = PackageMetadataReader.Read(nspPartition, keySet, out var containsNcz);

        accuratePackageType = containsNcz ? AccuratePackageType.NSZ : AccuratePackageType.NSP;

        return contents;
    }

    private static List<Content> LoadXciContents(string filePath, KeySet keySet, out AccuratePackageType accuratePackageType)
    {
        accuratePackageType = AccuratePackageType.XCI;

        using var localFile = new LocalFile(filePath, OpenMode.Read);

        var fileStorage = new FileStorage(localFile);
        var xci = new Xci(keySet, fileStorage);

        var contents = new List<Content>();

        if (xci.HasPartition(XciPartitionType.Secure))
        {
            var xciPartition = xci.OpenPartition(XciPartitionType.Secure);

            var partitionContents = PackageMetadataReader.Read(xciPartition, keySet, out var containsNcz);
            contents.AddRange(partitionContents);

            if (accuratePackageType == AccuratePackageType.XCI && containsNcz)
                accuratePackageType = AccuratePackageType.XCZ;
        }

        return contents;
    }



}
