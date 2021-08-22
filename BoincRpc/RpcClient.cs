using System;
using System.Linq;
using System.Net.Sockets;
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
        private bool disposed = false;

        protected TcpClient tcpClient;
        protected SemaphoreSlim semaphore = new SemaphoreSlim(1);        

        public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(0.25);

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

        /// <summary>
        /// Exchange version info with the client.
        /// This request does not require authentication.
        /// </summary>
        /// <param name="clientName">The program name of the client.</param>
        /// <param name="localVersion">The version of the request's source.</param>
        /// <returns>The client's version info is returned.</returns>
        public async Task<VersionInfo> ExchangeVersionsAsync(string clientName, VersionInfo localVersion)
        {
            CheckDisposed();

            if (clientName == null)
                throw new ArgumentNullException(nameof(clientName));
            if (localVersion == null)
                throw new ArgumentNullException(nameof(localVersion));

            CheckConnected();

            XElement request = new XElement("exchange_versions",
                new XElement("major", localVersion.Major),
                new XElement("minor", localVersion.Minor),
                new XElement("release", localVersion.Release),
                new XElement("name", clientName));

            XElement response = await PerformRpcAsync(request);

            CheckResponse(response, "server_version");

            return new VersionInfo(response);
        }

        /// <summary>
        /// Get the client's 'static' state, i.e. its projects, apps, app_versions, workunits and results.
        /// This call is relatively slow and should only be done initially, and when needed later.
        /// This request does not require authentication.
        /// </summary>
        /// <returns>Client's entire state.</returns>
        public async Task<CoreClientState> GetStateAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_state/>");

            CheckResponse(response, "client_state");

            return new CoreClientState(response);
        }

        /// <summary>
        /// Get a list of results.
        /// Those that are in progress will have information such as CPU time and fraction done.
        /// Each result includes a name; use CoreClientState (lookup_result) to find this result in the current static state; if it's not there, call GetStateAsync() again.
        /// This request does not require authentication.
        /// </summary>
        /// <returns>List of results</returns>
        /// <seealso cref="GetStateAsync()"></seealso>
        public async Task<Result[]> GetResultsAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_results>\n<active_only>0</active_only>\n</get_results>\n");

            CheckResponse(response, "results");

            return response.Elements("result").Select(e => new Result(e)).ToArray();
        }

        /// <summary>
        /// Show all current file transfers.
        /// Each is linked by name to a project; use CoreClientState (lookup_project) to find this project in the current state; if it's not there, call GetStateAsync() again.
        /// This request does not require authentication.
        /// </summary>
        /// <returns>A list of file transfers in progress.</returns>
        /// <seealso cref="GetStateAsync()"></seealso>
        public async Task<FileTransfer[]> GetFileTransfersAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_file_transfers/>");

            CheckResponse(response, "file_transfers");

            return response.Elements("file_transfer").Select(e => new FileTransfer(e)).ToArray();
        }

        /// <summary>
        /// Show status of all attached projects. This request does not require authentication.
        /// </summary>
        /// <returns>List of projects.</returns>
        public async Task<Project[]> GetProjectStatusAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_project_status/>");

            CheckResponse(response, "projects");

            return response.Elements("project").Select(e => new Project(e)).ToArray();
        }

        /// <summary>
        /// Get a list of all the projects and account managers as found in the all_projects_list.xml file. 
        /// This request does not require authentication.
        /// </summary>
        /// <returns>List of projects and account managers</returns>
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

        /// <summary>
        /// Show disk usage by project. This request does not require authentication.
        /// </summary>
        /// <returns>Disk usage.</returns>
        public async Task<DiskUsage> GetDiskUsageAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_disk_usage/>");

            CheckResponse(response, "disk_usage_summary");

            return new DiskUsage(response);
        }

        /// <summary>
        /// Get statistics for the projects the client is attached to. This request does not require authentication.
        /// </summary>
        /// <returns>List of project's statistics.</returns>
        public async Task<ProjectStatistics[]> GetStatisticsAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_statistics/>");

            CheckResponse(response, "statistics");

            return response.Elements("project_statistics").Select(e => new ProjectStatistics(e)).ToArray();
        }

        /// <summary>
        /// Get CPU/GPU/network run modes and network connection status (version 6.12+)
        /// This request does not require authentication.
        /// </summary>
        /// <returns>CoreClientStatus (cc_status)</returns>
        public async Task<CoreClientStatus> GetCoreClientStatusAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_cc_status/>");

            CheckResponse(response, "cc_status");

            return new CoreClientStatus(response);
        }

        /// <summary>
        /// Retry deferred network communication. This request requires authentication.
        /// </summary>
        /// <returns></returns>
        public async Task NetworkAvailableAsync()
        {
            CheckDisposed();
            CheckConnected();

            CheckResponse(await PerformRpcAsync("<network_available/>"));
        }

        /// <summary>
        /// Project operations:
        /// - Reset a project.
        /// - Detach from a project.
        /// - Update a project.
        /// - Suspend a project.
        /// - Resume a project.
        /// - Stop getting new tasks for a project.
        /// - Receive new tasks for a project.
        /// - Detach from a project after all it's tasks are finished.
        /// - Don't detach from a project after all it's tasks are finished.
        /// This request requires authentication.
        /// </summary>
        /// <param name="project"></param>
        /// <param name="operation">Reset/Detach/Update/Suspend/Resume/NoMoreWork/AllowMoreWork/DetachWhenDone/DontDetachWhenDone</param>
        /// <returns></returns>
        public async Task PerformProjectOperationAsync(Project project, ProjectOperation operation)
        {
            CheckDisposed();

            if (project == null)
                throw new ArgumentNullException(nameof(project));

            CheckConnected();
            
            string tag;

            switch (operation)
            {
                case ProjectOperation.Reset:
                case ProjectOperation.Detach:
                case ProjectOperation.Update:
                case ProjectOperation.Suspend:
                case ProjectOperation.Resume:
                case ProjectOperation.AllowMoreWork:
                case ProjectOperation.NoMoreWork:
                    tag = "project_" + operation.ToString().ToLower();
                    break;
                case ProjectOperation.DetachWhenDone:
                    tag = "project_detach_when_done";
                    break;
                case ProjectOperation.DontDetachWhenDone:
                    tag = "project_dont_detach_when_done";
                    break;
                default:
                    throw new ArgumentException("Invalid project operation.", nameof(operation));
            }

            XElement request = new XElement(tag,
                new XElement("project_url", project.MasterUrl));

            CheckResponse(await PerformRpcAsync(request));
        }

        /// <summary>
        /// Project operation. Attach the client to a project. This request requires authentication.
        /// </summary>
        /// <param name="projectUrl"></param>
        /// <param name="authenticator"></param>
        /// <param name="projectName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Set run mode for given duration. This request requires authentication.
        /// </summary>
        /// <param name="mode">Always/Auto/Never/Restore.</param>
        /// <param name="duration"></param>
        /// <returns></returns>
        public async Task SetRunModeAsync(Mode mode, TimeSpan duration)
        {
            CheckDisposed();
            CheckConnected();

            XElement request = new XElement("set_run_mode",
                new XElement(mode.ToString().ToLower()),
                new XElement("duration", duration.TotalSeconds));

            CheckResponse(await PerformRpcAsync(request));
        }
        
        public Task SetGpuModeAsync(Mode mode)
        {
            return SetGpuModeAsync(mode, TimeSpan.Zero);
        }

        /// <summary>
        /// Set GPU run mode for given duration. This request requires authentication.
        /// </summary>
        /// <param name="mode">Always/Auto/Never/Restore.</param>
        /// <param name="duration"></param>
        /// <returns></returns>
        public async Task SetGpuModeAsync(Mode mode, TimeSpan duration)
        {
            CheckDisposed();
            CheckConnected();

            XElement request = new XElement("set_gpu_mode",
                new XElement(mode.ToString().ToLower()),
                new XElement("duration", duration.TotalSeconds));

            CheckResponse(await PerformRpcAsync(request));
        }
        
        public Task SetNetworkModeAsync(Mode mode)
        {
            return SetNetworkModeAsync(mode, TimeSpan.Zero);
        }

        /// <summary>
        /// Set the network mode for given duration. This request requires authentication.
        /// </summary>
        /// <param name="mode">Always/Auto/Never/Restore.</param>
        /// <param name="duration"></param>
        /// <returns></returns>
        public async Task SetNetworkModeAsync(Mode mode, TimeSpan duration)
        {
            CheckDisposed();
            CheckConnected();

            XElement request = new XElement("set_network_mode",
                new XElement(mode.ToString().ToLower()),
                new XElement("duration", duration.TotalSeconds));

            CheckResponse(await PerformRpcAsync(request));
        }

        /// <summary>
        /// Show suspend reason and active tasks. This request does not require authentication.
        /// </summary>
        /// <returns>List of results and suspend reason.</returns>
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

        /// <summary>
        /// Run benchmarks. This request requires authentication.
        /// </summary>
        /// <returns></returns>
        public async Task RunBenchmarksAsync()
        {
            CheckDisposed();
            CheckConnected();

            CheckResponse(await PerformRpcAsync("<run_benchmarks/>"));
        }

        /// <summary>
        /// Set the proxy settings. This request requires authentication.
        /// </summary>
        /// <param name="proxyInfo"></param>
        /// <returns></returns>
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
                    new XElement("socks5_user_name", proxyInfo.Socks5UserName),
                    new XElement("socks5_user_passwd", proxyInfo.Socks5UserPassword),
                    new XElement("socks5_remote_dns", proxyInfo.Socks5RemoteDns ? 1 : 0),
                    new XElement("no_proxy", proxyInfo.NoProxyHosts)));

            CheckResponse(await PerformRpcAsync(request));
        }

        /// <summary>
        /// Get proxy settings. This request requires authentication.
        /// </summary>
        /// <returns></returns>
        public async Task<ProxyInfo> GetProxySettingsAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_proxy_settings/>");

            CheckResponse(response, "proxy_info");

            return new ProxyInfo(response);
        }

        /// <summary>
        /// Show largest message seqno. Implemented in 6.10+ client version.
        /// This request does not require authentication.
        /// </summary>
        /// <returns>The greatest message sequence number.</returns>
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

        /// <summary>
        /// Returns a list of messages to be displayed to the user.
        /// Each message has a sequence number (1, 2, ...), a priority (1=informational, 2=error) and a timestamp.
        /// The RPC requests the messages with sequence numbers greater than seqno, in order of increasing sequence number.
        /// 
        /// If translatable is true, messages from 6.11+ clients may include translatable parts. These parts are enclosed in _("...").
        /// They should be translated according to the translation files in boinc/locale/*/BOINC-Client.po.
        /// This request does not require authentication.
        /// </summary>
        /// <param name="sequenceNumber">seqno</param>
        /// <returns>List of messages with sequence numbers beyond the given seqno.</returns>
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

        /// <summary>
        /// Returns both private and non-private notices with sequence number greater than 0.
        /// This request requires authentication.
        /// </summary>
        /// <returns>List of notices.</returns>
        public Task<Notice[]> GetNoticesAsync()
        {
            return GetNoticesAsync(0, false);
        }

        /// <summary>
        /// Returns a list of notices with sequence number greater than seqno.
        /// Notices are returned in order of increasing sequence number (which is the same as increasing arrival time).
        /// Unlike messages, notices can be removed. In this case, notices.complete is set to true, and notices.notices contains all notices. Otherwise notices.notices contains only new notices.
        /// Implemented in 6.11+ client version.
        /// </summary>
        /// <param name="sequenceNumber">seqno</param>
        /// <param name="publicOnly">Returns only non-private notices. Doesn't require authentication.</param>
        /// <returns>List of notices.</returns>
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

        /// <summary>
        /// File transfer operations. Abort a pending file transfer or Retry a file transfer. This request requires authentication.
        /// </summary>
        /// <param name="fileTransfer"></param>
        /// <param name="operation">Abort/Retry</param>
        /// <returns></returns>
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

        /// <summary>
        /// Task operations:
        ///  - Abort a task,
        ///  - Suspend a running task
        ///  - Resume a suspended task.
        /// This request requires authentication.
        /// </summary>
        /// <param name="result"></param>
        /// <param name="operation">Abort/Suspend/Resume</param>
        /// <returns></returns>
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

        /// <summary>
        /// Get information about host hardware and usage. This request does not require authentication.
        /// </summary>
        /// <returns>Host info.</returns>
        public async Task<HostInfo> GetHostInfoAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_host_info/>");

            CheckResponse(response, "host_info");

            return new HostInfo(response);
        }
        
        /// <summary>
        /// Tell the client to get host parameters (RAM and disk sizes, #CPUs) again.
        /// Do this if you're running the client in a container, and you change the parameters of the container.
        /// This request requires authentication.
        /// </summary>
        public async Task ResetHostInfoAsync()
        {
            CheckDisposed();
            CheckConnected();

            CheckResponse(await PerformRpcAsync("<reset_host_info/>"));
        }

        /// <summary>
        /// Tell client to exit. This request requires authentication.
        /// </summary>
        /// <returns></returns>
        public async Task QuitAsync()
        {
            CheckDisposed();
            CheckConnected();

            CheckResponse(await PerformRpcAsync("<quit/>"));
        }

        /// <summary>
        /// Account manager operation. Do an Account Manager RPC to the given URL, passing the given name/password.
        /// If the RPC is successful, save the account info on disk (it can be retrieved later using GetAccountManagerInfoAsync()).
        /// This request requires authentication.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="name"></param>
        /// <param name="password"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
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

            CheckResponse(await PerformRpcAsync(request));

            return new AccountManagerRpcReply(await PollRpcAsync("<acct_mgr_rpc_poll/>", cancellationToken));
        }

        /// <summary>
        /// Account manager operation. Remove account manager info from disk.
        /// This request requires authentication.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<AccountManagerRpcReply> AccountManagerDetachAsync(CancellationToken cancellationToken)
        {
            return AccountManagerAttachAsync(string.Empty, string.Empty, string.Empty, cancellationToken);
        }

        /// <summary>
        /// Account manager operation. Do an RPC to the current account manager.
        /// This request requires authentication.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<AccountManagerRpcReply> AccountManagerUpdateAsync(CancellationToken cancellationToken)
        {
            CheckDisposed();
            CheckConnected();

            CheckResponse(await PerformRpcAsync("<acct_mgr_rpc>\n<use_config_file/>\n</acct_mgr_rpc>"));

            return new AccountManagerRpcReply(await PollRpcAsync("<acct_mgr_rpc_poll/>", cancellationToken));
        }

        /// <summary>
        /// Account manager operation. Retrieve account manager information.
        /// This request requires authentication.
        /// </summary>
        /// <returns>Return the URL/name of the current account manager (if any), and the user name and password.</returns>
        public async Task<AccountManagerInfo> GetAccountManagerInfoAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<acct_mgr_info/>");

            CheckResponse(response, "acct_mgr_info");

            return new AccountManagerInfo(response);
        }

        /// <summary>
        /// Project operation. Get the contents of the project_init.xml file if present.
        /// This request requires authentication.
        /// </summary>
        /// <returns></returns>
        public async Task<ProjectInitStatus> GetProjectInitStatusAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_project_init_status/>");

            CheckResponse(response, "get_project_init_status");

            return new ProjectInitStatus(response);
        }

        /// <summary>
        /// Project operation. Fetch the project configuration file from the specified url.
        /// This request requires authentication.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Account operation. Look for an account in a given project.
        /// This request requires authentication.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="emailAddress"></param>
        /// <param name="password"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Account operation. Look for an account in a given project using LDAP authentication.
        /// This request requires authentication.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="emailAddress"></param>
        /// <param name="password"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<AccountInfo> LookupAccountLdapAsync(string url, string uid, string password, CancellationToken cancellationToken)
        {
            CheckDisposed();

            if (url == null)
                throw new ArgumentNullException(nameof(url));
            if (uid == null)
                throw new ArgumentNullException(nameof(uid));
            if (password == null)
                throw new ArgumentNullException(nameof(password));

            CheckConnected();

            XElement request = new XElement("lookup_account",
                new XElement("url", url),
                new XElement("email_addr", uid),
                new XElement("passwd_hash", password),
                new XElement("ldap_auth", 1));

            CheckResponse(await PerformRpcAsync(request));

            return new AccountInfo(await PollRpcAsync("<lookup_account_poll/>", cancellationToken));
        }

        public Task<AccountInfo> CreateAccountAsync(string url, string emailAddress, string password, string username, bool consentedToTerms, CancellationToken cancellationToken)
        {
            return CreateAccountAsync(url, emailAddress, password, username, null, consentedToTerms, cancellationToken);
        }

        /// <summary>
        /// Account operation. Create an account for a given project.
        /// This request requires authentication.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="emailAddress"></param>
        /// <param name="password"></param>
        /// <param name="username"></param>
        /// <param name="teamName"></param>
        /// <param name="consentedToTerms"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<AccountInfo> CreateAccountAsync(string url, string emailAddress, string password, string username, string teamName, bool consentedToTerms, CancellationToken cancellationToken)
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
                new XElement("team_name", teamName),
                consentedToTerms ? new XElement("consented_to_terms") : null);

            CheckResponse(await PerformRpcAsync(request));

            return new AccountInfo(await PollRpcAsync("<create_account_poll/>", cancellationToken));
        }

        /// <summary>
        /// Get newer version number, if any, and download url.
        /// This request does not require authentication.
        /// </summary>
        public async Task<NewerVersionInfo> GetNewerVersionAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_newer_version/>");

            CheckResponse(response, "boinc_gui_rpc_reply");

            return new NewerVersionInfo(response);
        }

        /// <summary>
        /// Read the cc_config.xml file and set the configuration accordingly.
        /// If no such file is present or it's contents are not formatted correctly the defaults are used.
        /// This request requires authentication.
        /// </summary>
        /// <returns></returns>
        public async Task ReadCoreClientConfigAsync()
        {
            CheckDisposed();
            CheckConnected();

            CheckResponse(await PerformRpcAsync("<read_cc_config/>"));
        }

        /// <summary>
        /// Global preferences operation. Get the contents of the global_prefs.xml file if present.
        /// This request requires authentication.
        /// </summary>
        /// <returns>Contents of the global_prefs.xml</returns>
        public async Task<GlobalPreferences> GetGlobalPreferencesFileAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_global_prefs_file/>");

            CheckResponse(response, "global_preferences");

            return new GlobalPreferences(response);
        }

        /// <summary>
        /// Global preferences operation: Get the currently used global_prefs.xml.
        /// This request requires authentication.
        /// </summary>
        /// <returns></returns>
        public async Task<GlobalPreferences> GetGlobalPreferencesWorkingAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_global_prefs_working/>");

            CheckResponse(response, "global_preferences");

            return new GlobalPreferences(response);
        }

        /// <summary>
        /// Global preferences operation: Get the contents of the global_prefs_override.xml file if present.
        /// This request requires authentication.
        /// </summary>
        /// <returns>Contents of the global preferences override.xml</returns>
        public async Task<XElement> GetGlobalPreferencesOverrideAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_global_prefs_override/>");

            // TODO: fail gracefully if no override has been set instead of throwing exception?

            CheckResponse(response, "global_preferences");

            return response;
        }

        /// <summary>
        /// Global preferences operation: Write the given contents to the global preferences override file. If the argument is an empty string, delete the file.
        /// This request requires authentication.
        /// </summary>
        /// <param name="globalPreferencesOverride"></param>
        /// <returns></returns>
        public async Task SetGlobalPreferencesOverrideAsync(XElement globalPreferencesOverride)
        {
            CheckDisposed();

            if (globalPreferencesOverride == null)
                throw new ArgumentNullException(nameof(globalPreferencesOverride));

            CheckConnected();

            if (globalPreferencesOverride != null)
            {
                XElement request = new XElement("set_global_prefs_override", globalPreferencesOverride);
                CheckResponse(await PerformRpcAsync(request));
            }
            else
            {
                CheckResponse(await PerformRpcAsync("<set_global_prefs_override></set_global_prefs_override>"));
            }
        }

        /// <summary>
        /// Global preferences operation: Tells the client to reread the global_prefs_override.xml file, and set the preferences accordingly.
        /// This request requires authentication.
        /// </summary>
        /// <returns></returns>
        public async Task ReadGlobalPreferencesOverrideAsync()
        {
            CheckDisposed();
            CheckConnected();

            CheckResponse(await PerformRpcAsync("<read_global_prefs_override/>"));
        }

        /// <summary>
        /// Set the language field in the client_state.xml file to append it in any subsequent GET calls to the original URL and translate notices.
        /// This request requires authentication.
        /// </summary>
        /// <param name="language"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Get the contents of the cc_config.xml file if present. This request requires authentication.
        /// </summary>
        /// <returns>The content of the file.</returns>
        public async Task<XElement> GetCoreClientConfigAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_cc_config/>");

            CheckResponse(response, "cc_config");

            return response;
        }

        /// <summary>
        /// Write a new cc_config.xml file. This request requires authentication.
        /// </summary>
        /// <param name="coreClientConfig"></param>
        /// <returns></returns>
        public async Task SetCoreClientConfigAsync(XElement coreClientConfig)
        {
            CheckDisposed();

            if (coreClientConfig == null)
                throw new ArgumentNullException(nameof(coreClientConfig));

            CheckConnected();

            XElement request = new XElement("set_cc_config", coreClientConfig);

            CheckResponse(await PerformRpcAsync(request));
        }

        /// <summary>
        /// Get a daily history of number of bytes uploaded and downloaded. Read from daily_xfer_history.xml.
        /// Implemented in 6.13.7+ clients. This request does not require authentication.
        /// </summary>
        /// <returns>Daily transfer statistics</returns>
        public async Task<DailyTransferStatistics[]> GetDailyTransferHistoryAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_daily_xfer_history/>");

            CheckResponse(response, "daily_xfers");

            return response.Elements("dx").Select(e => new DailyTransferStatistics(e)).ToArray();
        }

        /// <summary>
        /// Get a list of results that have been completed in the last hour and have been reported to their project. (These results are not returned by GetResultsAsync()).
        /// This request does not require authentication.
        /// </summary>
        /// <returns>List of results.</returns>
        public async Task<OldResult[]> GetOldResultsAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_old_results/>");

            CheckResponse(response, "old_results");

            return response.Elements("old_result").Select(e => new OldResult(e)).ToArray();
        }

        /// <summary>
        /// Get the app config for a project. This request requires authentication.
        /// </summary>
        public async Task<XElement> GetAppConfigAsync(Project project)
        {
            CheckDisposed();

            if (project == null)
                throw new ArgumentNullException(nameof(project));

            CheckConnected();

            XElement request = new XElement("get_app_config",
                new XElement("url", project.MasterUrl));

            XElement response = await PerformRpcAsync(request);

            CheckResponse(response, "app_config");

            return response;
        }

        /// <summary>
        /// Set the app config for a project. This request requires authentication.
        /// </summary>
        public async Task SetAppConfigAsync(Project project, XElement appConfig)
        {
            CheckDisposed();

            if (project == null)
                throw new ArgumentNullException(nameof(project));
            if (appConfig == null)
                throw new ArgumentNullException(nameof(appConfig));

            CheckConnected();

            XElement request = new XElement("set_app_config",
                new XElement("url", project.MasterUrl),
                appConfig);

            CheckResponse(await PerformRpcAsync(request));
        }

        /// <summary>
        /// Start graphics app (only on macOS 10.15 or later).
        /// This request requires authentication.
        /// </summary>
        /// <param name="slot">Slot number of the task for which to start the graphics app, or -1 to start the default screensaver.</param>
        /// <param name="fullscreen">Whether to start the graphics app full screen or in windowed mode.</param>
        /// <param name="screensaverLoginUser">User name of the user that invoked the screen saver (the currently logged-in user).</param>
        /// <returns></returns>
        public async Task RunGraphicsAppAsync(int slot, bool fullscreen, string screensaverLoginUser)
        {
            CheckDisposed();

            if (screensaverLoginUser == null)
                throw new ArgumentNullException(nameof(screensaverLoginUser));

            CheckConnected();

            string request = "<run_graphics_app>" +
                new XElement("slot", slot) +
                (fullscreen ? "<runfullscreen/>" : "<run/>") +
                new XElement("ScreensaverLoginUser", screensaverLoginUser) +
                "</run_graphics_app>";

            CheckResponse(await PerformRpcAsync(request));
        }

        /// <summary>
        /// Stop a running graphics app (only on macOS 10.13 or later).
        /// This request requires authentication.
        /// </summary>
        /// <param name="graphicsPid">Process ID of the graphics app to stop.</param>
        /// <param name="screensaverLoginUser">User name of the user that invoked the screen saver (the currently logged-in user).</param>
        /// <returns></returns>
        public async Task StopGraphicsAppAsync(int graphicsPid, string screensaverLoginUser)
        {
            CheckDisposed();

            if (screensaverLoginUser == null)
                throw new ArgumentNullException(nameof(screensaverLoginUser));

            CheckConnected();

            string request = "<run_graphics_app>" +
                new XElement("graphics_pid", graphicsPid) +
                "<stop/>" +
                new XElement("ScreensaverLoginUser", screensaverLoginUser) +
                "</run_graphics_app>";

            CheckResponse(await PerformRpcAsync(request));
        }

        /// <summary>
        /// Check whether the specified graphics app is still running (only on macOS 10.13 or later).
        /// This request requires authentication.
        /// </summary>
        /// <param name="graphicsPid">Process ID of the graphics app to check.</param>
        /// <returns>True if the graphics app is still running, otherwise false.</returns>
        public async Task<bool> TestGraphicsAppAsync(int graphicsPid)
        {
            CheckDisposed();
            CheckConnected();

            string request = "<run_graphics_app>" +
                new XElement("graphics_pid", graphicsPid) +
                "<test/>" +
                "</run_graphics_app>";

            XElement response = await PerformRpcAsync(request);

            CheckResponse(response, "boinc_gui_rpc_reply");
            
            return response.ElementInt("graphics_pid") != 0;
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
                await Task.Delay(PollingInterval, cancellationToken).ConfigureAwait(false);

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
            }
            catch (XmlException exception)
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
            const int minReceiveBufferSize = 256;

            byte[] replyBuffer = new byte[minReceiveBufferSize];
            int replyLength = 0;

            await semaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                NetworkStream networkStream = tcpClient.GetStream();
                
                byte[] sendBuffer = Encoding.ASCII.GetBytes(request);

                await networkStream.WriteAsync(sendBuffer, 0, sendBuffer.Length).ConfigureAwait(false);

                do
                {
                    if (replyBuffer.Length - replyLength < minReceiveBufferSize)
                        Array.Resize(ref replyBuffer, replyBuffer.Length * 2);

                    int bytesRead = await networkStream.ReadAsync(replyBuffer, replyLength, replyBuffer.Length - replyLength).ConfigureAwait(false);

                    if (bytesRead == 0)
                        throw new InvalidRpcResponseException("RPC response is truncated.");

                    replyLength += bytesRead;
                } while (replyBuffer[replyLength - 1] != 0x03);
            }
            finally
            {
                semaphore.Release();
            }

            return Encoding.ASCII.GetString(replyBuffer, 0, replyLength - 1);
        }

        public bool Connected
        {
            get
            {
                CheckDisposed();

                return tcpClient != null && tcpClient.Connected;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

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

            disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void CheckDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().FullName);
        }

        protected void CheckConnected()
        {
            if (!Connected)
                throw new InvalidOperationException("RpcClient is not connected.");
        }
    }

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
    }

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
    }

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
    }
}
