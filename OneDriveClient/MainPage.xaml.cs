using Newtonsoft.Json;
using OneDriveClient.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Security.Authentication.Web;
using Windows.Security.Credentials;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http;
using Windows.Web.Http.Headers;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace OneDriveClient
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        List<ItemViewModel> _fileList;
        public List<ItemViewModel> FileList
        {
            get { return _fileList; }
            set
            {
                if (value == _fileList)
                    return;
                _fileList = value;
                RaisePropertyChanged();
            }
        }

        private const string AppId = "UWPOneDriveClient";
        private const string ClientIdResourceName = AppId + "ClientId";
        private const string OauthTokenResourceName = AppId + "OauthToken";
        private const string RefreshTokenResourceName = AppId + "OauthTokenRefresh";
        private const string UserName = "User";

        //const string ClientId = "f164241c-bd0f-4948-85c3-b5d3defe4080";
        public string ClientId { get; set; }

        const string Resource = "https://graph.microsoft.com/beta";
        const string AuthEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize";
        const string TokenEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/token";
        const string RedirectUri = "urn:ietf:wg:oauth:2.0:oob";
        
        // Using offline_access will cause a refresh token to be returned in the code grant
        const string Scopes = "openid offline_access https://graph.microsoft.com/files.Read";

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName]string propName = "")
        {
            var pc = PropertyChanged;
            if (pc == null)
                return;
            PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        public MainPage()
        {
            this.InitializeComponent();
            Loaded += OnLoad;
        }

        private async void OnLoad(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            // Try to get Client Id from the Password Vault - if no 
            // entry found put up a dialog box to request..
            var appCredentials = GetTokenFromVault(ClientIdResourceName);
            if (string.IsNullOrEmpty(appCredentials.Item1))
            {
                // Put up a UI prompting for the app IDs...
                var cdr = await ClientDialog.ShowAsync();
                if (cdr == ContentDialogResult.Primary)
                {
                    // Wipe out all other cached credentials
                    RemoveTokensFromVault();
                    FileList = new List<ItemViewModel>();

                    ClientId = ClientDialog.ClientId;
                    if (string.IsNullOrEmpty(ClientId))
                        return;
                    AddTokenToVault(ClientIdResourceName, ClientId, UserName);
                }
            }
            else
            {
                ClientId = appCredentials.Item1;
            }
        }

        private static Uri CreateOAuthCodeRequestUri(string ClientId)
        {
            UriBuilder uri = new UriBuilder(AuthEndpoint);
            var query = new StringBuilder();

            var cburi = WebAuthenticationBroker.GetCurrentApplicationCallbackUri();

            query.AppendFormat("client_id={0}", Uri.EscapeUriString(ClientId));
            query.AppendFormat("&response_type=code");
            query.AppendFormat("&redirect_uri={0}", Uri.EscapeUriString(RedirectUri));
            query.AppendFormat("&scope=" + Uri.EscapeUriString(Scopes));

            uri.Query = query.ToString();
            return uri.Uri;
        }

        private async Task<string> GetTokenAsync()
        {
            var token = GetTokenFromVault(OauthTokenResourceName);
            if (!string.IsNullOrEmpty(token.Item2))
                return token.Item2;

            var uri = CreateOAuthCodeRequestUri(ClientId);
            var result =
                await WebAuthenticationBroker.AuthenticateAsync(WebAuthenticationOptions.None,
                    uri, new Uri(RedirectUri));

            if (result.ResponseStatus == WebAuthenticationStatus.Success)
            {
                var data = result.ResponseData;

                // Parse out the auth code from the response
                string authorization_code = data.ToString().Split('=')[1].Split('&')[0];
                var authToken = await GetAndSecurelyStoreAuthTokensFromAuthCodeAsync(authorization_code);
                return authToken;
            }

            return "";
        }

        private async Task<string> MakeRequestAsync(string path, string query = "")
        {
            var token = await GetTokenAsync();
            var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new HttpCredentialsHeaderValue("Bearer", token);

            var ub = new UriBuilder(Resource);

            ub.Path = "v1.0/me/drive/root/children";
            ub.Query = query;

            string resStr = string.Empty;

            var resp = await http.GetAsync(ub.Uri);

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                // If we are unauthorized here assume that our token may have expired and use the 
                // refresh token to get a new one and then try the request again..
                var currentToken = GetTokenFromVault(RefreshTokenResourceName);
                string rt = currentToken.Item2.Trim('"');
                var newToken = await GetAndSecurelyStoreAuthTokensFromAuthCodeAsync("", rt);

                // Re-issue the same request (will use new auth token now)
                return await MakeRequestAsync(path, query);
            }

            if (resp.IsSuccessStatusCode)
            {
                resStr = await resp.Content.ReadAsStringAsync();
                var root = JsonConvert.DeserializeObject<RootObject>(resStr);

                // create list of view models..
                var list = new List<ItemViewModel>();
                foreach (var item in root.value)
                {
                    if (item.file != null)
                        list.Add(new FileViewModel(item));
                    else
                        list.Add(new FolderViewModel(item));
                }
                FileList = list;
            }
            return resStr;
        }

        private async Task<string> GetAndSecurelyStoreAuthTokensFromAuthCodeAsync(string code, string refreshToken = "")
        {
            // We must use a post here otherwise we get error
            // AADSTS90056: This endpoint only accepts POST requests
            var postData = new Dictionary<string, string>();

            string grant = "authorization_code";
            if (!string.IsNullOrEmpty(refreshToken))
            {
                grant = "refresh_token";
                postData.Add("refresh_token", refreshToken);
            }
            else
            {
                postData.Add("code", code);
            }

            postData.Add("grant_type", grant);
            postData.Add("client_id", ClientId);
            postData.Add("redirect_uri", RedirectUri);
            postData.Add("scope", Scopes);

            string authToken = string.Empty;

            // Create an HTTP Client
            using (var client = new System.Net.Http.HttpClient())
            {
                // Post the data we compiled above to the TokenURI
                var resp = await client.PostAsync(TokenEndpoint, new System.Net.Http.FormUrlEncodedContent(postData));
                var resStr = await resp.Content.ReadAsStringAsync();

                var value = JsonValue.Parse(resStr).GetObject();
                authToken = value.GetNamedValue("access_token").ToString().Trim('"');
                var rT = value.GetNamedValue("refresh_token").ToString().Trim('"');

                AddTokenToVault(OauthTokenResourceName, UserName, authToken);
                AddTokenToVault(RefreshTokenResourceName, UserName, rT);
            }
            return authToken;
        }

        private static void AddTokenToVault(string resName, string userName, string token)
        {
            var vault = new PasswordVault();
            var credential = new PasswordCredential(resName, userName, token);
            vault.Add(credential);
        }

        private static Tuple<string, string> GetTokenFromVault(string resName)
        {
            string userName = string.Empty;
            string password = string.Empty;

            var vault = new PasswordVault();
            try
            {
                var credential = vault.FindAllByResource(resName).FirstOrDefault();
                if (credential != null)
                {
                    userName = credential.UserName;
                    password = vault.Retrieve(resName, userName).Password;
                }
            }
            catch (Exception)
            {
            }
            return new Tuple<string, string>(userName, password);
        }

        private void SignOutClick(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            RemoveTokensFromVault();
            FileList = new List<ItemViewModel>();
        }

        private void RemoveTokensFromVault()
        {
            var vault = new PasswordVault();
            IReadOnlyList<PasswordCredential> credentials;
            try
            {
                credentials = vault.FindAllByResource(OauthTokenResourceName);
                foreach (var cred in credentials)
                {
                    vault.Remove(cred);
                }
            }
            catch (Exception) { }

            try
            {
                credentials = vault.FindAllByResource(RefreshTokenResourceName);
                foreach (var cred in credentials)
                {
                    vault.Remove(cred);
                }
            }
            catch (Exception) { }
        }

        private bool _busy;

        public bool Busy
        {
            get { return _busy; }
            set
            {
                if (_busy == value)
                    return;
                _busy = value;
                RaisePropertyChanged();
            }
        }

        private async void FilesClick(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                Busy = true;
                var resp = await MakeRequestAsync("");
            }
            finally
            {
                Busy = false;
            }
        }

        private async void SettingsClick(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var cdr = await ClientDialog.ShowAsync();
            if (cdr == ContentDialogResult.Primary)
            {
                ClientId = ClientDialog.ClientId;
                if (string.IsNullOrEmpty(ClientId))
                    return;
                AddTokenToVault(ClientIdResourceName, ClientId, UserName);
            }
        }
    }
}
