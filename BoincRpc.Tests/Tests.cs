using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace BoincRpc.Tests
{
    public class TestsBase : IDisposable
    {
        public TestContext TestContext { get; set; }
        protected RpcClient RpcClient { get; private set; } = new RpcClient();

        protected string RpcHost => Convert.ToString(TestContext.Properties["RpcHost"]);
        protected int RpcPort => Convert.ToInt32(TestContext.Properties["RpcPort"]);
        protected string RpcPassword => Convert.ToString(TestContext.Properties["RpcPassword"]);

        bool disposed;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                    RpcClient?.Dispose();

                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    [TestClass]
    public class AuthorizationTests : TestsBase
    {
        [TestMethod]
        public async Task AuthorizationFailsWithWrongPassword()
        {
            await RpcClient.ConnectAsync(RpcHost, RpcPort);

            bool authorized = await RpcClient.AuthorizeAsync("wrongpassword");

            Assert.IsFalse(authorized);
        }

        [TestMethod]
        public async Task AuthorizationSucceedsWithCorrectPassword()
        {
            await RpcClient.ConnectAsync(RpcHost, RpcPort);

            bool authorized = await RpcClient.AuthorizeAsync(RpcPassword);

            Assert.IsTrue(authorized);
        }
    }

    [TestClass]
    public class RpcTests : TestsBase
    {
        [TestInitialize]
        public async Task ConnectAndAuthorize()
        {
            await RpcClient.ConnectAsync(RpcHost, RpcPort);
            await RpcClient.AuthorizeAsync(RpcPassword);
        }

        [TestMethod]
        public async Task AccountManagerAttach()
        {
            AccountManagerRpcReply reply = await RpcClient.AccountManagerAttachAsync("http://www.gridrepublic.org/", "invalid_username", "invalid_password", CancellationToken.None);

            Assert.AreEqual(ErrorCode.BadEmailAddr, reply.ErrorCode);
        }

        [TestMethod]
        public async Task AccountManagerDetach()
        {
            AccountManagerRpcReply reply = await RpcClient.AccountManagerDetachAsync(CancellationToken.None);

            // ErrorCode.InvalidUrl will be returned if not attached to any account manager
            Assert.AreEqual(ErrorCode.InvalidUrl, reply.ErrorCode);
        }

        [TestMethod]
        public async Task AccountManagerUpdate()
        {
            // An RpcFailureException will be thrown if not attached to any account manager
            RpcFailureException ex = await Assert.ThrowsExceptionAsync<RpcFailureException>(() => RpcClient.AccountManagerUpdateAsync(CancellationToken.None));

            Assert.AreEqual("bad arg", ex.Message);
        }

        [TestMethod]
        public void Close()
        {
            Assert.IsTrue(RpcClient.Connected);

            RpcClient.Close();

            Assert.IsFalse(RpcClient.Connected);
        }

        [TestMethod]
        public async Task CreateAccount()
        {
            AccountInfo accountInfo = await RpcClient.CreateAccountAsync("http://non.existing.domain.xyz", "non@existing.mail", "password", "username", "team", false, CancellationToken.None);

            Assert.AreEqual(ErrorCode.GetHostByName, accountInfo.ErrorCode);
        }

        [TestMethod]
        public async Task ExchangeVersions()
        {
            VersionInfo versionInfo = await RpcClient.ExchangeVersionsAsync("BoincRpc", new VersionInfo(7, 6, 33));

            Assert.IsTrue(versionInfo.Major >= 7);
        }

        [TestMethod]
        public async Task PerformProjectOperation()
        {
            foreach (Project project in await RpcClient.GetProjectStatusAsync())
                await RpcClient.PerformProjectOperationAsync(project, ProjectOperation.Update);
        }

        [TestMethod]
        public async Task PerformFileTransferOperation()
        {
            foreach (FileTransfer fileTransfer in await RpcClient.GetFileTransfersAsync())
                await RpcClient.PerformFileTransferOperationAsync(fileTransfer, FileTransferOperation.Retry);
        }

        [TestMethod]
        public async Task PerformResultOperation()
        {
            foreach (Result result in await RpcClient.GetResultsAsync())
                await RpcClient.PerformResultOperationAsync(result, ResultOperation.Suspend);
        }

        [TestMethod]
        public async Task GetAccountManagerInfo()
        {
            AccountManagerInfo accountManagerInfo = await RpcClient.GetAccountManagerInfoAsync();
        }

        [TestMethod]
        public async Task GetAllAccountManagersList()
        {
            AccountManagerListEntry[] accountManagers = await RpcClient.GetAllAccountManagersListAsync();

            Assert.AreNotEqual(accountManagers.Length, 0);
            Assert.AreNotEqual(string.Empty, accountManagers[0].Name);
        }

        [TestMethod]
        public async Task GetAllProjectsList()
        {
            ProjectListEntry[] projects = await RpcClient.GetAllProjectsListAsync();

            Assert.AreNotEqual(projects.Length, 0);
            Assert.AreNotEqual(string.Empty, projects[0].Name);
        }

        [TestMethod]
        public async Task GetCoreClientConfig()
        {
            XElement coreClientConfig = await RpcClient.GetCoreClientConfigAsync();
        }

        [TestMethod]
        public async Task GetCoreClientStatus()
        {
            CoreClientStatus coreClientStatus = await RpcClient.GetCoreClientStatusAsync();
        }

        [TestMethod]
        public async Task GetDailyTransferHistory()
        {
            DailyTransferStatistics[] dailyTransferHistory = await RpcClient.GetDailyTransferHistoryAsync();

            Assert.AreNotEqual(dailyTransferHistory.Length, 0);
            Assert.IsTrue(dailyTransferHistory[0].Day > new DateTime(2000, 1, 1));
        }

        [TestMethod]
        public async Task GetDiskUsage()
        {
            DiskUsage diskUsage = await RpcClient.GetDiskUsageAsync();

            Assert.IsTrue(diskUsage.Total > 0);
        }

        [TestMethod]
        public async Task GetFileTransfers()
        {
            FileTransfer[] fileTransfers = await RpcClient.GetFileTransfersAsync();
        }

        [TestMethod]
        public async Task GetGlobalPreferencesFile()
        {
            GlobalPreferences globalPreferences = await RpcClient.GetGlobalPreferencesFileAsync();

            Assert.IsTrue(globalPreferences.ModifiedTime > new DateTime(2000, 1, 1));
        }

        [TestMethod]
        public async Task GetGlobalPreferencesWorking()
        {
            GlobalPreferences globalPreferences = await RpcClient.GetGlobalPreferencesWorkingAsync();

            Assert.IsTrue(globalPreferences.ModifiedTime > new DateTime(2000, 1, 1));
        }

        [TestMethod]
        public async Task GetGlobalPreferencesOverride()
        {
            XElement globalPreferences = await RpcClient.GetGlobalPreferencesOverrideAsync();
        }

        [TestMethod]
        public async Task GetHostInfo()
        {
            HostInfo hostInfo = await RpcClient.GetHostInfoAsync();

            Assert.AreNotEqual(string.Empty, hostInfo.DomainName);
        }

        [TestMethod]
        public async Task ResetHostInfo()
        {
            await RpcClient.ResetHostInfoAsync();
        }

        [TestMethod]
        public async Task GetMessageCount()
        {
            int messageCount = await RpcClient.GetMessageCountAsync();

            Assert.IsTrue(messageCount > 0);
        }

        [TestMethod]
        public async Task GetMessages()
        {
            Message[] messages = await RpcClient.GetMessagesAsync();

            Assert.AreNotEqual(messages.Length, 0);
        }

        [TestMethod]
        public async Task GetNewerVersion()
        {
            NewerVersionInfo newerVersionInfo = await RpcClient.GetNewerVersionAsync();
        }

        [TestMethod]
        public async Task GetNotices()
        {
            Notice[] notices = await RpcClient.GetNoticesAsync();
        }

        [TestMethod]
        public async Task GetNoticesPublicOnly()
        {
            Notice[] notices = await RpcClient.GetNoticesAsync(0, true);
        }

        [TestMethod]
        public async Task GetOldResults()
        {
            OldResult[] oldResults = await RpcClient.GetOldResultsAsync();
        }

        [TestMethod]
        public async Task GetProjectConfig()
        {
            ProjectConfig projectConfig = await RpcClient.GetProjectConfigAsync("http://boinc.bakerlab.org/rosetta/", CancellationToken.None);

            Assert.AreNotEqual(string.Empty, projectConfig.Name);
        }

        [TestMethod]
        public async Task GetProjectInitStatus()
        {
            ProjectInitStatus projectInitStatus = await RpcClient.GetProjectInitStatusAsync();
        }

        [TestMethod]
        public async Task GetProjectStatus()
        {
            Project[] projects = await RpcClient.GetProjectStatusAsync();

            Assert.AreNotEqual(projects.Length, 0);

            foreach (Project project in projects)
                Assert.AreNotEqual(string.Empty, project.ProjectName);
        }

        [TestMethod]
        public async Task GetProxySettings()
        {
            ProxyInfo proxyInfo = await RpcClient.GetProxySettingsAsync();
        }

        [TestMethod]
        public async Task GetResults()
        {
            Result[] results = await RpcClient.GetResultsAsync();

            Assert.AreNotEqual(results.Length, 0);

            foreach (Result result in results)
                Assert.AreNotEqual(string.Empty, result.Name);
        }

        [TestMethod]
        public async Task GetScreensaverTasks()
        {
            Tuple<Result[], SuspendReason> screensaverTasks = await RpcClient.GetScreensaverTasksAsync();
        }

        [TestMethod]
        public async Task GetState()
        {
            CoreClientState coreClientState = await RpcClient.GetStateAsync();

            Assert.AreNotEqual(coreClientState.Apps.Count, 0);
            Assert.AreNotEqual(coreClientState.AppVersions.Count, 0);
            Assert.AreNotEqual(coreClientState.Platforms.Count, 0);
            Assert.AreNotEqual(coreClientState.Projects.Count, 0);
            Assert.AreNotEqual(coreClientState.Results.Count, 0);
            Assert.AreNotEqual(coreClientState.Workunits.Count, 0);
        }

        [TestMethod]
        public async Task GetStatistics()
        {
            ProjectStatistics[] statistics = await RpcClient.GetStatisticsAsync();

            Assert.AreNotEqual(statistics.Length, 0);
            Assert.AreNotEqual(string.Empty, statistics[0].MasterUrl);
        }

        [TestMethod]
        public async Task LookupAccount()
        {
            AccountInfo accountInfo = await RpcClient.LookupAccountAsync("http://boinc.bakerlab.org/rosetta/", "non@existing.mail", "password", CancellationToken.None);

            Assert.AreEqual(ErrorCode.DBNotFound, accountInfo.ErrorCode);
        }

        [TestMethod]
        public async Task LookupAccountLdap()
        {
            AccountInfo accountInfo = await RpcClient.LookupAccountLdapAsync("http://boinc.bakerlab.org/rosetta/", "@nonexistinguid", "password", CancellationToken.None);

            Assert.AreEqual(ErrorCode.DBNotFound, accountInfo.ErrorCode);
        }

        [TestMethod]
        public async Task NetworkAvailable()
        {
            await RpcClient.NetworkAvailableAsync();
        }

        [TestMethod]
        public async Task ProjectAttach()
        {
            ProjectAttachReply reply = await RpcClient.ProjectAttachAsync("http://boinc.bakerlab.org/rosetta/", "invalid_authenticator", "Rosetta@home", CancellationToken.None);

            Assert.AreEqual(ErrorCode.Success, reply.ErrorCode);
        }

        [TestMethod]
        public async Task ReadCoreClientConfig()
        {
            await RpcClient.ReadCoreClientConfigAsync();
        }

        [TestMethod]
        public async Task ReadGlobalPreferencesOverride()
        {
            await RpcClient.ReadGlobalPreferencesOverrideAsync();
        }

        [TestMethod]
        public async Task RunBenchmarksAsync()
        {
            await RpcClient.RunBenchmarksAsync();
        }

        [TestMethod]
        public async Task SetCoreClientConfig()
        {
            XElement coreClientConfig = await RpcClient.GetCoreClientConfigAsync();

            await RpcClient.SetCoreClientConfigAsync(coreClientConfig);
        }

        [TestMethod]
        public async Task SetGlobalPreferencesOverride()
        {
            await RpcClient.SetGlobalPreferencesOverrideAsync(new XElement("global_preferences"));
        }

        [TestMethod]
        public async Task SetGpuMode()
        {
            await RpcClient.SetGpuModeAsync(Mode.Always, TimeSpan.FromSeconds(5));
        }

        [TestMethod]
        public async Task SetRunModeAsync()
        {
            await RpcClient.SetRunModeAsync(Mode.Always, TimeSpan.FromSeconds(5));
        }

        [TestMethod]
        public async Task SetNetworkModeAsync()
        {
            await RpcClient.SetNetworkModeAsync(Mode.Always, TimeSpan.FromSeconds(5));
        }

        [TestMethod]
        public async Task SetLanguage()
        {
            await RpcClient.SetLanguageAsync("de-DE");
        }

        [TestMethod]
        public async Task SetProxySettings()
        {
            ProxyInfo proxyInfo = await RpcClient.GetProxySettingsAsync();
            await RpcClient.SetProxySettingsAsync(proxyInfo);
        }

        [TestMethod]
        public async Task GetAppConfig()
        {
            try
            {
                Project[] projects = await RpcClient.GetProjectStatusAsync();
                Project project = projects.First(p => p.MasterUrl == "http://boinc.bakerlab.org/rosetta/");

                XElement appConfig = await RpcClient.GetAppConfigAsync(project);
                Assert.AreEqual("app_config", appConfig.Name);
            }
            catch (RpcFailureException ex)
            {
                // An RpcFailureException will be thrown if no app_config.xml exists for the project
                Assert.AreEqual("app_config.xml not found", ex.Message);
            }
        }

        [TestMethod]
        public async Task SetAppConfig()
        {
            Project[] projects = await RpcClient.GetProjectStatusAsync();
            Project project = projects.First(p => p.MasterUrl == "http://boinc.bakerlab.org/rosetta/");

            XElement appConfig = new XElement("app_config",
                new XElement("dummy", 42));
            await RpcClient.SetAppConfigAsync(project, appConfig);
        }

        [TestMethod]
        public async Task RunGraphicsApp()
        {
            try
            {
                await RpcClient.RunGraphicsAppAsync(-1, true, "");
            }
            catch (RpcFailureException ex)
            {
                Assert.AreEqual("run_graphics_app RPC is currently available only on Mac OS", ex.Message);
            }
        }

        [TestMethod]
        public async Task StopGraphicsApp()
        {
            try
            {
                await RpcClient.StopGraphicsAppAsync(1234567, "");
            }
            catch (RpcFailureException ex)
            {
                Assert.AreEqual("run_graphics_app RPC is currently available only on Mac OS", ex.Message);
            }
        }

        [TestMethod]
        public async Task TestGraphicsApp()
        {
            try
            {
                await RpcClient.TestGraphicsAppAsync(1234567);
            }
            catch (RpcFailureException ex)
            {
                Assert.AreEqual("run_graphics_app RPC is currently available only on Mac OS", ex.Message);
            }
        }
    }
}
