using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SnipIt.Models;
using SnipIt.Services;
namespace SnipIt.Views;
internal sealed class SettingsWindow : Window
{
    public SettingsWindow()
    {
        App.SettingsWindows++; Closed+=(_,_)=>App.SettingsWindows--; Title = Ui.L("SnipIt 설정"); var liveConfig = AppSettingsConfig.Instance; var config = System.Text.Json.JsonSerializer.Deserialize<AppSettingsConfig>(System.Text.Json.JsonSerializer.Serialize(liveConfig))!; var resetCapture = new List<Action>(); var resetEditor = new List<Action>(); var originalStartup = config.RunAtStartup; var root = new Grid { Background = Ui.Background };
        root.ColumnDefinitions.Add(new() { Width = new GridLength(180) }); root.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) }); root.RowDefinitions.Add(new() { Height = GridLength.Auto }); Content = root;
        var nav = new StackPanel { Padding = new Thickness(14,20,14,14), Spacing = 8 }; nav.Children.Add(Ui.Text(Ui.L("설정"),24)); Grid.SetRowSpan(nav,2); root.Children.Add(nav);
        var pages = new List<(ScrollViewer View,Button Button)>();
        StackPanel Page(string title) { var contents = new StackPanel { Padding = new Thickness(20), Spacing = 12 }; contents.Children.Add(Ui.Text(Ui.L(title),22)); var view = new ScrollViewer { Content = contents, Visibility = Visibility.Collapsed }; Grid.SetColumn(view,1);root.Children.Add(view); var button = Ui.Button(Ui.L(title),()=> { foreach(var page in pages) { var selected = page.View == view; page.View.Visibility = selected ? Visibility.Visible : Visibility.Collapsed; Ui.Emphasis(page.Button,selected); } }); pages.Add((view,button));nav.Children.Add(button);return contents; }
        var general = Page("일반"); var gif = Page("GIF 녹화"); var captureKeys = Page("전역 단축키"); var editKeys = Page("에디터 단축키"); pages[0].View.Visibility=Visibility.Visible;Ui.Emphasis(pages[0].Button,true);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(16) }; Grid.SetRow(footer,1);Grid.SetColumn(footer,1);root.Children.Add(footer);
        var panel = general; var changes = new List<Action>();var hotkeyValues=new List<Func<HotkeyConfig>>();var editorValues=new List<Func<System.Windows.Forms.Keys>>();
        foreach (var (name, title) in new[] { ("CaptureCursor", Ui.L("마우스 커서 포함")), ("CopyToClipboard", Ui.L("캡처 후 클립보드 복사")), ("PlaySound", Ui.L("캡처 소리")), ("StartMinimized", Ui.L("Windows 자동 실행 시 트레이에서 시작")), ("RunAtStartup", Ui.L("Windows 시작 시 실행")), ("SilentMode", Ui.L("편집창 없이 캡처")), ("SilentModeAutoSave", Ui.L("빠른 저장 시 경로 자동 지정")), ("CheckForUpdates", Ui.L("새 버전 자동 확인")), ("AutoDownloadUpdates", Ui.L("업데이트 자동 다운로드")) })
        { var property = typeof(AppSettingsConfig).GetProperty(name)!; var toggle = new ToggleSwitch { IsOn = (bool)property.GetValue(config)!, OnContent = "", OffContent = "", HorizontalAlignment = HorizontalAlignment.Right }; Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(toggle,title); var row = new Grid { MinHeight = 36 }; var label = Ui.Text(title,13); label.Margin = new Thickness(0,0,72,0); label.TextWrapping=TextWrapping.Wrap;row.Children.Add(label);row.Children.Add(toggle);panel.Children.Add(row); changes.Add(() => property.SetValue(config, toggle.IsOn)); }
        var path = new TextBox { Header = Ui.L("기본 저장 폴더"), Text = config.SavePath }; panel.Children.Add(path); changes.Add(() => config.SavePath = path.Text); panel.Children.Add(Ui.Button(Ui.L("폴더 선택"), () => { var selected = Ui.SelectFolder(this, path.Text); if (selected != null) path.Text = selected; }));
        void Choice(string title, string[] choices, int selected, Action<int> save) { var box = new ComboBox { Header = title, ItemsSource = choices, SelectedIndex = selected, HorizontalAlignment = HorizontalAlignment.Stretch }; panel.Children.Add(box); changes.Add(() => save(box.SelectedIndex)); }
        Choice(Ui.L("언어 (새로 여는 창부터 적용)"), new[] { Ui.L("한국어"), "English" }, (int)config.Language, i => config.Language = (Language)i);
        Choice(Ui.L("기본 저장 형식"),new[]{Ui.L("PNG (무손실)"),"JPEG","Bitmap","GIF"},Math.Max(0,Array.IndexOf(new[]{"png","jpg","bmp","gif"},config.DefaultFormat)),i=>config.DefaultFormat=new[]{"png","jpg","bmp","gif"}[i]);
        Choice(Ui.L("확대경 위치"),new[]{Ui.L("커서 왼쪽 위"),Ui.L("커서 오른쪽 위"),Ui.L("커서 왼쪽 아래"),Ui.L("커서 오른쪽 아래"),Ui.L("화면 왼쪽 위"),Ui.L("화면 오른쪽 위"),Ui.L("화면 왼쪽 아래"),Ui.L("화면 오른쪽 아래")},(int)config.MagnifierPosition,i=>config.MagnifierPosition=(MagnifierPosition)i);
        Choice(Ui.L("초기 이미지 배율"), new[] { Ui.L("화면 맞춤"), Ui.L("원본 100%") }, (int)config.EditorInitialZoom, i => config.EditorInitialZoom = (EditorInitialZoom)i);
        panel = gif;
        Choice(Ui.L("GIF 품질"), new[] { Ui.L("원본"), Ui.L("중복 프레임 제거"), Ui.L("중복 제거 + 크기 50%") }, (int)config.GifQuality, i => config.GifQuality = (GifQualityPreset)i);
        Choice(Ui.L("GIF 프레임"), new[] { "15 FPS", "30 FPS", "60 FPS" }, config.GifFps == 15 ? 0 : config.GifFps == 60 ? 2 : 1, i => config.GifFps = new[] { 15, 30, 60 }[i]);
        var duration = new NumberBox { Header = Ui.L("GIF 최대 녹화 시간(초)"), Value = config.GifMaxDurationSeconds, Minimum = 1, Maximum = 3600 }; panel.Children.Add(duration); changes.Add(() => config.GifMaxDurationSeconds = (int)duration.Value);
        panel = general;
        var dim = new Slider { Header = Ui.L("선택 화면 어둡기"), Value = config.CaptureDimmingOpacity, Minimum = 0, Maximum = 100 }; panel.Children.Add(dim); changes.Add(() => config.CaptureDimmingOpacity = (int)dim.Value);
        panel = captureKeys;
        foreach (var property in typeof(AppSettingsConfig).GetProperties().Where(x => x.PropertyType == typeof(HotkeyConfig)))
        { var key = (HotkeyConfig)property.GetValue(config)!; var box = new TextBox { Header = property.Name switch { "FullScreenHotkey"=>Ui.L("전체 화면 단축키"),"ActiveWindowHotkey"=>Ui.L("활성 창 단축키"),"RegionHotkey"=>Ui.L("영역 캡처 단축키"),_=>Ui.L("GIF 녹화 단축키") }, Text = key.ToString(), IsReadOnly = true }; HotkeyConfig? replacement = null; resetCapture.Add(() => { replacement = (HotkeyConfig)property.GetValue(new AppSettingsConfig())!; box.Text = replacement.ToString(); });hotkeyValues.Add(()=>replacement??key); box.KeyDown += (_, e) => { var modifiers = ModifierKeys.None; foreach (var (vk, flag) in new[] { (Windows.System.VirtualKey.Control, ModifierKeys.Control), (Windows.System.VirtualKey.Menu, ModifierKeys.Alt), (Windows.System.VirtualKey.Shift, ModifierKeys.Shift), (Windows.System.VirtualKey.LeftWindows, ModifierKeys.Windows) }) if (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(vk).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) modifiers |= flag; if (e.Key is Windows.System.VirtualKey.Control or Windows.System.VirtualKey.Menu or Windows.System.VirtualKey.Shift or Windows.System.VirtualKey.LeftWindows or Windows.System.VirtualKey.RightWindows) return; replacement = new(modifiers, (System.Windows.Forms.Keys)(int)e.Key); box.Text = replacement.ToString(); e.Handled = true; }; panel.Children.Add(box); changes.Add(() => { if (replacement != null) property.SetValue(config, replacement); }); }
        panel = editKeys;
        foreach(var property in typeof(EditorToolShortcuts).GetProperties())
        {
            var selected=(System.Windows.Forms.Keys)property.GetValue(config.EditorShortcuts)!;editorValues.Add(()=>selected);
            var box=new TextBox{Header=Ui.L("편집 도구 · ")+Ui.ToolLabel(property.Name),Text=EditorToolShortcuts.GetKeyDisplayName(selected),IsReadOnly=true};
            resetEditor.Add(() => { selected = (System.Windows.Forms.Keys)property.GetValue(new EditorToolShortcuts())!; box.Text = EditorToolShortcuts.GetKeyDisplayName(selected); });
            box.KeyDown+=(_,e)=>{if(((int)e.Key>=48&&(int)e.Key<=57)||((int)e.Key>=65&&(int)e.Key<=90)){selected=(System.Windows.Forms.Keys)(int)e.Key;box.Text=EditorToolShortcuts.GetKeyDisplayName(selected);e.Handled=true;}};
            panel.Children.Add(box);changes.Add(()=>property.SetValue(config.EditorShortcuts,selected));
        }
        captureKeys.Children.Add(Ui.Button(Ui.L("캡처 단축키 기본값"), () => { foreach (var reset in resetCapture) reset(); }));
        editKeys.Children.Add(Ui.Button(Ui.L("편집 단축키 기본값"), () => { foreach (var reset in resetEditor) reset(); }));
        captureKeys.Children.Add(Ui.Button(Ui.L("현재 단축키 다시 등록"), App.RegisterHotkeys));
        footer.Children.Add(Ui.Button(Ui.L("취소"), Close));
        footer.Children.Add(Ui.AsyncButton(Ui.L("저장"), async () => { if (!Directory.Exists(path.Text)) { App.Notify(Ui.L("존재하는 저장 폴더를 입력해 주세요.")); return; } if (!double.IsFinite(duration.Value)) { App.Notify(Ui.L("녹화 시간을 숫자로 입력해 주세요.")); return; } var keys=hotkeyValues.Select(get=>get()).ToArray();if(keys.Select(key=>(key.Modifiers,key.Key)).Distinct().Count()!=keys.Length||keys.Any(key=>key.Modifiers==(ModifierKeys.Control|ModifierKeys.Shift)&&key.Key==System.Windows.Forms.Keys.E)||editorValues.Select(get=>get()).Distinct().Count()!=editorValues.Count){App.Notify(Ui.L("중복 단축키를 변경해 주세요. Ctrl+Shift+E는 최근 캡처 편집에 사용됩니다."));return;}foreach (var change in changes) change(); await config.SaveCheckedAsync(); foreach (var property in typeof(AppSettingsConfig).GetProperties().Where(property => property.CanWrite && property.GetMethod?.IsStatic == false)) property.SetValue(liveConfig, property.GetValue(config)); LocalizationService.Instance.CurrentLanguage = config.Language; config.ApplyToAppSettings(); App.RegisterHotkeys(); if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SNIPIT_DATA_DIRECTORY"))) using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true)) { if (config.RunAtStartup) key?.SetValue("SnipIt", "\"" + Environment.ProcessPath + "\" --startup"); else key?.DeleteValue("SnipIt", false); } Close(); }, true)); Ui.Size(this, 760, 680);
    }
}

