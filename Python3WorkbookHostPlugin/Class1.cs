using System;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using UnicornOne.Workbook;

namespace Python3WorkbookHostPlugin
{
    public sealed class Python3WorkbookHostPlugin : IWorkbookPlugin, IWorkbookController, IDisposable
    {
        private IWorkbookHost _host;
        private CancellationTokenSource _cts;

        private readonly HttpClient _http = new HttpClient();
        private string _pythonBaseUrl = "http://127.0.0.1:5051";

        private string _name = "Python3Workbook";
        private string _version = "0.1";

        public string Name => _name;
        public string Version => _version;

        public void Initialize(string jsonConfig, IWorkbookHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _cts = new CancellationTokenSource();

            try
            {
                string health = CheckHealthAsync().GetAwaiter().GetResult();
                _host.AppendLog("Python3", "Info", $"Health: {health}");

                string initResponse = InitializePythonAsync(jsonConfig ?? "").GetAwaiter().GetResult();
                _host.AppendLog("Python3", "Info", $"Python server initialized: {initResponse}");
            }
            catch (Exception ex)
            {
                _host.AppendLog("Python3", "Error", $"Initialize failed: {ex.Message}");
            }
        }

        public void BuildUI()
        {
            var shell = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };

            _host.Theme.ApplyBaseStyles(shell);

            var title = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Text = $"{Name}",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Padding = new Padding(0, 0, 0, 8),
            };

            var info = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Text = $"Python server: {_pythonBaseUrl}",
                Padding = new Padding(0, 0, 0, 12),
            };

            _host.AddMenuButton("Run", () => Run());
            _host.AddMenuButton("Cancel", () => RequestCancel());

            shell.Controls.Add(info);
            shell.Controls.Add(title);

            _host.AddWidget(shell, "Main");
        }

        public void OnShown()
        {
        }

        public void OnHidden()
        {
        }

        public void Run()
        {
            _ = RunAsync();
        }

        private async Task RunAsync()
        {
            try
            {
                _host.SetStatus("Running python3 workbook...");
                _host.AppendLog("Python3", "Info", "Calling /run...");

                string responseText = await RunPythonAsync(_cts.Token);

                _host.AppendLog("Python3", "Info", $"Run response: {responseText}");
                _host.SetStatus("Complete");
                _host.MarkComplete();
            }
            catch (OperationCanceledException)
            {
                _host.AppendLog("Python3", "Warning", "Cancelled.");
                _host.SetStatus("Cancelled");
                _host.MarkComplete();
            }
            catch (Exception ex)
            {
                _host.AppendLog("Python3", "Error", $"Run failed: {ex.Message}");
                _host.SetStatus("Error");
                _host.MarkComplete();
            }
        }

        public void RequestCancel()
        {
            _host.AppendLog("Python3", "Warning", "Cancel requested.");
            _host.RequestCancel();

            try
            {
                _cts?.Cancel();
            }
            catch { }

            try
            {
                string response = CancelPythonAsync().GetAwaiter().GetResult();
                _host.AppendLog("Python3", "Info", $"Cancel response: {response}");
            }
            catch (Exception ex)
            {
                _host.AppendLog("Python3", "Error", $"Cancel call failed: {ex.Message}");
            }
        }

        private async Task<string> CheckHealthAsync()
        {
            using (var response = await _http.GetAsync($"{_pythonBaseUrl}/health").ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }

        private async Task<string> InitializePythonAsync(string jsonConfig)
        {
            var payload = new
            {
                workbookName = "Python3Workbook",
                jsonConfig = jsonConfig,
                uoBaseUrl = "http://127.0.0.1:5050"
            };

            string json = JsonSerializer.Serialize(payload);
            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                using (var response = await _http.PostAsync($"{_pythonBaseUrl}/initialize", content).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
 
            }
               
         
        }

        private async Task<string> RunPythonAsync(CancellationToken token)
        {
            var payload = new
            {
                runId = Guid.NewGuid().ToString(),
                whenUtc = DateTime.UtcNow
            };

            string json = JsonSerializer.Serialize(payload);
            using (var content = new StringContent(json, Encoding.UTF8, "application/json")) {
                using (var response = await _http.PostAsync($"{_pythonBaseUrl}/run", content, token)) {
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync();
                }   
            }
           


        }

        private async Task<string> CancelPythonAsync()
        {
            var payload = new
            {
                requestedAtUtc = DateTime.UtcNow
            };

            string json = JsonSerializer.Serialize(payload);
            using (var content = new StringContent(json, Encoding.UTF8, "application/json")) {
                using (var response = await _http.PostAsync($"{_pythonBaseUrl}/cancel", content))
                {
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync();
                }
            }
         

    
        }

        public void Dispose()
        {
            try { _cts?.Cancel(); } catch { }
            _cts?.Dispose();
            _http?.Dispose();
        }
    }
}