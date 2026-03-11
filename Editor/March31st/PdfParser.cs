#if MARCH31ST_TABULA_AVAILABLE
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Tabula;
using Tabula.Extractors;
using UnityEngine;
using UglyToad.PdfPig;

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
            using var document = PdfDocument.Open(pdfPath, new ParsingOptions { ClipPaths = true });
            for (var i = 1; i <= document.NumberOfPages; i++) {
                var page = ObjectExtractor.Extract(document, i);
                IExtractionAlgorithm ea = new SpreadsheetExtractionAlgorithm();
                var tables = ea.Extract(page);
                var table = tables[0];
                var rows = table.Rows;
                foreach (var row in rows) {
                    var cellsInRow = row.Count;
                    if (cellsInRow != 2)
                        continue;

                    AddInfo(list, row[0].GetText(), row[1].GetText());
                }
            }

            return list;
        }

        static void AddInfo(List<AssetInfo> list, string name, string publisher) {
            if (name == null || publisher == null)
                return;

            if (name == "" && publisher == "")
                return;

            if (name == "A S S E T" && publisher == "P U B L I S H E R")
                return;

            var sanitizedName = name
                .Replace(" ", "")
                .Replace("_", "")
                .Replace("\t", "")
                .Replace("\r", "")
                .Replace("\n", "")
                .Replace(",", "");

            var sanitizedPublisher = publisher
                .Replace("\r\n", " ")
                .Replace('\n', ' ')
                .Replace('\r', ' ')
                .Trim(' ')
                .Trim('_')
                .Trim(' ')
                .Replace(",", "");

            list.Add(new AssetInfo {
                _name = sanitizedName,
                _sanitizedName = sanitizedName,
                _sanitizedPublisher = sanitizedPublisher
            });
        }
    }
}
#endif