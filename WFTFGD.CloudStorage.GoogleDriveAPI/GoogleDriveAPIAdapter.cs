﻿using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Drive.v2;
using Google.Apis.Drive.v2.Data;
using Google.Apis.Logging;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Discovery;
using System.Collections.Generic;
using Google.Apis.Auth.OAuth2.Flows;
using System.Net.Http;

namespace WFTFGD.CloudStorage.GoogleDriveAPI
{
    public class GoogleDriveAPIAdapter
    {
        #region Constants
        private const string ApplicationFolderName = "Files Time Machine";
        private const string ApplicationFolderContentType = "application/vnd.google-apps.folder";
        #endregion Constants

        private static readonly string[] Scopes =
            new[] { DriveService.Scope.DriveFile, DriveService.Scope.Drive };

        private async Task<File> CreateApplicationDirectory(DriveService driveService)
        {
            File applicationDirectory = new File();
            applicationDirectory.Title = ApplicationFolderName;
            applicationDirectory.MimeType = ApplicationFolderContentType;
            FilesResource.InsertRequest fileInsertRequest =
                driveService.Files.Insert(applicationDirectory);
            return await fileInsertRequest.ExecuteAsync();
        }
        private async Task<Boolean> IsApplicationDirectoryExist(DriveService driveService)
        {
            FilesResource.ListRequest filesListRequest = driveService.Files.List();
            filesListRequest.Q = String.Format("mimeType='{0}' and title='{1}' and trashed=false",
                ApplicationFolderContentType,
                ApplicationFolderName);
            FileList filelist = await filesListRequest.ExecuteAsync();
            return filelist.Items.Count > 0;
        }

        public async Task Run(DriveService service)
        {
            
            GoogleAPIAuthorization googleApiAuthorization = GoogleAPIAuthorization.Instance;
            
            //File file = null;
            //Boolean exists = await IsApplicationDirectoryExist(service);
            //if (exists)
            //{
            //    //OK
            //}
            //else
            //{
                //await CreateApplicationDirectory(service);
                await googleApiAuthorization.Deauthorize(service);
                
                
                //await CreateApplicationDirectory(service);
                    
                //await googleApiAuthorization.Deauthorize(service);
                     
            //}

            
           // await googleApiAuthorization.Deauthorize(service);
            
        }

        
        /*
        /// <summary>Uploads file asynchronously.</summary>
        private Task<IUploadProgress> UploadFileAsync(DriveService service)
        {
            var title = UploadFileName;
            if (title.LastIndexOf('\\') != -1)
            {
                title = title.Substring(title.LastIndexOf('\\') + 1);
            }

            var uploadStream = new System.IO.FileStream(UploadFileName, System.IO.FileMode.Open,
                System.IO.FileAccess.Read);

            //service.Files.
            
            var insert = service.Files.Insert(new File { Title = title }, uploadStream, ContentType);
            //service.Files.List().
            //service.

            insert.ChunkSize = FilesResource.InsertMediaUpload.MinimumChunkSize * 2;
            insert.ProgressChanged += Upload_ProgressChanged;
            insert.ResponseReceived += Upload_ResponseReceived;

            var task = insert.UploadAsync();

            task.ContinueWith(t =>
            {
                // NotOnRanToCompletion - this code will be called if the upload fails
                Console.WriteLine("Upload Filed. " + t.Exception);
            }, TaskContinuationOptions.NotOnRanToCompletion);
            task.ContinueWith(t =>
            {
                //Logger.Debug("Closing the stream");
                uploadStream.Dispose();
                //Logger.Debug("The stream was closed");
            });

            return task;
        }

        /// <summary>Downloads the media from the given URL.</summary>
        private async Task DownloadFile(DriveService service, string url)
        {
            MediaDownloader downloader = new MediaDownloader(service);
            downloader.ChunkSize = DownloadChunkSize;
            // add a delegate for the progress changed event for writing to console on changes
            downloader.ProgressChanged += Download_ProgressChanged;

            // figure out the right file type base on UploadFileName extension
            var lastDot = UploadFileName.LastIndexOf('.');
            var fileName = DownloadDirectoryName + @"\Download" +
                (lastDot != -1 ? "." + UploadFileName.Substring(lastDot + 1) : "");
            using (var fileStream = new System.IO.FileStream(fileName,
                System.IO.FileMode.Create, System.IO.FileAccess.Write))
            {
                var progress = await downloader.DownloadAsync(url, fileStream);
                if (progress.Status == DownloadStatus.Completed)
                {
                    Console.WriteLine(fileName + " was downloaded successfully");
                }
                else
                {
                    Console.WriteLine("Download {0} was interpreted in the middle. Only {1} were downloaded. ",
                        fileName, progress.BytesDownloaded);
                }
            }
        }

        /// <summary>Deletes the given file from drive (not the file system).</summary>
        private async Task DeleteFile(DriveService service, File file)
        {
            Console.WriteLine("Deleting file '{0}'...", file.Id);
            await service.Files.Delete(file.Id).ExecuteAsync();
            Console.WriteLine("File was deleted successfully");
        }

        #region Progress and Response changes

        static void Download_ProgressChanged(IDownloadProgress progress)
        {
            Console.WriteLine(progress.Status + " " + progress.BytesDownloaded);
        }

        static void Upload_ProgressChanged(IUploadProgress progress)
        {
            Console.WriteLine(progress.Status + " " + progress.BytesSent);
        }

        static void Upload_ResponseReceived(File file)
        {
            uploadedFile = file;
        }

        #endregion

        
        static GoogleDriveAPIAdapter()
        {
            // initialize the log instance
            ApplicationContext.RegisterLogger(new Log4NetLogger());
            Logger = ApplicationContext.Logger.ForType<ResumableUpload<GoogleDriveAPIAdapter>>();
        }
        */
       

    }
}
