using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Feyra;

public partial class MainWindow : Window
{
    private readonly string _dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Feyra");
    private readonly ObservableCollection<BrowserTab> _tabs = new();
    private readonly List<HistoryEntry> _history = new();
    private readonly List<BookmarkEntry> _bookmarks = new();
    private readonly List<DownloadEntry> _downloads = new();
    private readonly List<string> _closedTabs = new();
    private BrowserSettings _settings = new();
    private WebView2? _splitWeb;
    private TabItem? _splitTab;
    private bool _splitEnabled;
    private bool _restoring;
    private bool _closing;
    private bool _verticalTabs;

    public MainWindow()
    {
        InitializeComponent();
        Directory.CreateDirectory(_dataDirectory);
        LoadData();
        ApplySettings();
        Loaded += async (_, _) =>
        {
            await RestoreSessionAsync();
            _ = UpdateService.CheckAndApplyAsync();
        };
        PreviewKeyDown += MainWindow_PreviewKeyDown;
    }

    private string DataPath(string name) => Path.Combine(_dataDirectory, name);

    private void LoadData()
    {
        try
        {
            if (File.Exists(DataPath("settings.json")))
                _settings = JsonSerializer.Deserialize<BrowserSettings>(File.ReadAllText(DataPath("settings.json"))) ?? new();
            if (File.Exists(DataPath("bookmarks.json")))
                _bookmarks.AddRange(JsonSerializer.Deserialize<List<BookmarkEntry>>(File.ReadAllText(DataPath("bookmarks.json"))) ?? []);
            if (File.Exists(DataPath("history.json")))
                _history.AddRange(JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(DataPath("history.json"))) ?? []);
            if (File.Exists(DataPath("downloads.json")))
                _downloads.AddRange(JsonSerializer.Deserialize<List<DownloadEntry>>(File.ReadAllText(DataPath("downloads.json"))) ?? []);
        }
        catch { StatusText.Text = "Saved data could not be fully loaded. A fresh profile is available."; }
    }

    private void SaveData()
    {
        try
        {
            File.WriteAllText(DataPath("settings.json"), JsonSerializer.Serialize(_settings));
            File.WriteAllText(DataPath("bookmarks.json"), JsonSerializer.Serialize(_bookmarks));
            File.WriteAllText(DataPath("history.json"), JsonSerializer.Serialize(_history.TakeLast(5000)));
            File.WriteAllText(DataPath("downloads.json"), JsonSerializer.Serialize(_downloads.TakeLast(1000)));
        }
        catch { StatusText.Text = "Could not save browser data."; }
    }

    private void ApplySettings()
    {
        Tabs.TabStripPlacement = _settings.VerticalTabs ? Dock.Left : Dock.Top;
        _verticalTabs = _settings.VerticalTabs;
        Tabs.Width = _verticalTabs ? 210 : double.NaN;
        Tabs.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        if (_settings.StartupRestore && _settings.LastSession.Count > 0) return;
    }

    private async Task RestoreSessionAsync()
    {
        if (_restoring) return;
        _restoring = true;
        var pages = _settings.StartupRestore ? _settings.LastSession.ToList() : [];
        if (pages.Count == 0) await CreateTabAsync();
        else
        {
            foreach (var page in pages.Take(20)) await CreateTabAsync(page);
        }
        _restoring = false;
    }

    private async Task CreateTabAsync(string? address = null, bool isPrivate = false, bool select = true)
    {
        var web = new WebView2();
        var profile = isPrivate
            ? Path.Combine(_dataDirectory, "PrivateProfiles", Guid.NewGuid().ToString("N"))
            : Path.Combine(_dataDirectory, "DefaultProfile");
        var state = new BrowserTab(web, address ?? "", isPrivate, false, profile);
        var tab = new TabItem { Header = MakeTabHeader("New tab"), Content = web, Tag = state };
        Tabs.Items.Add(tab);
        _tabs.Add(state);
        AttachTabContextMenu(tab);
        if (select) Tabs.SelectedItem = tab;

        try
        {
            Directory.CreateDirectory(profile);
            var environment = await CoreWebView2Environment.CreateAsync(null, profile);
            await web.EnsureCoreWebView2Async(environment);
            var core = web.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = _settings.ContextMenus;
            core.Settings.AreDevToolsEnabled = _settings.DeveloperTools;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = true;
            core.Settings.IsZoomControlEnabled = true;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            web.ZoomFactor = _settings.DefaultZoom;

            core.NavigationStarting += (_, e) => Dispatcher.Invoke(() =>
            {
                if (Tabs.SelectedItem == tab) { AddressBox.Text = e.Uri; StatusText.Text = "Loading…"; }
            });
            core.NavigationCompleted += (_, e) => Dispatcher.Invoke(() =>
            {
                if (e.IsSuccess && Uri.TryCreate(web.Source?.ToString(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
                {
                    ((BrowserTab)tab.Tag).LastUrl = uri.ToString();
                    if (!isPrivate && _settings.SaveHistory)
                    {
                        _history.Add(new HistoryEntry(uri.ToString(), DateTimeOffset.Now));
                        SaveData();
                    }
                    UpdateTabTitle(tab, string.IsNullOrWhiteSpace(core.DocumentTitle) ? GetHost(uri) : core.DocumentTitle);
                }
                if (Tabs.SelectedItem == tab)
                {
                    StatusText.Text = e.IsSuccess ? "Ready" : $"Page could not load · {e.WebErrorStatus}";
                    UpdateAddress();
                    UpdateZoomLabel();
                }
            });
            core.DocumentTitleChanged += (_, _) => Dispatcher.Invoke(() =>
            {
                if (Tabs.Items.Contains(tab)) UpdateTabTitle(tab, core.DocumentTitle);
            });
            core.FaviconChanged += (_, _) => { };
            core.DownloadStarting += (_, e) => TrackDownload(e);
            core.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                _ = CreateTabAsync(e.Uri, isPrivate);
            };
            core.PermissionRequested += (_, e) =>
            {
                // Fail closed until FEYRA has a real per-site permission prompt
                // and permission manager. Never silently grant camera, microphone,
                // geolocation, notifications, or other web permissions.
                e.State = CoreWebView2PermissionState.Deny;
            };
            core.ProcessFailed += (_, e) => Dispatcher.Invoke(() =>
            {
                StatusText.Text = $"A page process stopped unexpectedly ({e.ProcessFailedKind}). Reload the tab to recover.";
            });

            if (!string.IsNullOrWhiteSpace(address)) Navigate(web, address);
            else web.NavigateToString(StartPageHtml());

            await ApplyTabMemoryPolicyAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = "WebView2 initialization failed. Install the Microsoft Edge WebView2 Runtime.";
            MessageBox.Show(this, ex.Message, "FEYRA initialization", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AttachTabContextMenu(TabItem tab)
    {
        var menu = new ContextMenu();
        var duplicate = new MenuItem { Header = "Duplicate tab" };
        duplicate.Click += (_, _) => _ = CreateTabAsync((tab.Tag as BrowserTab)?.LastUrl);
        var pin = new MenuItem { Header = "Pin / unpin tab" };
        pin.Click += (_, _) =>
        {
            if (tab.Tag is not BrowserTab state) return;
            state.IsPinned = !state.IsPinned;
            UpdateTabTitle(tab, state.IsPinned ? "◆ " + (state.LastUrl is { Length: > 0 } u ? GetHost(new Uri(u)) : "Pinned") : (state.LastUrl is { Length: > 0 } v ? GetHost(new Uri(v)) : "New tab"));
        };
        var mute = new MenuItem { Header = "Mute / unmute tab" };
        mute.Click += async (_, _) =>
        {
            if (tab.Content is WebView2 web && web.CoreWebView2 != null)
            {
                var muted = await web.CoreWebView2.ExecuteScriptAsync("(()=>{const v=[...document.querySelectorAll('video,audio')];v.forEach(x=>x.muted=!x.muted);return v.some(x=>x.muted)})()");
                StatusText.Text = muted.Contains("true") ? "Media muted." : "Media unmuted.";
            }
        };
        var close = new MenuItem { Header = "Close tab" };
        close.Click += (_, _) => CloseTab(tab);
        var closeOthers = new MenuItem { Header = "Close other tabs" };
        closeOthers.Click += (_, _) =>
        {
            foreach (var other in Tabs.Items.OfType<TabItem>().Where(x => x != tab).ToList()) CloseTab(other);
            Tabs.SelectedItem = tab;
        };
        menu.Items.Add(duplicate); menu.Items.Add(pin); menu.Items.Add(mute);
        menu.Items.Add(new Separator()); menu.Items.Add(close); menu.Items.Add(closeOthers);
        tab.ContextMenu = menu;
    }

    private UIElement MakeTabHeader(string title)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new TextBlock { Text = title, MaxWidth = 150, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
        var close = new Button { Content = "×", Padding = new Thickness(5, 0, 5, 0), Margin = new Thickness(8, 0, -4, 0), FontSize = 13, ToolTip = "Close tab" };
        close.Click += (_, _) =>
        {
            var tab = FindTabItem(panel);
            if (tab != null) CloseTab(tab);
        };
        panel.Children.Add(close);
        return panel;
    }

    private TabItem? FindTabItem(DependencyObject child)
    {
        DependencyObject? current = child;
        while (current != null && current is not TabItem) current = VisualTreeHelper.GetParent(current);
        return current as TabItem;
    }

    private void UpdateTabTitle(TabItem tab, string? title)
    {
        if (tab.Header is not StackPanel panel || panel.Children.Count == 0) return;
        if (panel.Children[0] is TextBlock label)
            label.Text = string.IsNullOrWhiteSpace(title) ? "New tab" : title.Length > 25 ? title[..25] + "…" : title;
    }

    private static string GetHost(Uri uri) => string.IsNullOrWhiteSpace(uri.Host) ? "New tab" : uri.Host.Replace("www.", "");

    private static string StartPageHtml() => """
<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<style>
*{box-sizing:border-box}body{margin:0;background:radial-gradient(ellipse at 50% 25%,#242b3a 0,#151922 42%,#111318 78%);color:#f3f4f8;font:15px 'Segoe UI',sans-serif;min-height:100vh;display:grid;place-items:center}
main{width:min(720px,90vw);text-align:center;margin-top:-7vh}.mark{font-size:42px;color:#a9b8ff}.word{font-size:13px;letter-spacing:.34em;font-weight:700;margin-top:8px}.tag{color:#9298a7;margin:14px 0 30px}
form{display:flex;background:#242a35;border:1px solid #3b4352;border-radius:16px;padding:7px;box-shadow:0 20px 70px #0005}input{flex:1;min-width:0;background:transparent;border:0;outline:0;color:white;padding:13px;font-size:15px}button{background:#a9b8ff;color:#141722;border:0;border-radius:11px;padding:0 20px;font-weight:650;cursor:pointer}
.hint{font-size:12px;color:#737d90;margin-top:20px}
</style></head><body><main><div class="mark">◈</div><div class="word">FEYRA</div><div class="tag">A quieter place to browse.</div>
<form onsubmit="event.preventDefault();window.location.href='https://www.google.com/search?q='+encodeURIComponent(document.querySelector('input').value)"><input autofocus placeholder="Search the web or enter an address"><button>Search ↗</button></form><div class="hint">Your browser. Your pace.</div></main></body></html>
""";

    private void Navigate(WebView2 web, string value)
    {
        var text = value.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
        {
            web.Source = uri;
            return;
        }
        if (!text.Contains(' ') && text.Contains('.') && Uri.TryCreate("https://" + text, UriKind.Absolute, out var hostUri))
        {
            web.Source = hostUri;
            return;
        }
        var searchUrl = _settings.SearchTemplate.Replace("{query}", Uri.EscapeDataString(text));
        web.Source = new Uri(searchUrl);
    }

    private WebView2? CurrentWeb() => (Tabs.SelectedItem as TabItem)?.Tag is BrowserTab state ? state.View : null;
    private void UpdateAddress()
    {
        var web = CurrentWeb();
        AddressBox.Text = web?.Source?.ToString() ?? "";
        UpdateZoomLabel();
    }
    private void UpdateZoomLabel() { if (CurrentWeb() is { } web) ZoomText.Text = $"{Math.Round(web.ZoomFactor * 100)}%"; }

    private async void NewTab_Click(object sender, RoutedEventArgs e) => await CreateTabAsync();
    private async void NewPrivate_Click(object sender, RoutedEventArgs e)
    {
        await CreateTabAsync(null, true);
        StatusText.Text = "Private profile · history, cookies and cache are isolated for this tab.";
    }
    private void Back_Click(object sender, RoutedEventArgs e) { if (CurrentWeb()?.CanGoBack == true) CurrentWeb()!.GoBack(); }
    private void Forward_Click(object sender, RoutedEventArgs e) { if (CurrentWeb()?.CanGoForward == true) CurrentWeb()!.GoForward(); }
    private void Reload_Click(object sender, RoutedEventArgs e) => CurrentWeb()?.Reload();
    private void AddressBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { if (CurrentWeb() is { } web) Navigate(web, AddressBox.Text); Keyboard.ClearFocus(); }
        if (e.Key == Key.Escape) { UpdateAddress(); Keyboard.ClearFocus(); }
    }
    private void AddressBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => AddressBox.SelectAll();
    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateAddress();
        _ = ApplyTabMemoryPolicyAsync();
    }

    // WebView2 suspension stops background page timers/scripts while retaining the tab.
    // It is a low-risk first memory/CPU optimization; it does not guarantee process RAM is freed.
    private async Task ApplyTabMemoryPolicyAsync()
    {
        var selected = Tabs.SelectedItem as TabItem;
        foreach (var item in Tabs.Items.OfType<TabItem>())
        {
            if (item.Tag is not BrowserTab state || state.View.CoreWebView2 is not { } core) continue;
            try
            {
                if (item == selected && state.IsSuspended)
                {
                    core.Resume();
                    state.IsSuspended = false;
                }
                else if (item != selected && !state.IsSuspended)
                {
                    await core.TrySuspendAsync();
                    state.IsSuspended = true;
                }
            }
            catch
            {
                // Some pages/process states cannot be suspended; keep browsing uninterrupted.
            }
        }
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (e.Key == Key.N) { _ = CreateTabAsync(null, true); e.Handled = true; return; }
            if (e.Key == Key.T && _closedTabs.Count > 0) { var url = _closedTabs[0]; _closedTabs.RemoveAt(0); _ = CreateTabAsync(url); e.Handled = true; return; }
            if (e.Key == Key.Tab) { Tabs.SelectedIndex = (Tabs.SelectedIndex - 1 + Tabs.Items.Count) % Math.Max(1, Tabs.Items.Count); e.Handled = true; return; }
        }
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.T: _ = CreateTabAsync(); e.Handled = true; break;
                case Key.W: if (Tabs.SelectedItem is TabItem selected) CloseTab(selected); e.Handled = true; break;
                case Key.L: AddressBox.Focus(); AddressBox.SelectAll(); e.Handled = true; break;
                case Key.R: CurrentWeb()?.Reload(); e.Handled = true; break;
                case Key.D: BookmarkCurrent(); e.Handled = true; break;
                case Key.H: History_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                case Key.J: Downloads_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                case Key.F: FindBar.Visibility = Visibility.Visible; FindBox.Focus(); FindBox.SelectAll(); e.Handled = true; break;
                case Key.N: _ = CreateTabAsync(null, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)); e.Handled = true; break;
                case Key.OemPlus: ChangeZoom(0.1); e.Handled = true; break;
                case Key.OemMinus: ChangeZoom(-0.1); e.Handled = true; break;
                case Key.D0: if (CurrentWeb() is { } w) w.ZoomFactor = 1; UpdateZoomLabel(); e.Handled = true; break;
                case Key.Tab: SelectNextTab(); e.Handled = true; break;
            }
        }
        else if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.Left) Back_Click(this, new RoutedEventArgs());
        else if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.Right) Forward_Click(this, new RoutedEventArgs());
        else if (e.Key == Key.Escape && FindBar.Visibility == Visibility.Visible) CloseFind_Click(this, new RoutedEventArgs());
    }

    private void SelectNextTab()
    {
        if (Tabs.Items.Count < 2) return;
        Tabs.SelectedIndex = (Tabs.SelectedIndex + 1) % Tabs.Items.Count;
    }

    private void CloseTab(TabItem tab)
    {
        if (tab.Tag is BrowserTab state && !state.IsPrivate && !string.IsNullOrWhiteSpace(state.LastUrl))
            _closedTabs.Insert(0, state.LastUrl);
        if (Tabs.Items.Count == 1) { _ = CreateTabAsync(); }
        if (_splitTab == tab) RestoreSplitView();
        Tabs.Items.Remove(tab);
        if (tab.Tag is BrowserTab closed)
        {
            if (tab.Content is Grid splitGrid) splitGrid.Children.Clear();
            closed.View.Dispose();
            if (closed.IsPrivate) _ = DeletePrivateProfileAsync(closed.ProfilePath);
        }
        SaveSession();
    }

    private void BookmarkCurrent_Click(object sender, RoutedEventArgs e) => BookmarkCurrent();
    private void BookmarkCurrent()
    {
        var url = CurrentWeb()?.Source?.ToString();
        if (string.IsNullOrWhiteSpace(url) || (Tabs.SelectedItem as TabItem)?.Tag is BrowserTab { IsPrivate: true })
        { StatusText.Text = "Private pages cannot be bookmarked."; return; }
        if (_bookmarks.All(x => x.Url != url))
        {
            _bookmarks.Add(new BookmarkEntry(url, GetHost(new Uri(url)), "General"));
            SaveData();
            StatusText.Text = "Bookmark saved.";
        }
        else StatusText.Text = "Already bookmarked.";
    }

    private void TrackDownload(CoreWebView2DownloadStartingEventArgs e)
    {
        var operation = e.DownloadOperation;
        var item = new DownloadEntry(operation.ResultFilePath, operation.Uri, "In progress", 0, 0, DateTimeOffset.Now);
        _downloads.Add(item);
        SaveData();
        operation.BytesReceivedChanged += (_, _) => Dispatcher.Invoke(() =>
        {
            var index = _downloads.FindIndex(x => x.Path == item.Path && x.Url == item.Url);
            if (index < 0) return;
            var updated = item with { Received = (long)operation.BytesReceived, Total = (long)operation.TotalBytesToReceive };
            _downloads[index] = updated;
            StatusText.Text = updated.Total > 0 ? $"Downloading · {updated.Received * 100 / updated.Total}%" : "Downloading…";
        });
        operation.StateChanged += (_, _) => Dispatcher.Invoke(() =>
        {
            var state = operation.State.ToString();
            var index = _downloads.IndexOf(item);
            if (index >= 0) _downloads[index] = item with { State = state };
            SaveData();
            StatusText.Text = $"Download {state.ToLowerInvariant()}";
        });
    }

    private void History_Click(object sender, RoutedEventArgs e) => ShowManager("History", _history.OrderByDescending(x => x.VisitedAt).Select(x => $"{x.VisitedAt.LocalDateTime:g}  |  {x.Url}").ToList(), true);
    private void Downloads_Click(object sender, RoutedEventArgs e) => ShowManager("Downloads", _downloads.Select(x => $"{x.State}  |  {x.Path}").ToList(), false);
    private void Bookmarks_Click(object sender, RoutedEventArgs e) => ShowManager("Bookmarks", _bookmarks.Select(x => $"{x.Folder}  |  {x.Title}  |  {x.Url}").ToList(), true);

    private void ShowManager(string title, List<string> entries, bool canOpen)
    {
        var dialog = new Window { Title = "FEYRA · " + title, Owner = this, Width = 850, Height = 570, Background = (Brush)FindResource("CanvasBrush"), Foreground = (Brush)FindResource("TextBrush"), WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var root = new DockPanel { Margin = new Thickness(18) };
        var top = new StackPanel();
        var search = new TextBox { Margin = new Thickness(0,0,0,10), Padding = new Thickness(10), FontSize = 14 };
        top.Children.Add(search);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,10) };
        var clear = new Button { Content = title == "History" ? "Clear history" : title == "Downloads" ? "Clear list" : "Remove selected", Margin = new Thickness(0,0,8,0) };
        actions.Children.Add(clear);
        var open = new Button { Content = "Open selected", IsEnabled = canOpen };
        actions.Children.Add(open);
        top.Children.Add(actions);
        DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        var list = new ListBox { Background = (Brush)FindResource("SurfaceBrush"), Foreground = (Brush)FindResource("TextBrush"), BorderThickness = new Thickness(0), Padding = new Thickness(8) };
        void Filter() => list.ItemsSource = entries.Where(x => x.Contains(search.Text, StringComparison.OrdinalIgnoreCase)).ToList();
        search.TextChanged += (_, _) => Filter(); Filter();
        clear.Click += (_, _) =>
        {
            if (title == "History") _history.Clear();
            else if (title == "Downloads") _downloads.Clear();
            else if (list.SelectedItem is string selected)
            {
                var url = selected.Split('|').Last().Trim();
                _bookmarks.RemoveAll(x => x.Url == url);
            }
            SaveData();
            entries.Clear();
            Filter();
        };
        open.Click += async (_, _) =>
        {
            if (list.SelectedItem is not string selected) return;
            var url = selected.Split('|').Last().Trim();
            await CreateTabAsync(url);
            dialog.Close();
        };
        list.MouseDoubleClick += async (_, _) =>
        {
            if (!canOpen || list.SelectedItem is not string selected) return;
            await CreateTabAsync(selected.Split('|').Last().Trim());
            dialog.Close();
        };
        root.Children.Add(list); dialog.Content = root; dialog.ShowDialog();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Window { Title = "FEYRA · Settings", Owner = this, Width = 620, Height = 650, Background = (Brush)FindResource("CanvasBrush"), Foreground = (Brush)FindResource("TextBrush"), WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var root = new StackPanel { Margin = new Thickness(24) };
        root.Children.Add(new TextBlock { Text = "Preferences", FontSize = 24, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,0,18) });
        var dark = AddCheck(root, "Use dark browser chrome", _settings.DarkTheme);
        var restore = AddCheck(root, "Restore previous session on startup", _settings.StartupRestore);
        var history = AddCheck(root, "Save browsing history", _settings.SaveHistory);
        var permissions = AddCheck(root, "Ask before websites request permissions", _settings.AskSitePermissions);
        var context = AddCheck(root, "Enable browser context menus", _settings.ContextMenus);
        var devtools = AddCheck(root, "Enable developer tools", _settings.DeveloperTools);
        var vertical = AddCheck(root, "Use vertical tabs", _settings.VerticalTabs);
        var compact = AddCheck(root, "Compact browser chrome", _settings.CompactMode);
        root.Children.Add(new TextBlock { Text = "Default search engine", Foreground = (Brush)FindResource("MutedBrush"), Margin = new Thickness(0,18,0,5) });
        var search = new ComboBox { ItemsSource = new[] { "Google", "Bing", "DuckDuckGo" }, SelectedItem = _settings.SearchEngine, Padding = new Thickness(8), Width = 240, HorizontalAlignment = HorizontalAlignment.Left };
        root.Children.Add(search);
        root.Children.Add(new TextBlock { Text = "Default page zoom", Foreground = (Brush)FindResource("MutedBrush"), Margin = new Thickness(0,14,0,5) });
        var zoom = new Slider { Minimum = 0.5, Maximum = 2, Value = _settings.DefaultZoom, TickFrequency = 0.1, IsSnapToTickEnabled = false, Width = 300, HorizontalAlignment = HorizontalAlignment.Left };
        root.Children.Add(zoom);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,24,0,0) };
        var save = new Button { Content = "Save preferences", Background = (Brush)FindResource("SurfaceRaisedBrush"), Padding = new Thickness(16,9,16,9) };
        save.Click += (_, _) =>
        {
            _settings.DarkTheme = dark.IsChecked == true;
            _settings.StartupRestore = restore.IsChecked == true;
            _settings.SaveHistory = history.IsChecked == true;
            _settings.AskSitePermissions = permissions.IsChecked == true;
            _settings.ContextMenus = context.IsChecked == true;
            _settings.DeveloperTools = devtools.IsChecked == true;
            _settings.VerticalTabs = vertical.IsChecked == true;
            _settings.CompactMode = compact.IsChecked == true;
            _settings.SearchEngine = search.SelectedItem?.ToString() ?? "Google";
            _settings.SearchTemplate = _settings.SearchEngine switch { "Bing" => "https://www.bing.com/search?q={query}", "DuckDuckGo" => "https://duckduckgo.com/?q={query}", _ => "https://www.google.com/search?q={query}" };
            _settings.DefaultZoom = zoom.Value;
            SaveData();
            ApplySettings();
            StatusText.Text = "Preferences saved. Some changes apply to new tabs.";
            dialog.Close();
        };
        buttons.Children.Add(save);
        var cancel = new Button { Content = "Cancel", Margin = new Thickness(10,0,0,0), Padding = new Thickness(16,9,16,9) };
        cancel.Click += (_, _) => dialog.Close(); buttons.Children.Add(cancel);
        root.Children.Add(buttons);
        dialog.Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        dialog.ShowDialog();
    }

    private CheckBox AddCheck(Panel panel, string label, bool value)
    {
        var check = new CheckBox { Content = label, IsChecked = value, Margin = new Thickness(0,7,0,7), Foreground = (Brush)FindResource("TextBrush") };
        panel.Children.Add(check); return check;
    }

    private void SplitView_Click(object sender, RoutedEventArgs e) => ToggleSplitView();
    private async void ToggleSplitView()
    {
        if (Tabs.SelectedItem is not TabItem selected || selected.Tag is not BrowserTab tabState) return;
        if (_splitEnabled && _splitTab == selected)
        {
            RestoreSplitView();
            StatusText.Text = "Split view closed.";
            return;
        }
        if (_splitEnabled) RestoreSplitView();
        var current = tabState.View;
        _splitWeb = new WebView2();
        try
        {
            var profile = tabState.IsPrivate ? tabState.ProfilePath : Path.Combine(_dataDirectory, "DefaultProfile");
            var env = await CoreWebView2Environment.CreateAsync(null, profile);
            await _splitWeb.EnsureCoreWebView2Async(env);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            selected.Content = null;
            Grid.SetColumn(current, 0);
            grid.Children.Add(current);
            Grid.SetColumn(_splitWeb, 2);
            grid.Children.Add(_splitWeb);
            selected.Content = grid;
            _splitTab = selected;
            _splitWeb.Source = current.Source ?? new Uri("https://www.google.com");
            _splitEnabled = true;
            StatusText.Text = "Split view enabled.";
        }
        catch (Exception ex)
        {
            _splitWeb?.Dispose();
            _splitWeb = null;
            _splitTab = null;
            _splitEnabled = false;
            selected.Content = current;
            StatusText.Text = "Could not start split view: " + ex.Message;
        }
    }

    private void RestoreSplitView()
    {
        if (_splitTab?.Tag is BrowserTab owner && _splitTab.Content is Grid splitGrid)
        {
            splitGrid.Children.Clear();
            _splitTab.Content = owner.View;
        }
        _splitWeb?.Dispose();
        _splitWeb = null;
        _splitTab = null;
        _splitEnabled = false;
    }

    private void MoreMenu_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        AddMenu(menu, "Reopen closed tab", () => { if (_closedTabs.Count > 0) { var url = _closedTabs[0]; _closedTabs.RemoveAt(0); _ = CreateTabAsync(url); } });
        AddMenu(menu, "Toggle vertical tabs", () => { _settings.VerticalTabs = !_settings.VerticalTabs; ApplySettings(); SaveData(); });
        AddMenu(menu, "Clear browsing data", () => ClearBrowsingData());
        AddMenu(menu, "Picture in picture", () => _ = CurrentWeb()?.CoreWebView2.ExecuteScriptAsync("document.querySelector('video')?.requestPictureInPicture().catch(()=>{})"));
        AddMenu(menu, "Reader mode", () => _ = CurrentWeb()?.CoreWebView2.ExecuteScriptAsync("document.body.innerHTML='<main style=\u0027max-width:760px;margin:8vh auto;padding:24px;font:20px/1.7 Georgia,serif;color:#222;background:#fff\u0027>'+document.body.innerHTML+'</main>';document.body.style='background:#fff'"));
        menu.PlacementTarget = (Button)sender; menu.IsOpen = true;
    }

    private void AddMenu(ContextMenu menu, string title, Action action)
    {
        var item = new MenuItem { Header = title }; item.Click += (_, _) => action(); menu.Items.Add(item);
    }

    private void ClearBrowsingData()
    {
        if (MessageBox.Show(this, "Clear browsing history and saved download list? Bookmarks will be kept.", "Clear browsing data", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _history.Clear(); _downloads.Clear(); SaveData(); StatusText.Text = "Browsing history and download list cleared.";
    }

    private void ChangeZoom(double delta)
    {
        if (CurrentWeb() is { } web) { web.ZoomFactor = Math.Clamp(web.ZoomFactor + delta, 0.25, 5); UpdateZoomLabel(); }
    }

    private void FindNext_Click(object sender, RoutedEventArgs e) => FindText();
    private void FindBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) FindText(); }
    private void FindText()
    {
        if (CurrentWeb() is not { } web) return;
        var value = JsonSerializer.Serialize(FindBox.Text);
        _ = web.CoreWebView2.ExecuteScriptAsync($"window.find({value});");
    }
    private void CloseFind_Click(object sender, RoutedEventArgs e) { FindBar.Visibility = Visibility.Collapsed; CurrentWeb()?.Focus(); }

    private async Task DeletePrivateProfileAsync(string path)
    {
        await Task.Delay(700);
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { /* WebView2 may still hold files until its process exits. */ }
    }

    private void SaveSession()
    {
        if (_restoring || _closing || !_settings.StartupRestore) return;
        _settings.LastSession = Tabs.Items.OfType<TabItem>().Select(t => t.Tag is BrowserTab b && !b.IsPrivate ? b.LastUrl : "").Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        SaveData();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _closing = true;
        _settings.LastSession = Tabs.Items.OfType<TabItem>().Select(t => t.Tag is BrowserTab b && !b.IsPrivate ? b.LastUrl : "").Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        SaveData();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        foreach (var tab in Tabs.Items.OfType<TabItem>())
        {
            if (tab.Content is Grid splitGrid) splitGrid.Children.Clear();
            if (tab.Tag is BrowserTab state)
            {
                state.View.Dispose();
                if (state.IsPrivate) _ = DeletePrivateProfileAsync(state.ProfilePath);
            }
        }
        _splitWeb?.Dispose();
        base.OnClosed(e);
    }

    private sealed class BrowserTab(WebView2 view, string lastUrl, bool isPrivate, bool isPinned, string profilePath)
    {
        public WebView2 View { get; } = view;
        public string LastUrl { get; set; } = lastUrl;
        public bool IsPrivate { get; } = isPrivate;
        public bool IsPinned { get; set; } = isPinned;
        public string ProfilePath { get; } = profilePath;
        public bool IsSuspended { get; set; }
    }
    private sealed record HistoryEntry(string Url, DateTimeOffset VisitedAt);
    private sealed record BookmarkEntry(string Url, string Title, string Folder);
    private sealed record DownloadEntry(string Path, string Url, string State, long Received, long Total, DateTimeOffset StartedAt);
    private sealed class BrowserSettings
    {
        public bool DarkTheme { get; set; } = true;
        public bool StartupRestore { get; set; } = true;
        public bool SaveHistory { get; set; } = true;
        public bool AskSitePermissions { get; set; } = true;
        public bool ContextMenus { get; set; } = true;
        public bool DeveloperTools { get; set; }
        public bool VerticalTabs { get; set; }
        public bool CompactMode { get; set; }
        public string SearchEngine { get; set; } = "Google";
        public string SearchTemplate { get; set; } = "https://www.google.com/search?q={query}";
        public double DefaultZoom { get; set; } = 1;
        public List<string> LastSession { get; set; } = [];
    }
}