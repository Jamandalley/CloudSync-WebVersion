using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using CloudSync.Models;
using CloudSync.Services;
using Microsoft.Extensions.Logging;
using CloudSync2.Extensions;
using CloudSync2.Models_copy;

namespace CloudSync.Controllers;

public class HomeController : Controller
{
    private readonly IAuthenticationService _authService;
    private readonly IGoogleDriveService _driveService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        IAuthenticationService authService, 
        IGoogleDriveService driveService,
        ILogger<HomeController> logger)
    {
        _authService = authService;
        _driveService = driveService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Auth()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Success()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Upload(List<IFormFile> files)
    {
        _logger.LogInformation($"Files to be uploaded {files}");

        if (files == null || files.Count == 0)
        {
            return BadRequest("No files uploaded.");
        }

        try
        {
            // Store files temporarily
            var tempFiles = new Dictionary<byte [], string>();
            
            int fileIndex = 0;
            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await file.CopyToAsync(memoryStream);
                        byte[] fileBytes = memoryStream.ToArray();

                        // Store the file as a byte array in the session with unique keys
                        HttpContext.Session.SetByteArray($"UploadedFile_{fileIndex}", fileBytes);
                        HttpContext.Session.SetString($"UploadedFileName_{fileIndex}", file.FileName);
                        HttpContext.Session.SetString($"UploadedFileContentType_{fileIndex}", file.ContentType);
                    }
                    fileIndex++;
                }
            }

            _logger.LogInformation("Files uploaded successfully. Please proceed with authorization.");

            return Ok(new { message = "Files uploaded successfully. Please proceed with authorization." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during file upload");
            return StatusCode(500, "An error occurred during file upload. Please try again.");
        }
    }


    [HttpPost]
    public async Task<IActionResult> Authorize([FromBody] AuthorizationRequest request)
    {
        _logger.LogInformation("Initializing Authoriztion...");
        try
        {
            var isAuthenticated = await _authService.IsUserAuthenticatedAsync(request.Email);
            if (isAuthenticated)
            {
                _logger.LogInformation("User already authenticated");
                await UploadFiles(request.Email);
                return Ok(new { isAuthenticated = true });
            }
            else
            {
                HttpContext.Session.SetString("UserEmail", request.Email);
                var authUrl = await _authService.GetAuthorizationUrlAsync(request.Email);
                return Ok(new { isAuthenticated = false, authUrl });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during authorization for email: {Email}", request.Email);
            return StatusCode(500, $"An error occurred during authorization: {ex.Message}");
        }
    }

    [HttpGet]
    public async Task<IActionResult> AuthCallback(string code, string error, string state)
    {
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogError("Error during OAuth callback: {Error}", error);
            return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        try
        {
            var email = state; // Use the state parameter as the email
            if (string.IsNullOrEmpty(email))
            {
                throw new Exception("User email not found in state parameter.");
            }

            await _authService.ExchangeCodeForTokenAsync(code, email);
            await UploadFiles(email);
            return RedirectToAction("Success");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during auth callback");
            return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }

    // private async Task<bool> UploadFiles(string email)
    // {
    //     // Retrieve the file details from the session
    //     byte[] fileBytes = HttpContext.Session.Get("UploadedFile");
    //     string fileName = HttpContext.Session.GetString("UploadedFileName");
    //     string contentType = HttpContext.Session.GetString("UploadedFileContentType");

    //     _logger.LogInformation("The retrieved file from session is: {FileName}", fileName);
        
    //     // Check if there is a file to upload
    //     if (fileBytes == null || string.IsNullOrEmpty(fileName))
    //     {
    //         _logger.LogWarning("No files to upload for email: {Email}", email);
    //         return true; // No files to upload is not an error condition
    //     }
    //     else
    //     {
    //         try
    //         {
    //             // Upload the file to Google Drive
    //             var uploadedFile = await _driveService.UploadFileAsync(email, fileBytes, fileName);
    //             _logger.LogInformation("File uploaded successfully. Google Drive File ID: {FileId}", uploadedFile.Id);
    //         }
    //         catch (Exception ex)
    //         {
    //             _logger.LogError(ex, "Error uploading file {FileName} for email {Email}", fileName, email);
    //             return false; // Return false if the upload failed
    //         }
    //     }
        
    //     // If only the primary file from session was uploaded, return true
    //     return true;
    // }
    private async Task<bool> UploadFiles(string email)
    {
        // Retrieve files from session
        var files = new List<(byte[] FileBytes, string FileName, string ContentType)>();
        int fileIndex = 0;

        while (true)
        {
            var fileBytes = HttpContext.Session.GetByteArray($"UploadedFile_{fileIndex}");
            var fileName = HttpContext.Session.GetString($"UploadedFileName_{fileIndex}");
            var contentType = HttpContext.Session.GetString($"UploadedFileContentType_{fileIndex}");

            if (fileBytes == null || string.IsNullOrEmpty(fileName))
            {
                break; // Exit loop if no more files are found
            }

            files.Add((fileBytes, fileName, contentType));
            fileIndex++;
        }

        if (files.Count == 0)
        {
            _logger.LogWarning("No files to upload for email: {Email}", email);
            return true; // No files to upload is not an error condition
        }

        _logger.LogInformation("Attempting to upload {FileCount} files for email: {Email}", files.Count, email);

        bool allUploadsSuccessful = true;

        foreach (var file in files)
        {
            try
            {
                var uploadedFile = await _driveService.UploadFileAsync(email, file.FileBytes, file.FileName);
                _logger.LogInformation("File uploaded successfully. Google Drive File ID: {FileId}", uploadedFile.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file {FileName} for email {Email}", file.FileName, email);
                allUploadsSuccessful = false;
            }
        }

        // Clean up session
        for (int i = 0; i < fileIndex; i++)
        {
            HttpContext.Session.Remove($"UploadedFile_{i}");
            HttpContext.Session.Remove($"UploadedFileName_{i}");
            HttpContext.Session.Remove($"UploadedFileContentType_{i}");
        }

        _logger.LogInformation("File upload process completed for email: {Email}. All uploads successful: {AllUploadsSuccessful}", email, allUploadsSuccessful);

        return allUploadsSuccessful;
    }




}