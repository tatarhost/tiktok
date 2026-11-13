using System.Diagnostics;
using System.Drawing;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace TikTokModPC;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    private const string ApkUrl = "https://github.com/namecallfilter/tiktokmodcloud/releases/download/v46.4.3-mod-fix/46.4.3_universal_fix.apk";
    private const string ExpectedSha256 = "88845094adb702e2b92ada1fec56920083208ff48db03a879621f3de8a14190c";
    private const string ApkFileName = "46.4.3_universal_fix.apk";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(30) };

    private readonly Label _emulatorStatus = new();
    private readonly Label _status = new();
    private readonly Label _progressText = new();
    private readonly ProgressBar _progress = new();
    private readonly RichTextBox _log = new();
    private readonly CheckBox _warning = new();
    private readonly Button _install = new();
    private BlueStacksInstall? _blueStacks;
    private bool _busy;

    public MainForm()
    {
        Text = "TikTok Mod PC";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(800, 620);
        Size = new Size(920, 700);
        BackColor = Color.FromArgb(9, 10, 14);
        ForeColor = Color.FromArgb(245, 246, 250);
        Font = new Font("Segoe UI", 10F);

        var title = MakeLabel("TikTok Mod PC", 27, true, Color.White, 26, 20, 400, 42);
        var subtitle = MakeLabel("Android-клиент 46.4.3 · Windows launcher", 10, false, Color.FromArgb(157, 163, 175), 28, 66, 500, 24);
        Controls.Add(title);
        Controls.Add(subtitle);

        var info = new Panel { Location = new Point(26, 105), Size = new Size(850, 128), BackColor = Color.FromArgb(21, 24, 33), BorderStyle = BorderStyle.FixedSingle, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        info.Controls.Add(MakeLabel("Запуск настоящего Android-приложения на ПК", 15, true, Color.White, 18, 15, 800, 28));
        info.Controls.Add(MakeLabel("Лаунчер скачает APK из GitHub Releases, проверит SHA-256, установит его в BlueStacks и откроет сам Android-клиент. Это не веб-клон.", 10, false, Color.FromArgb(178, 184, 197), 18, 48, 805, 34));
        info.Controls.Add(MakeLabel("Нужен установленный BlueStacks 5 с включённым Android Debug Bridge (ADB). Эмулятор в EXE не встраивается.", 10, true, Color.FromArgb(240, 182, 194), 18, 88, 805, 28));
        Controls.Add(info);

        var statusCard = new Panel { Location = new Point(26, 249), Size = new Size(850, 68), BackColor = Color.FromArgb(17, 20, 27), BorderStyle = BorderStyle.FixedSingle, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        statusCard.Controls.Add(MakeLabel("СОСТОЯНИЕ ЭМУЛЯТОРА", 8, false, Color.FromArgb(157, 163, 175), 14, 8, 300, 20));
        _emulatorStatus.SetBounds(14, 31, 810, 27);
        _emulatorStatus.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        statusCard.Controls.Add(_emulatorStatus);
        Controls.Add(statusCard);

        _install.Text = "Скачать, установить и запустить";
        _install.SetBounds(26, 335, 270, 43);
        StyleButton(_install, Color.FromArgb(254, 44, 85));
        _install.Click += async (_, _) => await InstallAndLaunchAsync();
        Controls.Add(_install);

        var open = new Button { Text = "Открыть BlueStacks" };
        open.SetBounds(307, 335, 175, 43);
        StyleButton(open, Color.FromArgb(37, 40, 50));
        open.Click += (_, _) => OpenBlueStacks();
        Controls.Add(open);

        var get = new Button { Text = "Скачать BlueStacks" };
        get.SetBounds(493, 335, 175, 43);
        StyleButton(get, Color.FromArgb(37, 40, 50));
        get.Click += (_, _) => Process.Start(new ProcessStartInfo("https://www.bluestacks.com/download.html") { UseShellExecute = true });
        Controls.Add(get);

        _status.Text = "Готов к запуску";
        _status.SetBounds(28, 393, 650, 22);
        _status.ForeColor = Color.FromArgb(199, 203, 212);
        Controls.Add(_status);

        _progressText.Text = "0%";
        _progressText.SetBounds(760, 393, 110, 22);
        _progressText.TextAlign = ContentAlignment.MiddleRight;
        _progressText.ForeColor = Color.FromArgb(157, 163, 175);
        Controls.Add(_progressText);

        _progress.SetBounds(28, 420, 840, 8);
        _progress.Minimum = 0;
        _progress.Maximum = 100;
        _progress.Style = ProgressBarStyle.Continuous;
        Controls.Add(_progress);

        var logTitle = MakeLabel("ЖУРНАЛ", 8, false, Color.FromArgb(157, 163, 175), 28, 444, 150, 20);
        Controls.Add(logTitle);
        _log.SetBounds(26, 466, 850, 112);
        _log.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _log.ReadOnly = true;
        _log.BackColor = Color.FromArgb(15, 18, 24);
        _log.ForeColor = Color.FromArgb(199, 203, 212);
        _log.BorderStyle = BorderStyle.FixedSingle;
        _log.Font = new Font("Consolas", 9.2F);
        Controls.Add(_log);

        _warning.Text = "Я понимаю, что APK сторонний, и его безопасность не подтверждена разработчиками TikTok.";
        _warning.SetBounds(28, 590, 840, 24);
        _warning.ForeColor = Color.FromArgb(174, 180, 192);
        _warning.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_warning);

        var safety = MakeLabel("SHA-256 проверяет целостность файла относительно релиза, но не доказывает отсутствие вредоносного кода. Не используй основной аккаунт в непроверенном моде.", 8.5F, false, Color.FromArgb(127, 135, 149), 28, 615, 840, 34);
        Controls.Add(safety);

        Http.DefaultRequestHeaders.UserAgent.ParseAdd("TikTokModPC/1.0");
        Load += (_, _) => RefreshBlueStacksStatus();
    }

    private static Label MakeLabel(string text, float size, bool bold, Color color, int x, int y, int w, int h)
        => new() { Text = text, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = color, BackColor = Color.Transparent, Location = new Point(x, y), Size = new Size(w, h), AutoEllipsis = true };

    private static void StyleButton(Button button, Color background)
    {
        button.BackColor = background;
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = background;
        button.FlatAppearance.BorderSize = 1;
        button.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
    }

    private void RefreshBlueStacksStatus()
    {
        _blueStacks = BlueStacksInstall.Detect();
        if (_blueStacks is null)
        {
            _emulatorStatus.Text = "BlueStacks не найден — установи BlueStacks 5 и включи ADB.";
            AddLog("После установки BlueStacks открой Настройки → Дополнительно → Android Debug Bridge и включи ADB.");
        }
        else
        {
            _emulatorStatus.Text = $"Найден BlueStacks · {_blueStacks.InstanceName} · ADB порт {_blueStacks.AdbPort}";
            AddLog($"Найден эмулятор: {_blueStacks.PlayerPath}");
        }
    }

    private async Task InstallAndLaunchAsync()
    {
        if (_busy) return;
        if (!_warning.Checked)
        {
            MessageBox.Show("Подтверди, что понимаешь: APK — сторонняя модификация и её безопасность не гарантирована.", "Подтверждение", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        RefreshBlueStacksStatus();
        if (_blueStacks is null)
        {
            SetStatus("Сначала установи BlueStacks 5 и включи ADB.");
            return;
        }

        SetBusy(true);
        try
        {
            var apk = await EnsureApkAsync();
            SetStatus("Запускаю Android-эмулятор…");
            AddLog($"Запускаю экземпляр {_blueStacks.InstanceName}.");
            StartBlueStacks(_blueStacks);
            var serial = await WaitForDeviceAsync(_blueStacks);
            AddLog($"ADB подключён: {serial}");

            SetStatus("Проверяю установленные приложения…");
            var before = await GetThirdPartyPackagesAsync(_blueStacks, serial);
            SetStatus("Устанавливаю APK в BlueStacks…");
            AddLog("Установка APK размером около 502 МБ. Это может занять несколько минут.");
            var install = await RunAdbAsync(_blueStacks, serial, "install", TimeSpan.FromMinutes(15), "-r", apk);
            if (install.ExitCode != 0 || !install.Output.Contains("Success", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("ADB не смог установить APK:\n" + TrimOutput(install.Output));

            var after = await GetThirdPartyPackagesAsync(_blueStacks, serial);
            var added = after.Except(before, StringComparer.OrdinalIgnoreCase).ToList();
            var package = added.Count == 1 ? added[0] : ChooseTikTokPackage(added.Count > 0 ? added : after);
            if (package is null)
            {
                SetStatus("APK установлен; запусти приложение с домашнего экрана BlueStacks.");
                AddLog("Установка завершена, но имя Android-пакета не определилось автоматически.");
                return;
            }

            SetStatus("Запускаю TikTok Mod…");
            var launch = await RunAdbAsync(_blueStacks, serial, "shell", TimeSpan.FromMinutes(2), "monkey", "-p", package, "1");
            if (launch.ExitCode != 0 || launch.Output.Contains("No activities found", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Приложение установлено, но запуск через Android не удался:\n" + TrimOutput(launch.Output));

            AddLog($"Запущен пакет: {package}");
            SetStatus("Готово — настоящий Android-клиент открыт в BlueStacks.");
            _progress.Value = 100;
            _progressText.Text = "100%";
        }
        catch (Exception ex)
        {
            AddLog("ОШИБКА: " + ex.Message);
            SetStatus("Не удалось завершить запуск. Подробности в журнале.");
            MessageBox.Show(ex.Message, "TikTok Mod PC", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { SetBusy(false); }
    }

    private async Task<string> EnsureApkAsync()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TikTokModPC", "downloads");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, ApkFileName);
        if (File.Exists(path))
        {
            SetStatus("Проверяю скачанный APK…");
            if ((await ComputeSha256Async(path)).Equals(ExpectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                AddLog("APK уже сохранён, SHA-256 совпадает.");
                _progress.Value = 100;
                _progressText.Text = "100%";
                return path;
            }
            File.Delete(path);
            AddLog("Сохранённый APK не прошёл проверку; скачиваю заново.");
        }

        SetStatus("Скачиваю APK из GitHub Releases…");
        AddLog("Источник: namecallfilter/tiktokmodcloud · v46.4.3-mod-fix");
        using var response = await Http.GetAsync(ApkUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync();
        await using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, true))
        {
            var buffer = new byte[1024 * 1024];
            long received = 0;
            int read;
            while ((read = await input.ReadAsync(buffer)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read));
                received += read;
                if (total is > 0)
                {
                    var percent = Math.Clamp(received * 100.0 / total.Value, 0, 100);
                    _progress.Value = (int)percent;
                    _progressText.Text = $"{percent:0}% · {received / 1024 / 1024}/{total.Value / 1024 / 1024} МБ";
                }
            }
        }

        SetStatus("Проверяю SHA-256…");
        var hash = await ComputeSha256Async(path);
        if (!hash.Equals(ExpectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(path);
            throw new InvalidDataException($"SHA-256 не совпал. Получено: {hash}. Файл удалён.");
        }
        AddLog("Загрузка завершена, SHA-256 совпадает с релизом.");
        _progress.Value = 100;
        _progressText.Text = "100%";
        return path;
    }

    private static async Task<string> ComputeSha256Async(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
    }

    private static void StartBlueStacks(BlueStacksInstall install)
    {
        var start = new ProcessStartInfo(install.PlayerPath) { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("--instance");
        start.ArgumentList.Add(install.InstanceName);
        Process.Start(start);
    }

    private async Task<string> WaitForDeviceAsync(BlueStacksInstall install)
    {
        var serial = $"127.0.0.1:{install.AdbPort}";
        var connect = await RunAdbAsync(install, null, "connect", TimeSpan.FromSeconds(20), serial);
        AddLog(TrimOutput(connect.Output));
        var until = DateTime.UtcNow.AddMinutes(3);
        while (DateTime.UtcNow < until)
        {
            var devices = await RunAdbAsync(install, null, "devices", TimeSpan.FromSeconds(15));
            if (devices.Output.Split('\n').Any(line => line.Trim().StartsWith(serial, StringComparison.OrdinalIgnoreCase) && line.Contains("device", StringComparison.OrdinalIgnoreCase))) return serial;
            await Task.Delay(2000);
            await RunAdbAsync(install, null, "connect", TimeSpan.FromSeconds(15), serial);
        }
        throw new TimeoutException("BlueStacks не подключился по ADB за 3 минуты. Включи ADB в настройках BlueStacks: Настройки → Дополнительно → Android Debug Bridge.");
    }

    private static async Task<HashSet<string>> GetThirdPartyPackagesAsync(BlueStacksInstall install, string serial)
    {
        var result = await RunAdbAsync(install, serial, "shell", TimeSpan.FromMinutes(1), "pm", "list", "packages", "-3");
        if (result.ExitCode != 0) throw new InvalidOperationException("Не удалось прочитать список Android-приложений: " + TrimOutput(result.Output));
        return result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim())
            .Where(x => x.StartsWith("package:", StringComparison.OrdinalIgnoreCase))
            .Select(x => x[8..].Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string? ChooseTikTokPackage(IEnumerable<string> packageNames)
    {
        var packages = packageNames.ToList();
        string[] known = ["com.zhiliaoapp.musically", "com.ss.android.ugc.aweme", "com.ss.android.ugc.trill"];
        foreach (var candidate in known)
            if (packages.Contains(candidate, StringComparer.OrdinalIgnoreCase)) return candidate;
        var matches = packages.Where(p => Regex.IsMatch(p, "tiktok|musically|trill|ugc\\.aweme", RegexOptions.IgnoreCase)).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    private static async Task<ProcessResult> RunAdbAsync(BlueStacksInstall install, string? serial, string command, TimeSpan timeout, params string[] args)
    {
        var start = new ProcessStartInfo(install.AdbPath)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        };
        if (!string.IsNullOrWhiteSpace(serial)) { start.ArgumentList.Add("-s"); start.ArgumentList.Add(serial); }
        start.ArgumentList.Add(command);
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = start };
        if (!process.Start()) throw new InvalidOperationException("Не удалось запустить HD-Adb.exe.");
        using var cts = new CancellationTokenSource(timeout);
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);
        try { await process.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { }
            throw new TimeoutException($"Команда ADB превысила лимит времени: {command}");
        }
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        return new ProcessResult(process.ExitCode, string.Join(Environment.NewLine, new[] { stdout, stderr }.Where(x => !string.IsNullOrWhiteSpace(x))));
    }

    private void OpenBlueStacks()
    {
        _blueStacks = BlueStacksInstall.Detect();
        if (_blueStacks is null) { SetStatus("BlueStacks не найден."); return; }
        StartBlueStacks(_blueStacks);
        SetStatus("Запущен BlueStacks.");
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _install.Enabled = !busy;
        _install.Text = busy ? "Выполняется…" : "Скачать, установить и запустить";
    }

    private void SetStatus(string text) => _status.Text = text;

    private void AddLog(string text)
    {
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }

    private static string TrimOutput(string value)
    {
        value = value.Trim();
        return value.Length > 1200 ? value[^1200..] : value;
    }

    private sealed record ProcessResult(int ExitCode, string Output);

    private sealed record BlueStacksInstall(string PlayerPath, string AdbPath, string InstanceName, int AdbPort)
    {
        public static BlueStacksInstall? Detect()
        {
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var pd = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var roots = new[] { Path.Combine(pf, "BlueStacks_nxt"), Path.Combine(pf86, "BlueStacks_nxt"), Path.Combine(pd, "BlueStacks_nxt") }
                .Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists).ToList();
            foreach (var root in roots)
            {
                var player = Path.Combine(root, "HD-Player.exe");
                var adb = new[] { Path.Combine(root, "HD-Adb.exe"), Path.Combine(root, "Bluestacks_ext", "HD-Adb.exe") }.FirstOrDefault(File.Exists);
                if (!File.Exists(player) || adb is null) continue;
                var configs = new[] { Path.Combine(pd, "BlueStacks_nxt", "bluestacks.conf"), Path.Combine(root, "bluestacks.conf") };
                foreach (var config in configs.Where(File.Exists))
                {
                    var content = File.ReadAllText(config);
                    var matches = Regex.Matches(content, "bst\\.instance\\.(?<name>[^.]+)\\.status\\.adb_port\\s*=\\s*\\\"?(?<port>\\d+)", RegexOptions.IgnoreCase)
                        .Cast<Match>().Where(m => int.TryParse(m.Groups["port"].Value, out var port) && port > 0).ToList();
                    var match = matches.OrderByDescending(m => m.Groups["name"].Value.Contains("64", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    if (match is not null && int.TryParse(match.Groups["port"].Value, out var port))
                        return new BlueStacksInstall(player, adb, match.Groups["name"].Value, port);
                }
            }
            return null;
        }
    }
}
