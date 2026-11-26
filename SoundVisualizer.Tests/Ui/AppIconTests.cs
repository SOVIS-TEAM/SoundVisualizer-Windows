// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Security.Cryptography;

namespace SoundVisualizer.Tests.Ui;

/// <summary>
/// 앱 아이콘(#42, #52). 로고 디자인은 원래 그대로다(어두운 바탕에 가는 회색 고리, 양옆이 볼록한 소리 물결).
/// 256px 한 장만 있으면 Windows 가 탐색기·작업 표시줄·알림 영역에서 크게 줄여 고리가 사라지므로, 작은 크기를 따로 넣는다.
/// 2.3.1 에서 로고를 새 디자인으로 바꿨다가 되돌렸다(#52): 원래 로고 파일과 MSIX 타일은 바이트까지 같아야 한다.
/// </summary>
public class AppIconTests
{
    // 원래 로고(2026-05 'icon update' 커밋의 SoundVisualizer.ico 안 256px 그림)와 원래 MSIX 타일의 SHA-256.
    private const string LogoSha256 = "5803546e4848c977f9507c7415715b395a71f436cf312091b56de69ffce90287";

    private static readonly Dictionary<string, string> MsixTileSha256 = new()
    {
        ["Square150x150Logo.png"] = "9929ac051f5d3fbf3c8011755293c2fdb4813a69b177799787d12dce46aa6886",
        ["Square44x44Logo.png"] = "b27d54693ca47fa8a28da8a58d9580056e3752661393fad372fd2bfa5c2dd924",
        ["StoreLogo.png"] = "8a52937800f38894d243acc081d296e68ff9d8001600816320565b2498aa2077",
        ["Wide310x150Logo.png"] = "6678a00babe108eae08196180d82410b1266e1d46488a33af50f1085b8beafbc",
    };

    private static string IconPath => Path.Combine(RepositoryPaths.Root, "SoundVisualizer", "SoundVisualizer.ico");

    private static string Sha256(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    /// <summary>아이콘 파일의 그림들: (크기, 그림 바이트).</summary>
    private static List<(int Size, byte[] Data)> IconFrames(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        Assert.Equal(1, BitConverter.ToUInt16(data, 2)); // 아이콘
        int count = BitConverter.ToUInt16(data, 4);
        var frames = new List<(int, byte[])>();
        for (int i = 0; i < count; i++)
        {
            int entry = 6 + 16 * i;
            int width = data[entry];
            int length = BitConverter.ToInt32(data, entry + 8);
            int offset = BitConverter.ToInt32(data, entry + 12);
            frames.Add((width == 0 ? 256 : width, data.AsSpan(offset, length).ToArray()));
        }
        return frames;
    }

    [Fact]
    public void 아이콘_파일에는_작은_크기가_따로_들어_있다()
    {
        var sizes = IconFrames(IconPath).Select(f => f.Size).ToList();
        foreach (int size in new[] { 16, 20, 24, 32, 40, 48, 64, 96, 128, 256 })
        {
            Assert.Contains(size, sizes);
        }
    }

    [Fact]
    public void 로고_원본은_원래_로고_그대로다()
    {
        byte[] logo = File.ReadAllBytes(Path.Combine(RepositoryPaths.Root, "tools", "icon", "logo_256.png"));
        Assert.Equal(LogoSha256, Sha256(logo));
    }

    [Fact]
    public void 아이콘의_256px_그림은_원래_로고_파일_그대로다()
    {
        var big = IconFrames(IconPath).Single(f => f.Size == 256);
        Assert.Equal(LogoSha256, Sha256(big.Data));
    }

    [Fact]
    public void MSIX_타일은_원래_파일_그대로다()
    {
        string assets = Path.Combine(RepositoryPaths.Root, "packaging", "msix", "Assets");
        foreach (var (name, sha) in MsixTileSha256)
        {
            Assert.Equal(sha, Sha256(File.ReadAllBytes(Path.Combine(assets, name))));
        }
    }

    [Fact]
    public void 작은_그림도_원래_로고처럼_무채색이다()
    {
        // 작은 그림은 로고 픽셀에서만 만든다. 다른 색(예: 2.3.1 의 파란 타일)이 섞이면 R·G·B 가 벌어진다.
        foreach (var (size, data) in IconFrames(IconPath).Where(f => f.Size < 256))
        {
            var (width, height, rgba) = DecodePng(data);
            Assert.Equal(size, width);
            Assert.Equal(size, height);
            for (int i = 0; i < rgba.Length; i += 4)
            {
                int r = rgba[i], g = rgba[i + 1], b = rgba[i + 2];
                int spread = Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
                Assert.True(spread <= 12, $"{size}px 픽셀 {i / 4} 색이 섞였다: {r},{g},{b}");
            }
        }
    }

    /// <summary>8비트 RGB/RGBA PNG(비월 없음)를 RGBA 바이트로 푼다. 아이콘 그림을 읽는 데만 쓴다.</summary>
    private static (int Width, int Height, byte[] Rgba) DecodePng(byte[] png)
    {
        Assert.True(png.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }), "PNG 여야 한다");
        int width = 0, height = 0, channels = 0;
        using var idat = new MemoryStream();
        for (int pos = 8; pos < png.Length;)
        {
            int length = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(pos, 4));
            string type = System.Text.Encoding.ASCII.GetString(png, pos + 4, 4);
            var body = png.AsSpan(pos + 8, length);
            if (type == "IHDR")
            {
                width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(body);
                height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(body[4..]);
                Assert.Equal(8, body[8]); // 비트 깊이
                channels = body[9] switch { 2 => 3, 6 => 4, _ => throw new InvalidDataException($"색 형식 {body[9]}") };
                Assert.Equal(0, body[12]); // 비월 없음
            }
            else if (type == "IDAT")
            {
                idat.Write(body);
            }
            pos += 12 + length;
        }
        idat.Position = 0;
        using var inflated = new MemoryStream();
        using (var z = new System.IO.Compression.ZLibStream(idat, System.IO.Compression.CompressionMode.Decompress))
        {
            z.CopyTo(inflated);
        }
        byte[] raw = inflated.ToArray();
        int stride = width * channels;
        var rows = new byte[height * stride];
        for (int y = 0; y < height; y++)
        {
            byte filter = raw[y * (stride + 1)];
            for (int x = 0; x < stride; x++)
            {
                int v = raw[y * (stride + 1) + 1 + x];
                int left = x >= channels ? rows[y * stride + x - channels] : 0;
                int up = y > 0 ? rows[(y - 1) * stride + x] : 0;
                int upLeft = x >= channels && y > 0 ? rows[(y - 1) * stride + x - channels] : 0;
                int predictor = filter switch
                {
                    0 => 0,
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => throw new InvalidDataException($"필터 {filter}"),
                };
                rows[y * stride + x] = (byte)(v + predictor);
            }
        }
        var rgba = new byte[width * height * 4];
        for (int p = 0; p < width * height; p++)
        {
            rgba[p * 4] = rows[p * channels];
            rgba[p * 4 + 1] = rows[p * channels + 1];
            rgba[p * 4 + 2] = rows[p * channels + 2];
            rgba[p * 4 + 3] = channels == 4 ? rows[p * channels + 3] : (byte)255;
        }
        return (width, height, rgba);
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    [Fact]
    public void 알림_영역은_큰_그림을_줄이지_않고_그_크기_그림을_꺼낸다()
    {
        string tray = AppSource.Read("TrayIcon.cs");
        Assert.Contains("ExtractIcon(exe, 0, Forms.SystemInformation.SmallIconSize.Width)", tray);
        Assert.DoesNotContain("ExtractIcon(exe, 0, 256)", tray);
    }
}
