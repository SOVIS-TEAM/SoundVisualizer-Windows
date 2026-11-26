// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Globalization;
using System.Xml.Linq;

namespace SoundVisualizer.Tests.Ui;

/// <summary>런처 창 틀(#44): 크기 조절, 최대화 버튼, 기본·최소 크기, 직각 모서리. WPF 창을 띄우지 않고 소스를 읽어 지킨다.</summary>
public class LauncherFrameTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace Ui = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static XElement Launcher => XDocument.Parse(AppSource.Read("LauncherWindow.xaml")).Root!;

    private static double Number(XElement element, string name) =>
        double.Parse((string?)element.Attribute(name) ?? "0", CultureInfo.InvariantCulture);

    [Fact]
    public void 크기를_바꿀_수_있는_Windows_창_틀을_쓴다()
    {
        var window = Launcher;
        // 테두리 없는 창(None)은 최대화하면 작업 표시줄을 덮고 가장자리로 크기를 바꿀 수 없다.
        Assert.Equal("SingleBorderWindow", (string?)window.Attribute("WindowStyle"));
        Assert.Equal("CanResize", (string?)window.Attribute("ResizeMode"));
        Assert.Null(window.Attribute("AllowsTransparency"));
        var chrome = window.Descendants(Presentation + "WindowChrome").Single();
        Assert.Equal("0", (string?)chrome.Attribute("CaptionHeight"));
        Assert.True(double.Parse((string?)chrome.Attribute("ResizeBorderThickness") ?? "0", CultureInfo.InvariantCulture) >= 4);
    }

    [Fact]
    public void 기본_크기는_880x700_이고_최소_크기가_있다()
    {
        var window = Launcher;
        Assert.Equal(880, Number(window, "Width"));
        Assert.Equal(700, Number(window, "Height"));
        Assert.True(Number(window, "MinWidth") is >= 600 and < 880);
        Assert.True(Number(window, "MinHeight") is >= 500 and < 700);
    }

    [Fact]
    public void 최소화_최대화_닫기_버튼이_있고_모서리에서도_누를_수_있다()
    {
        var buttons = Launcher.Descendants(Presentation + "Button")
            .Where(b => ((string?)b.Attribute("Style") ?? "").Contains("Button.Chrome", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(new[] { "BtnMinimize_Click", "BtnMaximize_Click", "BtnClose_Click" }, buttons.Select(b => (string?)b.Attribute("Click")));
        Assert.All(buttons, b => Assert.Equal("True", (string?)b.Attribute("WindowChrome.IsHitTestVisibleInChrome")));
    }

    [Fact]
    public void 최대화하면_화면_밖으로_나간_만큼_여백을_두고_모서리는_둥글리지_않는다()
    {
        string frame = AppSource.Read("LauncherWindow.Frame.cs");
        // Windows 는 틀 있는 창을 최대화하면 틀 두께만큼 화면 밖으로 내보낸다. 틀까지 앱이 그리므로 그만큼 안쪽 여백을 둔다.
        Assert.Contains("RootFrame.Margin = new Thickness(", frame);
        Assert.Contains("info.Work.Left - window.Left", frame);
        Assert.Contains("window.Bottom - info.Work.Bottom", frame);
        Assert.Contains("TransformFromDevice", frame);
        Assert.Contains("DwmWindowCornerPreference = 33", frame);
        Assert.Contains("DwmCornerDoNotRound = 1", frame);
        Assert.Contains("x:Name=\"RootFrame\"", AppSource.Read("LauncherWindow.xaml"));
    }

    [Fact]
    public void 넓혀도_내용은_1200_까지만_늘고_창_버튼은_모서리에_남는다()
    {
        var column = Launcher.Descendants().Single(e => (string?)e.Attribute(Xaml + "Name") == "ContentColumn");
        Assert.Equal(1200, Number(column, "MaxWidth"));
        // 창 버튼이 내용 칸 안에 있으면 최대화했을 때 화면 가운데로 따라 들어온다.
        string[] chrome = { "BtnMinimize_Click", "BtnMaximize_Click", "BtnClose_Click" };
        Assert.DoesNotContain(column.Descendants(Presentation + "Button"), b => chrome.Contains((string?)b.Attribute("Click")));
        Assert.Equal(3, Launcher.Descendants(Presentation + "Button").Count(b => chrome.Contains((string?)b.Attribute("Click"))));
    }

    [Fact]
    public void 최대화_버튼의_이름은_상태에_따라_바뀐다()
    {
        string frame = AppSource.Read("LauncherWindow.Frame.cs");
        Assert.Contains("\"common.restore\"", frame);
        Assert.Contains("\"common.maximize\"", frame);
        int handler = frame.IndexOf("StateChanged +=", StringComparison.Ordinal);
        Assert.True(handler >= 0, "창 상태가 바뀔 때 버튼을 맞추지 않습니다.");
        Assert.Contains("UpdateMaximizeButton();", frame[handler..frame.IndexOf("};", handler, StringComparison.Ordinal)]);
    }
}
