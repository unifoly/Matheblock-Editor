using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace HexMap
{
    /// <summary>
    /// 自动更新管理器：在 Splash 场景启动时检查远程版本，
    /// 若有新版本则下载 Release Assets（编译构建产物），全程通过事件通知 UI 层显示进度。
    /// 挂载到 Splash 场景中任意持久化 GameObject 上即可。
    /// </summary>
    public class AutoUpdateManager : MonoBehaviour
    {
        // --- 更新源配置 ---

        [Header("更新源")]
        [Tooltip("选择主更新源：Gitee（国内快）或 GitHub")]
        [SerializeField] private UpdateSource m_primarySource = UpdateSource.Gitee;

        [Tooltip("主源请求失败时，自动尝试备选源")]
        [SerializeField] private bool m_fallbackToSecondary = true;

        [Header("Gitee（国内推荐）")]
        [Tooltip("Gitee Releases API 地址\n" +
                 "格式: https://gitee.com/api/v5/repos/{用户名}/{仓库名}/releases/latest\n" +
                 "示例: https://gitee.com/api/v5/repos/yourname/Matheblock-Editor/releases/latest")]
        [SerializeField] private string m_giteeApiUrl = string.Empty;

        [Tooltip("Gitee 私人令牌（私有仓库必须填写，公开仓库可留空）\n" +
                 "申请地址: https://gitee.com/profile/personal_access_tokens\n" +
                 "注意: 令牌会随构建产物分发，请使用只读权限的令牌并定期更换")]
        [SerializeField] private string m_giteeAccessToken = string.Empty;

        [Header("GitHub（备选）")]
        [Tooltip("GitHub Releases API 地址\n" +
                 "格式: https://api.github.com/repos/{用户名}/{仓库名}/releases/latest\n" +
                 "示例: https://api.github.com/repos/yourname/Matheblock-Editor/releases/latest")]
        [SerializeField] private string m_githubApiUrl = string.Empty;

        [Tooltip("GitHub Personal Access Token（私有仓库必填，公开仓库可留空）\n" +
                 "申请地址: https://github.com/settings/tokens\n" +
                 "注意: 令牌会随构建产物分发，请使用只读权限的令牌并定期更换")]
        [SerializeField] private string m_githubToken = string.Empty;

        [Header("通用配置")]
        [Tooltip("下载超时时间（秒）")]
        [SerializeField] private float m_downloadTimeout = 300f;

        [Tooltip("是否在启动时自动检查更新")]
        [SerializeField] private bool m_checkOnStart = true;

        // --- 事件定义（past-tense 命名） ---

        /// <summary>更新检查完成，参数为：是否有新版本、最新版本号、当前版本号</summary>
        public event Action<bool, string, string> UpdateCheckCompleted;

        /// <summary>下载进度变化，参数为：进度百分比(0~1)、已下载字节数、总字节数</summary>
        public event Action<float, long, long> DownloadProgressChanged;

        /// <summary>下载完成，参数为：保存路径</summary>
        public event Action<string> DownloadCompleted;

        /// <summary>更新过程中发生错误，参数为：错误信息</summary>
        public event Action<string> UpdateErrorOccurred;

        /// <summary>更新状态文本变化（如"正在检查更新…"、"正在下载…"等）</summary>
        public event Action<string> StatusTextChanged;

        // --- 运行时状态 ---

        /// <summary>当前更新状态</summary>
        public UpdateState State { get; private set; } = UpdateState.Idle;

        /// <summary>远程最新版本号（检查完成后可用）</summary>
        public string LatestVersion { get; private set; }

        /// <summary>当前下载进度 (0~1)，未下载时为 0</summary>
        public float Progress { get; private set; }

        /// <summary>单例引用，供 SplashInfoDisplay 等组件获取</summary>
        public static AutoUpdateManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // 仅销毁重复组件，保留 GameObject（同对象上可能挂有 SplashInfoDisplay 等场景组件，
                // 整体销毁会导致从其他场景返回 Splash 时信息显示失效）
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (m_checkOnStart)
            {
                StartCoroutine(CheckForUpdateCo());
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 手动触发更新检查
        /// </summary>
        public void CheckForUpdate()
        {
            if (State == UpdateState.Checking || State == UpdateState.Downloading)
            {
                return;
            }

            StartCoroutine(CheckForUpdateCo());
        }

        /// <summary>
        /// 检查远程版本信息的协程
        /// 优先使用主源，失败时（若开启回退）尝试备选源
        /// </summary>
        private IEnumerator CheckForUpdateCo()
        {
            State = UpdateState.Checking;
            StatusTextChanged?.Invoke("正在检查更新…");

            // 构建尝试顺序：主源 -> 备选源
            var sources = BuildSourceList();
            if (sources.Count == 0)
            {
                State = UpdateState.Error;
                StatusTextChanged?.Invoke("未配置更新源");
                UpdateErrorOccurred?.Invoke("请在 Inspector 中填写 Gitee 或 GitHub API 地址");
                yield break;
            }

            ReleaseInfo releaseInfo = null;
            string lastError = null;

            foreach (var source in sources)
            {
                StatusTextChanged?.Invoke($"正在检查更新（{source.Name}）…");

                using (var request = UnityWebRequest.Get(source.Url))
                {
                    request.SetRequestHeader("User-Agent", $"{AppVersion.AppName}/{AppVersion.CurrentVersion}");
                    ApplyAuthorization(request, source.Url);
                    request.timeout = 15;

                    yield return request.SendWebRequest();

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        // 记录 HTTP 状态码与响应体片段，便于定位 403/404/超时等真实原因
                        lastError = $"{source.Name}: {request.error} (HTTP {request.responseCode})";
                        Debug.LogWarning($"[AutoUpdateManager] {lastError}", this);
                        continue; // 尝试下一个源
                    }

                    // Gitee 和 GitHub 的 Releases API 返回格式一致
                    releaseInfo = ParseReleaseJson(request.downloadHandler.text);
                    if (releaseInfo == null || string.IsNullOrEmpty(releaseInfo.Version))
                    {
                        lastError = $"{source.Name}: 版本信息解析失败";
                        continue;
                    }

                    // 成功获取版本信息
                    break;
                }
            }

            if (releaseInfo == null || string.IsNullOrEmpty(releaseInfo.Version))
            {
                // 检查失败应进入 Error 态而非 Idle（Idle 会显示"就绪"，掩盖真实错误）
                State = UpdateState.Error;
                StatusTextChanged?.Invoke("更新检查失败");
                UpdateErrorOccurred?.Invoke($"所有更新源均不可用: {lastError}");
                yield break;
            }

            LatestVersion = releaseInfo.Version;
            bool hasUpdate = IsNewerVersion(releaseInfo.Version, AppVersion.CurrentVersion);

            State = hasUpdate ? UpdateState.UpdateAvailable : UpdateState.UpToDate;
            StatusTextChanged?.Invoke(hasUpdate ? "发现新版本" : "已是最新版本");

            UpdateCheckCompleted?.Invoke(hasUpdate, releaseInfo.Version, AppVersion.CurrentVersion);

            // 若有新版本且有构建产物可下载，自动开始下载
            if (hasUpdate)
            {
                if (string.IsNullOrEmpty(releaseInfo.DownloadUrl))
                {
                    // Release 存在但未上传构建产物
                    State = UpdateState.Error;
                    StatusTextChanged?.Invoke($"发现新版本 v{releaseInfo.Version}，但暂无可下载的构建产物");
                    UpdateErrorOccurred?.Invoke("该版本未上传构建产物（Release Assets），无法自动下载");
                }
                else
                {
                    yield return StartCoroutine(DownloadUpdateCo(releaseInfo));
                }
            }
        }

        /// <summary>
        /// 下载更新包的协程
        /// </summary>
        private IEnumerator DownloadUpdateCo(ReleaseInfo releaseInfo)
        {
            State = UpdateState.Downloading;
            StatusTextChanged?.Invoke($"正在下载 v{releaseInfo.Version}…");
            Progress = 0f;

            var saveDir = Path.Combine(Application.persistentDataPath, "Updates");
            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
            }

            // 从下载 URL 提取文件名，兜底用版本号命名
            var fileName = ExtractFileName(releaseInfo.DownloadUrl);
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = $"MatheblockEditor_v{releaseInfo.Version}.zip";
            }

            var savePath = Path.Combine(saveDir, fileName);

            using (var request = UnityWebRequest.Get(releaseInfo.DownloadUrl))
            {
                request.SetRequestHeader("User-Agent", $"{AppVersion.AppName}/{AppVersion.CurrentVersion}");
                ApplyAuthorization(request, releaseInfo.DownloadUrl);
                request.timeout = (int)m_downloadTimeout;

                var downloadHandler = new DownloadHandlerFile(savePath);
                request.downloadHandler = downloadHandler;

                request.SendWebRequest();

                // 轮询下载进度
                while (!request.isDone)
                {
                    Progress = request.downloadProgress;
                    DownloadProgressChanged?.Invoke(
                        Progress,
                        (long)request.downloadedBytes,
                        (long)(request.downloadProgress > 0 ? request.downloadedBytes / request.downloadProgress : 0));

                    yield return null;
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    // 下载失败同样进入 Error 态，避免 UI 回落显示"就绪"
                    State = UpdateState.Error;
                    Progress = 0f;
                    StatusTextChanged?.Invoke("下载失败");
                    UpdateErrorOccurred?.Invoke($"下载更新失败: {request.error} (HTTP {request.responseCode})");
                    yield break;
                }

                Progress = 1f;
                State = UpdateState.DownloadComplete;
                StatusTextChanged?.Invoke("下载完成，请重启以应用更新");

                DownloadProgressChanged?.Invoke(1f, (long)request.downloadedBytes, (long)request.downloadedBytes);
                DownloadCompleted?.Invoke(savePath);
            }
        }

        /// <summary>
        /// 为私有仓库请求附加认证头。
        /// Gitee: Authorization: token {私人令牌}（附件下载仅支持请求头，URL 参数会 403）
        /// GitHub: Authorization: Bearer {PAT}（私有仓库 API 与附件下载均需认证）
        /// </summary>
        private void ApplyAuthorization(UnityWebRequest request, string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            if (url.Contains("gitee.com", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(m_giteeAccessToken))
            {
                request.SetRequestHeader("Authorization", $"token {m_giteeAccessToken}");
            }
            else if (url.Contains("github.com", StringComparison.OrdinalIgnoreCase) ||
                     url.Contains("githubusercontent.com", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(m_githubToken))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {m_githubToken}");
                }
            }
        }

        /// <summary>
        /// 根据主源和回退设置，构建要尝试的更新源列表
        /// </summary>
        private System.Collections.Generic.List<UpdateSourceEntry> BuildSourceList()
        {
            var list = new System.Collections.Generic.List<UpdateSourceEntry>(2);

            // 主源
            var primaryUrl = m_primarySource == UpdateSource.Gitee ? m_giteeApiUrl : m_githubApiUrl;
            if (!string.IsNullOrEmpty(primaryUrl))
            {
                list.Add(new UpdateSourceEntry(m_primarySource.ToString(), primaryUrl));
            }

            // 备选源
            if (m_fallbackToSecondary)
            {
                var secondarySource = m_primarySource == UpdateSource.Gitee ? UpdateSource.GitHub : UpdateSource.Gitee;
                var secondaryUrl = secondarySource == UpdateSource.Gitee ? m_giteeApiUrl : m_githubApiUrl;
                if (!string.IsNullOrEmpty(secondaryUrl))
                {
                    list.Add(new UpdateSourceEntry(secondarySource.ToString(), secondaryUrl));
                }
            }

            return list;
        }

        /// <summary>
        /// 解析 Releases API 返回的 JSON（Gitee 和 GitHub 格式一致）
        /// 提取 tag_name（版本号）和 assets 中的 browser_download_url（构建产物下载链接）
        /// </summary>
        private ReleaseInfo ParseReleaseJson(string json)
        {
            try
            {
                var info = new ReleaseInfo();

                // 提取 tag_name（版本号）
                info.Version = ExtractJsonValue(json, "tag_name");

                // 提取 body（发布说明）
                info.ReleaseNotes = ExtractJsonValue(json, "body");

                // 提取 Release Assets 中的编译构建产物（browser_download_url）
                // 注意：跳过源码归档（Gitee 会附带 archive/refs/... 链接），仅取真实上传的构建产物
                var assetsIndex = json.IndexOf("\"assets\"", StringComparison.OrdinalIgnoreCase);
                if (assetsIndex >= 0)
                {
                    var searchIndex = assetsIndex;
                    while (info.DownloadUrl == null)
                    {
                        var urlStart = json.IndexOf("\"browser_download_url\"", searchIndex, StringComparison.OrdinalIgnoreCase);
                        if (urlStart < 0)
                        {
                            break;
                        }

                        urlStart = json.IndexOf('"', urlStart + "\"browser_download_url\"".Length);
                        if (urlStart < 0)
                        {
                            break;
                        }

                        var urlEnd = json.IndexOf('"', urlStart + 1);
                        if (urlEnd <= urlStart)
                        {
                            break;
                        }

                        var candidateUrl = json.Substring(urlStart + 1, urlEnd - urlStart - 1);

                        // 跳过 Git 源码归档链接（zipball_url 与 archive/refs 均为源码而非构建产物）
                        if (candidateUrl.Contains("archive/refs", StringComparison.OrdinalIgnoreCase))
                        {
                            searchIndex = urlEnd;
                            continue;
                        }

                        info.DownloadUrl = candidateUrl;
                    }
                }

                // 清理版本号前缀（如 v0.2.0 -> 0.2.0）
                if (!string.IsNullOrEmpty(info.Version) && info.Version.StartsWith("v"))
                {
                    info.Version = info.Version.Substring(1);
                }

                return info;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 从 JSON 字符串中提取指定键的字符串值（简易解析，避免依赖额外库）
        /// </summary>
        private static string ExtractJsonValue(string json, string key)
        {
            var searchKey = $"\"{key}\"";
            var index = json.IndexOf(searchKey, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return null;
            }

            // 跳过 key 和冒号
            index += searchKey.Length;
            while (index < json.Length && (json[index] == ' ' || json[index] == ':' || json[index] == '\t'))
            {
                index++;
            }

            if (index >= json.Length)
            {
                return null;
            }

            // 字符串值
            if (json[index] == '"')
            {
                var end = json.IndexOf('"', index + 1);
                // 处理转义引号
                while (end > 0 && json[end - 1] == '\\')
                {
                    end = json.IndexOf('"', end + 1);
                }

                return end > index ? json.Substring(index + 1, end - index - 1) : null;
            }

            // 非字符串值（数字、布尔等），取到逗号或右括号
            var valueEnd = index;
            while (valueEnd < json.Length && json[valueEnd] != ',' && json[valueEnd] != '}' && json[valueEnd] != '\n')
            {
                valueEnd++;
            }

            return json.Substring(index, valueEnd - index).Trim();
        }

        /// <summary>
        /// 从 URL 中提取文件名
        /// </summary>
        private static string ExtractFileName(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return null;
            }

            var uri = new Uri(url);
            return Path.GetFileName(uri.LocalPath);
        }

        /// <summary>
        /// 比较版本号，判断 remote 是否比 local 新
        /// 支持带字母后缀的版本号（如 0.2.0a, 1.0.0b）
        /// </summary>
        private static bool IsNewerVersion(string remote, string local)
        {
            if (string.IsNullOrEmpty(remote) || string.IsNullOrEmpty(local))
            {
                return false;
            }

            var remoteParts = ParseVersionParts(remote);
            var localParts = ParseVersionParts(local);

            var maxLen = Math.Max(remoteParts.Length, localParts.Length);

            for (var i = 0; i < maxLen; i++)
            {
                var r = i < remoteParts.Length ? remoteParts[i] : 0;
                var l = i < localParts.Length ? localParts[i] : 0;

                if (r > l) return true;
                if (r < l) return false;
            }

            return false;
        }

        /// <summary>
        /// 将版本号字符串解析为数字数组（忽略字母后缀）
        /// </summary>
        private static int[] ParseVersionParts(string version)
        {
            // 去除前缀 v 和字母后缀
            var cleaned = version.TrimStart('v', 'V');
            var alphaIndex = cleaned.IndexOfAny(new[] { 'a', 'b', 'A', 'B', 'r', 'R' });
            if (alphaIndex > 0)
            {
                cleaned = cleaned.Substring(0, alphaIndex);
            }

            var parts = cleaned.Split('.');
            var result = new int[parts.Length];

            for (var i = 0; i < parts.Length; i++)
            {
                int.TryParse(parts[i], out result[i]);
            }

            return result;
        }

        // --- 数据模型 ---

        /// <summary>更新源类型</summary>
        public enum UpdateSource
        {
            Gitee,
            GitHub
        }

        /// <summary>更新状态枚举</summary>
        public enum UpdateState
        {
            Idle,
            Checking,
            UpdateAvailable,
            UpToDate,
            Downloading,
            DownloadComplete,
            Error
        }

        /// <summary>更新源条目（内部使用）</summary>
        private readonly struct UpdateSourceEntry
        {
            public readonly string Name;
            public readonly string Url;

            public UpdateSourceEntry(string name, string url)
            {
                Name = name;
                Url = url;
            }
        }

        /// <summary>远程发布信息</summary>
        private class ReleaseInfo
        {
            public string Version;
            public string DownloadUrl;
            public string ReleaseNotes;
        }
    }
}
