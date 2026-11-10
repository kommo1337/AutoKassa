using AutoKassa.Services.Backup.YandexDisk;

namespace AutoKassa.Tests.Services.Backup.YandexDisk
{
    /// <summary>
    /// Программируемая реализация IYandexDiskApiClient.
    /// Делегаты без настройки бросают исключение — чтобы тест не прошёл случайно.
    /// </summary>
    public sealed class FakeYandexDiskApiClient : IYandexDiskApiClient
    {
        public Func<string, bool, CancellationToken, Task<DiskUploadLink>> GetUploadLinkAsyncFunc { get; set; }
            = (_, _, _) => Task.FromResult(new DiskUploadLink("https://upload.example.test/put", "PUT"));

        public Func<string, string, CancellationToken, Task<YandexDiskUploadResult>> UploadFileAsyncFunc { get; set; }
            = (_, _, _) => Task.FromResult(YandexDiskUploadResult.Created);

        public Func<string, CancellationToken, Task<DiskResourceInfo>> GetResourceAsyncFunc { get; set; }
            = (path, _) => throw new InvalidOperationException($"GetResourceAsync не настроен для {path}");

        public Func<CancellationToken, Task<IReadOnlyList<DiskResourceInfo>>> GetAppFolderResourcesAsyncFunc { get; set; }
            = _ => Task.FromResult<IReadOnlyList<DiskResourceInfo>>(new List<DiskResourceInfo>());

        public Func<string, CancellationToken, Task> DeleteResourceAsyncFunc { get; set; }
            = (_, _) => Task.CompletedTask;

        public int GetUploadLinkCallCount { get; private set; }
        public int UploadFileCallCount { get; private set; }
        public int GetResourceCallCount { get; private set; }
        public int GetAppFolderResourcesCallCount { get; private set; }
        public int DeleteResourceCallCount { get; private set; }

        public List<string> UploadedLocalPaths { get; } = new();
        public List<string> DeletedRemotePaths { get; } = new();
        public List<(string Path, bool Overwrite)> GetUploadLinkCalls { get; } = new();

        public void ResetCounters()
        {
            GetUploadLinkCallCount = 0;
            UploadFileCallCount = 0;
            GetResourceCallCount = 0;
            GetAppFolderResourcesCallCount = 0;
            DeleteResourceCallCount = 0;
            UploadedLocalPaths.Clear();
            DeletedRemotePaths.Clear();
            GetUploadLinkCalls.Clear();
        }

        public Task<DiskUploadLink> GetUploadLinkAsync(string remotePath, bool overwrite, CancellationToken ct = default)
        {
            GetUploadLinkCallCount++;
            GetUploadLinkCalls.Add((remotePath, overwrite));
            return GetUploadLinkAsyncFunc(remotePath, overwrite, ct);
        }

        public Task<YandexDiskUploadResult> UploadFileAsync(string href, string localFilePath, CancellationToken ct = default)
        {
            UploadFileCallCount++;
            UploadedLocalPaths.Add(localFilePath);
            return UploadFileAsyncFunc(href, localFilePath, ct);
        }

        public Task<DiskResourceInfo> GetResourceAsync(string remotePath, CancellationToken ct = default)
        {
            GetResourceCallCount++;
            return GetResourceAsyncFunc(remotePath, ct);
        }

        public Task<IReadOnlyList<DiskResourceInfo>> GetAppFolderResourcesAsync(CancellationToken ct = default)
        {
            GetAppFolderResourcesCallCount++;
            return GetAppFolderResourcesAsyncFunc(ct);
        }

        public Task DeleteResourceAsync(string remotePath, CancellationToken ct = default)
        {
            DeleteResourceCallCount++;
            DeletedRemotePaths.Add(remotePath);
            return DeleteResourceAsyncFunc(remotePath, ct);
        }
    }
}
