using WavyFi.Localization;
using static WavyFi.Localization.Locale;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using WavyFi.Analysis;
using WavyFi.Models;
using WavyFi.Settings;
using WavyFi.Ui;
using WavyFi.WifiDirect;
using WavyFi.Wlan;

namespace WavyFi;

public partial class MainWindow : Window
{
    private readonly WifiScanner _scanner;
    private readonly WifiDirectWatcher _wifiDirect;
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _peerTimer;
    private readonly NetworkStore _store;
    private readonly PeerStore _peerStore;
    private readonly ListCollectionView _view;
    private readonly ListCollectionView _peersView;
    private readonly Demo.DemoData? _demo;
    private bool _refreshing;
    private bool _closing;
    private int _lastScanCount;
    private DateTime? _lastScanTime;

    public MainWindow(bool demo = false, NetworkStore? store = null, PeerStore? peerStore = null, Demo.DemoData? demoData = null)
    {
        _store = store ?? new NetworkStore();
        _peerStore = peerStore ?? new PeerStore();
        _demo = demo ? demoData ?? new Demo.DemoData() : null;
        Application.Current.Resources["ScanOnText"] = T("Scan ON", "扫描开启");
        Application.Current.Resources["ScanOffText"] = T("Scan OFF", "扫描关闭");
        InitializeComponent();
        LanguageSelector.SelectedIndex = Locale.IsChinese ? 0 : 1;
        UserSettings.Restore(this);
        UserSettings.RestoreColumnLayout("net", NetworksGrid);
        UserSettings.RestoreColumnLayout("p2p", PeersGrid);
        Closing += (_, _) =>
        {
            _closing = true;
            UserSettings.Save(this);
            UserSettings.SaveColumnLayout("net", NetworksGrid);
            UserSettings.SaveColumnLayout("p2p", PeersGrid);
        };

        _view = new ListCollectionView(_store.Entries);
        _view.Filter = FilterPredicate;
        _view.SortDescriptions.Add(new SortDescription(nameof(NetworkEntry.IsConnected), ListSortDirection.Descending));
        _view.SortDescriptions.Add(new SortDescription(nameof(NetworkEntry.Rssi), ListSortDirection.Descending));
        _view.IsLiveSorting = true;
        _view.LiveSortingProperties.Add(nameof(NetworkEntry.IsConnected));
        _view.LiveSortingProperties.Add(nameof(NetworkEntry.Rssi));
        NetworksGrid.ItemsSource = _view;

        _peersView = new ListCollectionView(_peerStore.Entries);
        _peersView.Filter = PeerFilterPredicate;
        _peersView.SortDescriptions.Add(new SortDescription(nameof(PeerEntry.SignalDbm), ListSortDirection.Descending));
        PeersGrid.ItemsSource = _peersView;

        GridChrome.Setup(NetworksGrid, compact: false);
        GridChrome.Setup(PeersGrid, compact: true);

        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        TitleRun.Text = $"WavyFi v{version?.Major ?? 1}.{version?.Minor ?? 0} · © 2026 zhgzhg @@ GitHub.com";

        _fontScale = UserSettings.LoadFontScale();
        ApplyFontScale();

        _scanner = new WifiScanner();
        // Read results the moment a sweep finishes instead of waiting for the
        // next timer tick — first data lands ~2-4 s after launch. Read-only:
        // triggering another scan here would loop scan -> complete -> scan.
        _scanner.ScanCompleted += _ => Dispatcher.BeginInvoke(async () =>
        {
            if (!_closing && ScanToggle.IsChecked == true)
                await RefreshAsync(triggerScan: false);
        });
        AdapterList.ItemsSource = _adapterChoices;
        ReloadAdapterList();
        AdapterDropdown.IsEnabled = ScanToggle.IsChecked != true;
        _wifiDirect = new WifiDirectWatcher();
        _wifiDirect.PeersChanged += () => Dispatcher.BeginInvoke(() => { if (!_closing) UpdatePeersList(); });

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += async (_, _) => await RefreshAsync();

        // Peer ages keep counting regardless of the scan toggle — WiFi Direct
        // discovery runs independently of the WLAN scan loop.
        _peerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _peerTimer.Tick += (_, _) =>
        {
            if (_closing) return;
            _peerStore.Tick(DateTime.Now);
            _peersView.Refresh();
        };
        _peerTimer.Start();

        Loaded += async (_, _) =>
        {
            // The splitter must not shrink the left quadrants enough to wrap
            // the title/search/band row — measure its real one-line width.
            LeftColumn.MinWidth = Math.Ceiling(
                FilterRow1.Children.OfType<FrameworkElement>().Sum(c => c.DesiredSize.Width)) + 2;

            if (_demo is null) _wifiDirect.Start();
            if (ScanToggle.IsChecked == true)
            {
                await RefreshAsync();
                if (!_closing) _timer.Start();
            }
        };
        Closed += (_, _) =>
        {
            _timer.Stop();
            _peerTimer.Stop();
            _wifiDirect.Dispose();
            _scanner.Dispose();
        };
    }

    private async Task RefreshAsync(bool triggerScan = true)
    {
        if (_refreshing || _closing) return;
        _refreshing = true;
        try
        {
            IReadOnlyList<WifiNetwork> networks = _demo is not null
                ? _demo.NextScan()
                : await Task.Run(() =>
                {
                    if (triggerScan) _scanner.TriggerScan();
                    return _scanner.GetNetworks();
                });

            if (_closing) return;
            _store.ApplyScan(networks, DateTime.Now);
            if (_demo is not null)
            {
                _peerStore.Apply(_demo.Peers(DateTime.Now), DateTime.Now);
                _peersView.Refresh();
            }
            _view.Refresh();
            UpdateGraphs();
            // Advise from the persistent store, not the raw scan — single scans
            // can come back near-empty right after the BSS cache is flushed.
            // Skip the update while the user is selecting/copying from the box.
            if (!AdviceText.IsKeyboardFocusWithin)
                AdviceText.Text = ChannelAdvisor.BuildReport(_store.Entries.ToList());
            if (_demo is not null)
            {
                StatusText.Text = T("Scanning every 5 s.", "每 5 秒扫描一次。");
            }
            else
            {
                var off = _scanner.PoweredOffAdapters;
                StatusText.Text = off.Count == 0 ? T("Scanning every 5 s.", "每 5 秒扫描一次。")
                    : off.Count == _scanner.SelectedAdapters.Count
                        ? T("WiFi is turned off — turn it back on in quick settings or Settings > Network & internet to resume scanning.", "Wi-Fi 已关闭，请在快捷设置或“设置 > 网络和 Internet”中开启，以恢复扫描。")
                        : F("Radio off on {0} — scanning continues on the other adapters.", "{0} 的无线功能已关闭；其他网卡仍在扫描。", string.Join(", ", off));
            }
            _lastScanCount = networks.Count;
            _lastScanTime = DateTime.Now;
            UpdateStatsText();

            if (_demo is null)
            {
                // The scanner may have dropped a vanished adapter (unplug).
                var live = _scanner.SelectedAdapters.Select(a => a.Guid).ToHashSet();
                var shown = _adapterChoices.Where(c => c.IsSelected).Select(c => c.Adapter.Guid).ToHashSet();
                if (!live.SetEquals(shown))
                    ReloadAdapterList();
            }
        }
        catch (Exception ex)
        {
            if (_closing) return;
            StatusText.Text = ex.Message;
            ReloadAdapterList(); // reflect adapters that vanished or reappeared
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void UpdateStatsText()
    {
        StatsText.Text = _lastScanTime is DateTime time
            ? F("{0} in range  ·  {1} tracked  ·  updated {2:HH:mm:ss}", "范围内 {0} 个  ·  已记录 {1} 个  ·  更新于 {2:HH:mm:ss}", _lastScanCount, _store.Entries.Count, time)
            : "";
    }

    private void UpdateGraphs()
    {
        var visible = _view.Cast<NetworkEntry>().ToList();
        Graph24.SetEntries(visible);
        Graph5.SetEntries(visible);
        Graph6.SetEntries(visible);
        UpdateSignalGraph();
    }

    private void UpdateSignalGraph()
    {
        var selected = NetworksGrid.SelectedItems.Cast<NetworkEntry>().ToList();
        SignalGraphView.SetEntries(selected);
        SignalGraphView.SetPeers(PeersGrid.SelectedItems.Cast<PeerEntry>());

        var keys = selected.Select(n => n.Key).ToList();
        Graph24.SetSelection(keys);
        Graph5.SetSelection(keys);
        Graph6.SetSelection(keys);
    }

    private void PeersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSignalGraph();
    }

    private void NetworksGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Jump to the channel graph of the band the user just selected.
        if (e.AddedItems.Count > 0 &&
            e.AddedItems[e.AddedItems.Count - 1] is NetworkEntry added)
        {
            GraphTabs.SelectedIndex = added.Band switch
            {
                "2.4 GHz" => 0,
                "5 GHz" => 1,
                "6 GHz" => 2,
                _ => GraphTabs.SelectedIndex,
            };
        }

        UpdateSignalGraph();
    }

    /// <summary>Plain click toggles: clicking an already-selected row unselects
    /// it, clicking empty grid space clears the selection. Shared by both grids.</summary>
    private void Grid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid || Keyboard.Modifiers != ModifierKeys.None) return;

        var source = e.OriginalSource as DependencyObject;
        var row = FindParent<DataGridRow>(source);
        if (row is null)
        {
            if (FindParent<DataGridColumnHeader>(source) is null &&
                FindParent<ScrollBar>(source) is null)
                grid.UnselectAll();
            return;
        }

        if (row.IsSelected)
        {
            grid.SelectedItems.Remove(row.Item);
            e.Handled = true;
        }
    }

    private string _rightClickedCellText = "";
    private NetworkEntry? _rightClickedRow;

    private void NetworksGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var cell = FindParent<DataGridCell>(e.OriginalSource as DependencyObject);
        _rightClickedCellText = (cell?.Content as TextBlock)?.Text ?? "";
        _rightClickedRow = cell?.DataContext as NetworkEntry;
        CopyCellItem.IsEnabled = _rightClickedCellText.Length > 0;
    }

    private void CopyCell_Click(object sender, RoutedEventArgs e)
    {
        if (_rightClickedCellText.Length > 0)
            TrySetClipboard(_rightClickedCellText);
    }

    private void CopyRows_Click(object sender, RoutedEventArgs e)
    {
        var rows = NetworksGrid.SelectedItems.Cast<NetworkEntry>().ToList();
        // Right-clicking a row outside the selection copies that row, matching
        // "Copy cell" semantics; otherwise the selection wins.
        if (_rightClickedRow is not null && !rows.Contains(_rightClickedRow))
            rows = new List<NetworkEntry> { _rightClickedRow };
        if (rows.Count == 0) return;

        TrySetClipboard(DataGridCsv.TabSeparatedRows(NetworksGrid, rows));
    }

    private void CopyTable_Click(object sender, RoutedEventArgs e)
    {
        TrySetClipboard(DataGridCsv.Build(NetworksGrid, _view));
    }

    private void ExportTable_Click(object sender, RoutedEventArgs e)
    {
        ExportCsv("wavyfi-networks.csv", DataGridCsv.Build(NetworksGrid, _view), _view.Count);
    }

    private void ExportPeers_Click(object sender, RoutedEventArgs e)
    {
        ExportCsv("wavyfi-peers.csv", DataGridCsv.Build(PeersGrid, _peersView), _peersView.Count);
    }

    private void SignalGraph_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        ExportHistoryItem.IsEnabled =
            NetworksGrid.SelectedItems.Count > 0 || PeersGrid.SelectedItems.Count > 0;
    }

    private void ExportHistory_Click(object sender, RoutedEventArgs e)
    {
        var networks = NetworksGrid.SelectedItems.Cast<NetworkEntry>().ToList();
        var peers = PeersGrid.SelectedItems.Cast<PeerEntry>().ToList();
        if (networks.Count == 0 && peers.Count == 0) return;

        var sb = new StringBuilder();
        sb.AppendLine(T("Timestamp,Name,BSSID/MAC,RSSI (dBm),Kind", "时间,名称,BSSID/MAC,信号（dBm）,类型"));
        int rows = 0;
        foreach (var entry in networks)
        {
            foreach (var (time, rssi) in entry.History)
            {
                sb.AppendLine(string.Join(",",
                    time.ToString("yyyy-MM-dd HH:mm:ss"),
                    CsvFormat.Escape(entry.DisplayName), CsvFormat.Escape(entry.Bssid), rssi, T("network", "无线网络")));
                rows++;
            }
        }
        foreach (var peer in peers)
        {
            foreach (var (time, rssi) in peer.History)
            {
                sb.AppendLine(string.Join(",",
                    time.ToString("yyyy-MM-dd HH:mm:ss"),
                    CsvFormat.Escape(peer.NameText), CsvFormat.Escape(peer.Address), rssi, T("wifi-direct", "Wi-Fi 直连")));
                rows++;
            }
        }
        ExportCsv("wavyfi-signal-history.csv", sb.ToString(), rows);
    }

    private void ExportCsv(string suggestedName, string content, int rowCount)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = T("CSV files (*.csv)|*.csv|All files (*.*)|*.*", "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*"),
            FileName = suggestedName,
            DefaultExt = ".csv",
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            // UTF-8 with BOM so Excel detects the encoding.
            File.WriteAllText(dialog.FileName, content, new UTF8Encoding(true));
            StatusText.Text = F("Exported {0} rows to {1}", "已导出 {0} 行到 {1}", rowCount, dialog.FileName);
        }
        catch (Exception ex)
        {
            StatusText.Text = F("Export failed: {0}", "导出失败：{0}", ex.Message);
        }
    }

    private void TrySetClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch
        {
            StatusText.Text = T("Clipboard is busy — try copying again.", "剪贴板正忙，请重试复制。");
        }
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null and not T)
            child = System.Windows.Media.VisualTreeHelper.GetParent(child);
        return child as T;
    }

    private bool FilterPredicate(object item)
    {
        if (item is not NetworkEntry e) return false;

        var query = SearchBox.Text.Trim();
        if (query.Length > 0 &&
            !e.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) &&
            !e.Bssid.Contains(query, StringComparison.OrdinalIgnoreCase) &&
            !e.Vendor.Contains(query, StringComparison.OrdinalIgnoreCase))
            return false;

        if (BandFilter.SelectedIndex > 0 &&
            BandFilter.SelectedItem is ComboBoxItem band &&
            e.Band != (string)band.Content)
            return false;

        if (e.Rssi < MinRssiSlider.Value)
            return false;

        if (_maxAgeFilterSeconds >= 0 && e.LastSeenSeconds > _maxAgeFilterSeconds)
            return false;

        return true;
    }

    private const double MinAgeSeconds = 5;
    private const double MaxAgeSeconds = 168 * 3600; // 168 h inclusive
    private int _maxAgeFilterSeconds = -1;            // -1 = any

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (_view is null || _peersView is null) return; // fires during InitializeComponent
        MinRssiLabel.Text = MinRssiSlider.Value <= MinRssiSlider.Minimum
            ? T("any", "不限")
            : ((int)MinRssiSlider.Value).ToString();

        if (LastSeenSlider.Value >= LastSeenSlider.Maximum)
        {
            _maxAgeFilterSeconds = -1;
            LastSeenLabel.Text = T("any", "不限");
        }
        else
        {
            // Logarithmic scale: a linear 0-168 h slider would make seconds
            // and minutes unselectable.
            double t = LastSeenSlider.Value / LastSeenSlider.Maximum;
            _maxAgeFilterSeconds = (int)Math.Round(
                MinAgeSeconds * Math.Exp(t * Math.Log(MaxAgeSeconds / MinAgeSeconds)));
            LastSeenLabel.Text = AgeFormat.Short(_maxAgeFilterSeconds);
        }

        _view.Refresh();
        _peersView.Refresh();
        UpdateGraphs();
    }

    private void PeersGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var row = FindParent<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row is not null && !row.IsSelected)
        {
            PeersGrid.SelectedItems.Clear();
            row.IsSelected = true;
        }
    }

    private void CopyPeer_Click(object sender, RoutedEventArgs e)
    {
        if (PeersGrid.SelectedItem is not PeerEntry p) return;
        var name = p.IsPaired ? F("{0} (paired)", "{0}（已配对）", p.NameText) : p.NameText;
        var signal = p.SignalDbm is int s ? $"{s} dBm" : "";
        var details = new[] { p.DeviceTypeText, p.VendorText, p.Address, signal, F("seen {0}s ago", "{0} 秒前发现", p.LastSeenSeconds) }
            .Where(d => d.Length > 0);
        TrySetClipboard($"{name}{Environment.NewLine}{string.Join("  ·  ", details)}");
    }

    private void CopyPeerList_Click(object sender, RoutedEventArgs e)
    {
        var peers = _peersView.Cast<PeerEntry>().ToList(); // filtered, sorted view
        if (peers.Count == 0) return;
        TrySetClipboard(string.Join(Environment.NewLine, peers.Select(p =>
            string.Join("\t", p.NameText + (p.IsPaired ? T(" (paired)", "（已配对）") : ""),
                p.SignalText, p.DeviceTypeText, p.VendorText, p.Address, $"{p.LastSeenSeconds}s"))));
    }

    private void UpdatePeersList()
    {
        if (_closing) return;
        _peerStore.Apply(_wifiDirect.GetPeers(), DateTime.Now);
        _peersView.Refresh();
        UpdateSignalGraph(); // new peer samples should appear as dots promptly
    }

    private bool PeerFilterPredicate(object item)
    {
        if (item is not PeerEntry p) return false;

        var query = SearchBox.Text.Trim();
        if (query.Length > 0 &&
            !p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) &&
            !p.Address.Contains(query, StringComparison.OrdinalIgnoreCase) &&
            !p.Vendor.Contains(query, StringComparison.OrdinalIgnoreCase) &&
            !p.DeviceType.Contains(query, StringComparison.OrdinalIgnoreCase) &&
            !p.DeviceTypeText.Contains(query, StringComparison.OrdinalIgnoreCase))
            return false;

        // With the slider raised, peers with no signal reading can't qualify.
        if (MinRssiSlider.Value > MinRssiSlider.Minimum &&
            (p.SignalDbm is not int signal || signal < MinRssiSlider.Value))
            return false;

        if (_maxAgeFilterSeconds >= 0 && p.LastSeenSeconds > _maxAgeFilterSeconds)
            return false;

        return true;
    }

    private sealed class AdapterChoice : INotifyPropertyChanged
    {
        private bool _isSelected;

        public AdapterChoice(WlanAdapter adapter) => Adapter = adapter;

        public WlanAdapter Adapter { get; }
        public string Display => $"[{Adapter.Index}] {Adapter.Description}";

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private readonly System.Collections.ObjectModel.ObservableCollection<AdapterChoice> _adapterChoices = new();
    private bool _reloadingAdapters;

    private void ReloadAdapterList()
    {
        if (_demo is not null)
        {
            // No real hardware in the screenshot: a fixed made-up adapter.
            AdapterSummary.Text = Demo.DemoData.AdapterDisplay;
            return;
        }

        _reloadingAdapters = true;
        try
        {
            var adapters = _scanner.EnumerateAdapters();
            var selectedGuids = _scanner.SelectedAdapters.Select(a => a.Guid).ToHashSet();
            if (adapters.Count > 0 && !adapters.Any(a => selectedGuids.Contains(a.Guid)))
                selectedGuids = new HashSet<Guid> { adapters[0].Guid }; // default: first found

            _adapterChoices.Clear();
            foreach (var adapter in adapters)
                _adapterChoices.Add(new AdapterChoice(adapter)
                {
                    IsSelected = selectedGuids.Contains(adapter.Guid),
                });
        }
        finally
        {
            _reloadingAdapters = false;
        }
        ApplyAdapterSelection();
    }

    /// <summary>Pushes the checked adapters into the scanner and updates the
    /// summary text and the graphs' [index] labeling mode.</summary>
    private void ApplyAdapterSelection()
    {
        var chosen = _adapterChoices.Where(c => c.IsSelected).Select(c => c.Adapter).ToList();
        _scanner.SelectAdapters(chosen);

        bool multi = chosen.Count > 1;
        Graph24.ShowAdapterIndex = multi;
        Graph5.ShowAdapterIndex = multi;
        Graph6.ShowAdapterIndex = multi;
        SignalGraphView.ShowAdapterIndex = multi;

        AdapterSummary.Text = chosen.Count switch
        {
            0 => T("(no adapter)", "（无网卡）"),
            1 => $"[{chosen[0].Index}] {chosen[0].Description}",
            _ => F("{0} adapters: {1}", "{0} 个网卡：{1}", chosen.Count, string.Join(" ", chosen.Select(a => $"[{a.Index}]"))),
        };
    }

    private void AdapterDropdown_Opened(object sender, RoutedEventArgs e)
    {
        ReloadAdapterList(); // pick up USB adapters plugged in since startup
    }

    private void AdapterCheck_Changed(object sender, RoutedEventArgs e)
    {
        // Only reachable while scanning is off — the dropdown is locked otherwise.
        if (_reloadingAdapters) return;

        if (!_adapterChoices.Any(c => c.IsSelected))
        {
            if (sender is CheckBox { DataContext: AdapterChoice unchecked_ })
            {
                _reloadingAdapters = true;
                unchecked_.IsSelected = true;
                _reloadingAdapters = false;
            }
            StatusText.Text = T("At least one adapter must remain selected.", "请至少保留一个选中的网卡。");
            return;
        }

        ApplyAdapterSelection();
        ClearAccumulatedData();
        StatusText.Text = T("Adapter selection changed — turn scanning on to start.", "已更改网卡选择，请开启扫描。");
    }

    private async void ScanToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return; // initial IsChecked assignment during parse

        if (ScanToggle.IsChecked == true)
        {
            AdapterDropdown.IsEnabled = false;
            ClearAccumulatedData();
            await RefreshAsync();
            _timer.Start();
        }
        else
        {
            _timer.Stop();
            AdapterDropdown.IsEnabled = true;
            StatusText.Text = T("Scanning paused — table and graphs frozen; adapter can be switched.", "扫描已暂停，表格与图表保持不变；现在可切换网卡。");
        }
    }

    private double _fontScale = 1.0;

    /// <summary>Scales the text of the grids, graphs and recommendations.
    /// Grid rows grow with their font automatically; the graphs scale their
    /// drawn labels and axis margins via FontScale.</summary>
    private void ApplyFontScale()
    {
        NetworksGrid.FontSize = 12 * _fontScale;
        PeersGrid.FontSize = 11 * _fontScale;
        AdviceText.FontSize = 12 * _fontScale;
        Graph24.FontScale = _fontScale;
        Graph5.FontScale = _fontScale;
        Graph6.FontScale = _fontScale;
        SignalGraphView.FontScale = _fontScale;
    }

    private void SetFontScale(double scale)
    {
        _fontScale = Math.Clamp(Math.Round(scale, 1), 0.8, 1.6);
        ApplyFontScale();
        UserSettings.SaveFontScale(_fontScale);
        StatusText.Text = F("Font size: {0}%", "字体大小：{0}%", (int)(_fontScale * 100));
    }

    private void FontDecrease_Click(object sender, RoutedEventArgs e) => SetFontScale(_fontScale - 0.1);
    private void FontIncrease_Click(object sender, RoutedEventArgs e) => SetFontScale(_fontScale + 0.1);
    private void FontReset_Click(object sender, RoutedEventArgs e) => SetFontScale(1.0);

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        switch (e.Key)
        {
            case Key.OemPlus or Key.Add:
                SetFontScale(_fontScale + 0.1);
                e.Handled = true;
                break;
            case Key.OemMinus or Key.Subtract:
                SetFontScale(_fontScale - 0.1);
                e.Handled = true;
                break;
            case Key.D0 or Key.NumPad0:
                SetFontScale(1.0);
                e.Handled = true;
                break;
        }
    }

    private void RepoLink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void LanguageSelector_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || LanguageSelector.SelectedItem is not ComboBoxItem { Tag: string language }) return;
        if (Locale.Culture.Name == language) return;
        bool paused = ScanToggle.IsChecked != true;
        UserSettings.Save(this);
        UserSettings.SaveColumnLayout("net", NetworksGrid);
        UserSettings.SaveColumnLayout("p2p", PeersGrid);
        UserSettings.SaveLanguage(language);
        Locale.Culture = System.Globalization.CultureInfo.GetCultureInfo(language);
        var selectedNetworks = NetworksGrid.SelectedItems.Cast<NetworkEntry>().ToList();
        var selectedPeers = PeersGrid.SelectedItems.Cast<PeerEntry>().ToList();
        var replacement = new MainWindow(_demo is not null, _store, _peerStore, _demo);
        replacement._lastScanCount = _lastScanCount;
        replacement._lastScanTime = _lastScanTime;
        replacement.UpdateStatsText();
        replacement.SearchBox.Text = SearchBox.Text;
        replacement.BandFilter.SelectedIndex = BandFilter.SelectedIndex;
        replacement.MinRssiSlider.Value = MinRssiSlider.Value;
        replacement.LastSeenSlider.Value = LastSeenSlider.Value;
        foreach (var network in selectedNetworks) replacement.NetworksGrid.SelectedItems.Add(network);
        foreach (var peer in selectedPeers) replacement.PeersGrid.SelectedItems.Add(peer);
        replacement.GraphTabs.SelectedIndex = GraphTabs.SelectedIndex;
        replacement.AdviceText.Text = ChannelAdvisor.BuildReport(_store.Entries.ToList());
        replacement.UpdateGraphs();
        var selected = _adapterChoices.Where(c => c.IsSelected).Select(c => c.Adapter.Guid).ToHashSet();
        replacement._reloadingAdapters = true;
        foreach (var choice in replacement._adapterChoices)
            choice.IsSelected = selected.Contains(choice.Adapter.Guid);
        replacement._reloadingAdapters = false;
        if (replacement._demo is null) replacement.ApplyAdapterSelection();
        replacement.ScanToggle.IsChecked = !paused;
        replacement.AdapterDropdown.IsEnabled = paused;
        if (paused) replacement.StatusText.Text = T("Scanning paused — table and graphs frozen; adapter can be switched.", "扫描已暂停，表格与图表保持不变；现在可切换网卡。");
        Application.Current.MainWindow = replacement;
        replacement.Show();
        Close();
    }

    private void ClearAccumulatedData()
    {
        _lastScanCount = 0;
        _lastScanTime = null;
        UpdateStatsText();
        _store.Entries.Clear();
        _view.Refresh();
        UpdateGraphs();
        AdviceText.Text = T("Waiting for first scan...", "等待首次扫描…");
    }
}
