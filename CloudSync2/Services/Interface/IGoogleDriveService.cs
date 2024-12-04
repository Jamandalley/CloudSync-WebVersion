namespace CloudSync.Services;
public interface IGoogleDriveService
{
    Task<Google.Apis.Drive.v3.Data.File> UploadFileAsync(string email, byte[] fileBytes, string originalFileName);
}