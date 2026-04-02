namespace Common;

using Model;

public interface IBlobStorageHelper
{
    Task CopyFileToPoisonAsync(string connectionString, string fileName, string containerName);
    Task CopyFileToPoisonAsync(Uri serviceUri, string fileName, string containerName);
    Task CopyFileToPoisonAsync(string connectionString, string fileName, string containerName, string poisonContainerName, bool addTimestamp = false);
    Task CopyFileToPoisonAsync(Uri serviceUri, string fileName, string containerName, string poisonContainerName, bool addTimestamp = false);

    Task<string?> UploadFileToBlobStorageAndGetUri(string connectionString, string containerName, BlobFile blobFile, bool overwrite = false);
    Task<string?> UploadFileToBlobStorageAndGetUri(Uri serviceUri, string containerName, BlobFile blobFile, bool overwrite = false);
    Task<bool> UploadFileToBlobStorage(string connectionString, string containerName, BlobFile blobFile, bool overwrite = false);
    Task<bool> UploadFileToBlobStorage(Uri serviceUri, string containerName, BlobFile blobFile, bool overwrite = false);

    Task<BlobFile> GetFileFromBlobStorage(string connectionString, string containerName, string fileName);
    Task<BlobFile> GetFileFromBlobStorage(Uri serviceUri, string containerName, string fileName);
}
