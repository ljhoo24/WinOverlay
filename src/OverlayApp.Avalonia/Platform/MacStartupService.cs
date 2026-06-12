using System;
using System.IO;
using OverlayApp.Core.Abstractions;

namespace OverlayApp.Avalonia.Platform;

/// <summary>macOS 로그인 시 자동 실행 — ~/Library/LaunchAgents plist 등록.</summary>
public sealed class MacStartupService : IStartupService
{
    private const string PlistName = "com.ljhoo24.winoverlay.plist";

    private static string PlistPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents", PlistName);

    public bool IsEnabled => File.Exists(PlistPath);

    public void Enable()
    {
        if (!OperatingSystem.IsMacOS()) return;
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath)) return;

        var dir = Path.GetDirectoryName(PlistPath)!;
        Directory.CreateDirectory(dir);

        var plist = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
                <key>Label</key>
                <string>com.ljhoo24.winoverlay</string>
                <key>ProgramArguments</key>
                <array>
                    <string>{exePath}</string>
                </array>
                <key>RunAtLoad</key>
                <true/>
            </dict>
            </plist>
            """;
        File.WriteAllText(PlistPath, plist);
    }

    public void Disable()
    {
        try { File.Delete(PlistPath); } catch { /* 없으면 무시 */ }
    }
}
