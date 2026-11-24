// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Display;
using SoundVisualizer.Localization;

namespace SoundVisualizer.Tests.Display;

/// <summary>모니터 선택 칸(#29)에 적는 글과, 칸을 다시 만들지 가리는 표식.</summary>
public class DisplayChoiceTextTests
{
    private static readonly DisplayInfo Left = new(@"\\.\DISPLAY2", "left", 0, 0, 1920, 1080, IsPrimary: true, RefreshRate: 144);
    private static readonly DisplayInfo Right = new(@"\\.\DISPLAY1", "right", 1920, 0, 2560, 1440, IsPrimary: false, RefreshRate: 60, PixelWidth: 3840, PixelHeight: 2160);

    public static TheoryData<string> Codes()
    {
        var data = new TheoryData<string>();
        foreach (var code in StringTables.AvailableCodes) data.Add(code);
        return data;
    }

    [Fact]
    public void 칸의_이름과_해상도는_문자열_표의_틀에_번호와_실제_해상도를_넣는다()
    {
        Assert.Equal("모니터 2", DisplayChoiceText.Title("모니터 {0}", 2));
        Assert.Equal("1920 × 1080", DisplayChoiceText.Size("{0} × {1}", Left));
        // 배율이 다른 모니터는 실제 해상도를 적는다.
        Assert.Equal("3840 × 2160", DisplayChoiceText.Size("{0} × {1}", Right));
    }

    [Fact]
    public void 화면_읽어주기_이름은_이름_해상도_주_모니터를_차례로_잇는다()
    {
        Assert.Equal("모니터 2, 1920 × 1080, 주 모니터", DisplayChoiceText.Name("{0}, {1}", "모니터 2", "1920 × 1080", "주 모니터"));
        Assert.Equal("모니터 1, 3840 × 2160", DisplayChoiceText.Name("{0}, {1}", "모니터 1", "3840 × 2160", ""));
        Assert.Equal("모니터 1, 3840 × 2160", DisplayChoiceText.Name("{0}, {1}", "모니터 1", "3840 × 2160", null));
    }

    [Fact]
    public void 숫자는_어느_Windows_언어에서도_같은_꼴로_적는다()
    {
        // 천 단위 구분이나 다른 숫자 글자가 끼어들지 않는다(아랍어·힌디어 Windows).
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
            Assert.Equal("Display 12", DisplayChoiceText.Title("Display {0}", 12));
            Assert.Equal("3840 × 2160", DisplayChoiceText.Size("{0} × {1}", Right));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void 모니터_구성과_언어가_같으면_표식이_같고_하나라도_다르면_다르다()
    {
        var both = new[] { Left, Right };
        string signature = DisplayChoiceText.Signature(both, "ko");

        Assert.Equal(signature, DisplayChoiceText.Signature(new[] { Left with { }, Right with { } }, "ko"));
        // 주사율은 칸에 보이지 않는다. 바뀌어도 칸을 다시 만들지 않는다.
        Assert.Equal(signature, DisplayChoiceText.Signature(new[] { Left with { RefreshRate = 60 }, Right }, "ko"));

        Assert.NotEqual(signature, DisplayChoiceText.Signature(both, "en"));
        Assert.NotEqual(signature, DisplayChoiceText.Signature(new[] { Right, Left }, "ko"));
        Assert.NotEqual(signature, DisplayChoiceText.Signature(new[] { Left }, "ko"));
        Assert.NotEqual(signature, DisplayChoiceText.Signature(new[] { Left with { IsPrimary = false }, Right with { IsPrimary = true } }, "ko"));
        Assert.NotEqual(signature, DisplayChoiceText.Signature(new[] { Left, Right with { Left = -2560 } }, "ko"));
        Assert.NotEqual(signature, DisplayChoiceText.Signature(new[] { Left, Right with { PixelWidth = 2560, PixelHeight = 1440 } }, "ko"));
        Assert.NotEqual(signature, DisplayChoiceText.Signature(new[] { Left, Right with { DevicePath = "other" } }, "ko"));
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void 모든_언어의_틀로_칸의_글을_만들_수_있다(string code)
    {
        var table = StringTables.Get(code)!;
        Assert.True(table.TryGet("settings.display.item", out string? item));
        Assert.True(table.TryGet("settings.display.size", out string? size));
        Assert.True(table.TryGet("settings.display.primary", out string? primary));
        Assert.True(table.TryGet("easy.name_pair", out string? pair));

        string title = DisplayChoiceText.Title(item!, 2);
        Assert.Contains("2", title);
        Assert.NotEqual("2", title.Trim()); // 번호만 있으면 무엇의 번호인지 알 수 없다

        // 해상도는 어느 언어에서도 같은 꼴이다. 숫자뿐이라 번역하지 않는다.
        Assert.Equal("1920 × 1080", DisplayChoiceText.Size(size!, Left));

        string name = DisplayChoiceText.Name(pair!, title, "1920 × 1080", primary);
        Assert.Contains(title, name);
        Assert.Contains("1920 × 1080", name);
        Assert.Contains(primary!, name);
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void 도움말은_설정의_이름을_글자_그대로_인용한다(string code)
    {
        // 도움말의 {0} 자리에 설정 이름(settings.display.title)을 넣는다(HelpTab.ApplyTexts). 이름이 바뀌어도 도움말이 따라간다.
        var table = StringTables.Get(code)!;
        Assert.True(table.TryGet("help.display.body", out string? body));
        Assert.True(table.TryGet("settings.display.title", out string? title));
        Assert.Contains("{0}", body);
        Assert.Contains(title!, string.Format(System.Globalization.CultureInfo.InvariantCulture, body!, title));
    }
}
