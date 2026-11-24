// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Display;

namespace SoundVisualizer.Tests.Display;

/// <summary>
/// 오버레이를 표시할 모니터를 정하는 규칙(#29). 모니터를 두 대 쓰는 사용자가 "어떻게 설정해야 할지 모르겠다"고 알려 와 만든 설정이다.
/// 화면(설정 칸, 오버레이 창)은 이 규칙대로 보이고 옮기기만 하므로 여기서 지킨다.
/// </summary>
public class OverlayDisplaysTests
{
    // 흔한 배치: 주 모니터(2번)가 왼쪽, 보조 모니터(1번)가 오른쪽.
    private static readonly DisplayInfo Left = new(@"\\.\DISPLAY2", @"\\?\DISPLAY#AAA#1&left#{guid}", 0, 0, 1920, 1080, IsPrimary: true, RefreshRate: 144);
    private static readonly DisplayInfo Right = new(@"\\.\DISPLAY1", @"\\?\DISPLAY#BBB#1&right#{guid}", 1920, 0, 1920, 1080, IsPrimary: false, RefreshRate: 60);
    private static readonly DisplayInfo[] Both = { Right, Left };

    [Fact]
    public void 선택한_적이_없으면_주_모니터에_띄운다()
    {
        Assert.Equal("", OverlayDisplays.Primary);
        Assert.Same(Left, OverlayDisplays.Resolve(Both, OverlayDisplays.Primary));
        Assert.Same(Left, OverlayDisplays.Resolve(Both, null));
        Assert.False(OverlayDisplays.IsMissing(Both, OverlayDisplays.Primary));
        Assert.False(OverlayDisplays.IsMissing(Both, null));
    }

    [Fact]
    public void 선택한_모니터가_연결되어_있으면_그_모니터에_띄운다()
    {
        Assert.Same(Right, OverlayDisplays.Resolve(Both, Right.Key));
        Assert.Same(Left, OverlayDisplays.Resolve(Both, Left.Key));
        Assert.False(OverlayDisplays.IsMissing(Both, Right.Key));
    }

    [Fact]
    public void 저장하는_이름은_장치_경로이고_경로를_모르면_출력_이름이다()
    {
        Assert.Equal(@"\\?\DISPLAY#BBB#1&right#{guid}", Right.Key);
        Assert.Equal(@"\\.\DISPLAY1", (Right with { DevicePath = "" }).Key);
    }

    [Fact]
    public void 출력_번호가_서로_바뀌어도_장치_경로로_같은_모니터를_찾는다()
    {
        // 드라이버를 다시 깔거나 케이블을 옮겨 DISPLAY1 과 DISPLAY2 가 서로 바뀌었다. 경로는 그대로다.
        var swappedLeft = Left with { DeviceName = @"\\.\DISPLAY1" };
        var swappedRight = Right with { DeviceName = @"\\.\DISPLAY2" };

        Assert.Same(swappedRight, OverlayDisplays.Resolve(new[] { swappedLeft, swappedRight }, Right.Key));
    }

    [Fact]
    public void 경로를_모른_채_저장한_선택은_출력_이름으로_찾는다()
    {
        // 저장할 때 장치 경로를 알아내지 못해 출력 이름을 적어 둔 경우. 대소문자는 가리지 않는다.
        Assert.Same(Right, OverlayDisplays.Resolve(Both, @"\\.\display1"));

        // 같은 글자가 한 모니터의 경로이고 다른 모니터의 이름이면 경로가 먼저다.
        var odd = Left with { DevicePath = @"\\.\DISPLAY1" };
        Assert.Same(odd, OverlayDisplays.Resolve(new[] { Right, odd }, @"\\.\DISPLAY1"));
    }

    [Fact]
    public void 선택한_모니터가_연결되어_있지_않으면_주_모니터에_띄우고_그렇다고_알린다()
    {
        var onlyLeft = new[] { Left };

        Assert.Same(Left, OverlayDisplays.Resolve(onlyLeft, Right.Key));
        Assert.True(OverlayDisplays.IsMissing(onlyLeft, Right.Key));
        Assert.Null(OverlayDisplays.Find(onlyLeft, Right.Key));
    }

    [Fact]
    public void 주_모니터_표시가_없는_목록이면_첫_모니터에_띄운다()
    {
        var first = Left with { IsPrimary = false };

        Assert.Same(first, OverlayDisplays.Resolve(new[] { first, Right }, "nothing"));
    }

    [Fact]
    public void 모니터를_하나도_읽지_못했으면_띄울_곳을_정하지_않는다()
    {
        Assert.Null(OverlayDisplays.Resolve(Array.Empty<DisplayInfo>(), Right.Key));
        Assert.Null(OverlayDisplays.Resolve(Array.Empty<DisplayInfo>(), OverlayDisplays.Primary));
    }

    [Fact]
    public void 칸의_차례는_왼쪽에서_오른쪽이고_같은_자리면_위에서_아래다()
    {
        var above = new DisplayInfo(@"\\.\DISPLAY3", "", 0, -1080, 1920, 1080, false, 60);
        var farLeft = new DisplayInfo(@"\\.\DISPLAY4", "", -2560, 0, 2560, 1440, false, 60);

        var ordered = OverlayDisplays.Ordered(new[] { Right, Left, above, farLeft });

        Assert.Equal(new[] { farLeft, above, Left, Right }, ordered);
    }

    [Fact]
    public void 번호는_출력_이름의_번호를_쓴다()
    {
        var ordered = OverlayDisplays.Ordered(Both);

        // 왼쪽 칸이 2번, 오른쪽 칸이 1번이다. Windows 디스플레이 설정이 보이는 번호와 맞춘다.
        Assert.Equal(new[] { Left, Right }, ordered);
        Assert.Equal(new[] { 2, 1 }, OverlayDisplays.Numbers(ordered));
        Assert.Equal(new[] { 12 }, OverlayDisplays.Numbers(new[] { Left with { DeviceName = @"\\.\DISPLAY12" } }));
    }

    [Fact]
    public void 이름에서_번호를_읽지_못하거나_번호가_겹치면_차례대로_매긴다()
    {
        var unnamed = Right with { DeviceName = "WinDisc" };
        Assert.Equal(new[] { 1, 2 }, OverlayDisplays.Numbers(new[] { Left, unnamed }));

        var sameNumber = Right with { DeviceName = @"\\.\DISPLAY2" };
        Assert.Equal(new[] { 1, 2 }, OverlayDisplays.Numbers(new[] { Left, sameNumber }));

        var zero = Right with { DeviceName = @"\\.\DISPLAY0" };
        Assert.Equal(new[] { 1, 2 }, OverlayDisplays.Numbers(new[] { Left, zero }));
        Assert.Empty(OverlayDisplays.Numbers(Array.Empty<DisplayInfo>()));
    }

    [Fact]
    public void 배치_그림은_놓인_모양을_그대로_줄여_가운데에_넣는다()
    {
        // 1920×1080 두 대가 나란히: 전체 3840×1080 을 100×40 안에 넣으면 가로가 꽉 차고 세로는 가운데에 온다.
        var boxes = OverlayDisplays.Layout(new[] { Left, Right }, 100, 40);

        Assert.Equal(2, boxes.Count);
        Assert.Equal(new DisplayBox(0, 5.9375, 50, 28.125), boxes[0]);
        Assert.Equal(new DisplayBox(50, 5.9375, 50, 28.125), boxes[1]);
    }

    [Fact]
    public void 배치_그림은_주_모니터의_왼쪽이나_위에_놓인_모니터도_담는다()
    {
        // 주 모니터 위에 한 대(음수 좌표), 크기도 다르다.
        var top = new DisplayInfo(@"\\.\DISPLAY2", "", 320, -720, 1280, 720, false, 60);
        var boxes = OverlayDisplays.Layout(new[] { top, Left }, 96, 90);

        // 전체 1920×1800 → 배율 0.05, 가로 96 을 다 쓰고 세로는 90 그대로다.
        Assert.Equal(new DisplayBox(16, 0, 64, 36), boxes[0]);
        Assert.Equal(new DisplayBox(0, 36, 96, 54), boxes[1]);
    }

    [Fact]
    public void 그릴_것이_없으면_빈_그림이다()
    {
        Assert.Empty(OverlayDisplays.Layout(Array.Empty<DisplayInfo>(), 100, 40));
        Assert.Empty(OverlayDisplays.Layout(Both, 0, 40));
        Assert.Empty(OverlayDisplays.Layout(Both, 100, -1));
    }

    [Fact]
    public void 칸에_적는_해상도는_실제_해상도이고_모르면_창을_옮길_때_쓰는_크기다()
    {
        // 배율이 다른 모니터를 함께 쓰면 이 프로세스가 보는 크기(2560×1440)와 실제 해상도(3840×2160)가 다르다.
        var scaled = new DisplayInfo(@"\\.\DISPLAY2", "", 1920, 0, 2560, 1440, false, 60, PixelWidth: 3840, PixelHeight: 2160);
        Assert.Equal((3840, 2160), (scaled.LabelWidth, scaled.LabelHeight));

        Assert.Equal((1920, 1080), (Left.LabelWidth, Left.LabelHeight));
        var half = scaled with { PixelHeight = 0 };
        Assert.Equal((2560, 1440), (half.LabelWidth, half.LabelHeight));
    }

    [Fact]
    public void 지금_연결된_모니터를_읽어도_말이_되는_값만_나온다()
    {
        // 시험을 돌리는 기계의 모니터를 읽는다. 화면이 없는 곳(빌드 서버)에서는 빈 목록이어도 된다.
        var displays = DisplayEnumerator.Current();

        Assert.All(displays, display =>
        {
            Assert.False(string.IsNullOrEmpty(display.DeviceName));
            Assert.True(display.Width > 0 && display.Height > 0, $"{display.DeviceName}: {display.Width}×{display.Height}");
            Assert.True(display.RefreshRate >= 0);
            Assert.False(string.IsNullOrEmpty(display.Key));
        });
        Assert.True(displays.Count(display => display.IsPrimary) <= 1);
        if (displays.Count > 0)
        {
            Assert.Single(displays, display => display.IsPrimary);
            Assert.NotNull(OverlayDisplays.Resolve(displays, OverlayDisplays.Primary));
            Assert.Equal(displays.Count, displays.Select(display => display.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }
}
