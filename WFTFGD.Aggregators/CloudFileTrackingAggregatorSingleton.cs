using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using System.Xml.Serialization;
using Google.Apis.Download;
using Google.Apis.Drive.v2;
using Google.Apis.Drive.v2.Data;
using Google.Apis.Upload;
using Microsoft.Win32;
using WFTFGD.Adapters;
using WFTFGD.Aggregators.BridgingEntities;
using WFTFGD.CloudStorage.GoogleDriveAPI;
using WFTFGD.FileOperation.DifferencePatch;
using IO = System.IO;

namespace WFTFGD.Aggregators
{
    /// <summary>
    /// Manages files tracking process
    /// </summary>
    public class CloudFileTrackingAggregatorSingleton
    {
        private static Object _threadSync = new Object();
        private static CloudFileTrackingAggregatorSingleton _instance;
        public System.Windows.Window CurentUIThreadInvoker { get; set; }

        #region Constants
        private const string ApplicationFolderName = "Files Time Machine";
        private const string FolderContentType = "application/vnd.google-apps.folder";
        private const string XmlFileMimeType = "application/xml";
        private const string BsdiffStampDateTimeFormat = "dd/MM/yyyy HH:mm:ss";
        private const string BsdiffStampExtension = "bsdiffstamp";
        private const string BsdiffStampMIMEType = "application/bsdiffstamp";
        private const string FileLocationInfoExtension = "ftrackinfo";
        #endregion Constants



        private static readonly string[] Scopes =
            new[] { DriveService.Scope.DriveFile, DriveService.Scope.Drive };

        #region Application Directory Methods
        private async Task<File> TryCreateApplicationDirectoryAsync()
        {
            DriveService driveService = await TryGetAuthorizer();

            File applicationDirectory = new File();
            applicationDirectory.Title = ApplicationFolderName;
            applicationDirectory.MimeType = FolderContentType;
            FilesResource.InsertRequest fileInsertRequest =
                driveService.Files.Insert(applicationDirectory);
            return fileInsertRequest.Execute();
        }

        public async Task<DriveService> TryGetAuthorizer()
        {
            Dispatcher dispatcherThreadBroker = CurentUIThreadInvoker.Dispatcher;
            Func<Task<DriveService>> delegateForInvokingUI = GoogleAPIAuthorization.Instance.GetDriveServiceAsync;
            Task<DriveService> taskOnInvokedUI = dispatcherThreadBroker.Invoke<Task<DriveService>>(delegateForInvokingUI);
            return await taskOnInvokedUI;
        }

        public async Task<File> TryGetApplicationDirectoryAsync()
        {
            DriveService driveService = await TryGetAuthorizer();
            FilesResource.ListRequest filesListRequest = driveService.Files.List();
            filesListRequest.Q = String.Format("mimeType='{0}' and title='{1}' and trashed=false",
                FolderContentType,
                ApplicationFolderName);
            FileList filelist = filesListRequest.Execute();
            return filelist.Items.FirstOrDefault();
        }
        public async Task<String> TryResolveApplicationDirectoryAsync()
        {
            File existingApplicationDirectory = await TryGetApplicationDirectoryAsync();
            if (existingApplicationDirectory != default(File))
            {
                return existingApplicationDirectory.Id;
            }
            else
            {
                File createdApplicationDirectory = await TryCreateApplicationDirectoryAsync();
                if (createdApplicationDirectory != default(File))
                {
                    return createdApplicationDirectory.Id;
                }
                else
                {
                    return default(String);
                }
            }
        }
        #endregion Application Directory Methods

        #region Generic Directory Methods

        public async Task<File> TryCreateFileRecordDirectoryAsync(
            String rootFolderGoogleDriveId,
            String fileName)
        {
            DriveService driveService = await TryGetAuthorizer();
            File fileRecordDirectory = new File
            {
                Title = fileName,
                MimeType = FolderContentType,
                Parents = new ParentReference[] { new ParentReference { Id = rootFolderGoogleDriveId } }
            };
            FilesResource.InsertRequest fileInsertRequest =
                driveService.Files.Insert(fileRecordDirectory);
            return fileInsertRequest.Execute();
        }

        #endregion Generic Directory Methods

        #region Generic File Methods

        public String GetMimeType(string filePath)
        {
            String extension = IO.Path.GetExtension(filePath).ToLower();
            RegistryKey regKey = Registry.ClassesRoot.OpenSubKey(extension);
            return regKey != null ?
                regKey.GetValue("Content Type") as String ?? "application/unknown"
            : "application/unknown";
        }

        public String FormatBytes(long bytes)
        {
            const Int32 scale = 1024;
            String[] orders = new String[] { "GB", "MB", "KB", "Bytes" };
            Int64 max = (Int64)Math.Pow(scale, orders.Length - 1);
            foreach (String order in orders)
            {
                if (bytes > max)
                    return String.Format("{0:##.##} {1}", decimal.Divide(bytes, max), order);
                max /= scale;
            }
            return "0 Bytes";
        }

        public static Boolean IsLocalFileAddedForTracking(IEnumerable<FileEntityAggregator> loadedItems, String filePath)
        {
            return loadedItems.Count(item => item.LocalFilePath == filePath) > 0;
        }

        private IO.FileStream TryOpenLocalFile(String filePath)
        {
            IO.FileStream fileStream = default(IO.FileStream);
            try
            {
                fileStream = IO.File.Open(filePath, IO.FileMode.Open);
                return fileStream;
            }
            catch (IO.IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist
                return default(IO.FileStream);
            }
        }
        /// <summary>
        /// Tries to upload the file to directory.
        /// </summary>
        /// <param name="parentFolderIdentity">Parent folder ID</param>
        /// <param name="fileTitle">File title</param>
        /// <param name="fileMimeType">File MIME type</param>
        /// <param name="uploadFileStream">Opened file stream for uploading.</param>
        /// <returns>If succeed, returns record entity. If failed - default(UIFileRecordEntity)</returns>
        public async Task<FileEntityAggregator> TryUploadFileToDirectoryAsync(
            String parentFolderGoogleDriveId,
            String filePath,
            String fileTitle,
            String fileMimeType,
            IO.Stream uploadFileStream)
        {
            DriveService driveService = await TryGetAuthorizer();
            //Instantiate file entity
            File fileToUpload = new File
            {
                Title = fileTitle,
                Parents = new ParentReference[] { new ParentReference { Id = parentFolderGoogleDriveId } }
            };
            //Instantiate request
            var insertUploadRequest = driveService.Files.Insert(fileToUpload, uploadFileStream, fileMimeType);
            insertUploadRequest.ChunkSize = FilesResource.InsertMediaUpload.MinimumChunkSize * 2;
            //Start uploading task
            IUploadProgress uploadProgress = insertUploadRequest.Upload();
            //Return data according to upload completion state
            if (uploadProgress.Status == UploadStatus.Completed)
            {
                return new FileEntityAggregator
                {
                    LocalFilePath = filePath,
                    GoogleDriveId = insertUploadRequest.ResponseBody.Id,
                    GoogleDrivePath = insertUploadRequest.ResponseBody.DownloadUrl,
                    GoogleDriveParentId = parentFolderGoogleDriveId,
                    MIMEType = fileMimeType
                };
            }
            else
            {
                return default(FileEntityAggregator);
            }
        }

        public async Task<String> TryGetFileRecordMemoryTaken(String fileRecordDirectoryId)
        {
            DriveService driveService = await TryGetAuthorizer();
            //Get all files from folder
            FilesResource.ListRequest filesListRequest = driveService.Files.List();
            filesListRequest.Q = String.Format("'{0}' in parents and trashed=false", fileRecordDirectoryId);
            FileList filelist = filesListRequest.Execute();
            //Get diff stamps descriptors
            Int64 bytesTaken = filelist.Items.Sum((File file) => Convert.ToInt64(file.FileSize));
            return FormatBytes(bytesTaken);
        }


        public async Task<IO.MemoryStream> TryDownloadFileToStream(FileEntityAggregator targetFile)
        {
            DriveService driveService = await TryGetAuthorizer();
            MediaDownloader mediaDownloader = new MediaDownloader(driveService);
            IO.MemoryStream memoryStream = new IO.MemoryStream();
            try
            {
                mediaDownloader.Download(targetFile.GoogleDrivePath, memoryStream);
                return memoryStream;
            }
            catch (Exception)
            {
                memoryStream.Dispose();
                return default(IO.MemoryStream);
            }
        }


        public async Task<FileEntityAggregator> TryAddLocalFileForTracking(String filePath)
        {
            FileEntityAggregator result = default(FileEntityAggregator);

            IO.FileStream uploadStream = TryOpenLocalFile(filePath);
            if (uploadStream == default(IO.FileStream))
            {
                //Failed to open the file
            }
            else
            {

                String rootDirectoryGoogleDriveId = await TryResolveApplicationDirectoryAsync();
                if (rootDirectoryGoogleDriveId == default(String))
                {
                    //Failed to resolve root directory
                }
                else
                {
                    File fileRecordDirectory = await TryCreateFileRecordDirectoryAsync(
                        rootDirectoryGoogleDriveId,
                        IO.Path.GetFileName(filePath));

                    if (fileRecordDirectory == default(File))
                    {
                        //Failed to create file record directory
                    }
                    else
                    {

                        result = await TryUploadFileToDirectoryAsync(
                            fileRecordDirectory.Id,
                            filePath,
                            IO.Path.GetFileName(filePath),
                            GetMimeType(filePath),
                            uploadStream);

                        if (result == default(FileEntityAggregator))
                        {
                            //Upload failed
                            uploadStream.Close();
                        }
                        else
                        {
                            uploadStream.Close();
                            IO.MemoryStream memoryStream = new IO.MemoryStream();
                            FileTrackingInfo fileTrackingInfo = new FileTrackingInfo(filePath, GetMimeType(filePath));
                            XmlSerializer xmlSerializer = new XmlSerializer(typeof(FileTrackingInfo));
                            xmlSerializer.Serialize(memoryStream, fileTrackingInfo);
                            memoryStream.Position = 0;
                            FileEntityAggregator infoFile = await TryUploadFileToDirectoryAsync(
                                fileRecordDirectory.Id,
                                String.Empty,
                                String.Format("{0}.{1}", IO.Path.GetFileName(filePath), FileLocationInfoExtension),
                                XmlFileMimeType,
                                memoryStream);

                            if (infoFile == default(FileEntityAggregator))
                            {
                                memoryStream.Close();
                            }
                            else
                            {
                                memoryStream.Close();
                            }
                        }
                    }

                }
            }
            return result;
        }

        #endregion Generic File Methods

        #region Diffstamps Methods

        //Is OK
        public async Task<IList<File>> TryGetAllDiffStampsDescriptorsFromFileRecordDirectoryAsync(
            String fileRecordDirectoryId)
        {
            DriveService driveService = await TryGetAuthorizer();
            FilesResource.ListRequest filesListRequest = driveService.Files.List();
            filesListRequest.Q = String.Format("mimeType!='{0}' and trashed=false and '{1}' in parents",
                FolderContentType,
                fileRecordDirectoryId);
            FileList filelist = await filesListRequest.ExecuteAsync();
            IList<File> fileListDiffStamps = filelist.Items.Where(
                fileRecord => fileRecord.FileExtension == BsdiffStampExtension).
                ToList();
            return fileListDiffStamps;
        }

        //Refactoring OK++
        public async Task<FileEntityAggregator> TryUploadDiffStampForFileAsync(
            FileEntityAggregator targetFile,
            DiffStampAdapter diffStamp)
        {
            DriveService driveService = await TryGetAuthorizer();
            //Instantiate file entity
            File fileToUpload = new File
            {
                Title = String.Format("{0}.{1}", diffStamp.Description, BsdiffStampExtension),
                Parents = new ParentReference[] { new ParentReference { Id = targetFile.GoogleDriveParentId } }
            };
            //Instantiate request
            IUploadProgress uploadProgress = default(IUploadProgress);
            FileEntityAggregator result = default(FileEntityAggregator);
            using (IO.MemoryStream uploadFileStream = new IO.MemoryStream())
            {
                BinaryFormatter binaryFormatter = new BinaryFormatter();
                binaryFormatter.Serialize(uploadFileStream, diffStamp);
                uploadFileStream.Position = 0;
                var insertUploadRequest = driveService.Files.Insert(
                    fileToUpload, uploadFileStream, BsdiffStampMIMEType);
                insertUploadRequest.ChunkSize = FilesResource.InsertMediaUpload.MinimumChunkSize * 2;
                //Start uploading task
                uploadProgress = await insertUploadRequest.UploadAsync();
                //Return data according to upload completion state
                if (uploadProgress.Status == UploadStatus.Completed)
                {
                    result = new FileEntityAggregator
                    {
                        LocalFilePath = String.Empty,
                        GoogleDriveId = insertUploadRequest.ResponseBody.Id,
                        GoogleDrivePath = insertUploadRequest.ResponseBody.DownloadUrl,
                        GoogleDriveParentId = targetFile.GoogleDriveParentId,
                        MIMEType = BsdiffStampMIMEType
                    };
                }
                else
                {
                    result = default(FileEntityAggregator);
                }
            }
            return result;
        }

        //Refactoring OK++
        public async Task<FileEntityAggregator> TryMakeAndUploadDiffStamp(
            FileEntityAggregator targetFile,
            String diffStampDescription)
        {
            DiffStampAdapter diffStampAdapter =
                await CloudFileTrackingAggregatorSingleton.Instance.TryMakeDiffStamp(
                targetFile, diffStampDescription);

            if (diffStampAdapter == default(DiffStampAdapter))
            {
                //Fail
                return default(FileEntityAggregator);
            }
            else
            {
                return await TryUploadDiffStampForFileAsync(targetFile, diffStampAdapter);
            }
        }

        public async Task<String> TryRemoveRecordFromGoogleDrive(FileEntityAggregator targetFile)
        {
            DriveService driveService = await TryGetAuthorizer();
            FilesResource.DeleteRequest deleteRequest = new FilesResource.DeleteRequest(
                driveService,
                targetFile.GoogleDriveParentId);
            return await deleteRequest.ExecuteAsync();
        }

        public async Task<Byte[]> TryGetFilePatchedUntilDateTime(
            FileEntityAggregator targetFile,
            DateTime dateTimeUntil)
        {
            //Download source file
            IO.MemoryStream fileMemoryStream = await TryDownloadFileToStream(targetFile);

            if (fileMemoryStream == default(IO.MemoryStream))
            {
                //Failed to download
                return default(Byte[]);
            }
            else
            {
                //Download present diff stamps
                List<DiffStampAdapter> diffStamps = await TryDownloadAllDiffstampsForFileUntilDateTime(
                    targetFile, dateTimeUntil); //OK
                //Apply all of them (memory hungry section)
                Byte[] latestData = fileMemoryStream.ToArray();

                fileMemoryStream.Dispose(); //As there is a temp stream to apply patches
                foreach (DiffStampAdapter diffStamp in diffStamps)
                {
                    using (IO.MemoryStream outputStream = new IO.MemoryStream())
                    {
                        using (IO.MemoryStream currentPatchedMemoryStream = new IO.MemoryStream(latestData))
                        {
                            //Apply a patch
                            BinaryPatchUtility.Apply(
                                currentPatchedMemoryStream,
                                diffStamp.OpenMemoryStreamForPatching,
                                outputStream);
                            //Prepare applied data for next iteration patching
                            outputStream.Position = 0;
                            latestData = outputStream.ToArray();
                        }
                    }
                }
                return latestData;
            }
        }

        //Refactoring OK++
        public async Task<DiffStampAdapter> TryMakeDiffStamp(
            FileEntityAggregator targetFile,
            String diffStampDescription)
        {
            //Download source file
            IO.MemoryStream fileMemoryStream = await TryDownloadFileToStream(targetFile);

            if (fileMemoryStream == default(IO.MemoryStream))
            {
                //Failed to download
                return default(DiffStampAdapter);
            }
            else
            {
                //Download present diff stamps
                List<DiffStampAdapter> diffStamps = await TryDownloadAllDiffstampsForFile(targetFile); //OK

                //Apply all of them (memory hungry section)
                Byte[] latestData = fileMemoryStream.ToArray();

                fileMemoryStream.Dispose(); //As there is a temp stream to apply patches
                foreach (DiffStampAdapter diffStamp in diffStamps)
                {
                    using (IO.MemoryStream outputStream = new IO.MemoryStream())
                    {
                        using (IO.MemoryStream currentPatchedMemoryStream = new IO.MemoryStream(latestData))
                        {
                            //Apply a patch
                            BinaryPatchUtility.Apply(
                                currentPatchedMemoryStream,
                                diffStamp.OpenMemoryStreamForPatching,
                                outputStream);
                            //Prepare applied data for next iteration patching
                            outputStream.Position = 0;
                            latestData = outputStream.ToArray();
                        }
                    }
                }
                //When all patches are applied, make new one with the difference between last on cloud and on PC
                IO.FileStream fileStream = default(IO.FileStream);
                IO.MemoryStream latestPatchMemoryStream = new IO.MemoryStream();
                DiffStampAdapter resultPatch = default(DiffStampAdapter);
                try
                {
                    //Read a file to byte array
                    fileStream = new IO.FileStream(targetFile.LocalFilePath, IO.FileMode.Open);
                    Byte[] lastFromUserPC = new Byte[fileStream.Length];
                    fileStream.Read(lastFromUserPC, 0, Convert.ToInt32(fileStream.Length));
                    //Get bytes from latest patched from cloud
                    BinaryPatchUtility.Create(latestData, lastFromUserPC, latestPatchMemoryStream);
                    //Create new binary patch instance
                    resultPatch = new DiffStampAdapter(
                        latestPatchMemoryStream.ToArray(),
                        diffStampDescription,
                        DateTime.Now);
                }
                catch (Exception)
                {
                    //Patching failed
                }
                finally
                {
                    fileStream.Dispose();
                    latestPatchMemoryStream.Dispose();
                }
                return resultPatch;
            }
        }

        //Refactoring ++OK
        public async Task<List<DiffStampAdapter>> TryDownloadAllDiffstampsForFileUntilDateTime(
            FileEntityAggregator targetFile,
            DateTime dateTimeUntil)
        {
            DriveService driveService = await TryGetAuthorizer();
            //Get all files from folder
            FilesResource.ListRequest filesListRequest = driveService.Files.List();
            filesListRequest.Q = String.Format("'{0}' in parents and trashed=false", targetFile.GoogleDriveParentId);
            FileList filelist = await filesListRequest.ExecuteAsync();
            //Get diff stamps descriptors
            IEnumerable<File> foundDiffStamps =
                from items in filelist.Items
                where items.FileExtension == BsdiffStampExtension && DateTime.Parse(items.CreatedDate) <= dateTimeUntil
                orderby DateTime.Parse(items.CreatedDate, CultureInfo.InvariantCulture) ascending
                select items;
            //Download diff stamps
            MediaDownloader mediaDownloader = new MediaDownloader(driveService);
            List<DiffStampAdapter> resultDiffStampAdapters = new List<DiffStampAdapter>();
            foreach (File diffStampDescriptor in foundDiffStamps)
            {
                using (IO.MemoryStream memoryStream = new IO.MemoryStream())
                {
                    mediaDownloader.Download(diffStampDescriptor.DownloadUrl, memoryStream);
                    BinaryFormatter binaryFormatter = new BinaryFormatter();
                    memoryStream.Position = 0;
                    DiffStampAdapter diffStampAdapter =
                        binaryFormatter.Deserialize(memoryStream) as DiffStampAdapter;
                    resultDiffStampAdapters.Add(diffStampAdapter);
                }
            }
            return resultDiffStampAdapters;
        }

        //Refactoring ++OK
        public async Task<List<DiffStampAdapter>> TryDownloadAllDiffstampsForFile(FileEntityAggregator targetFile)
        {
            DriveService driveService = await TryGetAuthorizer();
            //Get all files from folder
            FilesResource.ListRequest filesListRequest = driveService.Files.List();
            filesListRequest.Q = String.Format("'{0}' in parents and trashed=false", targetFile.GoogleDriveParentId);
            FileList filelist = await filesListRequest.ExecuteAsync();
            //Get diff stamps descriptors
            IEnumerable<File> foundDiffStamps =
                from items in filelist.Items
                where items.FileExtension == BsdiffStampExtension
                orderby DateTime.Parse(items.CreatedDate, CultureInfo.InvariantCulture) ascending
                select items;
            //Download diff stamps
            MediaDownloader mediaDownloader = new MediaDownloader(driveService);
            List<DiffStampAdapter> resultDiffStampAdapters = new List<DiffStampAdapter>();
            foreach (File diffStampDescriptor in foundDiffStamps)
            {
                using (IO.MemoryStream memoryStream = new IO.MemoryStream())
                {
                    mediaDownloader.Download(diffStampDescriptor.DownloadUrl, memoryStream);
                    BinaryFormatter binaryFormatter = new BinaryFormatter();
                    memoryStream.Position = 0;
                    DiffStampAdapter diffStampAdapter =
                        binaryFormatter.Deserialize(memoryStream) as DiffStampAdapter;
                    resultDiffStampAdapters.Add(diffStampAdapter);
                }
            }
            return resultDiffStampAdapters;
        }


        #endregion Diffstamps Methods

        public async Task<IList<File>> TryGetAllFileRecordDirectoriesAsync()
        {
            String applicationDirectoryIdentity = await TryResolveApplicationDirectoryAsync();
            if (applicationDirectoryIdentity == default(String))
            {
                return default(IList<File>);
            }
            else
            {
                DriveService driveService = await TryGetAuthorizer();
                FilesResource.ListRequest filesListRequest = driveService.Files.List();
                filesListRequest.Q = String.Format("mimeType='{0}' and trashed=false and '{1}' in parents",
                    FolderContentType,
                    applicationDirectoryIdentity);
                FileList filelist = filesListRequest.Execute();
                return filelist.Items;
            }
        }

        public async Task<FileTrackingInfo> TryGetFileTrackingInfo(String fileRecordDirectoryId)
        {
            File fileTrackingInfoDescriptor = await TryGetFileTrackingInfoDescriptor(fileRecordDirectoryId);
            FileEntityAggregator fileTrackingInfoWrapper = new FileEntityAggregator(
                String.Empty,
                fileTrackingInfoDescriptor.Id,
                fileRecordDirectoryId,
                fileTrackingInfoDescriptor.DownloadUrl,
                FileLocationInfoExtension);
            IO.MemoryStream memoryStream = await TryDownloadFileToStream(fileTrackingInfoWrapper);

            if (memoryStream == default(IO.MemoryStream))
            {
                //Fail
                return default(FileTrackingInfo);
            }
            else
            {
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(FileTrackingInfo));
                memoryStream.Position = 0;
                FileTrackingInfo fileTrackingInfo = xmlSerializer.Deserialize(memoryStream) as FileTrackingInfo;
                memoryStream.Dispose();
                return fileTrackingInfo;
            }
        }

        public async Task<File> TryGetFileTrackingInfoDescriptor(String fileRecordDirectoryId)
        {
            DriveService driveService = await TryGetAuthorizer();
            FilesResource.ListRequest filesListRequest = driveService.Files.List();
            filesListRequest.Q = String.Format("mimeType!='{0}' and trashed=false and '{1}' in parents",
                FolderContentType,
                fileRecordDirectoryId);
            FileList filelist = await filesListRequest.ExecuteAsync();
            IEnumerable<File> fileListNotDiffStamps = filelist.Items.Where(fileRecord => fileRecord.Title.EndsWith(FileLocationInfoExtension));
            return fileListNotDiffStamps.FirstOrDefault();
        }

        public async Task<File> TryGetSourceFileDescriptorAsync(String fileRecordDirectoryId)
        {
            DriveService driveService = await TryGetAuthorizer();
            FilesResource.ListRequest filesListRequest = driveService.Files.List();
            filesListRequest.Q = String.Format("mimeType!='{0}' and mimeType!='{1}' and trashed=false and '{2}' in parents",
                FolderContentType,
                BsdiffStampMIMEType,
                fileRecordDirectoryId);
            FileList filelist = filesListRequest.Execute();
            IEnumerable<File> fileListNotDiffStamps = filelist.Items.Where(
                fileRecord =>
                    IO.Path.GetExtension(fileRecord.Title) != BsdiffStampExtension &&
                    IO.Path.GetExtension(fileRecord.Title) != FileLocationInfoExtension
            );
            return fileListNotDiffStamps.FirstOrDefault();
        }

        public async Task<FileEntityAggregator> UpdateFileEntityAggregator(FileEntityAggregator source)
        {
            DriveService driveService = await TryGetAuthorizer();
            FilesResource.ListRequest filesListRequest = driveService.Files.List();
            filesListRequest.Q = String.Format("'{0}' in parents and trashed=false", source.GoogleDriveParentId);
            FileList filelist = await filesListRequest.ExecuteAsync();
            Int64 bytesTaken = filelist.Items.Sum((File file) => Convert.ToInt64(file.FileSize));
            IEnumerable<File> fileListDiffStamps = filelist.Items.Where(
                fileRecord => fileRecord.FileExtension == BsdiffStampExtension);

            DateTime latestDiffStampDate =
                fileListDiffStamps.Count() > 0 ?
                fileListDiffStamps.Max(item => DateTime.Parse(item.CreatedDate)) :
                default(DateTime);
            source.DiffStampsAmount = fileListDiffStamps.Count();
            source.CloudMemory = FormatBytes(bytesTaken);
            source.LatestDiffStampTakenDateTime = latestDiffStampDate;
            return source;
        }

        public async Task<FileEntityAggregator> TryFormSourceFileEntityAggregator(File fileDescriptor)
        {
            FileEntityAggregator result = default(FileEntityAggregator);
            if (fileDescriptor == default(File))
            {
                //Descriptor empty
            }
            else
            {
                FileTrackingInfo fileTrackingInfo =
                    await TryGetFileTrackingInfo(fileDescriptor.Parents.First().Id);
                if (fileTrackingInfo == default(FileTrackingInfo))
                {
                    //Failed to get tracking data
                }
                else
                {
                    result = new FileEntityAggregator(
                        fileTrackingInfo.UserPCLocation,
                        fileDescriptor.Id,
                        fileDescriptor.Parents.First().Id,
                        fileDescriptor.DownloadUrl,
                        fileDescriptor.MimeType);
                    result = await UpdateFileEntityAggregator(result);
                }
            }
            return result;
        }

        public async Task<List<FileEntityAggregator>> TryInitializeGoogleDriveFileRecords()
        {
            List<FileEntityAggregator> result = new List<FileEntityAggregator>();
            IList<File> fileRecordDirectories = await TryGetAllFileRecordDirectoriesAsync();
            foreach (File fileRecordDirectory in fileRecordDirectories)
            {
                File sourceFile = await TryGetSourceFileDescriptorAsync(fileRecordDirectory.Id);

                FileEntityAggregator fileEntityAggregator =
                    await TryFormSourceFileEntityAggregator(sourceFile);
                result.Add(fileEntityAggregator);
            }
            return result;
        }

        private CloudFileTrackingAggregatorSingleton()
        {
        }

        public static CloudFileTrackingAggregatorSingleton Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_threadSync)
                    {
                        if (_instance == null)
                        {
                            _instance = new CloudFileTrackingAggregatorSingleton();
                        }
                    }
                }
                return _instance;
            }
        }


    }
}
