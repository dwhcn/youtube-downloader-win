using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace YouTubeDownloader
{
    internal sealed class Downloader
    {
        internal const string TitleMarker = "VIDEO:";
        internal const string ProgressMarker = "PROGRESS:";
        internal const string PostprocessMarker = "PROCESS:";
        internal const string FileMarker = "FILE:";

        private readonly string toolsDirectory;

        internal Downloader(string appDirectory)
        {
            toolsDirectory = Path.Combine(appDirectory, "tools");
        }

        internal string[] MissingTools()
        {
            return new[] { "yt-dlp.exe", "ffmpeg.exe", "ffprobe.exe", "deno.exe" }
                .Where(name => !File.Exists(Path.Combine(toolsDirectory, name)))
                .ToArray();
        }

        internal Process Download(string url, string folder, bool audioOnly, string resolution)
        {
            var args = new List<string> {
                "--no-cache-dir", "--no-playlist", "--no-overwrites", "--windows-filenames",
                "--retries", "3", "--newline", "--progress", "--progress-delta", "0.3",
                "--js-runtimes", "deno:" + Path.Combine(toolsDirectory, "deno.exe"),
                "--ffmpeg-location", toolsDirectory,
                "--no-simulate", "--print", "before_dl:" + TitleMarker + "%(title)s",
                "--print", "after_move:" + FileMarker + "%(filepath)s",
                "--progress-template", "download:" + ProgressMarker +
                    "%(progress._percent_str)s|%(progress._speed_str)s|%(progress._eta_str)s",
                "--progress-template", "postprocess:" + PostprocessMarker + "%(progress.postprocessor)s",
                "--paths", folder
            };

            if (audioOnly)
            {
                args.AddRange(new[] {
                    "--extract-audio", "--audio-format", "mp3",
                    "--output", "%(title).150B [%(id)s].%(ext)s"
                });
            }
            else
            {
                string resolutionSort = resolution.Length == 0 ? "res" : "res:" + resolution;
                args.AddRange(new[] {
                    "--format", "bv*+ba/b", "--format-sort-force",
                    "--format-sort", resolutionSort + ",vcodec:h264,acodec:aac",
                    "--merge-output-format", "mp4", "--remux-video", "mp4",
                    "--output", "%(title).150B [%(id)s] %(resolution)s.%(ext)s"
                });
            }

            args.Add("--");
            args.Add(url);
            return CreateProcess(args);
        }

        internal Process Update()
        {
            return CreateProcess(new[] { "--update" });
        }

        private Process CreateProcess(IEnumerable<string> args)
        {
            var commonArgs = new[] {
                "--ignore-config", "--no-plugin-dirs", "--encoding", "utf-8",
                "--color", "never", "--socket-timeout", "20"
            };
            var info = new ProcessStartInfo {
                FileName = Path.Combine(toolsDirectory, "yt-dlp.exe"),
                Arguments = string.Join(" ", commonArgs.Concat(args).Select(QuoteArgument)),
                WorkingDirectory = toolsDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            info.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            return new Process { StartInfo = info };
        }

        internal static async Task<int> RunAsync(Process process, IProgress<string> lines)
        {
            process.OutputDataReceived += (sender, args) => {
                if (args.Data != null) lines.Report(args.Data);
            };
            process.ErrorDataReceived += (sender, args) => {
                if (args.Data != null) lines.Report(args.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return await Task.Run(() => {
                process.WaitForExit();
                return process.ExitCode;
            });
        }

        internal static void Stop(Process process)
        {
            if (process.HasExited) return;

            var info = new ProcessStartInfo {
                FileName = Path.Combine(Environment.SystemDirectory, "taskkill.exe"),
                Arguments = "/PID " + process.Id.ToString(CultureInfo.InvariantCulture) + " /T /F",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (Process stop = Process.Start(info))
            {
                if (!stop.WaitForExit(5000)) stop.Kill();
            }
            if (!process.HasExited) process.Kill();
        }

        internal static bool TryGetVideoUrl(string input, out string url)
        {
            url = null;
            string candidate = input.Trim();
            if (!candidate.Contains("://")) candidate = "https://" + candidate;

            Uri uri;
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
                !uri.IsDefaultPort || uri.UserInfo.Length != 0)
                return false;

            string id = "";
            string[] path = uri.AbsolutePath.Trim('/').Split('/');
            if (uri.Host == "youtu.be" && path.Length == 1)
            {
                id = path[0];
            }
            else if (uri.Host == "youtube.com" || uri.Host.EndsWith(".youtube.com", StringComparison.Ordinal))
            {
                if (uri.AbsolutePath == "/watch")
                    id = Regex.Match(uri.Query, @"(?:\?|&)v=([\w-]{11})(?:&|$)").Groups[1].Value;
                else if (path.Length == 2 && (path[0] == "shorts" || path[0] == "live" || path[0] == "embed"))
                    id = path[1];
            }

            if (!Regex.IsMatch(id, @"\A[A-Za-z0-9_-]{11}\z")) return false;
            url = "https://www.youtube.com/watch?v=" + id;
            return true;
        }

        // Windows removes argument quotes before handing the arguments to yt-dlp.
        internal static string QuoteArgument(string value)
        {
            var quoted = new StringBuilder("\"");
            int backslashes = 0;
            foreach (char character in value)
            {
                if (character == '\\')
                {
                    backslashes++;
                    continue;
                }
                quoted.Append('\\', character == '"' ? backslashes * 2 + 1 : backslashes);
                quoted.Append(character);
                backslashes = 0;
            }
            return quoted.Append('\\', backslashes * 2).Append('"').ToString();
        }
    }
}
