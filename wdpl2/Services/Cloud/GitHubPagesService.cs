using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Wdpl2.Services;

/// <summary>
/// Service for deploying static websites to GitHub Pages
/// </summary>
public sealed class GitHubPagesService
{
    private readonly string _token;
    private readonly string _username;
    private readonly string _repoName;
    private readonly HttpClient _httpClient;
    
    private const string GitHubApiBase = "https://api.github.com";
    
    public GitHubPagesService(string token, string username, string repoName)
    {
        _token = token;
        _username = username;
        _repoName = repoName;
        
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("WDPL-App", "1.0"));
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }
    
    /// <summary>
    /// Deploy website files to GitHub Pages
    /// </summary>
    public async Task<(bool success, string message, string? siteUrl)> DeployAsync(
        Dictionary<string, string> files,
        bool createRepoIfNotExists = true,
        IProgress<string>? progress = null)
    {
        try
        {
            progress?.Report("Checking repository...");
            
            // Check if repo exists
            var repoExists = await CheckRepoExistsAsync();
            
            if (!repoExists)
            {
                if (!createRepoIfNotExists)
                {
                    return (false, $"Repository '{_repoName}' does not exist.", null);
                }
                
                progress?.Report("Creating repository...");
                var created = await CreateRepositoryAsync();
                if (!created)
                {
                    return (false, "Failed to create repository.", null);
                }
                
                // Wait a moment for GitHub to fully create the repository
                await Task.Delay(2000);
            }
            
            progress?.Report("Preparing files...");
            
            // Get current tree SHA (if repo has commits)
            var (treeSha, commitSha) = await GetCurrentTreeAsync();
            
            // Create blobs for all files
            var treeItems = new List<object>();
            var totalFiles = files.Count;
            var processedFiles = 0;
            
            foreach (var file in files)
            {
                processedFiles++;
                progress?.Report($"Uploading {file.Key} ({processedFiles}/{totalFiles})...");
                
                var blobSha = await CreateBlobAsync(file.Value);
                if (string.IsNullOrEmpty(blobSha))
                {
                    return (false, $"Failed to create blob for {file.Key}", null);
                }
                
                treeItems.Add(new
                {
                    path = file.Key,
                    mode = "100644",
                    type = "blob",
                    sha = blobSha
                });
            }
            
            progress?.Report("Creating tree...");
            
            // Create new tree
            var newTreeSha = await CreateTreeAsync(treeItems, treeSha);
            if (string.IsNullOrEmpty(newTreeSha))
            {
                return (false, "Failed to create tree.", null);
            }
            
            progress?.Report("Creating commit...");
            
            // Create commit
            var newCommitSha = await CreateCommitAsync(newTreeSha, commitSha, 
                $"Deploy website - {DateTime.Now:yyyy-MM-dd HH:mm}");
            if (string.IsNullOrEmpty(newCommitSha))
            {
                return (false, "Failed to create commit.", null);
            }
            
            progress?.Report("Updating branch...");
            
            // Update branch reference
            var updated = await UpdateBranchAsync(newCommitSha, commitSha == null);
            if (!updated)
            {
                return (false, "Failed to update branch.", null);
            }
            
            progress?.Report("Enabling GitHub Pages...");
            
            // Enable GitHub Pages with retry
            var pagesEnabled = await EnableGitHubPagesWithRetryAsync();
            
            var siteUrl = $"https://{_username}.github.io/{_repoName}/";
            
            progress?.Report("Deployment complete!");
            
            var message = $"Successfully deployed {files.Count} files to GitHub Pages!";
            if (!pagesEnabled)
            {
                message += "\n\nNote: GitHub Pages may need to be manually enabled. Go to repository Settings > Pages and select 'main' branch.";
            }
            
            return (true, message, siteUrl);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GitHub Pages deployment error: {ex}");
            return (false, $"Deployment failed: {ex.Message}", null);
        }
    }
    
    /// <summary>
    /// Validate credentials and connection
    /// </summary>
    public async Task<(bool success, string message)> ValidateConnectionAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{GitHubApiBase}/user");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var user = JsonSerializer.Deserialize<JsonElement>(content);
                var login = user.GetProperty("login").GetString();
                
                if (login?.Equals(_username, StringComparison.OrdinalIgnoreCase) == true)
                {
                    return (true, $"Connected as {login}");
                }
                else
                {
                    return (false, $"Token belongs to '{login}', not '{_username}'");
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return (false, "Invalid token. Please check your GitHub Personal Access Token.");
            }
            else
            {
                return (false, $"GitHub API error: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Connection failed: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Check GitHub Pages status for the repository
    /// </summary>
    public async Task<(bool enabled, string? url, string? status, string? buildError)> GetPagesStatusAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{GitHubApiBase}/repos/{_username}/{_repoName}/pages");
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return (false, null, "not_enabled", null);
            }
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return (false, null, "error", errorContent);
            }
            
            var content = await response.Content.ReadAsStringAsync();
            var pages = JsonSerializer.Deserialize<JsonElement>(content);
            
            var url = pages.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() : null;
            var status = pages.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : "unknown";
            
            // Check for build error
            string? buildError = null;
            if (pages.TryGetProperty("build_type", out var buildType) && 
                pages.TryGetProperty("source", out var source))
            {
                // Pages is configured
            }
            
            return (true, url, status, buildError);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetPagesStatus error: {ex}");
            return (false, null, "error", ex.Message);
        }
    }
    
    private async Task<bool> CheckRepoExistsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{GitHubApiBase}/repos/{_username}/{_repoName}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
    
    private async Task<bool> CreateRepositoryAsync()
    {
        try
        {
            var payload = new
            {
                name = _repoName,
                description = "Pool League Website - Generated by WDPL App",
                @private = false,
                auto_init = false,
                has_issues = false,
                has_projects = false,
                has_wiki = false
            };
            
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{GitHubApiBase}/user/repos", content);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
    
    private async Task<(string? treeSha, string? commitSha)> GetCurrentTreeAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{GitHubApiBase}/repos/{_username}/{_repoName}/git/refs/heads/main");
            
            if (!response.IsSuccessStatusCode)
            {
                // Try 'master' branch
                response = await _httpClient.GetAsync(
                    $"{GitHubApiBase}/repos/{_username}/{_repoName}/git/refs/heads/master");
            }
            
            if (!response.IsSuccessStatusCode)
            {
                return (null, null);
            }
            
            var content = await response.Content.ReadAsStringAsync();
            var refData = JsonSerializer.Deserialize<JsonElement>(content);
            var commitSha = refData.GetProperty("object").GetProperty("sha").GetString();
            
            // Get the tree SHA from the commit
            var commitResponse = await _httpClient.GetAsync(
                $"{GitHubApiBase}/repos/{_username}/{_repoName}/git/commits/{commitSha}");
            
            if (!commitResponse.IsSuccessStatusCode)
            {
                return (null, commitSha);
            }
            
            var commitContent = await commitResponse.Content.ReadAsStringAsync();
            var commitData = JsonSerializer.Deserialize<JsonElement>(commitContent);
            var treeSha = commitData.GetProperty("tree").GetProperty("sha").GetString();
            
            return (treeSha, commitSha);
        }
        catch
        {
            return (null, null);
        }
    }
    
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
                return null;
            }
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var blob = JsonSerializer.Deserialize<JsonElement>(responseContent);
            return blob.GetProperty("sha").GetString();
        }
        catch
        {
            return null;
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
                return null;
            }
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var tree = JsonSerializer.Deserialize<JsonElement>(responseContent);
            return tree.GetProperty("sha").GetString();
        }
        catch
        {
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
                return null;
            }
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var commit = JsonSerializer.Deserialize<JsonElement>(responseContent);
            return commit.GetProperty("sha").GetString();
        }
        catch
        {
            return null;
        }
    }
    
    private async Task<bool> UpdateBranchAsync(string commitSha, bool createNew)
    {
        try
        {
            if (createNew)
            {
                // Create new ref
                var payload = new
                {
                    @ref = "refs/heads/main",
                    sha = commitSha
                };
                
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync(
                    $"{GitHubApiBase}/repos/{_username}/{_repoName}/git/refs", content);
                
                return response.IsSuccessStatusCode;
            }
            else
            {
                // Update existing ref
                var payload = new
                {
                    sha = commitSha,
                    force = true
                };
                
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var request = new HttpRequestMessage(HttpMethod.Patch, 
                    $"{GitHubApiBase}/repos/{_username}/{_repoName}/git/refs/heads/main")
                {
                    Content = content
                };
                
                var response = await _httpClient.SendAsync(request);
                
                if (!response.IsSuccessStatusCode)
                {
                    // Try master branch
                    request = new HttpRequestMessage(HttpMethod.Patch, 
                        $"{GitHubApiBase}/repos/{_username}/{_repoName}/git/refs/heads/master")
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };
                    response = await _httpClient.SendAsync(request);
                }
                
                return response.IsSuccessStatusCode;
            }
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Enable GitHub Pages with retry logic
    /// </summary>
    private async Task<bool> EnableGitHubPagesWithRetryAsync()
    {
        // First check if Pages is already enabled
        var (enabled, _, status, _) = await GetPagesStatusAsync();
        if (enabled && status != "not_enabled")
        {
            System.Diagnostics.Debug.WriteLine($"GitHub Pages already enabled, status: {status}");
            return true;
        }
        
        // Try to enable Pages
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            System.Diagnostics.Debug.WriteLine($"Enabling GitHub Pages, attempt {attempt}...");
            
            try
            {
                var payload = new
                {
                    source = new
                    {
                        branch = "main",
                        path = "/"
                    },
                    build_type = "legacy"
                };
                
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                // Try POST first (for new setup)
                var response = await _httpClient.PostAsync(
                    $"{GitHubApiBase}/repos/{_username}/{_repoName}/pages", content);
                
                if (response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine("GitHub Pages enabled successfully via POST");
                    return true;
                }
                
                // If POST failed (already exists), try PUT to update
                if (response.StatusCode == System.Net.HttpStatusCode.Conflict ||
                    response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
                {
                    var putRequest = new HttpRequestMessage(HttpMethod.Put, 
                        $"{GitHubApiBase}/repos/{_username}/{_repoName}/pages")
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };
                    
                    response = await _httpClient.SendAsync(putRequest);
                    if (response.IsSuccessStatusCode)
                    {
                        System.Diagnostics.Debug.WriteLine("GitHub Pages updated successfully via PUT");
                        return true;
                    }
                }
                
                var responseContent = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Enable Pages response: {response.StatusCode} - {responseContent}");
                
                // Wait before retry
                if (attempt < 3)
                {
                    await Task.Delay(2000);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Enable Pages error: {ex.Message}");
            }
        }
        
        System.Diagnostics.Debug.WriteLine("Failed to enable GitHub Pages after 3 attempts");
        return false;
    }
}
