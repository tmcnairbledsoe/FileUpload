using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace FileUpload.Data
{
    public class FileServiceApp
    {
        private readonly string _blobConnectionString;
        private readonly string _sqlConnectionString;

        public FileServiceApp(IConfiguration configuration)
        {
            _blobConnectionString = configuration.GetConnectionString("AzureBlobStorage");
            _sqlConnectionString = configuration.GetConnectionString("AzureSql");
        }

        public async Task<string> UploadFileAsync(string fileName, Stream content)
        {
            BlobServiceClient blobServiceClient = new BlobServiceClient(_blobConnectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient("uploadedfiles");
            BlobClient blobClient = containerClient.GetBlobClient(fileName);

            // Check if file already exists in Blob Storage
            if (await blobClient.ExistsAsync())
            {
                return "File already exists";
            }

            // Copy the original stream to a MemoryStream
            using var memoryStream = new MemoryStream();
            await content.CopyToAsync(memoryStream);

            memoryStream.Position = 0;

            // Upload to Blob Storage from memoryStream
            await blobClient.UploadAsync(memoryStream);
            memoryStream.Position = 0;

            // Save metadata to Azure SQL
            using (IDbConnection db = new SqlConnection(_sqlConnectionString))
            {
                var fileContent = await new StreamReader(memoryStream).ReadToEndAsync();
                string sql = "INSERT INTO UploadedFiles (FileName, Contents, UploadTime) VALUES (@FileName, @Contents, @UploadTime)";
                await db.ExecuteAsync(sql, new { FileName = fileName, Contents = fileContent, UploadTime = DateTime.UtcNow });
            }

            return "File uploaded successfully";
        }

        public async Task<IEnumerable<FileData>> GetFilesAsync()
        {
            using (IDbConnection db = new SqlConnection(_sqlConnectionString))
            {
                string sql = "SELECT * FROM UploadedFiles";
                return await db.QueryAsync<FileData>(sql);
            }
        }

        public async Task<Stream> DownloadFileAsync(string fileName)
        {
            BlobServiceClient blobServiceClient = new BlobServiceClient(_blobConnectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient("uploadedfiles");
            BlobClient blobClient = containerClient.GetBlobClient(fileName);
            BlobDownloadInfo download = await blobClient.DownloadAsync();
            return download.Content;
        }
    }
}
