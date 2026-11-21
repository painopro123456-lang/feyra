using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Feyra;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<BrowserTab> _tabs = new();
    private readonly List<HistoryEntry> _history = new();
    private readonly List<string> _bookmarks = new();
    private readonly List<string> _downloads = new();
    private readonly string _dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Feyra");
    private bool _privateWindow;

    public MainWindow()
    {
        InitializeComponent();
        Directory.CreateDirectory(_dataDirectory);
        LoadData();
        Loaded += async (_, _) => await CreateTabAsync();
        PreviewKeyDown += MainWindow_PreviewKeyDown;
    }

    private string DataPath(string name) => Path.Combine(_dataDirectory, name);

    private void LoadData()
    {
        try
        {
            if (File.Exists(DataPath("bookmarks.json")))
                _bookmarks.AddRange(JsonSerializer.Deserialize<List<string>>(File.ReadAllText(DataPath("bookmarks.json"))) ?? []);
            if (File.Exists(DataPath("history.json")))
                _history.AddRange(JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(DataPath("history.json"))) ?? []);
            if (File.Exists(DataPath("downloads.json")))
                _downloads.AddRange(JsonSerializer.Deserialize<List<string>>(File.ReadAllText(DataPath("downloads.json"))) ?? []);
        }
        catch { StatusText.Text = "Some saved browser data could not be read."; }
    }

    private void SaveData()
    {
        if (_privateWindow) return;
        try
        {
            File.WriteAllText(DataPath("bookmarks.json"), JsonSerializer.Serialize(_bookmarks));
            File.WriteAllText(DataPath("history.json"), JsonSerializer.Serialize(_history.TakeLast(5000)));
            File.WriteAllText(DataPath("downloads.json"), JsonSerializer.Serialize(_downloads));
        }
        catch { StatusText.Text = "Could not save browser data."; }
    }

    private async Task CreateTabAsync(string? address = null)
    {
        var web = new WebView2();
        var page = new BrowserTab(web);
        var tab = new TabItem { Header = "New tab", Content = web, Tag = page };
        _tabs.Add(page);
        Tabs.Items.Add(tab);
        Tabs.SelectedItem = tab;
        try
        {
            await web.EnsureCoreWebView2Async();
            web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            web.CoreWebView2.Settings.AreDevToolsEnabled = true;
            web.CoreWebView2.Settings.IsStatusBarEnabled = false;
            web.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
            web.CoreWebView2.NavigationStarting += (_, e) =>
            {
                Dispatcher.Invoke(() => { StatusText.Text = "Loading…"; if (Tabs.SelectedItem == tab) AddressBox.Text = e.Uri; });
            };
            web.CoreWebView2.NavigationCompleted += (_, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (Tabs.SelectedItem == tab) StatusText.Text = e.IsSuccess ? "Ready" : $"Navigation failed: {e.WebErrorStatus}";
                    if (e.IsSuccess && Uri.TryCreate(web.Source?.ToString(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
                    {
                        tab.Header = GetHost(uri);
                        if (!_privateWindow) { _history.Add(new HistoryEntry(uri.ToString(), DateTimeOffset.Now)); SaveData(); }
                    }
                    if (Tabs.SelectedItem == tab) UpdateAddress();
                });
            };
            web.CoreWebView2.DocumentTitleChanged += (_, _) => Dispatcher.Invoke(() =>
            {
                var title = web.CoreWebView2.DocumentTitle;
                tab.Header = string.IsNullOrWhiteSpace(title) ? "New tab" : (title.Length > 24 ? title[..24] + "…" : title);
            });
            web.CoreWebView2.DownloadStarting += (_, e) =>
            {
                var path = e.ResultFilePath;
                Dispatcher.Invoke(() =>
                {
                    _downloads.Add(path);
                    SaveData();
                    StatusText.Text = $"Downloading: {Path.GetFileName(path)}";
                });
            };
            web.CoreWebView2.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                _ = CreateTabAsync(e.Uri);
            };
            if (!string.IsNullOrWhiteSpace(address)) Navigate(web, address);
            else web.NavigateToString(StartPageHtml());
        }
        catch (Exception ex)
        {
            StatusText.Text = "WebView2 could not start. Install the Microsoft Edge WebView2 Runtime.";
            MessageBox.Show(this, ex.Message, "FEYRA browser initialization", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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

    private static void Navigate(WebView2 web, string value)
    {
        var text = value.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && (uri.Scheme is "http" or "https"))
            web.Source = uri;
        else if (!text.Contains(' ') && text.Contains('.') && Uri.TryCreate("https://" + text, UriKind.Absolute, out var hostUri))
            web.Source = hostUri;
        else
            web.Source = new Uri("https://www.google.com/search?q=" + Uri.EscapeDataString(text));
    }

    private WebView2? CurrentWeb() => (Tabs.SelectedItem as TabItem)?.Content as WebView2;
    private void UpdateAddress()
    {
        var web = CurrentWeb();
        AddressBox.Text = web?.Source?.ToString() ?? "";
    }

    private async void NewTab_Click(object sender, RoutedEventArgs e) => await CreateTabAsync();
    private async void NewPrivate_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(this, "Open a private tab? This preview keeps private navigation out of local history and bookmarks. It does not make you anonymous online.", "Private browsing", MessageBoxButton.OKCancel, MessageBoxImage.Information);
        if (result != MessageBoxResult.OK) return;
        _privateWindow = true;
        await CreateTabAsync();
        StatusText.Text = "Private browsing · local history is not saved";
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
    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (IsLoaded) UpdateAddress(); }
    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.T: _ = CreateTabAsync(); e.Handled = true; break;
                case Key.W: CloseCurrentTab(); e.Handled = true; break;
                case Key.L: AddressBox.Focus(); AddressBox.SelectAll(); e.Handled = true; break;
                case Key.R: CurrentWeb()?.Reload(); e.Handled = true; break;
                case Key.D: BookmarkCurrent(); e.Handled = true; break;
                case Key.H: History_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                case Key.J: Downloads_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                case Key.F: CurrentWeb()?.CoreWebView2?.ExecuteScriptAsync("window.find('')"); e.Handled = true; break;
                case Key.N: _ = CreateTabAsync(); e.Handled = true; break;
            }
        }
        else if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.Left) Back_Click(this, new RoutedEventArgs());
        else if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.Right) Forward_Click(this, new RoutedEventArgs());
    }
    private void CloseCurrentTab()
    {
        if (Tabs.SelectedItem is not TabItem item) return;
        if (Tabs.Items.Count == 1) { _ = CreateTabAsync(); }
        Tabs.Items.Remove(item);
        if (item.Tag is BrowserTab page) _tabs.Remove(page);
    }
    private void BookmarkCurrent_Click(object sender, RoutedEventArgs e) => BookmarkCurrent();
    private void BookmarkCurrent()
    {
        var url = CurrentWeb()?.Source?.ToString();
        if (string.IsNullOrWhiteSpace(url) || _privateWindow) { StatusText.Text = "This page cannot be saved as a bookmark in private mode."; return; }
        if (!_bookmarks.Contains(url)) { _bookmarks.Add(url); SaveData(); StatusText.Text = "Bookmark saved."; }
        else StatusText.Text = "Already bookmarked.";
    }
    private void History_Click(object sender, RoutedEventArgs e) => ShowList("History", _history.OrderByDescending(x => x.VisitedAt).Select(x => $"{x.VisitedAt.LocalDateTime:g}  —  {x.Url}").ToList());
    private void Downloads_Click(object sender, RoutedEventArgs e) => ShowList("Downloads", _downloads.ToList());
    private void Bookmarks_Click(object sender, RoutedEventArgs e) => ShowList("Bookmarks", _bookmarks.ToList());
    private void ShowList(string title, List<string> entries)
    {
        var dialog = new Window { Title = "FEYRA · " + title, Owner = this, Width = 760, Height = 520, Background = (System.Windows.Media.Brush)FindResource("CanvasBrush"), Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"), WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var root = new DockPanel { Margin = new Thickness(18) };
        var search = new TextBox { Margin = new Thickness(0,0,0,12), Padding = new Thickness(10), FontSize = 14 };
        DockPanel.SetDock(search, Dock.Top); root.Children.Add(search);
        var list = new ListBox { Background = (System.Windows.Media.Brush)FindResource("SurfaceBrush"), Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"), BorderThickness = new Thickness(0) };
        void Filter() { list.ItemsSource = entries.Where(x => x.Contains(search.Text, StringComparison.OrdinalIgnoreCase)).ToList(); }
        search.TextChanged += (_, _) => Filter(); Filter();
        list.MouseDoubleClick += async (_, _) => { if (list.SelectedItem is string selected) { var value = selected.Contains(" — ") ? selected[(selected.IndexOf(" — ", StringComparison.Ordinal) + 3)..] : selected; await CreateTabAsync(value); dialog.Close(); } };
        root.Children.Add(list); dialog.Content = root; dialog.ShowDialog();
    }
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(this, "FEYRA preview settings\n\nThis first foundation build establishes the native shell and WebView2 browsing. Theme, privacy, permissions, startup, shortcuts and performance controls are being added in the next implementation stages.", "FEYRA Settings", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    protected override void OnClosed(EventArgs e) { SaveData(); base.OnClosed(e); }
    private sealed record BrowserTab(WebView2 View);
    private sealed record HistoryEntry(string Url, DateTimeOffset VisitedAt);
}
