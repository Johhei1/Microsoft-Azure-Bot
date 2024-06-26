using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

public class BlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;

    public BlobStorageService(IConfiguration configuration)
    {
        var connectionString = configuration["AzureBlobStorage:ConnectionString"];
        _containerName = configuration["AzureBlobStorage:ContainerName"];

        _blobServiceClient = new BlobServiceClient(connectionString);
    }

    public async Task<Stream> DownloadFileAsync(string fileName)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient(fileName);

        var downloadResponse = await blobClient.DownloadAsync();
        return downloadResponse.Value.Content;
    }

    public async Task UploadFileAsync(string blobName, Stream fileStream)
    {
        var blobContainerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        await blobContainerClient.CreateIfNotExistsAsync(); // Ensure the container exists
        var blobClient = blobContainerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(fileStream, overwrite: true);
    }
}
