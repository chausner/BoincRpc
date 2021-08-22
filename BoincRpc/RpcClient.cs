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

    /// <summary>
    /// Client for the BOINC GUI RPC protocol (https://boinc.berkeley.edu/trac/wiki/GuiRpcProtocol).
    /// </summary>
    public class RpcClient : IDisposable
    {
        private bool disposed = false;

        protected TcpClient tcpClient;
        protected SemaphoreSlim semaphore = new SemaphoreSlim(1);

        /// <summary>
        /// Gets or sets the time to wait between status update requests during polling operations.
        /// </summary>
        public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(0.25);

        /// <summary>
        /// Connect to a BOINC client.
        /// </summary>
        /// <param name="host">Host name or IP address of the BOINC client.</param>
        /// <param name="port">Port to connect to.</param>
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

        /// <summary>
        /// Close any active connection.
        /// </summary>
        public void Close()
        {
            CheckDisposed();

            if (tcpClient != null)
            {
                tcpClient.Dispose();
                tcpClient = null;
            }
        }

        /// <summary>
        /// Authenticate with the BOINC client.
        /// After successful authentication, operations that require authentication can be performed.
        /// </summary>
        /// <param name="password">GUI RPC password to use for authentication.</param>
        /// <returns>True if the authentication was successful, otherwise false.</returns>
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
        /// Exchange version information with the client.
        /// <para>This request does not require prior authentication.</para>
        /// </summary>
        /// <param name="clientName">Name of the program connected to the BOINC client.</param>
        /// <param name="localVersion">Version of the program connected to the BOINC client.</param>
        /// <returns>Version of the BOINC client.</returns>
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
        /// Get the client's "static" state, i.e. its projects, apps, app versions, workunits and results.
        /// This call is relatively slow and should only be done initially, and when needed later.
        /// <para>This request does not require prior authentication.</para>
        /// </summary>
        /// <returns>The client's entire state.</returns>
        public async Task<CoreClientState> GetStateAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_state/>");

            CheckResponse(response, "client_state");

            return new CoreClientState(response);
        }

        /// <summary>
        /// Get a list of all results.
        /// Those that are in progress will have information such as CPU time and fraction done.
        /// Each result includes a name; use the <see cref="CoreClientState"/> (via <see cref="GetStateAsync"/>) to find this result in the current static state; if it's not there, call <see cref="GetStateAsync"/> again.
        /// <para>This request does not require prior authentication.</para>
        /// </summary>
        /// <returns>A list of all results.</returns>
        public async Task<Result[]> GetResultsAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_results>\n<active_only>0</active_only>\n</get_results>\n");

            CheckResponse(response, "results");

            return response.Elements("result").Select(e => new Result(e)).ToArray();
        }

        /// <summary>
        /// Get a list of all current file transfers.
        /// Each is linked by name to a project; use the <see cref="CoreClientState"/> (via <see cref="GetStateAsync"/>) to find this project in the current state; if it's not there, call <see cref="GetStateAsync"/> again.
        /// <para>This request does not require prior authentication.</para>
        /// </summary>
        /// <returns>A list of file transfers in progress.</returns>
        public async Task<FileTransfer[]> GetFileTransfersAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_file_transfers/>");

            CheckResponse(response, "file_transfers");

            return response.Elements("file_transfer").Select(e => new FileTransfer(e)).ToArray();
        }

        /// <summary>
        /// Get a list of all attached projects.
        /// <para>This request does not require prior authentication.</para>
        /// </summary>
        /// <returns>A list of all projects.</returns>
        public async Task<Project[]> GetProjectStatusAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_project_status/>");

            CheckResponse(response, "projects");

            return response.Elements("project").Select(e => new Project(e)).ToArray();
        }

        /// <summary>
        /// Get a list of all the projects as found in the all_projects_list.xml file. 
        /// <para>This request does not require prior authentication.</para>
        /// </summary>
        /// <returns>A list of projects.</returns>
        public async Task<ProjectListEntry[]> GetAllProjectsListAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_all_projects_list/>"); // <get_all_projects_list/> returns both projects and acc. managers

            CheckResponse(response, "projects");

            return response.Elements("project").Select(e => new ProjectListEntry(e)).ToArray();
        }

        /// <summary>
        /// Get a list of all the account managers as found in the all_projects_list.xml file. 
        /// <para>This request does not require prior authentication.</para>
        /// </summary>
        /// <returns>A list of account managers.</returns>
        public async Task<AccountManagerListEntry[]> GetAllAccountManagersListAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_all_projects_list/>"); // <get_all_projects_list/> returns both projects and acc. managers

            CheckResponse(response, "projects");

            return response.Elements("account_manager").Select(e => new AccountManagerListEntry(e)).ToArray();
        }

        /// <summary>
        /// Get disk usage by project.
        /// <para>This request does not require prior authentication.</para>
        /// </summary>
        /// <returns>Disk usage information.</returns>
        public async Task<DiskUsage> GetDiskUsageAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_disk_usage/>");

            CheckResponse(response, "disk_usage_summary");

            return new DiskUsage(response);
        }

        /// <summary>
        /// Get statistics for the projects the client is attached to.
        /// <para>This request does not require prior authentication.</para>
        /// </summary>
        /// <returns>A list of project statistics.</returns>
        public async Task<ProjectStatistics[]> GetStatisticsAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_statistics/>");

            CheckResponse(response, "statistics");

            return response.Elements("project_statistics").Select(e => new ProjectStatistics(e)).ToArray();
        }

        /// <summary>
        /// Get CPU/GPU/network run modes and network connection status.
        /// <para>This request does not require prior authentication.</para>
        /// </summary>
        /// <returns>The core client status.</returns>
        public async Task<CoreClientStatus> GetCoreClientStatusAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_cc_status/>");

            CheckResponse(response, "cc_status");

            return new CoreClientStatus(response);
        }

        /// <summary>
        /// Retry deferred network communication.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        public async Task NetworkAvailableAsync()
        {
            CheckDisposed();
            CheckConnected();

            CheckResponse(await PerformRpcAsync("<network_available/>"));
        }

        /// <summary>
        /// Perform one of the following operations on a project:
        /// <list type="bullet">
        /// <item>Reset a project.</item>
        /// <item>Detach from a project.</item>
        /// <item>Update a project.</item>
        /// <item>Suspend a project.</item>
        /// <item>Resume a project.</item>
        /// <item>Stop getting new tasks for a project.</item>
        /// <item>Receive new tasks for a project.</item>
        /// <item>Detach from a project after all it's tasks are finished.</item>
        /// <item>Don't detach from a project after all it's tasks are finished.</item>
        /// </list>
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <param name="project">The project to perform the operation on.</param>
        /// <param name="operation">The operation to perform.</param>
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
        /// Attach the client to a project.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <param name="projectUrl">The master URL of the project.</param>
        /// <param name="authenticator">The authenticator key.</param>
        /// <param name="projectName">The name of the project.</param>
        /// <param name="cancellationToken">A cancellation token which can be used to cancel the asynchronous operation before it completes.</param>
        /// <returns>Status and result of the attachment operation.</returns>
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

        /// <summary>
        /// Set the run mode.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <param name="mode">The run mode to set.</param>
        public Task SetRunModeAsync(Mode mode)
        {
            return SetRunModeAsync(mode, TimeSpan.Zero);
        }

        /// <summary>
        /// Temporarily set the run mode for the given duration.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <param name="mode">The run mode to set temporarily.</param>
        /// <param name="duration">The time span after which the run mode should get reset.</param>
        public async Task SetRunModeAsync(Mode mode, TimeSpan duration)
        {
            CheckDisposed();
            CheckConnected();

            XElement request = new XElement("set_run_mode",
                new XElement(mode.ToString().ToLower()),
                new XElement("duration", duration.TotalSeconds));

            CheckResponse(await PerformRpcAsync(request));
        }

        /// <summary>
        /// Set the GPU run mode.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <param name="mode">The GPU run mode to set.</param>
        public Task SetGpuModeAsync(Mode mode)
        {
            return SetGpuModeAsync(mode, TimeSpan.Zero);
        }

        /// <summary>
        /// Temporarily set the GPU run mode for the given duration.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <param name="mode">The GPU run mode to set temporarily.</param>
        /// <param name="duration">The time span after which the GPU run mode should get reset.</param>
        public async Task SetGpuModeAsync(Mode mode, TimeSpan duration)
        {
            CheckDisposed();
            CheckConnected();

            XElement request = new XElement("set_gpu_mode",
                new XElement(mode.ToString().ToLower()),
                new XElement("duration", duration.TotalSeconds));

            CheckResponse(await PerformRpcAsync(request));
        }

        /// <summary>
        /// Set the network mode.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <param name="mode">The network mode to set.</param>
        public Task SetNetworkModeAsync(Mode mode)
        {
            return SetNetworkModeAsync(mode, TimeSpan.Zero);
        }

        /// <summary>
        /// Temporarily set the network mode for the given duration.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <param name="mode">The network mode to set temporarily.</param>
        /// <param name="duration">The time span after which the network mode should get reset.</param>
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
        /// Get a list of all active tasks and reasons why computations may be currently suspended.
        /// <para>This request does not require prior authentication.</para>
        /// </summary>
        /// <returns>Tuploe of active tasks and the suspend reasons.</returns>
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
        /// Run benchmarks.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        public async Task RunBenchmarksAsync()
        {
            CheckDisposed();
            CheckConnected();

            CheckResponse(await PerformRpcAsync("<run_benchmarks/>"));
        }

        /// <summary>
        /// Set proxy settings.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <param name="proxyInfo">The proxy settings to apply.</param>
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
        /// Get proxy settings.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <returns>The current proxy settings.</returns>
        public async Task<ProxyInfo> GetProxySettingsAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_proxy_settings/>");

            CheckResponse(response, "proxy_info");

            return new ProxyInfo(response);
        }

        /// <summary>
        /// Get the sequence number of the latest message.
        /// <para>This request does not require prior authentication.</para>
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

        /// <summary>
        /// Get a list of all available messages.
        /// Each message has a sequence number (1, 2, ...), a priority (informational, error) and a timestamp.
        /// <para>This request does not require prior authentication.</para>
        /// </summary>
        /// <returns>A list of all messages.</returns>
        public Task<Message[]> GetMessagesAsync()
        {
            return GetMessagesAsync(0);
        }

        /// <summary>
        /// Get a list of new messages.
        /// Each message has a sequence number (1, 2, ...), a priority (informational, error) and a timestamp.
        /// The RPC requests all messages with a sequence numbers greater than <paramref name="sequenceNumber"/>, in order of increasing sequence number.
        /// <para>This request does not require prior authentication.</para>
        /// </summary>
        /// <param name="sequenceNumber">The sequence number of the last message already received.</param>
        /// <returns>The list of messages with sequence numbers beyond the given sequence number.</returns>
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
        /// Get a list of all available notices.
        /// Notices are returned in order of increasing sequence number (which is the same as increasing arrival time).
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <returns>A list of all notices.</returns>
        public Task<Notice[]> GetNoticesAsync()
        {
            return GetNoticesAsync(0, false);
        }

        /// <summary>
        /// Get a list of new notices.
        /// The RPC requests all notices with a sequence numbers greater than <paramref name="sequenceNumber"/>.
        /// Notices are returned in order of increasing sequence number (which is the same as increasing arrival time).
        /// <para>This request requires prior authentication if <paramref name="publicOnly"/> is false.</para>
        /// </summary>
        /// <param name="sequenceNumber">The sequence number of the last notice already received.</param>
        /// <param name="publicOnly">If true, only non-private notices are returned. In this case, no prior authentication is required.</param>
        /// <returns>The list of notices with sequence numbers beyond the given sequence number.</returns>
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
        /// Perform one of the following operations on file transfers:
        /// <list type="bullet">
        /// <item>Abort a pending file transfer.</item>
        /// <item>Retry a file transfer.</item>
        /// </list>
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <param name="fileTransfer">The file transfer to perform the operation on.</param>
        /// <param name="operation">The operation to perform.</param>
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
        /// Perform one of the following operations on tasks:
        /// <list type="bullet">
        /// <item>Abort a task.</item>
        /// <item>Resume a suspended task.</item>
        /// </list>
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <param name="result">The task to perform the operation on.</param>
        /// <param name="operation">The operation to perform.</param>
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
        /// Get information about host hardware and usage.
        /// <para>This request does not require prior authentication.</para>
        /// </summary>
        /// <returns>The host information.</returns>
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
        /// Do this if you're running the client in a container and you change the parameters of the container.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        public async Task ResetHostInfoAsync()
        {
            CheckDisposed();
            CheckConnected();

            CheckResponse(await PerformRpcAsync("<reset_host_info/>"));
        }

        /// <summary>
        /// Tell the client to exit.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        public async Task QuitAsync()
        {
            CheckDisposed();
            CheckConnected();

            CheckResponse(await PerformRpcAsync("<quit/>"));
        }

        /// <summary>
        /// Attach the client to an account manager.
        /// If the RPC is successful, the account info will be saved on disk and can be retrieved later using <see cref="GetAccountManagerInfoAsync"/>.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <param name="url">The URL of the account manager.</param>
        /// <param name="name">The user name to use for authenticating to the account manager.</param>
        /// <param name="password">The password to use for authenticating to the account manager.</param>
        /// <param name="cancellationToken">A cancellation token which can be used to cancel the asynchronous operation before it completes.</param>
        /// <returns>Status and result of the attachment operation.</returns>
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
        /// Detach the client from an account manager. Removes the account manager info from disk.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <param name="cancellationToken">A cancellation token which can be used to cancel the asynchronous operation before it completes.</param>
        /// <returns>Status and result of the operation.</returns>
        public Task<AccountManagerRpcReply> AccountManagerDetachAsync(CancellationToken cancellationToken)
        {
            return AccountManagerAttachAsync(string.Empty, string.Empty, string.Empty, cancellationToken);
        }

        /// <summary>
        /// Update (do an RPC to) the current account manager.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <param name="cancellationToken">A cancellation token which can be used to cancel the asynchronous operation before it completes.</param>
        /// <returns>Status and result of the operation.</returns>
        public async Task<AccountManagerRpcReply> AccountManagerUpdateAsync(CancellationToken cancellationToken)
        {
            CheckDisposed();
            CheckConnected();

            CheckResponse(await PerformRpcAsync("<acct_mgr_rpc>\n<use_config_file/>\n</acct_mgr_rpc>"));

            return new AccountManagerRpcReply(await PollRpcAsync("<acct_mgr_rpc_poll/>", cancellationToken));
        }

        /// <summary>
        /// Retrieve account manager information (URL/name of the current account manager (if any) and credential information).
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <returns>The account manager information.</returns>
        public async Task<AccountManagerInfo> GetAccountManagerInfoAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<acct_mgr_info/>");

            CheckResponse(response, "acct_mgr_info");

            return new AccountManagerInfo(response);
        }

        /// <summary>
        /// Get the contents of the project_init.xml file for a project, if present.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <returns>The project init status.</returns>
        public async Task<ProjectInitStatus> GetProjectInitStatusAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_project_init_status/>");

            CheckResponse(response, "get_project_init_status");

            return new ProjectInitStatus(response);
        }

        /// <summary>
        /// Fetch the project configuration file from the specified URL.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <param name="url">The master URL of the project.</param>
        /// <param name="cancellationToken">A cancellation token which can be used to cancel the asynchronous operation before it completes.</param>
        /// <returns>The project configuration.</returns>
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
        /// Look for an account in a given project and return an authenticator key.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <param name="url">The master URL of the project.</param>
        /// <param name="emailAddress">The email address of the account.</param>
        /// <param name="password">The password of the account.</param>
        /// <param name="cancellationToken">A cancellation token which can be used to cancel the asynchronous operation before it completes.</param>
        /// <returns>Result of the operation and the account information.</returns>
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
        /// Look for an account in a given project using LDAP authentication and return an authenticator key.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <param name="url">The master URL of the project.</param>
        /// <param name="uid">The LDAP user UID of the account.</param>
        /// <param name="password">The password of the account.</param>
        /// <param name="cancellationToken">A cancellation token which can be used to cancel the asynchronous operation before it completes.</param>
        /// <returns>Result of the operation and the account information.</returns>
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

        /// <summary>
        /// Create an account for a given project.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <param name="url">The master URL of the project.</param>
        /// <param name="emailAddress">The email address to set for the account.</param>
        /// <param name="password">The password to set for the account.</param>
        /// <param name="username">The user name to set for the account.</param>
        /// <param name="consentedToTerms">Whether the user has consented to the terms and conditions of the project.</param>
        /// <param name="cancellationToken">A cancellation token which can be used to cancel the asynchronous operation before it completes.</param>
        /// <returns>Result of the operation and the account information.</returns>
        public Task<AccountInfo> CreateAccountAsync(string url, string emailAddress, string password, string username, bool consentedToTerms, CancellationToken cancellationToken)
        {
            return CreateAccountAsync(url, emailAddress, password, username, null, consentedToTerms, cancellationToken);
        }

        /// <summary>
        /// Create an account for a given project.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <param name="url">The master URL of the project.</param>
        /// <param name="emailAddress">The email address to set for the account.</param>
        /// <param name="password">The password to set for the account.</param>
        /// <param name="username">The user name to set for the account.</param>
        /// <param name="teamName">The team name to set for the account.</param>
        /// <param name="consentedToTerms">Whether the user has consented to the terms and conditions of the project.</param>
        /// <param name="cancellationToken">A cancellation token which can be used to cancel the asynchronous operation before it completes.</param>
        /// <returns>Result of the operation and the account information.</returns>
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
        /// Get information about newer BOINC client versions, if any.
        /// <para>This request does not require prior authentication.</para>
        /// </summary>
        /// <returns>Information and download URL for a newer client version, if any.</returns>
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
        /// If no such file is present or its contents are not formatted correctly, the defaults are used.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        public async Task ReadCoreClientConfigAsync()
        {
            CheckDisposed();
            CheckConnected();

            CheckResponse(await PerformRpcAsync("<read_cc_config/>"));
        }

        /// <summary>
        /// Get the contents of the global_prefs.xml file, if present.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <returns>The contents of the global_prefs.xml.</returns>
        public async Task<GlobalPreferences> GetGlobalPreferencesFileAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_global_prefs_file/>");

            CheckResponse(response, "global_preferences");

            return new GlobalPreferences(response);
        }

        /// <summary>
        /// Get the contents of the currently used global_prefs.xml.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <returns>The contents of the currently used global_prefs.xml.</returns>
        public async Task<GlobalPreferences> GetGlobalPreferencesWorkingAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_global_prefs_working/>");

            CheckResponse(response, "global_preferences");

            return new GlobalPreferences(response);
        }

        /// <summary>
        /// Get the contents of the global_prefs_override.xml file, if present.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <returns>The contents of the global_prefs_override.xml.</returns>
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
        /// Write the given contents to the global_prefs_override.xml file or delete it.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <param name="globalPreferencesOverride">The contents for the global preferences override file, or null to delete the file.</param>
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
        /// Tell the client to reread the global_prefs_override.xml file and set the preferences accordingly.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        public async Task ReadGlobalPreferencesOverrideAsync()
        {
            CheckDisposed();
            CheckConnected();

            CheckResponse(await PerformRpcAsync("<read_global_prefs_override/>"));
        }

        /// <summary>
        /// Set the language field in the client_state.xml file to append it in any subsequent GET calls to the original URL and for translating notices.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <param name="language">The ISO language code.</param>
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
        /// Get the contents of the cc_config.xml file, if present.
        /// <para>This request requires prior authentication.</para>
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
        /// Write a new cc_config.xml file.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <param name="coreClientConfig">The content to write.</param>
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
        /// <para>This request does not require prior authentication.</para>
        /// </summary>
        /// <returns>The daily transfer statistics.</returns>
        public async Task<DailyTransferStatistics[]> GetDailyTransferHistoryAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_daily_xfer_history/>");

            CheckResponse(response, "daily_xfers");

            return response.Elements("dx").Select(e => new DailyTransferStatistics(e)).ToArray();
        }

        /// <summary>
        /// Get a list of results that have been completed in the last hour and have been reported to their project.
        /// (These results are not returned by <seealso cref="GetResultsAsync"/>).
        /// <para>This request does not require prior authentication.</para>
        /// </summary>
        /// <returns>A list of completed and reported results.</returns>
        public async Task<OldResult[]> GetOldResultsAsync()
        {
            CheckDisposed();
            CheckConnected();

            XElement response = await PerformRpcAsync("<get_old_results/>");

            CheckResponse(response, "old_results");

            return response.Elements("old_result").Select(e => new OldResult(e)).ToArray();
        }

        /// <summary>
        /// Get the app config (app_config.xml) for a project.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <param name="project">The project.</param>
        /// <returns>The content of the app_config.xml.</returns>
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
        /// Set the app config for a project.
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <param name="project">The project.</param>
        /// <param name="appConfig">The content of the app_config.xml to set.</param>
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
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <param name="slot">Slot number of the task for which to start the graphics app, or -1 to start the default screensaver.</param>
        /// <param name="fullscreen">Whether to start the graphics app full screen or in windowed mode.</param>
        /// <param name="screensaverLoginUser">User name of the user that invoked the screen saver (i.e. the currently logged-in user).</param>
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
        /// <para>This request requires prior authentication.</para>
        /// </summary>
        /// <param name="graphicsPid">Process ID of the graphics app to stop.</param>
        /// <param name="screensaverLoginUser">User name of the user that invoked the screen saver (i.e. the currently logged-in user).</param>
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
        /// <para>This request requires prior authentication.</para>
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

        /// <summary>
        /// Gets whether the RPC client is currently connected to a BOINC client.
        /// </summary>
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

        /// <summary>
        /// Close and free all resources of this object.
        /// </summary>
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

    /// <summary>
    /// Exception thrown by <see cref="RpcClient"/> when a corrupted, invalid or unexpected response is received from the BOINC client.
    /// </summary>
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

    /// <summary>
    /// Exception thrown by <see cref="RpcClient"/> when the BOINC client response indicates a failed operation.
    /// </summary>
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

    /// <summary>
    /// Exception thrown by <see cref="RpcClient"/> when an operation was attempted in an unauthorized connection context that requires prior authorization.
    /// </summary>
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
