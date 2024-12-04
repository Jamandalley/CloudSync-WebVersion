using CloudSync.Services;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;

namespace CloudSync2.Services.Implementation;

public class GoogleDriveService(
    IAuthenticationService authService,
    IConfiguration configuration,
    ILogger<GoogleDriveService> logger)
    : IGoogleDriveService
{
    private readonly IAuthenticationService _authService = authService;
    private static readonly string[] Scopes = [DriveService.Scope.DriveFile];
    private const string ApplicationName = "CloudSync";

    public async Task<Google.Apis.Drive.v3.Data.File> UploadFileAsync(string email, byte[] fileBytes, string originalFileName)
    {
        try
        {
            logger.LogInformation("Starting file upload process for {OriginalFileName}", originalFileName);

            // Get user credentials
            UserCredential credential = await GetCredentialAsync(email);
            logger.LogInformation("User credential obtained for email: {Email}", email);

            // Verify credentials
            if (!await VerifyCredentialAsync(credential))
            {
                throw new Exception("Invalid or expired credential");
            }

            // Initialize the Google Drive service
            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            // Set up file metadata
            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = originalFileName
            };

            logger.LogInformation("File metadata created for {OriginalFileName}", originalFileName);

            FilesResource.CreateMediaUpload request;
            
            // Use the provided byte array to create a MemoryStream
            using (var stream = new MemoryStream(fileBytes))
            {
                // Create the upload request
                request = service.Files.Create(fileMetadata, stream, GetMimeType(originalFileName));
                request.Fields = "id, name, size, createdTime";

                logger.LogInformation("Upload request created for {OriginalFileName}", originalFileName);

                // Upload the file
                var progress = await request.UploadAsync();

                // Check the upload status
                if (progress.Status == UploadStatus.Completed)
                {
                    logger.LogInformation("File upload completed successfully for {OriginalFileName}", originalFileName);
                }
                else
                {
                    logger.LogError("File upload failed for {OriginalFileName}. Status: {Status}", originalFileName, progress.Status);
                    throw new Exception($"File upload failed: {progress.Exception?.Message}");
                }
            }

            // Retrieve the uploaded file information
            var file = request.ResponseBody;
            logger.LogInformation("File uploaded: ID: {FileId}, Name: {FileName}, Size: {FileSize}, Created: {CreatedTime}", 
                file.Id, file.Name, file.Size, file.CreatedTime);

            return file;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in UploadFileAsync for file {OriginalFileName}", originalFileName);
            throw;
        }
    }


    private async Task<UserCredential> GetCredentialAsync(string email)
    {
        try
        {
            logger.LogInformation("Attempting to get user credential for email: {Email}", email);
            
            var clientSecrets = new ClientSecrets
            {
                ClientId = configuration["Google:ClientId"],
                ClientSecret = configuration["Google:ClientSecret"]
            };

            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                clientSecrets,
                Scopes,
                email,
                CancellationToken.None,
                new FileDataStore("token_store"));

            logger.LogInformation("User credential obtained successfully for email: {Email}", email);
            return credential;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in GetCredentialAsync for email {Email}", email);
            throw;
        }
    }

    private async Task<bool> VerifyCredentialAsync(UserCredential credential)
    {
        try
        {
            logger.LogInformation("Verifying user credential");
            
            if (credential.Token.IsExpired(credential.Flow.Clock))
            {
                logger.LogInformation("Token is expired, attempting to refresh");
                await credential.RefreshTokenAsync(CancellationToken.None);
            }

            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            // Attempt a simple API call to verify the credential
            var request = service.Files.List();
            request.PageSize = 1;
            request.Fields = "files(id, name)";
            var result = await request.ExecuteAsync();

            logger.LogInformation("Credential verification successful");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in VerifyCredentialAsync");
            return false;
        }
    }

    private string GetMimeType(string fileName)
    {
        var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
        string contentType;
        if (!provider.TryGetContentType(fileName, out contentType))
        {
            contentType = "application/octet-stream";
        }
        return contentType;
    }
}