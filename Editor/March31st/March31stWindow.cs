using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace March31st {
    // ReSharper disable once InconsistentNaming
    public class March31stWindow : EditorWindow {
        List<AssetInfo> _myAssets = new();
        List<AssetInfo> _pdfAssets = new();

        readonly List<AssetInfo> _matchedAssets = new();

        bool _isLoading;
        string _status = "Ready";

        CancellationTokenSource _cancellationTokenSource;

        VisualElement _root;
        Button _installButton;
        TextField _pdfPathField;
        Toggle _approximateToggle;
        SliderInt _approximateThresholdSlider;
        Button _loadButton;
        Label _statusLabel;
        MultiColumnListView _listView;

        bool _savePdfToCsv;
        bool _savePurchasesToCsv;
        bool _saveResultsToCsv;
        bool _fetchPublishers;

        [MenuItem("Tools/March 31st...")]
        public static void ShowWindow() => GetWindow<March31stWindow>("March 31st");

        public void CreateGUI() {
            _root = rootVisualElement;

            var rootPanel = new VisualElement();
            rootPanel.style.marginBottom = 10;
            rootPanel.style.marginTop = 10;
            rootPanel.style.marginLeft = 10;
            rootPanel.style.marginRight = 10;
            _root.Add(rootPanel);

            var mainTitle = new Label("March 31st Removed Assets Checker");
            mainTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            mainTitle.style.fontSize = 14;
            mainTitle.style.marginBottom = 10;
            rootPanel.Add(mainTitle);

#if MARCH31ST_TABULA_AVAILABLE
            var pdfPathContainer = new VisualElement();
            pdfPathContainer.style.flexDirection = FlexDirection.Row;
            pdfPathContainer.style.marginBottom = 5;

            _pdfPathField = new TextField("Pdf Path");
            _pdfPathField.value = GetInitialPdfPath();
            _pdfPathField.style.flexGrow = 1;
            _pdfPathField.labelElement.style.minWidth = 150;

            _pdfPathField.RegisterValueChangedCallback(evt =>
                SessionState.SetString("March31st_PdfPath", evt.newValue));

            pdfPathContainer.Add(_pdfPathField);

            var browseButton = new Button(() => {
                var path = EditorUtility.OpenFilePanel("Select PDF", GetDownloadsFolderPath(), "pdf");
                if (!string.IsNullOrEmpty(path))
                    _pdfPathField.value = path;
            });

            browseButton.text = "...";
            browseButton.style.marginLeft = 0;
            browseButton.style.marginRight = 0;
            pdfPathContainer.Add(browseButton);
            rootPanel.Add(pdfPathContainer);

            _approximateToggle = new Toggle("Enable Approximate Comparison (used only when no package ids provided)");
            _approximateToggle.value = SessionState.GetBool("March31st_ApproximateComparison", false);
            _approximateToggle.style.marginBottom = 5;
            _approximateToggle.labelElement.style.minWidth = 150;
            _approximateToggle.RegisterValueChangedCallback(evt =>
                SessionState.SetBool("March31st_ApproximateComparison", evt.newValue));
            rootPanel.Add(_approximateToggle);

            _approximateThresholdSlider = new SliderInt("Approximate Comparison Threshold", 0, 100);
            _approximateThresholdSlider.value = SessionState.GetInt("March31st_ApproximateThreshold", 75);
            _approximateThresholdSlider.style.marginBottom = 5;
            _approximateThresholdSlider.labelElement.style.minWidth = 150;
            _approximateThresholdSlider.RegisterValueChangedCallback(evt =>
                SessionState.SetInt("March31st_ApproximateThreshold", evt.newValue));
            rootPanel.Add(_approximateThresholdSlider);
            var checkboxRow = new VisualElement();
            checkboxRow.style.flexDirection = FlexDirection.Row;
            checkboxRow.style.marginBottom = 5;

            var pdfCsvToggle = new Toggle("Save parsed PDF to CSV");
            pdfCsvToggle.value = _savePdfToCsv;
            pdfCsvToggle.RegisterValueChangedCallback(evt => _savePdfToCsv = evt.newValue);
            checkboxRow.Add(pdfCsvToggle);

            var purchasesCsvToggle = new Toggle("Save parsed purchases to CSV");
            purchasesCsvToggle.value = _savePurchasesToCsv;
            purchasesCsvToggle.RegisterValueChangedCallback(evt => _savePurchasesToCsv = evt.newValue);
            checkboxRow.Add(purchasesCsvToggle);

            var resultsCsvToggle = new Toggle("Save matched results to CSV");
            resultsCsvToggle.value = _saveResultsToCsv;
            resultsCsvToggle.RegisterValueChangedCallback(evt => _saveResultsToCsv = evt.newValue);
            checkboxRow.Add(resultsCsvToggle);

            rootPanel.Add(checkboxRow);

            var fetchPublishersToggle = new Toggle("Fetching publisher names from Asset Store");
            fetchPublishersToggle.value = _fetchPublishers;
            fetchPublishersToggle.style.marginBottom = 5;
            fetchPublishersToggle.RegisterValueChangedCallback(evt => _fetchPublishers = evt.newValue);
            rootPanel.Add(fetchPublishersToggle);

            _loadButton = new Button(RunCheck);
            _loadButton.style.height = 30;
            _loadButton.style.marginLeft = 0;
            _loadButton.style.marginRight = 0;
            _loadButton.text = "Parse PDF, fetch Purchases, and compare";
            rootPanel.Add(_loadButton);

            _statusLabel = new Label($"Status: {_status}");
            _statusLabel.style.marginBottom = 5;
            rootPanel.Add(_statusLabel);

            _listView = new MultiColumnListView();
            _listView.itemsSource = _matchedAssets;
            _listView.showAlternatingRowBackgrounds = AlternatingRowBackground.All;
            _listView.reorderable = false;
            _listView.style.flexGrow = 1;
            _listView.style.minWidth = 600;
            _listView.sortingMode = ColumnSortingMode.Custom;

            _listView.RegisterCallback<GeometryChangedEvent>(_ => {
                var scrollView = _listView.Q<ScrollView>();
                if (scrollView == null)
                    return;

                scrollView.mode = ScrollViewMode.VerticalAndHorizontal;
            });

            var columns = _listView.columns;

            columns.Add(new Column {
                name = "Asset Name",
                title = "Asset Name",
                makeCell = () => {
                    var tf = new TextField();
                    tf.isReadOnly = true;
                    tf.style.backgroundColor = Color.clear;
                    tf.style.borderBottomWidth = 0;
                    tf.style.borderTopWidth = 0;
                    tf.style.borderLeftWidth = 0;
                    tf.style.borderRightWidth = 0;
                    tf.style.marginTop = 0;
                    tf.style.marginBottom = 0;
                    tf.style.marginLeft = 0;
                    tf.style.marginRight = 0;
                    return tf;
                },
                bindCell = (element, index) => ((TextField)element).value = _matchedAssets[index]._name,
                width = 250,
                stretchable = true,
                sortable = true
            });

            columns.Add(new Column {
                name = "Publisher",
                title = "Publisher",
                makeCell = () => {
                    var tf = new TextField();
                    tf.isReadOnly = true;
                    tf.style.backgroundColor = Color.clear;
                    tf.style.borderBottomWidth = 0;
                    tf.style.borderTopWidth = 0;
                    tf.style.borderLeftWidth = 0;
                    tf.style.borderRightWidth = 0;
                    tf.style.marginTop = 0;
                    tf.style.marginBottom = 0;
                    tf.style.marginLeft = 0;
                    tf.style.marginRight = 0;
                    return tf;
                },
                bindCell = (element, index) => ((TextField)element).value = _matchedAssets[index]._sanitizedPublisher,
                width = 200,
                stretchable = true,
                sortable = true
            });

            columns.Add(new Column {
                name = "Package ID",
                title = "Package ID",
                makeCell = () => {
                    var tf = new TextField();
                    tf.isReadOnly = true;
                    tf.style.backgroundColor = Color.clear;
                    tf.style.borderBottomWidth = 0;
                    tf.style.borderTopWidth = 0;
                    tf.style.borderLeftWidth = 0;
                    tf.style.borderRightWidth = 0;
                    tf.style.marginTop = 0;
                    tf.style.marginBottom = 0;
                    tf.style.marginLeft = 0;
                    tf.style.marginRight = 0;
                    return tf;
                },
                bindCell = (element, index) => ((TextField)element).value = _matchedAssets[index]._packageId,
                width = 200,
                stretchable = true,
                sortable = true
            });

            _listView.columnSortingChanged += OnColumnSortingChanged;
            void OnColumnSortingChanged() {
                var sortedColumn = _listView.sortedColumns.ToArray()[0];
                var ascending = sortedColumn.direction == SortDirection.Ascending;

                if (sortedColumn.columnName == "Asset Name") {
                    if (ascending)
                        _matchedAssets.Sort((a, b) => string.Compare(a._name, b._name, StringComparison.OrdinalIgnoreCase));
                    else
                        _matchedAssets.Sort((a, b) => string.Compare(b._name, a._name, StringComparison.OrdinalIgnoreCase));
                }

                if (sortedColumn.columnName == "Publisher") {
                    if (ascending)
                        _matchedAssets.Sort((a, b) => string.Compare(a._sanitizedPublisher, b._sanitizedPublisher, StringComparison.OrdinalIgnoreCase));
                    else
                        _matchedAssets.Sort((a, b) => string.Compare(b._sanitizedPublisher, a._sanitizedPublisher, StringComparison.OrdinalIgnoreCase));
                }

                if (sortedColumn.columnName == "Package ID") {
                    if (ascending)
                        _matchedAssets.Sort((a, b) => int.Parse(a._packageId).CompareTo(int.Parse(b._packageId)));
                    else
                        _matchedAssets.Sort((a, b) => int.Parse(b._packageId).CompareTo(int.Parse(a._packageId)));
                }

                if (sortedColumn.columnName == "Purchased within last 6 month") {
                    if (ascending)
                        _matchedAssets.Sort((a, b) => DateTime.Compare(Parse(a._purchaseDate), Parse(b._purchaseDate)));
                    else
                        _matchedAssets.Sort((a, b) => DateTime.Compare(Parse(b._purchaseDate), Parse(a._purchaseDate)));
                }

                _listView.RefreshItems();
            }

            DateTime Parse(string purchaseDate) {
                if (string.IsNullOrEmpty(purchaseDate))
                    return default;

                if (!DateTime.TryParse(purchaseDate, out var date))
                    return default;

                return date;
            }

            columns.Add(new Column {
                name = "Purchased within last 6 month",
                title = "",
                makeCell = () => {
                    var label = new Label();
                    label.style.unityTextAlign = TextAnchor.MiddleCenter;
                    return label;
                },
                bindCell = (element, index) => {
                    var asset = _matchedAssets[index];

                    var watchSymbol = "";
                    try {
                        if (string.IsNullOrEmpty(asset._purchaseDate))
                            return;

                        if (!DateTime.TryParse(asset._purchaseDate, out var date))
                            return;

                        if (date < DateTime.Now.AddMonths(-6))
                            return;

                        watchSymbol = "\u231A"; // Watch emoji
                    }
                    finally {
                        ((Label)element).text = watchSymbol;
                    }
                },
                width = 30,
                stretchable = false,
                sortable = true
            });

            columns.Add(new Column {
                name = "Asset Store Link",
                title = "",
                makeCell = () => {
                    var btn = new Button { text = "Store Link" };
                    btn.style.backgroundColor = new StyleColor(Color.clear);
                    btn.style.borderBottomWidth = 0;
                    btn.style.borderTopWidth = 0;
                    btn.style.borderLeftWidth = 0;
                    btn.style.borderRightWidth = 0;
                    btn.style.color = new StyleColor(new Color(0.3f, 0.5f, 1f)); // Link color
                    btn.style.unityFontStyleAndWeight = FontStyle.Bold;
                    return btn;
                },
                bindCell = (element, index) => {
                    var btn = (Button)element;
                    btn.clickable = new Clickable(() => Application.OpenURL(_matchedAssets[index]._link));
                },
                width = 100,
                stretchable = false,
                sortable = false
            });

            _root.Add(_listView);
#else
            _installButton = new Button(Install);
            _installButton.text = "Install NuGetForUnity, Tabula, UglyToad.PdfPig and Microsoft.Bcl.HashCode";
            _installButton.style.height = 30;
            _installButton.style.marginLeft = 0;
            _installButton.style.marginRight = 0;
            rootPanel.Add(_installButton);
#endif
        }

        void Install() => Survivor.InstallDependencies();

        void OnEnable() {
            _cancellationTokenSource = new CancellationTokenSource();
        }

        void OnDisable() {
            if (_cancellationTokenSource != null) {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
        }

        void UpdateStatus(string status) {
            _status = status;
            if (_statusLabel == null)
                return;

            _statusLabel.text = $"Status: {status}";
        }

        async void RunCheck() {
            try {
                if (_cancellationTokenSource != null) {
                    _cancellationTokenSource.Cancel();
                    _cancellationTokenSource.Dispose();
                }
                _cancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = _cancellationTokenSource.Token;

                _loadButton.SetEnabled(false);
                UpdateStatus("Reading PDF...");

#if MARCH31ST_TABULA_AVAILABLE
                _pdfAssets = await PdfParser.LoadPdfContentsAsync(_pdfPathField.value, cancellationToken);
                UpdateStatus($"PDF read. Found {_pdfAssets.Count} entries. Fetching my assets...");

                if (_savePdfToCsv)
                    SaveToCsv(Path.Combine(Application.dataPath, "Pdf.csv"), _pdfAssets);

                if (_pdfAssets.Count != 0) {
                    _myAssets = await PurchasesFetcher.FetchMyAssets(cancellationToken, UpdateStatus);
                    UpdateStatus($"My assets fetched ({_myAssets.Count}). Comparing...");

                    if (_savePurchasesToCsv)
                        SaveToCsv(Path.Combine(Application.dataPath, "Purchases.csv"), _myAssets);

                    await CompareAssets(
                        _approximateToggle.value,
                        _approximateThresholdSlider.value,
                        cancellationToken);

                    var matchedAssetsCopy = _matchedAssets.ToList();
                    if (_fetchPublishers) {
                        _loadButton.SetEnabled(true);
                        UpdateStatus("Fetching publisher names...");
                        foreach (var asset in matchedAssetsCopy) {
                            cancellationToken.ThrowIfCancellationRequested();
                            var result = await PurchasesFetcher.FetchPublisher(asset._packageId, cancellationToken);
                            if (!string.IsNullOrEmpty(result))
                                asset._sanitizedPublisher = result;

                            _listView.RefreshItems();
                        }
                    }

                    if (_saveResultsToCsv)
                        SaveToCsv(Path.Combine(Application.dataPath, "Results.csv"), matchedAssetsCopy);
                }
#endif

                UpdateStatus($"Done. Found {_matchedAssets.Count} matches.");

                _listView.Rebuild();
            }
            catch (OperationCanceledException) {
                await Awaitable.MainThreadAsync();
                UpdateStatus("Cancelled.");
            }
            catch (Exception e) {
                UpdateStatus($"Error: {e.Message}");
                Debug.LogException(e);
            }
            finally {
                _loadButton.SetEnabled(true);
            }
        }

        static string GetInitialPdfPath() =>
            SessionState.GetString("March31st_PdfPath", GetDefaultPdfPath());

        static string GetDefaultPdfPath() =>
            Path.Combine(GetDownloadsFolderPath(), "Assets_being_removed_March_31st.pdf");

        static string GetDownloadsFolderPath() {
            var userProfileFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var downloadsFolder = Path.Combine(userProfileFolder, "Downloads");

            if (Directory.Exists(downloadsFolder))
                return downloadsFolder;

            return userProfileFolder;
        }

        async Awaitable CompareAssets(bool approximateEnabled,
                                      int approximateThreshold,
                                      CancellationToken cancellationToken) {
            await Awaitable.BackgroundThreadAsync();

            _matchedAssets.Clear();
            foreach (var pdfAsset in _pdfAssets) {
                cancellationToken.ThrowIfCancellationRequested();

                AssetInfo match = null;
                if (string.IsNullOrEmpty(pdfAsset._packageId)) {
                    foreach (var myAsset in _myAssets) {
                        cancellationToken.ThrowIfCancellationRequested();
                        var result = myAsset._sanitizedName.Equals(pdfAsset._sanitizedName);
                        if (!result && approximateEnabled) {
                            result = LevenshteinDistance.GetSimilarity(
                                myAsset._sanitizedName, pdfAsset._sanitizedName) > approximateThreshold;
                        }

                        if (result) {
                            match = myAsset;
                            break;
                        }
                    }
                }
                else {
                    match = _myAssets.FirstOrDefault(a =>
                        a._packageId.Equals(pdfAsset._packageId, StringComparison.OrdinalIgnoreCase));
                }

                if (match == null)
                    continue;

                match._sanitizedPublisher = pdfAsset._sanitizedPublisher;
                _matchedAssets.Add(match);
            }

            await Awaitable.MainThreadAsync();
        }

        static class LevenshteinDistance {
            static int Compute(string s, string t) {
                var n = s.Length;
                var m = t.Length;
                var d = new int[n + 1, m + 1];

                if (n == 0) return m;
                if (m == 0) return n;

                for (var i = 0; i <= n; d[i, 0] = i++) { }
                for (var j = 0; j <= m; d[0, j] = j++) { }

                for (var i = 1; i <= n; i++) {
                    for (var j = 1; j <= m; j++) {
                        var cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                        d[i, j] = Math.Min(
                            Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                            d[i - 1, j - 1] + cost);
                    }
                }

                return d[n, m];
            }

            public static double GetSimilarity(string s1, string s2) {
                if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
                    return 0.0;

                var distance = Compute(s1, s2);
                var maxLength = Math.Max(s1.Length, s2.Length);
                return ((double)(maxLength - distance) / maxLength) * 100;
            }
        }

        static void SaveToCsv(string filePath, List<AssetInfo> assets) {
            try {
                using var writer = new StreamWriter(filePath);
                writer.WriteLine("Name,Publisher,Asset ID,Clock Icon,Asset Link");

                foreach (var asset in assets) {
                    var clockIcon = "";
                    if (!string.IsNullOrEmpty(asset._purchaseDate) && 
                        DateTime.TryParse(asset._purchaseDate, out var date) && 
                        date >= DateTime.Now.AddMonths(-6)) {
                        clockIcon = "\u231A";
                    }

                    var name = EscapeCsv(asset._name);
                    var publisher = EscapeCsv(asset._sanitizedPublisher);
                    var packageId = EscapeCsv(asset._packageId);
                    var link = EscapeCsv(asset._link);

                    writer.WriteLine($"{name},{publisher},{packageId},{clockIcon},{link}");
                }
            }
            catch (Exception e) {
                Debug.LogError($"Failed to save CSV to {filePath}: {e.Message}");
            }
        }

        static string EscapeCsv(string value) {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r")) {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }
    }
}