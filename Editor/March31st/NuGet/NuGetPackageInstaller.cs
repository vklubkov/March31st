#if MARCH31ST_NUGET_AVAILABLE
using System.Linq;
using NugetForUnity;
using NugetForUnity.Models;

namespace March31st {
    public static class NuGetPackageInstaller {
        public static bool Install(string packageToAdd) {
            var installedPackages = InstalledPackagesManager.InstalledPackages
                    .Select(package => package.Id)
                    .ToHashSet();

            if (installedPackages.Contains(packageToAdd))
                return false;

            var nuGetPackageIdentifier = new NugetPackageIdentifier(packageToAdd, version: null);
            NugetPackageInstaller.InstallIdentifier(nuGetPackageIdentifier, refreshAssets: false);
            return true;
        }
    }
}
#endif