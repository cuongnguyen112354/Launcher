using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace Launcher
{
    public partial class Launcher : Form
    {
        private static string gameString = "Pulse Frenzy";
        private string rootPath = Directory.GetParent(Application.StartupPath).Parent.Parent.FullName;
        private string versionFile = Path.Combine(Directory.GetParent(Application.StartupPath).Parent.FullName, "version.txt");
        private string versionContent => File.Exists(versionFile) ? File.ReadAllText(versionFile) : "";
        private string installPath => Path.Combine(rootPath, gameString);

        private readonly HttpClient httpClient = new HttpClient();
        private readonly BackgroundWorker worker = new BackgroundWorker();

        public Launcher()
        {
            InitializeComponent();
            httpClient.Timeout = TimeSpan.FromMinutes(5); // Đặt timeout cho HttpClient
            LoadViewStateAndInit();
            label1.Text = gameString;
        }

        private async void LoadViewStateAndInit()
        {
            await ViewState.LoadFromApi();

            string buildPath = Path.Combine(installPath, $"{gameString}.exe");

            if (!File.Exists(buildPath))
            {
                controlBtn.Text = "Install";
            }
            else if (HasNewUpdate())
            {
                controlBtn.Text = "Update";
                MessageBox.Show($"New version available: {ViewState.LatestVersion}", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                controlBtn.Text = "Play";
            }

            controlBtn.Enabled = true;
            versionLb.Text = $"Version: {versionContent}";
        }

        private void controlBtn_Click(object sender, EventArgs e)
        {
            string exePath = Path.Combine(installPath, $"{gameString}.exe");

            if (File.Exists(exePath) && !HasNewUpdate())
            {
                System.Diagnostics.Process.Start(exePath);
                Application.Exit();
            }
            else
            {
                DownloadAndInstall();
            }
        }

        public static class ViewState
        {
            public static string DownloadUrl { get; set; }
            public static string LatestVersion { get; set; }

            public static async Task LoadFromApi()
            {
                try
                {
                    using (HttpClient client = new HttpClient())
                    {
                        string apiUrl = $"https://hfo6t8luc8.execute-api.ap-southeast-1.amazonaws.com/product/get";

                        var requestData = new
                        {
                            gameName = gameString
                        };

                        string json = JsonConvert.SerializeObject(requestData);

                        var content = new StringContent(json, Encoding.UTF8, "application/json");

                        HttpResponseMessage response = await client.PostAsync(apiUrl, content);
                        response.EnsureSuccessStatusCode();

                        string responseBody = await response.Content.ReadAsStringAsync();

                        var result = JsonConvert.DeserializeObject<ViewStateResponse>(responseBody);

                        if (result.found == 1)
                        {
                            DownloadUrl = result.data.linkDownload;
                            LatestVersion = result.data.version;
                        }
                        else
                        {
                            MessageBox.Show("Không tìm thấy game trên server");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Không thể lấy thông tin game từ server: " + ex.Message);
                }
            }

            public class ViewStateResponse
            {
                public int found { get; set; }
                public GameData data { get; set; }
            }

            public class GameData
            {
                public string gameName { get; set; }
                public string version { get; set; }
                public string linkDownload { get; set; }
            }
        }

        public void DownloadAndInstall()
        {
            string tempZipPath = Path.Combine(Path.GetTempPath(), $"{gameString}.zip");

            try
            {
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

                worker.WorkerReportsProgress = true;
                worker.DoWork += Worker_DoWork;
                worker.ProgressChanged += Worker_ProgressChanged;
                worker.RunWorkerCompleted += Worker_RunWorkerCompleted;

                progressBar1.Visible = true;
                lblSize.Visible = true;
                if (controlBtn.Text == "Update")
                    controlBtn.Text = "Updating";
                else
                    controlBtn.Text = "Installing";
                controlBtn.Enabled = false;

                worker.RunWorkerAsync(new { Url = ViewState.DownloadUrl, TempZipPath = tempZipPath });
            }
            catch (Exception ex)
            {
                controlBtn.Enabled = true;
                MessageBox.Show("Lỗi tổng quát khi tải file: " + ex.Message);
            }
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            var args = e.Argument as dynamic;
            string url = args.Url;
            string tempZipPath = args.TempZipPath;

            try
            {
                if (string.IsNullOrEmpty(url))
                {
                    throw new Exception("URL tải file không hợp lệ hoặc trống.");
                }

                using (HttpResponseMessage response = httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).Result)
                {
                    response.EnsureSuccessStatusCode();
                    long totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    if (totalBytes <= 0)
                    {
                        MessageBox.Show("Không thể xác định kích thước file từ server.");
                    }

                    long totalBytesRead = 0;
                    byte[] buffer = new byte[8192];
                    int bytesRead;

                    using (FileStream fs = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write))
                    using (Stream contentStream = response.Content.ReadAsStreamAsync().Result)
                    {
                        Stopwatch stopwatch = Stopwatch.StartNew();

                        while ((bytesRead = contentStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            fs.Write(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;

                            int progressPercentage = totalBytes > 0 ? (int)((totalBytesRead * 100) / totalBytes) : 0;
                            double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                            double speed = elapsedSeconds > 0 ? (totalBytesRead / 1024.0) / elapsedSeconds : 0;

                            if (worker.CancellationPending) break; // Kiểm tra hủy
                            worker.ReportProgress(progressPercentage, new
                            {
                                TotalBytesRead = totalBytesRead,
                                TotalBytes = totalBytes,
                                Speed = speed
                            });
                        }
                    }

                    if (File.Exists(tempZipPath))
                    {
                        if (Directory.Exists(installPath))
                            Directory.Delete(installPath, true);

                        Directory.CreateDirectory(installPath);
                        ZipFile.ExtractToDirectory(tempZipPath, installPath);

                        File.WriteAllText(versionFile, ViewState.LatestVersion);
                    }
                    else
                    {
                        throw new Exception("File ZIP không được tạo sau khi tải.");
                    }
                }
            }
            catch (Exception ex)
            {
                e.Result = ex;
                MessageBox.Show($"Lỗi trong Worker_DoWork: {ex.Message}");
            }
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => Worker_ProgressChanged(sender, e)));
                return;
            }

            var state = e.UserState as dynamic;
            if (state != null)
            {
                progressBar1.Value = Math.Min(e.ProgressPercentage, 100);

                double totalBytesReadMB = state.TotalBytesRead / (1024.0 * 1024.0);
                double totalBytesMB = state.TotalBytes / (1024.0 * 1024.0);
                lblSize.Text = $"{totalBytesReadMB:F2}/{totalBytesMB:F2} MB";
            }
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => Worker_RunWorkerCompleted(sender, e)));
                return;
            }

            progressBar1.Visible = false;
            lblSize.Visible = false;
            controlBtn.Enabled = true;
            controlBtn.Text = "Play";

            if (e.Error != null)
            {
                MessageBox.Show($"Lỗi khi cập nhật: {e.Error.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else if (e.Cancelled)
            {
                MessageBox.Show("Cập nhật bị hủy.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (e.Result is Exception ex)
            {
                MessageBox.Show($"Lỗi khi cập nhật: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                MessageBox.Show("Cập nhật thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                versionLb.Text = $"Version: {ViewState.LatestVersion}";
                controlBtn.Text = "Play";
            }
        }

        private bool HasNewUpdate()
        {
            if (File.Exists(versionFile))
            {
                if (ViewState.LatestVersion != versionContent)
                    return true;
                else
                    return false;
            }
            return true;
        }

        private void uninstallBtn_Click(object sender, EventArgs e)
        {
            // Tìm tất cả các file bắt đầu bằng "unins" và kết thúc bằng ".exe"
            string[] uninsFiles = Directory.GetFiles(rootPath, "unins*.exe");

            if (uninsFiles.Length > 0)
            {
                // Lấy file đầu tiên tìm được
                string uninsFile = uninsFiles[0];

                Process.Start(uninsFile);
                Application.Exit();
            }
            else
            {
                MessageBox.Show("Không tìm thấy file gỡ cài đặt", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

    }
}