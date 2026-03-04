using System.Collections.Generic;

namespace March31st {
    public static class ScopedRegistryAdder {
        public static bool Add(string path, ScopedRegistryInfo registryToAdd) {
            using var manifestLoader = new ManifestLoader(path);
            var manifest = manifestLoader.ManifestInfo;
            manifest.ScopedRegistries ??= new List<ScopedRegistryInfo>();
            var existingRegistry = manifest.ScopedRegistries.Find(item => item.Name == registryToAdd.Name);
            if (existingRegistry == null)
                manifest.ScopedRegistries.Add(registryToAdd);
            else
                UpdateExistingRegistry(existingRegistry, registryToAdd);

            return true;
        }

        static void UpdateExistingRegistry(ScopedRegistryInfo existingRegistry, ScopedRegistryInfo newRegistry) {
            existingRegistry.Url = newRegistry.Url;

            if (existingRegistry.Scopes == null || existingRegistry.Scopes.Count == 0) {
                existingRegistry.Scopes = newRegistry.Scopes;
                return;
            }

            foreach (var newRegistryScope in newRegistry.Scopes) {
                if (!existingRegistry.Scopes.Contains(newRegistryScope)) {
                    existingRegistry.Scopes.Add(newRegistryScope);
                }
            }
        }
    }
}