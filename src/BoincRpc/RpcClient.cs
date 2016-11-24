using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace BoincRpc
{
    // RPC command reference in https://github.com/BOINC/boinc/blob/master/client/gui_rpc_server_ops.cpp

    public class RpcClient : IDisposable
    {
        protected TcpClient tcpClient;

        protected SemaphoreSlim semaphore = new SemaphoreSlim(1);        

        public Task ConnectAsync(string host, int port)
        {
            CheckDisposed();

            if (host == null)
                throw new ArgumentNullException(nameof(host));
            if (port < 0 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port));
            if (Connected)
                throw new InvalidOperationException("RpcClient is already connected. Disconnect first before opening a new connection.");

            Close();

            tcpClient = new TcpClient();

            return tcpClient.ConnectAsync(host, port);
        }

        public void Close()
        {
            CheckDisposed();

            if (tcpClient != null)
            {
                tcpClient.Dispose();
                tcpClient = null;
            }
        }

        public async Task<bool> AuthorizeAsync(string password)
        {
            CheckDisposed();

            if (password == null)
                throw new ArgumentNullException(nameof(password));

            CheckConnected();

            XElement response1 = await PerformRpcAsync("<auth1/>");

            CheckResponse(response1, "nonce");

            string nonce = (string)response1;

            string nonceHash = Utils.GetMD5Hash(nonce + password);

            XElement request2 = new XElement("auth2",
                new XElement("nonce_hash", nonceHash));

            XElement response2 = await PerformRpcAsync(request2);

            switch (response2.Name.ToString())
            {
                case "authorized":
                    return true;
                case "unauthorized":
                    return false;
                default:
                    throw new InvalidRpcResponseException(string.Format("Expected <authorized/> or <unauthorized/> element but encountered <{0}>.", response2.Name));
            }
        }

        public async Task<VersionInfo> ExchangeVersionsAsync(VersionInfo localVersion)
        {
            CheckDisposed();

            if (localVersion == null)
                throw new ArgumentNullException(nameof(localVersion));

            CheckConnected();

            XElement request = new XElement("exchange_versions",
                new XElement("major", localVersion.Major),
                new XElement("minor", localVersion.Minor),
                new XElement("release", localVersion.Release));

            XElement response = await PerformRpcAsync(request);

            CheckResponse(response, "server_version");

            return new VersionInfo(response);
        }

        public async Task<CoreClientState> GetStateAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_state/>");

            CheckResponse(response, "client_state");

            return new CoreClientState(response);
        }

        public async Task<Result[]> GetResultsAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_results>\n<active_only>0</active_only>\n</get_results>\n");

            CheckResponse(response, "results");

            return response.Elements("result").Select(e => new Result(e)).ToArray();
        }

        public async Task<FileTransfer[]> GetFileTransfersAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_file_transfers/>");

            CheckResponse(response, "file_transfers");

            return response.Elements("file_transfer").Select(e => new FileTransfer(e)).ToArray();
        }

        public async Task<Project[]> GetProjectStatusAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_project_status/>");

            CheckResponse(response, "projects");

            return response.Elements("project").Select(e => new Project(e)).ToArray();
        }

        public async Task<ProjectListEntry[]> GetAllProjectsListAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_all_projects_list/>"); // <get_all_projects_list/> returns both projects and acc. managers

            CheckResponse(response, "projects");

            return response.Elements("project").Select(e => new ProjectListEntry(e)).ToArray();
        }

        public async Task<AccountManagerListEntry[]> GetAllAccountManagersListAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_all_projects_list/>"); // <get_all_projects_list/> returns both projects and acc. managers

            CheckResponse(response, "projects");

            return response.Elements("account_manager").Select(e => new AccountManagerListEntry(e)).ToArray();
        }        

        public async Task<DiskUsage> GetDiskUsageAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_disk_usage/>");

            CheckResponse(response, "disk_usage_summary");

            return new DiskUsage(response);
        }

        public async Task<ProjectStatistics[]> GetStatisticsAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_statistics/>");

            CheckResponse(response, "statistics");

            return response.Elements("project_statistics").Select(e => new ProjectStatistics(e)).ToArray();
        }

        public async Task<CoreClientStatus> GetCoreClientStatusAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_cc_status/>");

            CheckResponse(response, "cc_status");

            return new CoreClientStatus(response);
        }

        public async Task NetworkAvailableAsync()
        {
            CheckDisposed();
            CheckConnected();

            CheckResponse(await PerformRpcAsync("<network_available/>"));
        }

        public async Task PerformProjectOperationAsync(Project project, ProjectOperation operation)
        {
            CheckDisposed();

            if (project == null)
                throw new ArgumentNullException(nameof(project));

            CheckConnected();

            string tag = "project_" + operation.ToString().ToLower();

            XElement request = new XElement(tag,
                new XElement("project_url", project.MasterUrl));

            CheckResponse(await PerformRpcAsync(request));
        }

        public async Task<ProjectAttachReply> ProjectAttachAsync(string projectUrl, string authenticator, string projectName, CancellationToken cancellationToken)
        {
            CheckDisposed();

            if (projectUrl == null)
                throw new ArgumentNullException(nameof(projectUrl));
            if (authenticator == null)
                throw new ArgumentNullException(nameof(authenticator));
            if (projectName == null)
                throw new ArgumentNullException(nameof(projectName));

            CheckConnected();

            XElement request = new XElement("project_attach",
               new XElement("project_url", projectUrl),
               new XElement("authenticator", authenticator),
               new XElement("project_name", projectName));

            // await PerformRpcAsync("<project_attach>\n<use_config_file/></project_attach>");

            CheckResponse(await PerformRpcAsync(request));

            return new ProjectAttachReply(await PollRpcAsync("<project_attach_poll/>", cancellationToken));
        }
        
        public Task SetRunModeAsync(Mode mode)
        {
            return SetRunModeAsync(mode, TimeSpan.Zero);
        }

        public async Task SetRunModeAsync(Mode mode, TimeSpan duration)
        {
            CheckDisposed();
            CheckConnected();

            XElement request = new XElement("set_run_mode",
                new XElement(mode.ToString().ToLower()),
                new XElement("duration", Utils.ConvertTimeSpanToSeconds(duration)));

            CheckResponse(await PerformRpcAsync(request));
        }
        
        public Task SetGpuModeAsync(Mode mode)
        {
            return SetGpuModeAsync(mode, TimeSpan.Zero);
        }

        public async Task SetGpuModeAsync(Mode mode, TimeSpan duration)
        {
            CheckDisposed();
            CheckConnected();

            XElement request = new XElement("set_gpu_mode",
                new XElement(mode.ToString().ToLower()),
                new XElement("duration", Utils.ConvertTimeSpanToSeconds(duration)));

            CheckResponse(await PerformRpcAsync(request));
        }
        
        public Task SetNetworkModeAsync(Mode mode)
        {
            return SetNetworkModeAsync(mode, TimeSpan.Zero);
        }

        public async Task SetNetworkModeAsync(Mode mode, TimeSpan duration)
        {
            CheckDisposed();
            CheckConnected();

            XElement request = new XElement("set_network_mode",
                new XElement(mode.ToString().ToLower()),
                new XElement("duration", Utils.ConvertTimeSpanToSeconds(duration)));

            CheckResponse(await PerformRpcAsync(request));
        }

        public async Task<Tuple<Result[], SuspendReason>> GetScreensaverTasksAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_screensaver_tasks/>");

            CheckResponse(response, "handle_get_screensaver_tasks");

            Result[] results = response.Elements("result").Select(e => new Result(e)).ToArray();

            SuspendReason suspendReason = (SuspendReason)response.ElementInt("suspend_reason");

            return new Tuple<Result[], SuspendReason>(results, suspendReason);
        }

        public async Task RunBenchmarksAsync()
        {
            CheckDisposed();
            CheckConnected();

            CheckResponse(await PerformRpcAsync("<run_benchmarks/>"));
        }

        public async Task SetProxySettingsAsync(ProxyInfo proxyInfo)
        {
            CheckDisposed();

            if (proxyInfo == null)
                throw new ArgumentNullException(nameof(proxyInfo));

            CheckConnected();

            XElement request = new XElement("set_proxy_settings",
                proxyInfo.UseHttpProxy ? new XElement("use_http_proxy") : null,
                proxyInfo.UseSocksProxy ? new XElement("use_socks_proxy") : null,
                proxyInfo.UseHttpAuthentication ? new XElement("use_http_auth") : null,
                new XElement("proxy_info",
                    new XElement("http_server_name", proxyInfo.HttpServerName),
                    new XElement("http_server_port", proxyInfo.HttpServerPort),
                    new XElement("http_user_name", proxyInfo.HttpUserName),
                    new XElement("http_user_passwd", proxyInfo.HttpUserPassword),
                    new XElement("socks_server_name", proxyInfo.SocksServerName),
                    new XElement("socks_server_port", proxyInfo.SocksServerPort),
                    new XElement("socks_version", proxyInfo.SocksVersion),
                    new XElement("socks5_user_name", proxyInfo.Socks5UserName),
                    new XElement("socks5_user_passwd", proxyInfo.Socks5UserPassword),
                    new XElement("no_proxy", proxyInfo.NoProxyHosts)));

            CheckResponse(await PerformRpcAsync(request));
        }

        public async Task<ProxyInfo> GetProxySettingsAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_proxy_settings/>");

            CheckResponse(response, "proxy_info");

            return new ProxyInfo(response);
        }

        public async Task<int> GetMessageCountAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_message_count/>");

            CheckResponse(response, "seqno");

            return (int)response; // TODO: this could throw an exception
        }

        public Task<Message[]> GetMessagesAsync()
        {
            return GetMessagesAsync(0);
        }

        public async Task<Message[]> GetMessagesAsync(int sequenceNumber)
        {
            CheckDisposed();

            if (sequenceNumber < 0)
                throw new ArgumentOutOfRangeException(nameof(sequenceNumber));

            CheckConnected();

            XElement request = new XElement("get_messages",
                new XElement("seqno", sequenceNumber));

            XElement response = await PerformRpcAsync(request);

            CheckResponse(response, "msgs");

            return response.Elements("msg").Select(e => new Message(e)).ToArray();
        }

        public Task<Notice[]> GetNoticesAsync()
        {
            return GetNoticesAsync(0, false);
        }

        public async Task<Notice[]> GetNoticesAsync(int sequenceNumber, bool publicOnly)
        {
            CheckDisposed();

            if (sequenceNumber < 0)
                throw new ArgumentOutOfRangeException(nameof(sequenceNumber));

            CheckConnected();

            XElement request = new XElement(publicOnly ? "get_notices_public" : "get_notices",
                new XElement("seqno", sequenceNumber));

            XElement response = await PerformRpcAsync(request);

            CheckResponse(response, "notices");

            return response.Elements("notice").Select(e => new Notice(e)).ToArray();
        }

        public async Task PerformFileTransferOperationAsync(FileTransfer fileTransfer, FileTransferOperation operation)
        {
            CheckDisposed();

            if (fileTransfer == null)
                throw new ArgumentNullException(nameof(fileTransfer));

            CheckConnected();

            string tag = operation.ToString().ToLower() + "_file_transfer";

            XElement request = new XElement(tag,
                new XElement("project_url", fileTransfer.ProjectUrl),
                new XElement("filename", fileTransfer.Name));

            CheckResponse(await PerformRpcAsync(request));
        }

        public async Task PerformResultOperationAsync(Result result, ResultOperation operation)
        {
            CheckDisposed();

            if (result == null)
                throw new ArgumentNullException(nameof(result));

            CheckConnected();

            string tag = operation.ToString().ToLower() + "_result";

            XElement request = new XElement(tag,
                new XElement("project_url", result.ProjectUrl),
                new XElement("name", result.Name));

            CheckResponse(await PerformRpcAsync(request));
        }

        public async Task<HostInfo> GetHostInfoAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_host_info/>");

            CheckResponse(response, "host_info");

            return new HostInfo(response);
        }

        public async Task QuitAsync()
        {
            CheckDisposed();
            CheckConnected();

            CheckResponse(await PerformRpcAsync("<quit/>"));
        }

        public async Task<AccountManagerRpcReply> AccountManagerAttachAsync(string url, string name, string password, CancellationToken cancellationToken)
        {
            CheckDisposed();

            if (url == null)
                throw new ArgumentNullException(nameof(url));
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (password == null)
                throw new ArgumentNullException(nameof(password));

            CheckConnected();

            XElement request = new XElement("acct_mgr_rpc",
                new XElement("url", url),
                new XElement("name", name),
                new XElement("password", password));

            // await PerformRpcAsync("<acct_mgr_rpc>\n<use_config_file/>\n</acct_mgr_rpc>");

            CheckResponse(await PerformRpcAsync(request));

            return new AccountManagerRpcReply(await PollRpcAsync("<acct_mgr_rpc_poll/>", cancellationToken));
        }        

        public async Task<AccountManagerInfo> GetAccountManagerInfoAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<acct_mgr_info/>");

            CheckResponse(response, "acct_mgr_info");

            return new AccountManagerInfo(response);
        }

        public async Task<ProjectInitStatus> GetProjectInitStatusAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_project_init_status/>");

            CheckResponse(response, "get_project_init_status");

            return new ProjectInitStatus(response);
        }

        public async Task<ProjectConfig> GetProjectConfigAsync(string url, CancellationToken cancellationToken)
        {
            CheckDisposed();

            if (url == null)
                throw new ArgumentNullException(nameof(url));

            CheckConnected();

            XElement request = new XElement("get_project_config",
                new XElement("url", url));

            CheckResponse(await PerformRpcAsync(request));

            return new ProjectConfig(await PollRpcAsync("<get_project_config_poll/>", cancellationToken));
        }

        public async Task<AccountInfo> LookupAccountAsync(string url, string emailAddress, string password, CancellationToken cancellationToken)
        {
            CheckDisposed();

            if (url == null)
                throw new ArgumentNullException(nameof(url));
            if (emailAddress == null)
                throw new ArgumentNullException(nameof(emailAddress));
            if (password == null)
                throw new ArgumentNullException(nameof(password));

            CheckConnected();

            string passwordHash = Utils.GetMD5Hash(password + emailAddress.ToLower());

            XElement request = new XElement("lookup_account",
                new XElement("url", url),
                new XElement("email_addr", emailAddress),
                new XElement("passwd_hash", passwordHash),
                new XElement("ldap_auth", 0));

            CheckResponse(await PerformRpcAsync(request));

            return new AccountInfo(await PollRpcAsync("<lookup_account_poll/>", cancellationToken));
        }

        public Task<AccountInfo> CreateAccountAsync(string url, string emailAddress, string password, string username, CancellationToken cancellationToken)
        {
            return CreateAccountAsync(url, emailAddress, password, username, null, cancellationToken);
        }

        public async Task<AccountInfo> CreateAccountAsync(string url, string emailAddress, string password, string username, string teamName, CancellationToken cancellationToken)
        {
            CheckDisposed();

            if (url == null)
                throw new ArgumentNullException(nameof(url));
            if (emailAddress == null)
                throw new ArgumentNullException(nameof(emailAddress));
            if (password == null)
                throw new ArgumentNullException(nameof(password));
            if (username == null)
                throw new ArgumentNullException(nameof(username));

            CheckConnected();

            string passwordHash = Utils.GetMD5Hash(password + emailAddress.ToLower());

            XElement request = new XElement("create_account",
                new XElement("url", url),
                new XElement("email_addr", emailAddress),
                new XElement("passwd_hash", passwordHash),
                new XElement("user_name", username),
                new XElement("team_name", teamName));

            CheckResponse(await PerformRpcAsync(request));

            return new AccountInfo(await PollRpcAsync("<create_account_poll/>", cancellationToken));
        }

        public async Task<string> GetNewerVersionAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_newer_version/>");

            CheckResponse(response, "boinc_gui_rpc_reply");

            return response.ElementString("newer_version");
        }

        public async Task ReadCoreClientConfigAsync()
        {
            CheckDisposed();
            CheckConnected();

            CheckResponse(await PerformRpcAsync("<read_cc_config/>"));
        }

        public async Task<GlobalPreferences> GetGlobalPreferencesFileAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_global_prefs_file/>");

            CheckResponse(response, "global_preferences");

            return new GlobalPreferences(response);
        }

        public async Task<GlobalPreferences> GetGlobalPreferencesWorkingAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_global_prefs_working/>");

            CheckResponse(response, "global_preferences");

            return new GlobalPreferences(response);
        }

        public async Task<XElement> GetGlobalPreferencesOverrideAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_global_prefs_override/>");

            // TODO: fail gracefully if no override has been set instead of throwing exception?

            CheckResponse(response, "global_preferences");

            return response;
        }

        public async Task SetGlobalPreferencesOverrideAsync(XElement globalPreferencesOverride)
        {
            CheckDisposed();

            if (globalPreferencesOverride == null)
                throw new ArgumentNullException(nameof(globalPreferencesOverride));

            CheckConnected();

            XElement request = new XElement("set_global_prefs_override", globalPreferencesOverride);

            CheckResponse(await PerformRpcAsync(request));
        }

        public async Task ReadGlobalPreferencesOverrideAsync()
        {
            CheckDisposed();
            CheckConnected();

            CheckResponse(await PerformRpcAsync("<read_global_prefs_override/>"));
        }

        public async Task SetLanguageAsync(string language)
        {
            CheckDisposed();

            if (language == null)
                throw new ArgumentNullException(nameof(language));

            CheckConnected();

            XElement request = new XElement("set_language",
                new XElement("language", language));

            CheckResponse(await PerformRpcAsync(request));
        }

        public async Task<XElement> GetCoreClientConfigAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_cc_config/>");

            CheckResponse(response, "cc_config");

            return response;
        }

        public async Task SetCoreClientConfigAsync(XElement coreClientConfig)
        {
            CheckDisposed();

            if (coreClientConfig == null)
                throw new ArgumentNullException(nameof(coreClientConfig));

            CheckConnected();

            XElement request = new XElement("set_cc_config", coreClientConfig);

            CheckResponse(await PerformRpcAsync(request));
        }

        public async Task<DailyTransferStatistics[]> GetDailyTransferHistoryAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_daily_xfer_history/>");

            CheckResponse(response, "daily_xfers");

            return response.Elements("dx").Select(e => new DailyTransferStatistics(e)).ToArray();
        }

        public async Task<OldResult[]> GetOldResultsAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_old_results/>");

            CheckResponse(response, "old_results");

            return response.Elements("old_result").Select(e => new OldResult(e)).ToArray();
        }

        protected void CheckResponse(XElement response, string expectedElementName = null)
        {
            string elementName = response.Name.ToString();
            
            if (expectedElementName == null && elementName == "success")
                return;
            if (expectedElementName != null && elementName == expectedElementName)
                return;
            
            switch (elementName)
            {
                case "error":
                case "status":
                    string message = (string)response;
                    throw new RpcFailureException(message);
                case "unauthorized":
                    throw new RpcUnauthorizedException();
                default:
                    if (expectedElementName == null)
                        throw new InvalidRpcResponseException(string.Format("Expected <success/>, <error>, <status> or <unauthorized/> element but encountered <{0}>.", response.Name));
                    else
                        throw new InvalidRpcResponseException(string.Format("Expected <{0}> element but encountered <{1}>.", expectedElementName, response.Name));
            }
        }

        protected async Task<XElement> PollRpcAsync(string request, CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await Task.Delay(PollingInterval).ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                XElement element = await PerformRpcAsync(request).ConfigureAwait(false);

                ErrorCode errorCode = (ErrorCode)element.ElementInt("error_num");

                if (errorCode != ErrorCode.InProgress)
                    return element;
            }
        }

        protected Task<XElement> PerformRpcAsync(XElement request)
        {
            return PerformRpcAsync(request.ToString(SaveOptions.DisableFormatting));
        }

        protected async Task<XElement> PerformRpcAsync(string request)
        {
            string requestText = string.Format("<boinc_gui_rpc_request>\n{0}\n</boinc_gui_rpc_request>\n\x03", request);

            string responseText = await PerformRpcRawAsync(requestText).ConfigureAwait(false);

            // workaround for some RPC commands returning invalid XML, see https://github.com/BOINC/boinc/pull/1509
            if (responseText.Contains("<?xml version=\"1.0\" encoding=\"ISO-8859-1\" ?>"))
                responseText = responseText.Replace("<?xml version=\"1.0\" encoding=\"ISO-8859-1\" ?>", string.Empty);

            XElement response;

            try
            {
                response = XElement.Parse(responseText, LoadOptions.None);
            } catch (XmlException exception)
            {
                throw new InvalidRpcResponseException("RPC response is malformed.", exception);
            }

            if (response.Elements().Count() == 1)
                return response.Elements().First();
            else
                return response;
        }

        protected async Task<string> PerformRpcRawAsync(string request)
        {
            await semaphore.WaitAsync().ConfigureAwait(false);

            MemoryStream reply = new MemoryStream();

            try
            {
                NetworkStream networkStream = tcpClient.GetStream();
                
                byte[] sendBuffer = Encoding.ASCII.GetBytes(request);

                await networkStream.WriteAsync(sendBuffer, 0, sendBuffer.Length).ConfigureAwait(false);

                byte[] receiveBuffer = new byte[tcpClient.ReceiveBufferSize];

                int bytesRead;

                do
                {
                    bytesRead = await networkStream.ReadAsync(receiveBuffer, 0, receiveBuffer.Length).ConfigureAwait(false);

                    if (bytesRead == 0)
                        break;

                    reply.Write(receiveBuffer, 0, bytesRead);
                }
                while (receiveBuffer[bytesRead - 1] != 0x03);
            }
            finally
            {
                semaphore.Release();
            }

            reply.SetLength(reply.Length - 1);
            reply.Seek(0, SeekOrigin.Begin);

            using (StreamReader streamReader = new StreamReader(reply))
                return streamReader.ReadToEnd();
        }

        public bool Connected
        {
            get
            {
                CheckDisposed();

                return tcpClient != null && tcpClient.Connected;
            }
        }

        public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(0.25);

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (tcpClient != null)
                {
                    tcpClient.Dispose();
                    tcpClient = null;
                }
                if (semaphore != null)
                {
                    semaphore.Dispose();
                    semaphore = null;
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected void CheckDisposed()
        {
            if (semaphore == null)
                throw new ObjectDisposedException(GetType().FullName);
        }

        protected void CheckConnected()
        {
            if (!Connected)
                throw new InvalidOperationException("RpcClient is not connected.");
        }
    }

    [Serializable]
    public class InvalidRpcResponseException : Exception
    {
        public InvalidRpcResponseException()
        {
        }

        public InvalidRpcResponseException(string message) : base(message)
        {
        }

        public InvalidRpcResponseException(string message, Exception inner) : base(message, inner)
        {
        }
        
        protected InvalidRpcResponseException(SerializationInfo info, StreamingContext context)
        {
        }
    }

    [Serializable]
    public class RpcFailureException : Exception
    {
        public RpcFailureException()
        {
        }

        public RpcFailureException(string message) : base(message)
        {
        }

        public RpcFailureException(string message, Exception inner) : base(message, inner)
        {
        }
                
        protected RpcFailureException(SerializationInfo info, StreamingContext context)
        {
        }
    }

    [Serializable]
    public class RpcUnauthorizedException : Exception
    {
        public RpcUnauthorizedException()
        {
        }

        public RpcUnauthorizedException(string message) : base(message)
        {
        }

        public RpcUnauthorizedException(string message, Exception inner) : base(message, inner)
        {
        }
                
        protected RpcUnauthorizedException(SerializationInfo info, StreamingContext context)
        {
        }
    }
}