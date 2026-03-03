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
        Label _statusLabel;
        Button _loadButton;
        TextField _pdfPathField;
        MultiColumnListView _listView;

        [MenuItem("Tools/March 31st...")]
        public static void ShowWindow() => GetWindow<March31stWindow>("March 31st");

        public void CreateGUI() {
            _root = rootVisualElement;

            var mainTitle = new Label("March 31st Asset Checker");
            mainTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            mainTitle.style.fontSize = 14;
            mainTitle.style.marginBottom = 10;
            mainTitle.style.marginTop = 5;
            mainTitle.style.marginLeft = 5;
            _root.Add(mainTitle);

            _pdfPathField = new TextField("Pdf Path");
            _pdfPathField.style.marginLeft = 5;
            _pdfPathField.style.marginRight = 5;
            _pdfPathField.style.marginBottom = 5;
            _pdfPathField.value = GetDefaultPdfPath();
            _root.Add(_pdfPathField);

            _loadButton = new Button(RunCheck) { text = "Load PDF and Fetch My Assets", style = { height = 30 } };
            _root.Add(_loadButton);

            _statusLabel = new Label($"Status: {_status}") { style = { marginLeft = 5, marginTop = 5, marginBottom = 5 } };
            _root.Add(_statusLabel);

            _listView = new MultiColumnListView();
            _listView.itemsSource = _matchedAssets;
            _listView.showAlternatingRowBackgrounds = AlternatingRowBackground.All;
            _listView.reorderable = false;
            _listView.style.flexGrow = 1;
            _listView.style.minWidth = 600;

            _listView.RegisterCallback<GeometryChangedEvent>(_ => {
                var scrollView = _listView.Q<ScrollView>();
                if (scrollView == null)
                    return;

                scrollView.mode = ScrollViewMode.VerticalAndHorizontal;
            });

            var columns = _listView.columns;

            columns.Add(new Column {
                title = "Asset Name",
                makeCell = () => new Label(),
                bindCell = (element, index) => ((Label)element).text = _matchedAssets[index]._name,
                width = 250,
                stretchable = true
            });

            columns.Add(new Column {
                title = "Publisher",
                makeCell = () => new Label(),
                bindCell = (element, index) => ((Label)element).text = _matchedAssets[index]._publisher,
                width = 200,
                stretchable = true
            });

            columns.Add(new Column {
                title = "",
                makeCell = () => new Label { style = { unityTextAlign = TextAnchor.MiddleCenter } },
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
                stretchable = false
            });

            columns.Add(new Column {
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
                stretchable = false
            });

            _root.Add(_listView);
        }

        void OnEnable() {
            _cancellationTokenSource = new CancellationTokenSource();
        }

        void OnDisable() {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }

        void UpdateStatus(string status) {
            _status = status;
            if (_statusLabel == null)
                return;

            _statusLabel.text = $"Status: {status}";
        }

        async void RunCheck() {
            try {
                _loadButton.SetEnabled(false);
                UpdateStatus("Reading PDF...");

                _pdfAssets = await PdfParser.LoadPdfContentsAsync(_pdfPathField.value, _cancellationTokenSource.Token);
                UpdateStatus($"PDF read. Found {_pdfAssets.Count} entries. Fetching my assets...");

                _myAssets = await PurchasesFetcher.FetchMyAssets(_cancellationTokenSource.Token, UpdateStatus);
                UpdateStatus($"My assets fetched ({_myAssets.Count}). Comparing...");

                CompareAssets();
                UpdateStatus($"Done. Found {_matchedAssets.Count} matches.");

                _listView.Rebuild();
            }
            catch (OperationCanceledException) {
                await Awaitable.MainThreadAsync();
            }
            catch (Exception e) {
                UpdateStatus($"Error: {e.Message}");
                Debug.LogException(e);
            }
            finally {
                _loadButton.SetEnabled(true);
            }
        }

        static string GetDefaultPdfPath() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Assets_being_removed_March_31st.pdf");

        void CompareAssets() {
            _matchedAssets.Clear();
            foreach (var pdfAsset in _pdfAssets) {
                var match = _myAssets.FirstOrDefault(a => a._name.Equals(pdfAsset._name, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                    continue;

                // Getting publisher from PDF because Asset Store only has it on the product page
                match._publisher = pdfAsset._publisher;
                _matchedAssets.Add(match);
            }
        }
    }
}