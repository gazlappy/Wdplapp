using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Syncs league data to the GitHub Pages repository so it can be shared across machines.
/// Uses the Git Data API (blob → tree → commit → update ref) to avoid 409 Conflict errors
/// that occur when the Contents API collides with website deployments.
/// The file is stored at <c>data/league-data.json</c> and accessible via the custom domain.
/// </summary>
public sealed class CloudSyncService : IDisposable
{
    private const string GitHubApiBase = "https://api.github.com";
    private const string SyncFilePath = "data/league-data.json";

    private readonly HttpClient _httpClient;
    private readonly string _username;
    private readonly string _repoName;
    private string _lastApiError = "";

    public CloudSyncService(string token, string username, string repoName)
    {
        _username = username;
        _repoName = repoName;

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("WDPL-App", "1.0"));
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    /// <summary>
    /// Push the current league data to the GitHub repository using the Git Data API.
    /// This avoids the 409 Conflict issues that the Contents API has when website
    /// deployments modify the same branch concurrently.
    /// </summary>
    public async Task<(bool success, string message)> PushAsync(LeagueData data, IProgress<string>? progress = null)
    {
        const int maxRetries = 3;

        try
        {
            progress?.Report("Preparing data for upload...");

            // Strip secrets so GitHub push protection doesn't block the upload
            var sanitised = SanitiseForCloud(data);

            var json = JsonSerializer.Serialize(sanitised, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            });

            var dataSizeMB = json.Length / (1024.0 * 1024.0);
            System.Diagnostics.Debug.WriteLine($"[CloudSync] Data size: {dataSizeMB:F2} MB ({json.Length:N0} bytes)");

            // GitHub blob limit is 100 MB but practical limit is lower due to base64 overhead
            if (dataSizeMB > 80)
                return (false, $"Data too large ({dataSizeMB:F1} MB). Remove some gallery images or logos to reduce size.");

            // Create the blob once (file content doesn't change between retries)
            progress?.Report($"Uploading data ({dataSizeMB:F1} MB)...");
            var blobSha = await CreateBlobAsync(json);
            if (string.IsNullOrEmpty(blobSha))
                return (false, $"Failed to upload data blob. {_lastApiError}");

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                progress?.Report(attempt == 1 ? "Creating commit..." : $"Retrying ({attempt}/{maxRetries})...");

                // 1. Get the latest commit & tree SHA
                var (treeSha, commitSha) = await GetCurrentTreeAsync();
                if (treeSha == null && commitSha == null)
                    return (false, $"Failed to read repository branch. {_lastApiError}");

                // 2. Create a new tree that adds/updates our sync file, preserving all other files
                var treeItems = new List<object>
                {
                    new { path = SyncFilePath, mode = "100644", type = "blob", sha = blobSha }
                };
                var newTreeSha = await CreateTreeAsync(treeItems, treeSha);
                if (string.IsNullOrEmpty(newTreeSha))
                    return (false, $"Failed to create file tree. {_lastApiError}");

                // 3. Create a commit on top of the current HEAD
                var newCommitSha = await CreateCommitAsync(newTreeSha, commitSha,
                    $"Sync league data - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                if (string.IsNullOrEmpty(newCommitSha))
                    return (false, $"Failed to create commit. {_lastApiError}");

                // 4. Fast-forward the branch ref to our new commit
                var updated = await UpdateRefAsync(newCommitSha!);
                if (updated)
                {
                    progress?.Report("Data synced successfully!");
                    return (true, "League data pushed to cloud successfully.");
                }

                // Ref update failed — the branch moved between our read and write (concurrent deploy)
                System.Diagnostics.Debug.WriteLine($"[CloudSync] Ref update failed on attempt {attempt}");
                if (attempt < maxRetries)
                {
                    progress?.Report("Branch was updated by another operation, retrying...");
                    await Task.Delay(1500 * attempt);
                }
            }

            return (false, "Sync failed after multiple retries. A website deployment may be in progress — wait a moment and try again.");
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CloudSync] Push network error: {ex}");
            return (false, $"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CloudSync] Push error: {ex}");
            return (false, $"Sync failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Pull the league data from the GitHub repository.
    /// Returns the deserialized data, or null if the file doesn't exist or an error occurs.
    /// </summary>
    public async Task<(bool success, string message, LeagueData? data)> PullAsync(IProgress<string>? progress = null)
    {
        try
        {
            progress?.Report("Downloading league data...");

            var response = await _httpClient.GetAsync(
                $"{GitHubApiBase}/repos/{_username}/{_repoName}/contents/{SyncFilePath}");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                progress?.Report("No cloud data found.");
                return (false, "No league data found in the cloud. Push your data first.", null);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[CloudSync] Pull failed: {response.StatusCode} - {errorBody}");
                return (false, $"Download failed ({response.StatusCode}).", null);
            }

            progress?.Report("Parsing data...");
            var body = await response.Content.ReadAsStringAsync();
            var fileInfo = JsonSerializer.Deserialize<JsonElement>(body);

            // GitHub Contents API returns base64-encoded content
            var base64Content = fileInfo.GetProperty("content").GetString() ?? "";
            // GitHub adds newlines in the base64 string — strip them
            base64Content = base64Content.Replace("\n", "").Replace("\r", "");
            var jsonBytes = Convert.FromBase64String(base64Content);
            var json = Encoding.UTF8.GetString(jsonBytes);

            var data = JsonSerializer.Deserialize<LeagueData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (data == null)
            {
                return (false, "Downloaded data was empty or corrupt.", null);
            }

            progress?.Report("Data downloaded successfully!");
            return (true, "League data pulled from cloud successfully.", data);
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CloudSync] Pull network error: {ex}");
            return (false, $"Network error: {ex.Message}", null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CloudSync] Pull error: {ex}");
            return (false, $"Sync failed: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Validate that the credentials work and the repository exists.
    /// </summary>
    public async Task<(bool success, string message)> ValidateAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{GitHubApiBase}/user");
            if (!response.IsSuccessStatusCode)
                return (false, "Invalid GitHub token.");

            var userJson = await response.Content.ReadAsStringAsync();
            var user = JsonSerializer.Deserialize<JsonElement>(userJson);
            var login = user.GetProperty("login").GetString();

            if (!string.Equals(login, _username, StringComparison.OrdinalIgnoreCase))
                return (false, $"Token belongs to '{login}', not '{_username}'.");

            // Check repo exists
            var repoResponse = await _httpClient.GetAsync(
                $"{GitHubApiBase}/repos/{_username}/{_repoName}");

            if (!repoResponse.IsSuccessStatusCode)
                return (false, $"Repository '{_repoName}' not found.");

            return (true, $"Connected as {login}. Repository '{_repoName}' found.");
        }
        catch (Exception ex)
        {
            return (false, $"Connection failed: {ex.Message}");
        }
    }

    #region Git Data API helpers

    private async Task<string?> CreateBlobAsync(string fileContent)
    {
        try
        {
            var payload = new
            {
                content = Convert.ToBase64String(Encoding.UTF8.GetBytes(fileContent)),
                encoding = "base64"
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                $"{GitHubApiBase}/repos/{_username}/{_repoName}/git/blobs", content);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[CloudSync] CreateBlob failed: {response.StatusCode} - {err}");
                _lastApiError = $"GitHub {(int)response.StatusCode}: {TruncateError(err)}";
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var blob = JsonSerializer.Deserialize<JsonElement>(responseContent);
            return blob.GetProperty("sha").GetString();
        }
        catch (TaskCanceledException)
        {
            _lastApiError = "Request timed out. Data may be too large.";
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CloudSync] CreateBlob error: {ex}");
            _lastApiError = ex.Message;
            return null;
        }
    }

    private async Task<(string? treeSha, string? commitSha)> GetCurrentTreeAsync()
    {
        try
        {
            // Try 'main' branch first
            var response = await _httpClient.GetAsync(
                $"{GitHubApiBase}/repos/{_username}/{_repoName}/git/refs/heads/main");

            if (!response.IsSuccessStatusCode)
            {
                // Fall back to 'master'
                response = await _httpClient.GetAsync(
                    $"{GitHubApiBase}/repos/{_username}/{_repoName}/git/refs/heads/master");
            }

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                _lastApiError = $"Branch not found ({response.StatusCode}). {TruncateError(err)}";
                return (null, null);
            }

            var content = await response.Content.ReadAsStringAsync();
            var refData = JsonSerializer.Deserialize<JsonElement>(content);
            var commitSha = refData.GetProperty("object").GetProperty("sha").GetString();

            // Get the tree SHA from the commit
            var commitResponse = await _httpClient.GetAsync(
                $"{GitHubApiBase}/repos/{_username}/{_repoName}/git/commits/{commitSha}");

            if (!commitResponse.IsSuccessStatusCode)
                return (null, commitSha);

            var commitContent = await commitResponse.Content.ReadAsStringAsync();
            var commitData = JsonSerializer.Deserialize<JsonElement>(commitContent);
            var treeSha = commitData.GetProperty("tree").GetProperty("sha").GetString();

            return (treeSha, commitSha);
        }
        catch (Exception ex)
        {
            _lastApiError = ex.Message;
            return (null, null);
        }
    }

    private async Task<string?> CreateTreeAsync(List<object> treeItems, string? baseTreeSha)
    {
        try
        {
            object payload = baseTreeSha != null
                ? new { base_tree = baseTreeSha, tree = treeItems }
                : new { tree = treeItems };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                $"{GitHubApiBase}/repos/{_username}/{_repoName}/git/trees", content);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                _lastApiError = $"Tree creation failed ({response.StatusCode}). {TruncateError(err)}";
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var tree = JsonSerializer.Deserialize<JsonElement>(responseContent);
            return tree.GetProperty("sha").GetString();
        }
        catch (Exception ex)
        {
            _lastApiError = ex.Message;
            return null;
        }
    }

    private async Task<string?> CreateCommitAsync(string treeSha, string? parentSha, string message)
    {
        try
        {
            object payload = parentSha != null
                ? new { message, tree = treeSha, parents = new[] { parentSha } }
                : new { message, tree = treeSha };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                $"{GitHubApiBase}/repos/{_username}/{_repoName}/git/commits", content);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                _lastApiError = $"Commit failed ({response.StatusCode}). {TruncateError(err)}";
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var commit = JsonSerializer.Deserialize<JsonElement>(responseContent);
            return commit.GetProperty("sha").GetString();
        }
        catch (Exception ex)
        {
            _lastApiError = ex.Message;
            return null;
        }
    }

    private async Task<bool> UpdateRefAsync(string commitSha)
    {
        try
        {
            var payload = new { sha = commitSha, force = true };
            var json = JsonSerializer.Serialize(payload);

            // Try 'main' branch
            var request = new HttpRequestMessage(HttpMethod.Patch,
                $"{GitHubApiBase}/repos/{_username}/{_repoName}/git/refs/heads/main")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                // Fall back to 'master'
                request = new HttpRequestMessage(HttpMethod.Patch,
                    $"{GitHubApiBase}/repos/{_username}/{_repoName}/git/refs/heads/master")
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                response = await _httpClient.SendAsync(request);
            }

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static string TruncateError(string error)
    {
        if (string.IsNullOrEmpty(error)) return "";
        // Try to extract the "message" field from GitHub's JSON error response
        try
        {
            var doc = JsonSerializer.Deserialize<JsonElement>(error);
            if (doc.TryGetProperty("message", out var msg))
                return msg.GetString() ?? "";
        }
        catch { }
        return error.Length > 200 ? error[..200] + "..." : error;
    }

    #endregion

    /// <summary>
    /// Return a deep-serialised clone of the data with all credentials blanked out.
    /// GitHub push protection scans blob content for secrets (tokens, passwords, API keys)
    /// and returns 422 if any are found.
    /// </summary>
    private static LeagueData SanitiseForCloud(LeagueData source)
    {
        // Deep-clone via JSON round-trip so the original is never mutated
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var clone = JsonSerializer.Deserialize<LeagueData>(
            JsonSerializer.Serialize(source, opts), opts)!;

        var ws = clone.WebsiteSettings;
        ws.GitHubToken = "";
        ws.GitHubUsername = "";
        ws.GitHubRepoName = "";
        ws.FtpHost = "";
        ws.FtpUsername = "";
        ws.FtpPassword = "";
        ws.FormServiceApiToken = "";
        ws.FormServiceUrl = "";
        ws.FormServiceFetchUrl = "";

        return clone;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
