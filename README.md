# YouTube 下载器

Windows x64 图形下载器，支持把单个 YouTube 视频保存为 MP4 或 MP3。MP4 默认使用视频提供的最高画质，也可以指定优先清晰度。

## 安装与使用

1. 下载 `YouTube-Downloader-Windows-x64.zip`，解压到可写的文件夹。[GitHub 发布页](https://github.com/dwhcn/youtube-downloader-win/releases/latest)
2. 在解压后的文件夹中打开 PowerShell，运行以下命令。安装脚本会下载并校验所需组件，保存到同目录的 `tools` 文件夹。

   ```powershell
   Unblock-File -LiteralPath .\setup-tools.ps1
   powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\setup-tools.ps1
   ```

3. 双击 `YouTube Downloader.exe`，粘贴 YouTube 视频链接，选好格式，再点 **开始下载**。

文件默认保存在程序旁的 `下载` 文件夹。点击 **浏览…** 可更改保存位置，**打开文件夹** 可查看结果。使用 **更新 yt-dlp** 检查和应用更新。下载失败时，**查看详情** 会显示 yt-dlp 的原始报错。

## 源码与组件

在源码目录运行 `build.ps1`，即可使用 Windows .NET Framework 的 C# 编译器重建界面。可以用 `setup-tools.ps1` 安装所需组件，也可以把四个可执行文件手动放入 `tools`。

- `yt-dlp.exe`。[yt-dlp 发布页](https://github.com/yt-dlp/yt-dlp/releases)
- `deno.exe`，Windows x64 版。[Deno 发布页](https://github.com/denoland/deno/releases)
- `ffmpeg.exe` 和 `ffprobe.exe`，Windows Essentials 构建。[FFmpeg Windows 下载页](https://www.gyan.dev/ffmpeg/builds/)

移动程序时，把同目录的 `tools` 文件夹一起带上。下载组件会读取 Windows 的系统代理设置。

## 贡献者

Codex 编写了图形下载器和组件安装脚本。[Codex 的 GitHub 主页](https://github.com/codex)
