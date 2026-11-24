// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Xml.Linq;

namespace SoundVisualizer.Tests.Ui;

/// <summary>
/// 오버레이를 표시할 모니터(#29)의 화면 배선을 소스로 확인한다. 모니터를 두 대 쓰는 사용자가 "어떻게 설정해야 할지 모르겠다"고 알려 왔고,
/// 그때는 오버레이가 뜰 모니터를 정할 방법이 없었다. 런처와 오버레이는 WPF 창이라 단위 시험에서 띄우지 않는다.
/// 어느 모니터에 띄울지의 규칙은 Display/OverlayDisplaysTests 가, 칸의 글은 Display/DisplayChoiceTextTests 가 본다.
/// </summary>
public class OverlayDisplaySourceTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XElement Named(XDocument document, string name) =>
        document.Descendants().Single(e => (string?)e.Attribute(Xaml + "Name") == name);

    private static XElement StyleKeyed(XDocument document, string key) =>
        document.Descendants().Single(e => e.Name.LocalName == "Style" && (string?)e.Attribute(Xaml + "Key") == key);

    private static bool Sets(XElement style, string property, string value) =>
        style.Elements().Any(setter => (string?)setter.Attribute("Property") == property && (string?)setter.Attribute("Value") == value);

    // 한 시험 안에서는 같은 문서의 요소끼리 견준다. 그래서 시험마다 한 번만 읽는다.
    private static XDocument Launcher() => XDocument.Parse(AppSource.Read("LauncherWindow.xaml"));
    private static XDocument EasyView() => XDocument.Parse(AppSource.Read("Ui", "EasySettingsView.xaml"));
    private static XDocument Picker() => XDocument.Parse(AppSource.Read("Ui", "DisplayPicker.xaml"));

    [Fact]
    public void 오버레이는_띄우기_전에_선택한_모니터로_옮긴다()
    {
        string overlay = AppSource.Read("MainWindow.xaml.cs");
        // 창 핸들이 생긴 뒤의 처리는 등록한 차례로 돈다. 클릭 관통을 먼저 걸고 그다음에 자리를 잡는다(숨은 데스크톱에서 이 차례로 확인했다).
        int clickThrough = overlay.IndexOf("ApplyClickThroughMagic();", StringComparison.Ordinal);
        int display = overlay.IndexOf("InitOverlayDisplay();", StringComparison.Ordinal);
        Assert.True(clickThrough >= 0 && display > clickThrough, "InitOverlayDisplay 는 ApplyClickThroughMagic 다음에 부릅니다.");

        string part = AppSource.Read("MainWindow.Display.cs");
        Assert.Contains("SourceInitialized += (s, e) => MoveToSelectedDisplay();", AppSource.MethodBody(part, "InitOverlayDisplay"));

        string move = AppSource.MethodBody(part, "MoveToSelectedDisplay");
        Assert.Contains("OverlayDisplays.Resolve(DisplayEnumerator.Current(), AppSettings.OverlayDisplay)", move);
        // 모니터를 읽지 못했으면(null) 옮기지 않는다. 예전처럼 Windows 가 정한 자리에 뜬다.
        Assert.Contains("if (display != null) OverlayPlacement.MoveTo(this, display);", move);
        // 창을 옮기다 실패해도 오버레이는 떠야 한다.
        Assert.Contains("catch (Exception ex)", move);
    }

    [Fact]
    public void 떠_있는_창은_최대화를_풀지_않고_옮겨_초점을_빼앗지_않는다()
    {
        string placement = AppSource.Read("Ui", "OverlayPlacement.cs");
        string move = placement[placement.IndexOf("public static bool MoveTo(", StringComparison.Ordinal)..placement.IndexOf("public static string CurrentDeviceName(", StringComparison.Ordinal)];
        int handle = move.IndexOf("if (hwnd == IntPtr.Zero) return false;", StringComparison.Ordinal);
        int guard = move.IndexOf("if (window.WindowState == WindowState.Maximized && IsOn(hwnd, display)) return false;", StringComparison.Ordinal);
        int visible = move.IndexOf("if (window.IsVisible && window.WindowState == WindowState.Maximized", StringComparison.Ordinal);
        int normal = move.IndexOf("window.WindowState = WindowState.Normal;", StringComparison.Ordinal);
        int maximized = move.IndexOf("window.WindowState = WindowState.Maximized;", StringComparison.Ordinal);

        // 창 핸들이 없으면 옮길 것이 없고, 이미 그 모니터에 최대화되어 있으면(모니터 한 대, 주 모니터 그대로) 아무 일도 하지 않는다.
        Assert.True(handle >= 0 && handle < guard && guard < visible && visible < normal && normal < maximized,
            "핸들 확인 → 같은 모니터면 그대로 → 떠 있는 창 → 띄우기 전 창 차례입니다.");

        // 떠 있는 창은 WindowState 를 바꾸지 않는다. 바꾸면 WPF 가 ShowWindow(SW_RESTORE·SW_MAXIMIZE)를 불러 오버레이를 활성 창으로 만들고,
        // 런처나 게임이 키보드 초점을 잃는다(숨은 데스크톱에서 확인했다). 최대화한 창이 모니터 밖으로 넘친 만큼(테두리 두께)을 그대로 옮긴다.
        string shown = move[visible..normal];
        Assert.DoesNotContain("WindowState = WindowState.", shown);
        Assert.Contains("int left = display.Left + (now.Left - from.Left);", shown);
        Assert.Contains("int top = display.Top + (now.Top - from.Top);", shown);
        Assert.Contains("int right = display.Right + (now.Right - from.Right);", shown);
        Assert.Contains("int bottom = display.Bottom + (now.Bottom - from.Bottom);", shown);
        Assert.Contains("SetWindowPos(hwnd, IntPtr.Zero, left, top, right - left, bottom - top, SWP_NOZORDER | SWP_NOACTIVATE);", shown);
        Assert.Contains("return true;", shown);

        // 띄우기 전에는 최대화를 풀고 → 옮기고 → 다시 최대화한다. 보이지 않는 동안에는 WPF 가 ShowWindow 를 부르지 않는다.
        Assert.Contains("SetWindowPos(hwnd, IntPtr.Zero, display.Left, display.Top, display.Width, display.Height, SWP_NOZORDER | SWP_NOACTIVATE);", move[normal..maximized]);
    }

    [Fact]
    public void 런처는_칸의_알림과_모니터_구성의_변화를_한_곳에서_받는다()
    {
        string launcher = AppSource.Read("LauncherWindow.xaml.cs");
        Assert.Contains("WatchDisplays();", launcher);
        Assert.Contains("UnwatchDisplays();", AppSource.MethodBody(launcher, "OnClosed"));

        string part = AppSource.Read("LauncherWindow.Display.cs");
        string watch = AppSource.MethodBody(part, "WatchDisplays");
        Assert.Contains("AddHandler(DisplayPicker.DisplayChangedEvent, new RoutedEventHandler(OnOverlayDisplayChanged));", watch);
        Assert.Contains("SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;", watch);
        // 프로세스가 끝날 때까지 살아 있는 알림이라 창을 닫을 때 떼지 않으면 닫힌 창이 남는다.
        Assert.Contains("SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;", AppSource.MethodBody(part, "UnwatchDisplays"));
        // Windows 의 알림은 다른 스레드에서 올 수 있다.
        Assert.Contains("private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.InvokeAsync(OnDisplaysChanged);", part);
        Assert.Contains("private void OnOverlayDisplayChanged(object sender, RoutedEventArgs e) => ApplyOverlayDisplay();", part);
    }

    [Fact]
    public void 모니터_목록은_잠깐_읽어_둔_것을_쓰고_구성이_바뀌었다는_알림에는_먼저_다시_읽는다()
    {
        // 런처는 설정 화면을 다시 그릴 때마다 모니터를 묻고, 오버레이 편집 패널에서 슬라이더를 끄는 동안에는 값이 바뀔 때마다 다시 그린다.
        // 그때마다 Windows 에 묻지 않도록 읽어 둔 것을 쓴다(Display/BriefCacheTests 가 규칙을 본다).
        string enumerator = File.ReadAllText(Path.Combine(RepositoryPaths.Root, "SoundVisualizer.Core", "Display", "DisplayEnumerator.cs"));
        Assert.Contains("private static readonly BriefCache<IReadOnlyList<DisplayInfo>> Cache = new(Read, FreshMilliseconds);", enumerator);
        Assert.Contains("public static IReadOnlyList<DisplayInfo> Current() => Cache.Get();", enumerator);
        Assert.Contains("public static IReadOnlyList<DisplayInfo> Refresh() => Cache.Refresh();", enumerator);

        // 구성이 바뀐 바로 그때는 읽어 둔 것이 옛것이다. 버리고 새로 읽은 뒤에 화면과 오버레이를 맞춘다.
        string changed = AppSource.MethodBody(AppSource.Read("LauncherWindow.Display.cs"), "OnDisplaysChanged");
        int refresh = changed.IndexOf("DisplayEnumerator.Refresh();", StringComparison.Ordinal);
        int apply = changed.IndexOf("ApplyOverlayDisplay();", StringComparison.Ordinal);
        Assert.True(refresh >= 0 && refresh < apply, "다시 읽고 → 맞춥니다.");
    }

    [Fact]
    public void 모니터를_바꾸면_두_화면의_칸과_편집_패널을_맞추고_오버레이를_옮기며_최대_프레임의_저장값은_건드리지_않는다()
    {
        string part = AppSource.Read("LauncherWindow.Display.cs");
        string apply = AppSource.MethodBody(part, "ApplyOverlayDisplay");

        // 주사율이 낮은 모니터를 거쳐도(화살표로 칸을 지나가기, 모니터 절전, 60Hz 로 바꾸는 전체 화면 게임) 사용자가 정한 최대 프레임은
        // 남아야 한다. 그리기는 어차피 화면 주사율보다 자주 돌지 않는다.
        Assert.DoesNotContain("TargetFps", apply);
        Assert.DoesNotContain("AppSettings.Save", apply);

        int init = apply.IndexOf("_isInitializing = true;", StringComparison.Ordinal);
        int load = apply.IndexOf("LoadSettingsToUI();", StringComparison.Ordinal);
        int restore = apply.IndexOf("_isInitializing = wasInitializing;", StringComparison.Ordinal);
        int sync = apply.IndexOf("SyncOverlayEditPanel();", StringComparison.Ordinal);
        int move = apply.IndexOf("_overlayWindow?.MoveToSelectedDisplay();", StringComparison.Ordinal);
        Assert.True(init >= 0 && init < load && load < restore && restore < sync && sync < move,
            "칸을 맞추고(바뀜 알림이 저장하지 않게) → 편집 패널을 맞추고 → 오버레이를 옮깁니다.");

        // 슬라이더 끝값이 낮아지면 WPF 가 보이는 값을 끝값으로 좁힌다. 이 함수는 초기화 중이 아닐 때도 불리므로(오버레이 알림, 색 변경)
        // 그 바뀜이 Setting_Changed 로 저장되지 않게 막는다. 주사율이 높은 모니터로 돌아오면 저장값이 다시 보인다.
        string launcher = AppSource.Read("LauncherWindow.xaml.cs");
        string loadBody = AppSource.MethodBody(launcher, "LoadSettingsToUI");
        int cards = loadBody.IndexOf("ReloadDisplayCards();", StringComparison.Ordinal);
        int guardOn = loadBody.IndexOf("_isInitializing = true;", StringComparison.Ordinal);
        int maximum = loadBody.IndexOf("SldTargetFps.Maximum = AppSettings.GetMonitorRefreshRate();", StringComparison.Ordinal);
        int value = loadBody.IndexOf("SldTargetFps.Value = AppSettings.TargetFps;", StringComparison.Ordinal);
        int guardOff = loadBody.IndexOf("_isInitializing = wasInitializing;", StringComparison.Ordinal);
        Assert.True(cards >= 0 && cards < guardOn && guardOn < maximum && maximum < value && value < guardOff,
            "모니터 칸 → (저장 막기) 최대 프레임 끝값·값 → (막기 풀기) 차례입니다.");
        Assert.Contains("EasyView?.Reload();", loadBody);
        // 화면 언어를 바꾸면 칸의 이름("모니터 1", 주 모니터)도 다시 넣는다.
        Assert.Contains("ReloadDisplayCards();", AppSource.MethodBody(launcher, "ApplyCodeTexts"));

        string reload = AppSource.MethodBody(part, "ReloadDisplayCards");
        Assert.Contains("DisplayCardsDetailed.Reload();", reload);
        Assert.Contains("Show(PanelOverlayDisplay, DisplayCardsDetailed.HasChoices);", reload);

        // 쉬운 설정은 선택을 맞출 때마다(ReloadSelection → ApplyCodeTexts) 모니터 칸도 맞춘다.
        string easy = AppSource.Read("Ui", "EasySettingsView.xaml.cs");
        Assert.Contains("ApplyCodeTexts();", AppSource.MethodBody(easy, "ReloadSelection"));
        string easyTexts = AppSource.MethodBody(easy, "ApplyCodeTexts");
        Assert.Contains("DisplayCards.Reload();", easyTexts);
        Assert.Contains("PanelDisplay.Visibility = DisplayCards.HasChoices ? Visibility.Visible : Visibility.Collapsed;", easyTexts);

        // 시작할 때도 읽은 최대 프레임을 주사율로 깎지 않는다(동작은 AppSettingsTests 가 본다). 주사율을 모르는 값(0, 1)은 쓰지 않는다.
        string settings = File.ReadAllText(Path.Combine(RepositoryPaths.Root, "SoundVisualizer.Core", "AppSettings.cs"));
        Assert.DoesNotContain("if (TargetFps > maxRate)", settings);
        Assert.Contains("return rate > 1 ? rate : 60;", settings);
    }

    [Fact]
    public void 모든_설정을_되돌리면_떠_있는_오버레이도_주_모니터로_옮긴다()
    {
        // 되돌리기는 표시할 모니터도 기본값(주 모니터)으로 바꾼다. 오버레이가 다른 모니터에 떠 있으면 칸만 바뀌고 오버레이는 남아 있었다.
        string reset = AppSource.MethodBody(AppSource.Read("LauncherWindow.xaml.cs"), "BtnReset_Click");
        int defaults = reset.IndexOf("AppSettings.ResetToDefaults();", StringComparison.Ordinal);
        int move = reset.IndexOf("_overlayWindow?.MoveToSelectedDisplay();", StringComparison.Ordinal);
        Assert.True(defaults >= 0 && move > defaults, "기본값으로 되돌린 뒤 오버레이를 옮깁니다.");

        // 기본값의 최대 프레임은 주 모니터의 주사율까지다. 모니터를 먼저 되돌려야 그 모니터의 주사율로 자른다.
        string settings = File.ReadAllText(Path.Combine(RepositoryPaths.Root, "SoundVisualizer.Core", "AppSettings.cs"));
        string body = settings[settings.IndexOf("public static void ResetToDefaults()", StringComparison.Ordinal)..];
        int display = body.IndexOf("OverlayDisplay = OverlayDisplays.Primary;", StringComparison.Ordinal);
        int fps = body.IndexOf("TargetFps = Math.Min(DefaultTargetFps, GetMonitorRefreshRate());", StringComparison.Ordinal);
        Assert.True(display >= 0 && fps > display, "표시할 모니터를 되돌린 뒤 최대 프레임을 정합니다.");
    }

    [Fact]
    public void 모니터_구역은_두_화면에_있고_모니터가_한_대뿐이면_보이지_않는다()
    {
        // 처음에는 접어 두고, 모니터가 두 대 이상일 때만 코드가 편다. 한 대만 쓰는 사람의 화면은 예전과 같다.
        var easy = EasyView();
        var easyPanel = Named(easy, "PanelDisplay");
        Assert.Equal("Collapsed", (string?)easyPanel.Attribute("Visibility"));
        var easyCards = Named(easy, "DisplayCards");
        Assert.Equal("DisplayPicker", easyCards.Name.LocalName);
        Assert.Contains(easyCards, easyPanel.Descendants());
        Assert.Contains(easyPanel.Descendants(), e => (string?)e.Attribute("Text") == "{l:Loc settings.display.title}");
        // 쉬운 설정에서는 소리 종류 다음, 맨 아래에 온다.
        var easyOrder = easyPanel.Parent!.Elements().ToList();
        Assert.Same(easyPanel, easyOrder[^1]);
        var typesCard = Named(easy, "ChkAmbient").Ancestors().First(e => easyOrder.Contains(e));
        Assert.True(easyOrder.IndexOf(typesCard) < easyOrder.IndexOf(easyPanel));

        var launcher = Launcher();
        var detailedPanel = Named(launcher, "PanelOverlayDisplay");
        Assert.Equal("Collapsed", (string?)detailedPanel.Attribute("Visibility"));
        var detailedCards = Named(launcher, "DisplayCardsDetailed");
        Assert.Equal("DisplayPicker", detailedCards.Name.LocalName);
        Assert.Contains(detailedCards, detailedPanel.Descendants());
        Assert.Contains(detailedPanel.Descendants(), e => (string?)e.Attribute("Text") == "{l:Loc settings.display.title}");
        Assert.Contains(detailedPanel.Descendants(), e => (string?)e.Attribute("Text") == "{l:Loc settings.display.desc}");
        // 자세한 설정에서는 사운드 모드와 최대 프레임 사이에 있다. 최대 프레임의 끝값이 이 모니터의 주사율이다.
        var siblings = detailedPanel.Parent!.Elements().ToList();
        int soundMode = siblings.IndexOf(Named(launcher, "ChipsSoundMode"));
        int panel = siblings.IndexOf(detailedPanel);
        int maxFps = siblings.IndexOf(Named(launcher, "SldTargetFps"));
        Assert.True(soundMode >= 0 && soundMode < panel && panel < maxFps, "사운드 모드 → 표시할 모니터 → 최대 프레임 차례입니다.");
    }

    [Fact]
    public void 칸은_선택을_저장하고_알리며_연결되지_않은_선택은_지우지_않고_어느_칸도_선택해_두지_않는다()
    {
        string picker = AppSource.Read("Ui", "DisplayPicker.xaml.cs");
        string changed = AppSource.MethodBody(picker, "Cards_SelectionChanged");
        // 코드가 칸을 맞추는 동안의 바뀜은 사용자가 선택한 것이 아니다.
        Assert.StartsWith("if (_loading) return;", changed.TrimStart('{').TrimStart());
        int write = changed.IndexOf("AppSettings.OverlayDisplay = key;", StringComparison.Ordinal);
        int save = changed.IndexOf("AppSettings.Save();", StringComparison.Ordinal);
        int raise = changed.IndexOf("RaiseEvent(new RoutedEventArgs(DisplayChangedEvent, this));", StringComparison.Ordinal);
        Assert.True(write >= 0 && write < save && save < raise, "값을 쓰고 → 저장을 예약하고 → 알립니다.");
        Assert.Contains("string key = choice.Display.Key;", changed[..write]);
        // 알림은 창까지 올라가야 런처가 한 곳에서 받는다.
        Assert.Contains("RoutingStrategy.Bubble", picker);

        string reload = AppSource.MethodBody(picker, "Reload");
        // 선택해 둔 모니터가 연결되어 있지 않으면 어느 칸도 선택해 두지 않는다(쉬운 설정의 크기 칸 #425 와 같다). 주 모니터 칸을 선택해 두면
        // 그 칸을 눌러도 선택이 바뀌지 않아 '주 모니터에 두기'를 저장할 수 없었다. 저장된 값은 건드리지 않아, 다시 연결하면 돌아간다.
        Assert.Contains("bool missing = OverlayDisplays.IsMissing(ordered, saved);", reload);
        Assert.Contains("var target = missing ? null : OverlayDisplays.Resolve(ordered, saved);", reload);
        Assert.Contains("else Cards.ClearSelection();", reload);
        Assert.Contains("HasChoices = ordered.Count >= 2;", reload);
        Assert.Contains("bool showMissing = HasChoices && missing;", reload);
        Assert.Contains("TxtMissing.Visibility = showMissing ? Visibility.Visible : Visibility.Collapsed;", reload);
        Assert.DoesNotContain("AppSettings.OverlayDisplay =", reload);
        Assert.DoesNotContain("AppSettings.Save", reload);
        // 모니터 구성과 언어가 그대로면 칸을 다시 만들지 않는다(키보드 초점이 남는다). 다시 만들 때 칸에 있던 초점은 되돌려 준다.
        Assert.Contains("if (signature != _signature)", reload);
        Assert.Contains("bool hadFocus = Cards.IsKeyboardFocusWithin;", reload[..reload.IndexOf("FillCards(ordered);", StringComparison.Ordinal)]);
        Assert.Contains("if (rebuilt && hadFocus) Dispatcher.InvokeAsync(RestoreFocus, DispatcherPriority.Loaded);", reload);
        Assert.Contains("IsActive: true", AppSource.MethodBody(picker, "RestoreFocus"));

        // 안내는 화면 읽어주기가 읽는다. WPF 는 LiveSetting 만으로는 읽지 않아 직접 알린다. 칸이 보일 때, 배치가 끝난 뒤, 나타날 때마다 한 번이다.
        Assert.DoesNotContain("RaiseAutomationEvent", reload);
        Assert.Contains("if (showMissing) Dispatcher.InvokeAsync(AnnounceMissing, DispatcherPriority.Loaded);", reload);
        Assert.Contains("if (!showMissing) _missingAnnounced = false;", reload);
        string announce = AppSource.MethodBody(picker, "AnnounceMissing");
        Assert.Contains("if (_missingAnnounced || !TxtMissing.IsVisible) return;", announce);
        Assert.Contains("UIElementAutomationPeer.CreatePeerForElement(TxtMissing)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);", announce);
        Assert.Contains("if (IsVisible) Dispatcher.InvokeAsync(AnnounceMissing, DispatcherPriority.Loaded);", picker);

        var missingText = Named(Picker(), "TxtMissing");
        Assert.Equal("{l:Loc settings.display.missing}", (string?)missingText.Attribute("Text"));
        Assert.Equal("Collapsed", (string?)missingText.Attribute("Visibility"));
        Assert.Equal("Polite", (string?)missingText.Attribute("AutomationProperties.LiveSetting"));
    }

    [Fact]
    public void 칸의_차례와_그림과_해상도는_오른쪽에서_왼쪽으로_쓰는_언어에서도_뒤집지_않는다()
    {
        var picker = Picker();
        // 칸의 차례는 모니터가 놓인 차례다. 아랍어에서 줄을 뒤집으면 왼쪽 모니터의 칸이 오른쪽에 온다.
        Assert.True(Sets(StyleKeyed(picker, "DisplayCards"), "FlowDirection", "LeftToRight"));

        // 배치 그림은 실제 놓인 방향이다.
        Assert.Contains("FlowDirection = FlowDirection.LeftToRight;", AppSource.Read("Ui", "DisplayLayoutIcon.cs"));

        // 해상도는 숫자뿐이다. 아랍어의 흐름을 따르면 "1080 × 1920" 으로 보인다.
        var size = picker.Descendants().Single(e => e.Name.LocalName == "TextBlock" && ((string?)e.Attribute("Text") ?? "").Contains("Tag.Size"));
        Assert.Equal("LeftToRight", (string?)size.Attribute("FlowDirection"));

        // 체크 표시도 뒤집지 않는다(쉬운 설정의 칸과 같다).
        Assert.True(Sets(StyleKeyed(picker, "CheckMark"), "FlowDirection", "LeftToRight"));
    }

    [Fact]
    public void 도움말은_설정의_이름을_넣어_알려_준다()
    {
        Assert.Contains("TxtDisplayHelp.Text = Loc.Format(\"help.display.body\", Loc.Get(\"settings.display.title\"));",
            AppSource.MethodBody(AppSource.Read("HelpTab.xaml.cs"), "ApplyTexts"));
        var text = Named(XDocument.Parse(AppSource.Read("HelpTab.xaml")), "TxtDisplayHelp");
        Assert.Contains(text.Parent!.Elements(), e => (string?)e.Attribute("Text") == "{l:Loc help.display.title}");
    }
}
