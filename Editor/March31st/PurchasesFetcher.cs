using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace March31st {
    public static class PurchasesFetcher {
        public static async Awaitable<List<AssetInfo>> FetchMyAssets(CancellationToken cancellationToken, Action<string> onStatusUpdate = null) {
            const int pageLimit = 1000;

            var token = CloudProjectSettings.accessToken;

            var allAssets = new List<AssetInfo>();
            var offset = 0;
            var total = 1;

            while (allAssets.Count < total) {
                var url = $"https://packages-v2.unity.com/-/api/purchases?offset={offset}&limit={pageLimit}";

                using var request = UnityWebRequest.Get(url);
                request.SetRequestHeader("Authorization", $"Bearer {token}");

                var operation = request.SendWebRequest();
                while (!operation.isDone) {
                    await AsyncUtils.Yield();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (request.result != UnityWebRequest.Result.Success)
                    throw new Exception($"Failed to fetch assets: {request.error}\n{request.downloadHandler.text}");

                var json = request.downloadHandler.text;
                var (assets, totalCount) = ParsePurchases(json);
                total = totalCount;
                allAssets.AddRange(assets);
                offset += pageLimit;

                onStatusUpdate?.Invoke($"Fetching assets: {allAssets.Count}");
            }

            return allAssets;
        }

        static (List<AssetInfo> assets, int total) ParsePurchases(string json) {
            var list = new List<AssetInfo>();
            var totalPurchases = 0;

            var totalPurchasesString = GetStringValue(json, "total", false);
            if (!string.IsNullOrEmpty(totalPurchasesString))
                int.TryParse(totalPurchasesString, out totalPurchases);

            var parts = json.Split(new[] { "{\"id\":" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts) {
                var displayName = GetStringValue(part, "displayName");
                var packageIdS = GetStringValue(part, "packageId", false);
                var grantTime = GetStringValue(part, "grantTime");

                if (string.IsNullOrEmpty(displayName) || !int.TryParse(packageIdS, out _))
                    continue;

                list.Add(new AssetInfo {
                    _name = displayName,
                    _purchaseDate = grantTime,
                    _link = $"https://assetstore.unity.com/packages/slug/{packageIdS}"
                });
            }

            return (list, totalPurchases);
        }

        static string GetStringValue(string json, string key, bool isString = true) {
            var pattern = isString ? $"\"{key}\"\\s*:\\s*\"([^\"]*)\"" : $"\"{key}\"\\s*:\\s*([^,}}]*)";
            var match = Regex.Match(json, pattern);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }
    }
}