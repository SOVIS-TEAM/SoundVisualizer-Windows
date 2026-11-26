// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Updates;

namespace SoundVisualizer.Tests.Updates;

public class UpdateChannelTests
{
    [Fact]
    public void MSIX_패키지는_Store_다()
    {
        Assert.Equal(UpdateChannel.Store, UpdateChannels.Detect(true, @"C:\Program Files\WindowsApps\SOVIS.SoundVisualizer_2.2.0.0_x64__abc\", null));
    }

    [Theory]
    [InlineData(@"C:\ProgramData\chocolatey\lib\soundvisualizer\tools\", @"C:\ProgramData\chocolatey")]
    [InlineData(@"C:\ProgramData\chocolatey\lib\soundvisualizer\tools", @"C:\ProgramData\chocolatey\")]
    [InlineData(@"D:\tools\choco\lib\soundvisualizer\tools\", @"D:\tools\choco")]
    [InlineData(@"C:\ProgramData\chocolatey\lib\soundvisualizer\tools\", null)]
    [InlineData(@"C:\PROGRAMDATA\Chocolatey\LIB\soundvisualizer\tools\", "")]
    public void Chocolatey_의_lib_폴더_아래면_Chocolatey_다(string baseDirectory, string? chocolateyInstall)
    {
        Assert.Equal(UpdateChannel.Chocolatey, UpdateChannels.Detect(false, baseDirectory, chocolateyInstall));
    }

    [Theory]
    [InlineData(@"C:\Users\me\Downloads\SoundVisualizer_v2.2\", @"C:\ProgramData\chocolatey")]
    [InlineData(@"D:\Games\SoundVisualizer\", null)]
    [InlineData(@"C:\ProgramData\chocolatey-tools\SoundVisualizer\", @"C:\ProgramData\chocolatey")]
    [InlineData(@"C:\Users\me\김도환\SV\", null)]
    public void 그_밖은_zip_판이다(string baseDirectory, string? chocolateyInstall)
    {
        Assert.Equal(UpdateChannel.Portable, UpdateChannels.Detect(false, baseDirectory, chocolateyInstall));
    }

    [Fact]
    public void 주소들은_정해진_곳을_가리킨다()
    {
        Assert.StartsWith("ms-windows-store://pdp/?productid=9NGH48VCRFW2", UpdateChannels.StoreProductUri);
        Assert.Equal("https://github.com/SOVIS-Team/SoundVisualizer-Windows/releases/latest", UpdateChannels.LatestReleasePageUrl);
        Assert.Equal("choco upgrade soundvisualizer -y", UpdateChannels.ChocolateyUpgradeCommand);
    }
}
