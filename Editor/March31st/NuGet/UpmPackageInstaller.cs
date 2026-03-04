using System;
using System.Threading;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace March31st {
    public static class UpmPackageInstaller {
        public static async void InstallAsync(string package,
                                              CancellationToken cancellationToken,
                                              Action<bool> onComplete) {
            var result = false;
            try {
                if (string.IsNullOrEmpty(package))
                    return;

                AddRequest addRequest;
                try {
                    addRequest = Client.Add(package);
                }
                catch (Exception e) {
                    Debug.LogException(e);
                    return;
                }

                while (!addRequest.IsCompleted) {
                    await AsyncUtils.Yield();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (addRequest.Status != StatusCode.Success) {
                    if (addRequest.Error != null && !string.IsNullOrEmpty(addRequest.Error.message))
                        Debug.LogError($"Package installation failed with error: {addRequest.Error.message}");

                    return;
                }

                result = true;
            }
            catch (OperationCanceledException) { }
            catch (Exception e) {
                Debug.LogError($"Package installation failed with error: {e.Message}");
            }
            finally {
                onComplete?.Invoke(result);
            }
        }
    }
}