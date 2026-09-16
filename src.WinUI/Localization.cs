using SnipIt.Services;
namespace SnipIt;
internal static class NativeText
{
    internal static string Get(string value)
    {
        if (LocalizationService.Instance.CurrentLanguage == Language.Korean) return value;
        if (value.StartsWith("단축키로 화면을 캡처하고")) return "Capture and edit using keyboard shortcuts.\n\nRegion: drag to select / Esc to cancel\nEditor: pen, arrow, line, shapes, text, highlight, mosaic and crop\nCtrl+Z undo / Ctrl+Y redo\nCtrl+S save / Ctrl+C copy\nCtrl+mouse wheel zoom\nEsc closes the editor; in OCR mode it exits OCR.\n\nSettings includes capture and editor shortcuts, GIF quality and updates.";
        return English.GetValueOrDefault(value) ?? LocalizationService.Instance.TranslateKorean(value);
    }
    private static readonly Dictionary<string, string> English = Data.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.TrimEnd('\r').Split('|', 2)).ToDictionary(pair => pair[0], pair => pair[1]);
    private const string Data = """
 도구| tool
일반|General
Windows 자동 실행 시 트레이에서 시작|Start in tray at Windows login
도움말|Help
전역 단축키|Global shortcuts
에디터 단축키|Editor shortcuts
●  캡처 준비|●  Ready to capture
검정|Black
검증 완료. 작업을 마친 뒤 ‘업데이트 후 재시작’을 눌러 주세요.|Verified. Finish your work, then choose Update and restart.
굵게|Bold
글꼴|Font
글자 크기를 숫자로 입력해 주세요.|Enter a numeric font size.
기본 저장 폴더 선택|Choose default save folder
기본 저장 폴더|Default save folder
기본 저장 형식|Default image format
기울임|Italic
노랑|Yellow
녹화 시간을 숫자로 입력해 주세요.|Enter a numeric recording duration.
녹화 완료 · 저장|Stop and save recording
녹화 중…|Recording…
녹화 창을 캡처 영역 밖으로 옮겨 주세요.|Move the recording controls outside the capture area.
다른 캡처 열기|Open another capture
다시 실행 (Ctrl+Y)|Redo (Ctrl+Y)
다운로드 시간이 초과됐습니다. 다시 시도해 주세요.|Download timed out. Please try again.
다운로드 실패: |Download failed: 
다운로드|Download
다운로드를 취소했습니다.|Download cancelled.
다운로드한 새 버전이 준비돼 있어요. 업데이트 후 재시작을 선택해 주세요.|The downloaded update is ready. Choose Update and restart.
닫기 (Esc)|Close (Esc)
닫기|Close
더 좋아진 SnipIt|A better SnipIt
두께|Width
두께와 글자 크기를 숫자로 입력해 주세요.|Enter numeric stroke width and font size.
드래그해서 영역 선택 · Esc 취소|Drag to select · Esc to cancel
마우스 커서 포함|Include mouse cursor
블러|Blur
빠른 저장 시 경로 자동 지정|Quick save to the default folder
빨강|Red
사용자 지정 색상|Custom color
새 버전 자동 확인|Check for updates automatically
새 버전 확인|Check for updates
새 버전을 확인해 보세요.|Check for a new version.
새 캡처 열기|Open new capture
색상 선택|Choose color
서버 응답이 늦습니다. 나중에 다시 확인해 주세요.|The server is taking too long. Try again later.
선|Line
선택 복사|Copy selection
선택 이력 삭제|Delete selected capture
선택 화면 어둡기|Capture overlay dimming
선택한 도구|Selected tool
선택한 캡처를 삭제할까요?|Delete the selected capture?
설치 실패: |Installation failed: 
실행 취소 (Ctrl+Z)|Undo (Ctrl+Z)
앱을 종료하고 새 버전으로 다시 시작할까요?|Close the app and restart with the new version?
앱이 종료 중입니다.|The app is closing.
언어 (새로 여는 창부터 적용)|Language (applies to newly opened windows)
업데이트 다운로드 중…|Downloading update…
업데이트 설치|Install update
업데이트 자동 다운로드|Download updates automatically
업데이트 확인 실패: |Update check failed: 
업데이트 후 재시작|Update and restart
업데이트|Updates
영역 캡처 단축키|Region capture shortcut
영역 캡처|Region capture
원본 100%|Actual size (100%)
원본|Original
이력 삭제|Delete history
이미지 처리 중…|Processing image…
이미지에서 단어를 선택하거나 아래 텍스트를 편집하세요.|Select words in the image or edit the text below.
인식된 텍스트가 없습니다.|No text was recognized.
인식된 텍스트가 없습니다|No text was recognized
저장 (S)|Save (S)
저장된 캡처 이력을 모두 삭제할까요?|Delete all saved capture history?
저장된 캡처가 없습니다.|No saved captures.
저장하지 않은 편집 내용을 닫고 새 캡처를 열까요? 새 캡처 원본은 캡처 이력에 보관됩니다.|Discard unsaved edits and open the new capture? The original capture is kept in history.
저장하지 않은 편집 내용을 닫고 선택한 캡처를 열까요?|Discard unsaved edits and open the selected capture?
저장하지 않은 편집 내용을 닫을까요?|Close and discard unsaved edits?
저장했습니다.|Saved.
적용|Apply
전체 선택·복사|Select all and copy
전체 이력 삭제|Clear history
전체 텍스트|Full text
전체 화면 단축키|Full screen shortcut
전체 화면|Full screen
존재하는 저장 폴더를 입력해 주세요.|Enter an existing save folder.
준비|Ready
중복 단축키를 변경해 주세요. Ctrl+Shift+E는 최근 캡처 편집에 사용됩니다.|Choose unique shortcuts. Ctrl+Shift+E is reserved for the latest capture.
중복 제거 + 크기 50%|Skip duplicates and scale to 50%
중복 프레임 제거|Skip duplicate frames
직선|Line
초기 이미지 배율|Initial image zoom
초록|Green
최근 캡처 열기 실패: |Could not open latest capture: 
최근 캡처 편집|Edit latest capture
취소 · Esc|Cancel · Esc
캡처 단축키 기본값|Reset capture shortcuts
캡처 소리|Capture sound
캡처 실패: |Capture failed: 
캡처 완료 · 클립보드에 복사됨|Captured and copied to clipboard
캡처 이력 저장 실패: |Could not save capture history: 
캡처 후 클립보드 복사|Copy captures and edits to clipboard
캡처할 창을 선택한 뒤 활성 창 단축키를 사용해 주세요.|Select a window and use the active window shortcut.
커서 오른쪽 아래|Below-right of cursor
커서 오른쪽 위|Above-right of cursor
커서 왼쪽 아래|Below-left of cursor
커서 왼쪽 위|Above-left of cursor
클립보드 복사 실패: |Clipboard copy failed: 
클립보드에 복사했습니다.|Copied to clipboard.
텍스트 인식 중…|Recognizing text…
텍스트 인식|Recognize text
텍스트 입력 · Enter 완료 / Esc 취소|Type text · Enter to apply / Esc to cancel
텍스트를 클릭하거나 드래그해 선택하세요.|Click or drag to select text.
트레이에서 시작|Start in system tray
파랑|Blue
편집 (E)|Edit (E)
편집 단축키 기본값|Reset editor shortcuts
편집 도구 · |Editor tool · 
편집 도구|Editor tool
편집·녹화·캡처를 마친 뒤 업데이트해 주세요.|Finish editing, recording and capturing before updating.
편집기 닫기|Close editor
편집기 열기 실패: |Could not open editor: 
편집기와 녹화를 종료한 뒤 앱을 종료해 주세요.|Close the editor and recording before exiting.
편집창 없이 캡처|Capture without opening editor
폴더 선택|Choose folder
필요한 부분만, 선명하게|Capture exactly what you need
현재 단축키 다시 등록|Register current shortcuts again
현재 버전 v|Current version v
현재 편집 작업을 마친 뒤 캡처 이력에서 새 캡처를 열어 주세요.|Finish the current edit, then open the new capture from history.
화면 맞춤|Fit to window
화면 오른쪽 아래|Bottom-right of screen
화면 오른쪽 위|Top-right of screen
화면 왼쪽 아래|Bottom-left of screen
화면 왼쪽 위|Top-left of screen
확대경 위치|Magnifier position
확인|Confirm
확인을 취소했습니다.|Check cancelled.
활성 창 단축키|Active window shortcut
흰색|White
GIF 녹화 단축키|GIF recording shortcut
GIF 녹화|Record GIF
GIF 최대 녹화 시간(초)|Maximum recording duration (seconds)
GIF 품질|GIF quality
GIF 프레임|GIF frame rate
GitHub에서 최신 버전을 확인하고 있어요…|Checking GitHub for the latest release…
OCR · 클릭 / Ctrl 다중 선택 / 드래그|OCR · Click / Ctrl multi-select / Drag
PNG (무손실)|PNG (lossless)
SnipIt 도움말|SnipIt Help
SnipIt 설정|SnipIt Settings
SnipIt 업데이트|SnipIt Updates
SnipIt 영역 선택|SnipIt Region Capture
SnipIt 캡처 완료|SnipIt Capture Complete
SnipIt 편집|SnipIt Editor
SnipIt GIF 녹화|SnipIt GIF Recording
Windows 시작 시 실행|Run at Windows startup
녹화|Recording
프레임|frames
텍스트 복사됨|Text copied
개 단어|words
새 버전|New version
최신 버전을 사용 중입니다.|You are up to date.
버전이 준비됐어요.|is ready.
출시 · 업데이트에서 확인하세요.|released · See Updates.
단축키 등록 실패: |Could not register shortcut: 
Esc 취소|Esc to cancel
""";
}
