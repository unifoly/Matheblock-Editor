using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace HexMap
{
    /// <summary>
    /// 自动更新管理器：在 Splash 场景启动时从更新源（腾讯云 COS）拉取 latest.json，
    /// 若有新版本则下载 zip 构建包，全程通过事件通知 UI 层显示进度。
    /// 挂载到 Splash 场景中任意持久化 GameObject 上即可。
    /// </summary>
    public class AutoUpdateManager : MonoBehaviour
    {
        // --- 更新源配置 ---

        [Header("更新源")]
        [Tooltip("latest.json 的完整 URL\n" +
                 "腾讯云 COS 示例: https://mbe-update-125xxxxxx.cos.ap-shanghai.myqcloud.com/latest.json\n" +
                 "JSON 格式: { \"version\": \"0.1.10a\", \"url\": \"zip下载直链\", \"notes\": \"更新说明\" }")]
        [SerializeField] private string m_updateFeedUrl = string.Empty;

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
        /// 拉取 latest.json 并比较版本的协程
        /// </summary>
        private IEnumerator CheckForUpdateCo()
        {
            if (string.IsNullOrEmpty(m_updateFeedUrl))
            {
                State = UpdateState.Error;
                StatusTextChanged?.Invoke("未配置更新源");
                UpdateErrorOccurred?.Invoke("请在 Inspector 中填写 latest.json 的 URL");
                yield break;
            }

            State = UpdateState.Checking;
            StatusTextChanged?.Invoke("正在检查更新…");

            using (var request = UnityWebRequest.Get(m_updateFeedUrl))
            {
                request.SetRequestHeader("User-Agent", $"{AppVersion.AppName}/{AppVersion.CurrentVersion}");
                request.timeout = 15;

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    // 记录 HTTP 状态码，便于定位 403/404/超时等真实原因
                    State = UpdateState.Error;
                    StatusTextChanged?.Invoke("更新检查失败");
                    UpdateErrorOccurred?.Invoke($"获取版本信息失败: {request.error} (HTTP {request.responseCode})");
                    yield break;
                }

                var releaseInfo = ParseFeedJson(request.downloadHandler.text);
                if (releaseInfo == null || string.IsNullOrEmpty(releaseInfo.Version))
                {
                    State = UpdateState.Error;
                    StatusTextChanged?.Invoke("更新检查失败");
                    UpdateErrorOccurred?.Invoke("latest.json 解析失败（需要 version 与 url 字段）");
                    yield break;
                }

                LatestVersion = releaseInfo.Version;
                bool hasUpdate = IsNewerVersion(releaseInfo.Version, AppVersion.CurrentVersion);

                State = hasUpdate ? UpdateState.UpdateAvailable : UpdateState.UpToDate;
                StatusTextChanged?.Invoke(hasUpdate ? "发现新版本" : "已是最新版本");

                UpdateCheckCompleted?.Invoke(hasUpdate, releaseInfo.Version, AppVersion.CurrentVersion);

                // 若有新版本且有构建包可下载，自动开始下载
                if (hasUpdate)
                {
                    if (string.IsNullOrEmpty(releaseInfo.DownloadUrl))
                    {
                        // latest.json 缺少 url 字段
                        State = UpdateState.Error;
                        StatusTextChanged?.Invoke($"发现新版本 v{releaseInfo.Version}，但缺少下载地址");
                        UpdateErrorOccurred?.Invoke("latest.json 中未提供 url（zip 下载直链）");
                    }
                    else
                    {
                        yield return StartCoroutine(DownloadUpdateCo(releaseInfo));
                    }
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
                    // 下载失败进入 Error 态，避免 UI 回落显示"就绪"
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
        /// 解析 latest.json（自定义格式）
        /// { "version": "0.1.10a", "url": "https://.../MBE.v0.1.10a.zip", "notes": "..." }
        /// </summary>
        private ReleaseInfo ParseFeedJson(string json)
        {
            try
            {
                var info = new ReleaseInfo
                {
                    Version = ExtractJsonValue(json, "version"),
                    DownloadUrl = ExtractJsonValue(json, "url"),
                    ReleaseNotes = ExtractJsonValue(json, "notes")
                };

                return string.IsNullOrEmpty(info.Version) ? null : info;
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

        /// <summary>远程发布信息</summary>
        private class ReleaseInfo
        {
            public string Version;
            public string DownloadUrl;
            public string ReleaseNotes;
        }
    }
}
