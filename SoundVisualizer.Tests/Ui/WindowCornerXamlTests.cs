// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Xml.Linq;

namespace SoundVisualizer.Tests.Ui;

/// <summary>
/// 앱이 직접 그리는 창(제목줄 없이 바깥 테두리를 Border 로 그리는 창)의 바깥 모서리는 보통 Windows 창처럼 직각이다(#37).
/// 창 안의 카드·버튼은 둥글어도 되고, 게임 위에 뜨는 투명 오버레이(MainWindow)는 창 테두리가 없어 보지 않는다.
/// </summary>
public class WindowCornerXamlTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly string[] WindowFiles =
    {
        "LauncherWindow.xaml",
        "Tutorial/TutorialWindow.xaml",
        "LicenseWindow.xaml",
        "ConfirmWindow.xaml",
        "ColorPickerWindow.xaml",
    };

    public static TheoryData<string> Windows => new(WindowFiles);

    [Theory]
    [MemberData(nameof(Windows))]
    public void 창의_바깥_테두리는_모서리가_직각이다(string file)
    {
        var window = XDocument.Parse(AppSource.Read(file.Split('/'))).Root!;
        Assert.Equal("None", (string?)window.Attribute("WindowStyle"));

        // 창의 내용은 리소스(Window.Resources 같은 속성 요소)를 뺀 첫 자식이다.
        var frame = window.Elements().First(e => !e.Name.LocalName.Contains('.', StringComparison.Ordinal));
        Assert.Equal(Presentation + "Border", frame.Name);
        string corner = (string?)frame.Attribute("CornerRadius") ?? "0";
        Assert.True(corner == "0", $"{file} 의 바깥 테두리가 둥급니다(CornerRadius=\"{corner}\").");
    }

    [Fact]
    public void 직접_그리는_창은_목록에_모두_있다()
    {
        // 새 창을 더하면 이 시험의 목록에도 넣어 바깥 모서리를 같이 지킨다.
        string root = Path.Combine(RepositoryPaths.Root, "SoundVisualizer");
        var listed = WindowFiles;
        var found = Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                        && !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Select(path => (Path: path, Root: XDocument.Load(path).Root!))
            .Where(x => x.Root.Name == Presentation + "Window" && (string?)x.Root.Attribute("WindowStyle") == "None")
            .Select(x => Path.GetRelativePath(root, x.Path).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(file => file != "MainWindow.xaml")
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(listed.Order(StringComparer.Ordinal).ToArray(), found);
    }

    [Fact]
    public void 제목줄_버튼은_모서리를_둥글리지_않는다()
    {
        var theme = XDocument.Parse(AppSource.Read("Themes", "Theme.xaml")).Root!;
        foreach (string key in new[] { "Button.Chrome", "Button.ChromeClose" })
        {
            var style = theme.Elements(Presentation + "Style").Single(s => (string?)s.Attribute(Xaml + "Key") == key);
            Assert.DoesNotContain(style.Descendants(), e => e.Attribute("CornerRadius") is { } c && c.Value != "0");
            Assert.DoesNotContain(style.Elements(Presentation + "Setter"), s => (string?)s.Attribute("Property") == "Tag");
        }
    }
}
