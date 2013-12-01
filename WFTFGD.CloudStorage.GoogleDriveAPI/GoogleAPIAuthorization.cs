﻿using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v2;
using Google.Apis.Services;
using System.Net.Http;
using System.IO;
using WFTFGD.UI.AuthorizationWindow;
using WFTFGD.UI.AuthorizationWindow.MVVM;


namespace WFTFGD.CloudStorage.GoogleDriveAPI
{
    /// <summary>
    /// A singleton for handling Google authorization simply
    /// </summary>
    public class GoogleAPIAuthorization
    {
        //Singleton-related
        private static Object _threadSync               = new Object();
        private static GoogleAPIAuthorization _instance = default(GoogleAPIAuthorization);

        //Constants
        private static readonly String[] _scopes        = new String[]
        {
            DriveService.Scope.DriveFile,
            DriveService.Scope.Drive
        };
        private static readonly String _tokenUserName   = "wftfgdUser";
        private static readonly String _tokenFilePath   = Path.Combine(new String[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Google.Apis.Auth",
            String.Format("Google.Apis.Auth.OAuth2.Responses.TokenResponse-{0}", _tokenUserName)
        });

        //Fields
        private DriveService _driveService              = default(DriveService);
        private UserCredential _userCredential          = default(UserCredential);
        public static GoogleAPIAuthorization Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_threadSync)
                    {
                        if (_instance == null) 
                        {
                            _instance = new GoogleAPIAuthorization(); 
                        }
                    }
                }
                return _instance;
            }
        }

        private GoogleAPIAuthorization()
        {
        }

        public async Task<DriveService> GetDriveServiceAsync()
        {
            
            DriveService driveService = default(DriveService);
            //Instantiation of fully linked window and ViewModel (non-MVVM way somehow)
            ViewModel authorizationWindowViewModel = new ViewModel(this._AuthorizeDriveService);
            GoogleAPIAuthorizationWindow authorizationWindow =
                new GoogleAPIAuthorizationWindow(authorizationWindowViewModel);
            authorizationWindowViewModel.ParentWindow = authorizationWindow;

            if (System.IO.File.Exists(_tokenFilePath))
            {
                while (driveService == default(DriveService))
                {
                    try
                    {
                        driveService = await _AuthorizeDriveService();
                    }
                    catch (Exception) //If token has expired or there is no internet connection
                    {
                        //Must be reviewed, as the file may be removed in try block automatically
                        System.IO.File.Delete(_tokenFilePath);
                        driveService = default(DriveService);
                    }
                    //There isn't token file, so call the dialog
                    if (driveService == default(DriveService))
                    {
                        driveService = authorizationWindow.ShowDialog();
                    }
                }
            }
            else //If authorizing through a browser initially
            {
                while (driveService == default(DriveService))
                {
                    try
                    {
                        driveService = authorizationWindow.ShowDialog();
                    }
                    catch (Exception)
                    {
                        //If there is no connection or user cancelled - swallow
                        driveService = default(DriveService);
                    }
                }
            }
            return driveService;
        }

        public async Task<DriveService> _AuthorizeDriveService()
        {
            ClientSecrets clientSecrets = default(ClientSecrets);
            using (FileStream fileStream = new FileStream("client_secrets.json", FileMode.Open, FileAccess.Read))
            {
                GoogleClientSecrets googleClientSecrets = GoogleClientSecrets.Load(fileStream);
                clientSecrets = googleClientSecrets.Secrets;
            }
           
            this._userCredential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                clientSecrets,
                _scopes,
                _tokenUserName,
                CancellationToken.None
                );
            BaseClientService.Initializer baseClientServiceInitializer =
                new BaseClientService.Initializer()
                {
                    HttpClientInitializer = this._userCredential,
                    ApplicationName = "Files Time Machine For Google Drive"
                    
                };
            DriveService driveService = new DriveService(baseClientServiceInitializer);
            return driveService;
        }
        
        
        public async Task Deauthorize(DriveService driveService)
        {
            String tokenFilePath = Path.Combine(new String []
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Google.Apis.Auth",
                String.Format("Google.Apis.Auth.OAuth2.Responses.TokenResponse-{0}", _tokenUserName)
            });
            try
            {
                System.IO.File.Delete(tokenFilePath);
            }
            catch
            {
                //Couldn't delete token file
                MessageBox.Show("Token file cannot be deleted");
            }

            String revocationRequestString = String.Format(
                "https://accounts.google.com/o/oauth2/revoke?token={0}",
                this._userCredential.Token.AccessToken);
            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(
                HttpMethod.Post,
                revocationRequestString);
            DialogResult errorDialogResult = DialogResult.Cancel;
            do
            {
                try
                {
                    await driveService.HttpClient.SendAsync(httpRequestMessage);
                }
                catch (Exception)
                {
                    errorDialogResult = MessageBox.Show(
                        "Couldn't perform deauthorization. Your session may already been expired or internet connection problem appeared",
                        "Google deauthorization error",
                        MessageBoxButtons.RetryCancel,
                        MessageBoxIcon.Exclamation);
                }
            } while (errorDialogResult == DialogResult.Retry);
        }


    }
}
