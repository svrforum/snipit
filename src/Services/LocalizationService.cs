namespace SnipIt.Services;

public enum Language
{
    Korean,
    English
}

public class LocalizationService
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new LocalizationService();

    private Language _currentLanguage = Language.Korean;

    public Language CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            _currentLanguage = value;
            LanguageChanged?.Invoke();
        }
    }

    public event Action? LanguageChanged;

    private readonly Dictionary<string, Dictionary<Language, string>> _strings = new()
    {
        // App
        ["AppName"] = new() { { Language.Korean, "SnipIt" }, { Language.English, "SnipIt" } },

        // Main Window
        ["FullScreenCapture"] = new() { { Language.Korean, "전체 화면 캡쳐" }, { Language.English, "Full Screen Capture" } },
        ["ActiveWindowCapture"] = new() { { Language.Korean, "활성 창 캡쳐" }, { Language.English, "Active Window Capture" } },
        ["RegionCapture"] = new() { { Language.Korean, "영역 캡쳐" }, { Language.English, "Region Capture" } },
        ["Settings"] = new() { { Language.Korean, "설정" }, { Language.English, "Settings" } },
        ["Exit"] = new() { { Language.Korean, "종료" }, { Language.English, "Exit" } },

        // Editor Window
        ["EditorTitle"] = new() { { Language.Korean, "SnipIt - 편집기" }, { Language.English, "SnipIt - Editor" } },
        ["Save"] = new() { { Language.Korean, "저장" }, { Language.English, "Save" } },
        ["SaveShortcut"] = new() { { Language.Korean, "저장 (Ctrl+S)" }, { Language.English, "Save (Ctrl+S)" } },
        ["Copy"] = new() { { Language.Korean, "복사" }, { Language.English, "Copy" } },
        ["CopyShortcut"] = new() { { Language.Korean, "클립보드에 복사 (Ctrl+C)" }, { Language.English, "Copy to Clipboard (Ctrl+C)" } },
        ["Undo"] = new() { { Language.Korean, "실행 취소" }, { Language.English, "Undo" } },
        ["UndoShortcut"] = new() { { Language.Korean, "실행 취소 (Ctrl+Z)" }, { Language.English, "Undo (Ctrl+Z)" } },
        ["Redo"] = new() { { Language.Korean, "다시 실행" }, { Language.English, "Redo" } },
        ["RedoShortcut"] = new() { { Language.Korean, "다시 실행 (Ctrl+Y)" }, { Language.English, "Redo (Ctrl+Y)" } },

        // Tools
        ["Select"] = new() { { Language.Korean, "선택" }, { Language.English, "Select" } },
        ["Pen"] = new() { { Language.Korean, "펜" }, { Language.English, "Pen" } },
        ["Arrow"] = new() { { Language.Korean, "화살표" }, { Language.English, "Arrow" } },
        ["Line"] = new() { { Language.Korean, "직선" }, { Language.English, "Line" } },
        ["Rectangle"] = new() { { Language.Korean, "사각형" }, { Language.English, "Rectangle" } },
        ["Ellipse"] = new() { { Language.Korean, "타원" }, { Language.English, "Ellipse" } },
        ["Text"] = new() { { Language.Korean, "텍스트" }, { Language.English, "Text" } },
        ["Highlight"] = new() { { Language.Korean, "형광펜" }, { Language.English, "Highlight" } },
        ["Blur"] = new() { { Language.Korean, "모자이크" }, { Language.English, "Blur/Mosaic" } },
        ["Crop"] = new() { { Language.Korean, "자르기" }, { Language.English, "Crop" } },

        // Editor Labels
        ["Color"] = new() { { Language.Korean, "색상" }, { Language.English, "Color" } },
        ["Size"] = new() { { Language.Korean, "크기" }, { Language.English, "Size" } },
        ["History"] = new() { { Language.Korean, "히스토리" }, { Language.English, "History" } },
        ["ClearHistory"] = new() { { Language.Korean, "히스토리 삭제" }, { Language.English, "Clear History" } },
        ["Ready"] = new() { { Language.Korean, "준비됨" }, { Language.English, "Ready" } },
        ["CopiedToClipboard"] = new() { { Language.Korean, "클립보드에 복사됨" }, { Language.English, "Copied to clipboard" } },
        ["Saved"] = new() { { Language.Korean, "저장됨" }, { Language.English, "Saved" } },
        ["HistoryCleared"] = new() { { Language.Korean, "히스토리 삭제됨" }, { Language.English, "History cleared" } },
        ["LoadedCapture"] = new() { { Language.Korean, "캡쳐 불러옴" }, { Language.English, "Loaded capture" } },
        ["ImageCropped"] = new() { { Language.Korean, "이미지 잘라짐" }, { Language.English, "Image cropped" } },
        ["CropCancelled"] = new() { { Language.Korean, "자르기 취소됨 - 선택 영역이 너무 작음" }, { Language.English, "Crop cancelled - selection too small" } },
        ["SelectAreaToCrop"] = new() { { Language.Korean, "자를 영역을 선택하세요" }, { Language.English, "Select area to crop, then press Enter" } },

        // Dialogs
        ["ClearHistoryConfirm"] = new() { { Language.Korean, "모든 캡쳐 히스토리를 삭제하시겠습니까?" }, { Language.English, "Are you sure you want to clear all capture history?" } },
        ["ClearHistoryTitle"] = new() { { Language.Korean, "히스토리 삭제" }, { Language.English, "Clear History" } },

        // Settings Window
        ["SettingsTitle"] = new() { { Language.Korean, "SnipIt - 설정" }, { Language.English, "SnipIt - Settings" } },
        ["GeneralSettings"] = new() { { Language.Korean, "일반 설정" }, { Language.English, "General Settings" } },
        ["CaptureCursor"] = new() { { Language.Korean, "마우스 커서 포함" }, { Language.English, "Include mouse cursor" } },
        ["CopyToClipboard"] = new() { { Language.Korean, "캡쳐 후 클립보드에 복사" }, { Language.English, "Copy to clipboard after capture" } },
        ["PlaySound"] = new() { { Language.Korean, "캡쳐 효과음 재생" }, { Language.English, "Play capture sound" } },
        ["StartMinimized"] = new() { { Language.Korean, "시작 시 최소화" }, { Language.English, "Start minimized" } },
        ["Language"] = new() { { Language.Korean, "언어" }, { Language.English, "Language" } },
        ["Korean"] = new() { { Language.Korean, "한국어" }, { Language.English, "Korean" } },
        ["English"] = new() { { Language.Korean, "영어" }, { Language.English, "English" } },

        ["SaveSettings"] = new() { { Language.Korean, "저장 설정" }, { Language.English, "Save Settings" } },
        ["SavePath"] = new() { { Language.Korean, "저장 경로" }, { Language.English, "Save Path" } },
        ["Browse"] = new() { { Language.Korean, "찾아보기..." }, { Language.English, "Browse..." } },
        ["DefaultFormat"] = new() { { Language.Korean, "기본 형식" }, { Language.English, "Default Format" } },

        ["HotkeySettings"] = new() { { Language.Korean, "단축키 설정" }, { Language.English, "Hotkey Settings" } },
        ["FullScreen"] = new() { { Language.Korean, "전체 화면" }, { Language.English, "Full Screen" } },
        ["ActiveWindow"] = new() { { Language.Korean, "활성 창" }, { Language.English, "Active Window" } },
        ["Region"] = new() { { Language.Korean, "영역 선택" }, { Language.English, "Region" } },

        ["SaveButton"] = new() { { Language.Korean, "저장" }, { Language.English, "Save" } },
        ["CancelButton"] = new() { { Language.Korean, "취소" }, { Language.English, "Cancel" } },

        // Tray Menu
        ["Show"] = new() { { Language.Korean, "열기" }, { Language.English, "Show" } },

        // File Dialog
        ["PngImage"] = new() { { Language.Korean, "PNG 이미지" }, { Language.English, "PNG Image" } },
        ["JpegImage"] = new() { { Language.Korean, "JPEG 이미지" }, { Language.English, "JPEG Image" } },
        ["BmpImage"] = new() { { Language.Korean, "BMP 이미지" }, { Language.English, "BMP Image" } },
        ["GifImage"] = new() { { Language.Korean, "GIF 이미지" }, { Language.English, "GIF Image" } },
    };

    public string Get(string key)
    {
        if (_strings.TryGetValue(key, out var translations))
        {
            if (translations.TryGetValue(_currentLanguage, out var text))
            {
                return text;
            }
        }
        return key;
    }

    public string this[string key] => Get(key);
}
