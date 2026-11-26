// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Xml.Linq;

namespace SoundVisualizer.Tests.Ui;

/// <summary>업데이트 확인은 '더 알아보기' 탭의 업데이트 카드에 있다(#46, 사용자 요청). 도움말에는 버전과 라이선스만 남는다.</summary>
public class UpdateCardPlacementTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XElement Named(XElement root, string name) =>
        root.Descendants().Single(e => (string?)e.Attribute(Xaml + "Name") == name);

    [Fact]
    public void 업데이트_칸은_더_알아보기_탭에_있다()
    {
        var launcher = XDocument.Parse(AppSource.Read("LauncherWindow.xaml")).Root!;
        var moreInfo = Named(launcher, "TabMoreInfo");
        foreach (string name in new[] { "TxtAppVersion", "TxtUpdateStatus", "PanelChocolatey", "TxtChocolateyCommand", "BtnUpdateCheck", "BtnUpdateInstall", "BtnUpdatePage" })
        {
            Assert.Contains(moreInfo.Descendants(), e => (string?)e.Attribute(Xaml + "Name") == name);
        }
    }

    [Fact]
    public void 도움말에는_업데이트_칸이_없다()
    {
        string help = AppSource.Read("HelpTab.xaml") + AppSource.Read("HelpTab.xaml.cs");
        foreach (string name in new[] { "BtnUpdateCheck", "TxtUpdateStatus", "PanelChocolatey", "UpdateController" })
        {
            Assert.DoesNotContain(name, help);
        }
    }
}
