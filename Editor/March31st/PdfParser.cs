using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace March31st {
    public static class PdfParser {
        public static async Task<List<AssetInfo>> LoadPdfContentsAsync(string pdfPath, CancellationToken cancellationToken) {
            if (!File.Exists(pdfPath))
                throw new FileNotFoundException($"PDF file not found at {pdfPath}");

            await Awaitable.BackgroundThreadAsync();
            cancellationToken.ThrowIfCancellationRequested();
            var list = LoadPdfContents(pdfPath);
            cancellationToken.ThrowIfCancellationRequested();
            await Awaitable.MainThreadAsync();
            return list;
        }

        private static List<AssetInfo> LoadPdfContents(string pdfPath) {
            var list = new List<AssetInfo>();
            var process = new Process();
            process.StartInfo.FileName = "pdftotext";
            process.StartInfo.Arguments = $"\"{pdfPath}\" -";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            var lines = output.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .ToList();

            // The structure starts with ASSET, PUBLISHER
            var startIdx = -1;
            for (var i = 0; i < lines.Count - 1; i++) {
                if (lines[i].Equals("ASSET", StringComparison.OrdinalIgnoreCase) &&
                    lines[i + 1].Equals("PUBLISHER", StringComparison.OrdinalIgnoreCase)) {
                    startIdx = i + 2;
                    break;
                }
            }

            if (startIdx != -1) {
                for (var i = startIdx; i < lines.Count - 1; i += 2) {
                    list.Add(new AssetInfo { _name = lines[i], _publisher = lines[i + 1] });
                }
            }
            else if (lines.Count > 0) {
                // Fallback: try to guess if it's pairs from the beginning if no header found
                for (var i = 0; i < lines.Count - 1; i += 2) {
                    list.Add(new AssetInfo { _name = lines[i], _publisher = lines[i + 1] });
                }
            }

            return list;
        }
    }
}