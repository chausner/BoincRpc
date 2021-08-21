using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace BoincRpc.Tests
{
	[TestClass]
    public class Tests
    {
        public TestContext TestContext { get; set; }

        private string RpcHost => Convert.ToString(TestContext.Properties["RpcHost"]);
        private int RpcPort => Convert.ToInt32(TestContext.Properties["RpcPort"]);
        private string RpcPassword => Convert.ToString(TestContext.Properties["RpcPassword"]);

        private async Task ConnectAndAuthorize(Func<RpcClient, Task> action)
        {
            using (RpcClient rpcClient = new RpcClient())
            {
                await rpcClient.ConnectAsync(RpcHost, RpcPort);
                await rpcClient.AuthorizeAsync(RpcPassword);
                await action(rpcClient);
            }
        }

        [TestMethod]
        public async Task AuthorizationFailsWithWrongPassword()
        {
            using (RpcClient rpcClient = new RpcClient())
            {
                await rpcClient.ConnectAsync(RpcHost, RpcPort);

                bool authorized = await rpcClient.AuthorizeAsync("wrongpassword");

                Assert.IsFalse(authorized);
            }
        }

        [TestMethod]
        public async Task AuthorizationSucceedsWithCorrectPassword()
        {
            using (RpcClient rpcClient = new RpcClient())
            {
                await rpcClient.ConnectAsync(RpcHost, RpcPort);

                bool authorized = await rpcClient.AuthorizeAsync(RpcPassword);

                Assert.IsTrue(authorized);
            }
        }

        [TestMethod]
        public Task AccountManagerAttach()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                AccountManagerRpcReply reply = await rpcClient.AccountManagerAttachAsync("http://www.gridrepublic.org/", "invalid_username", "invalid_password", CancellationToken.None);

                Assert.AreEqual(ErrorCode.BadEmailAddr, reply.ErrorCode);
            });
        }

        [TestMethod]
        public Task AccountManagerDetach()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                AccountManagerRpcReply reply = await rpcClient.AccountManagerDetachAsync(CancellationToken.None);

                // ErrorCode.InvalidUrl will be returned if not attached to any account manager
                Assert.AreEqual(ErrorCode.InvalidUrl, reply.ErrorCode);
            });
        }

        [TestMethod]
        public Task AccountManagerUpdate()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                // An RpcFailureException will be thrown if not attached to any account manager
                RpcFailureException ex = await Assert.ThrowsExceptionAsync<RpcFailureException>(() => rpcClient.AccountManagerUpdateAsync(CancellationToken.None));
                
                Assert.AreEqual("bad arg", ex.Message);
            });
        }

        [TestMethod]
        public Task Close()
        {
            return ConnectAndAuthorize(rpcClient =>
            {
                Assert.IsTrue(rpcClient.Connected);

                rpcClient.Close();

                Assert.IsFalse(rpcClient.Connected);

                return Task.CompletedTask;
            });
        }

        [TestMethod]
        public Task CreateAccount()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                AccountInfo accountInfo = await rpcClient.CreateAccountAsync("http://non.existing.domain.xyz", "non@existing.mail", "password", "username", "team", false, CancellationToken.None);

                Assert.AreEqual(ErrorCode.GetHostByName, accountInfo.ErrorCode);
            });
        }

        [TestMethod]
        public Task ExchangeVersions()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                VersionInfo versionInfo = await rpcClient.ExchangeVersionsAsync("BoincRpc", new VersionInfo(7, 6, 33));

                Assert.IsTrue(versionInfo.Major >= 7);
            });
        }

        [TestMethod]
        public Task PerformProjectOperation()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                foreach (Project project in await rpcClient.GetProjectStatusAsync())
                    await rpcClient.PerformProjectOperationAsync(project, ProjectOperation.Update);
            });
        }

        [TestMethod]
        public Task PerformFileTransferOperation()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                foreach (FileTransfer fileTransfer in await rpcClient.GetFileTransfersAsync())
                    await rpcClient.PerformFileTransferOperationAsync(fileTransfer, FileTransferOperation.Retry);
            });
        }

        [TestMethod]
        public Task PerformResultOperation()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                foreach (Result result in await rpcClient.GetResultsAsync())
                    await rpcClient.PerformResultOperationAsync(result, ResultOperation.Suspend);
            });
        }

        [TestMethod]
        public Task GetAccountManagerInfo()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                AccountManagerInfo accountManagerInfo = await rpcClient.GetAccountManagerInfoAsync();
            });
        }

        [TestMethod]
        public Task GetAllAccountManagersList()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                AccountManagerListEntry[] accountManagers = await rpcClient.GetAllAccountManagersListAsync();

                Assert.AreNotEqual(accountManagers.Length, 0);
                Assert.AreNotEqual(string.Empty, accountManagers[0].Name);
            });
        }

        [TestMethod]
        public Task GetAllProjectsList()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                ProjectListEntry[] projects = await rpcClient.GetAllProjectsListAsync();

                Assert.AreNotEqual(projects.Length, 0);
                Assert.AreNotEqual(string.Empty, projects[0].Name);
            });
        }

        [TestMethod]
        public Task GetCoreClientConfig()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                XElement coreClientConfig = await rpcClient.GetCoreClientConfigAsync();
            });
        }

        [TestMethod]
        public Task GetCoreClientStatus()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                CoreClientStatus coreClientStatus = await rpcClient.GetCoreClientStatusAsync();
            });
        }

        [TestMethod]
        public Task GetDailyTransferHistory()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                DailyTransferStatistics[] dailyTransferHistory = await rpcClient.GetDailyTransferHistoryAsync();

                Assert.AreNotEqual(dailyTransferHistory.Length, 0);
                Assert.IsTrue(dailyTransferHistory[0].Day > new DateTime(2000, 1, 1));
            });
        }

        [TestMethod]
        public Task GetDiskUsage()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                DiskUsage diskUsage = await rpcClient.GetDiskUsageAsync();

                Assert.IsTrue(diskUsage.Total > 0);
            });
        }

        [TestMethod]
        public Task GetFileTransfers()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                FileTransfer[] fileTransfers = await rpcClient.GetFileTransfersAsync();
            });
        }

        [TestMethod]
        public Task GetGlobalPreferencesFile()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                GlobalPreferences globalPreferences = await rpcClient.GetGlobalPreferencesFileAsync();

                Assert.IsTrue(globalPreferences.ModifiedTime > new DateTime(2000, 1, 1));
            });
        }

        [TestMethod]
        public Task GetGlobalPreferencesWorking()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                GlobalPreferences globalPreferences = await rpcClient.GetGlobalPreferencesWorkingAsync();

                Assert.IsTrue(globalPreferences.ModifiedTime > new DateTime(2000, 1, 1));
            });
        }

        [TestMethod]
        public Task GetGlobalPreferencesOverride()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                XElement globalPreferences = await rpcClient.GetGlobalPreferencesOverrideAsync();
            });
        }

        [TestMethod]
        public Task GetHostInfo()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                HostInfo hostInfo = await rpcClient.GetHostInfoAsync();

                Assert.AreNotEqual(string.Empty, hostInfo.DomainName);
            });
        }

        [TestMethod]
        public Task ResetHostInfo()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                await rpcClient.ResetHostInfoAsync();
            });
        }

        [TestMethod]
        public Task GetMessageCount()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                int messageCount = await rpcClient.GetMessageCountAsync();

                Assert.IsTrue(messageCount > 0);
            });
        }

        [TestMethod]
        public Task GetMessages()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                Message[] messages = await rpcClient.GetMessagesAsync();

                Assert.AreNotEqual(messages.Length, 0);
            });
        }

        [TestMethod]
        public Task GetNewerVersion()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                string newerVersion = await rpcClient.GetNewerVersionAsync();
            });
        }

        [TestMethod]
        public Task GetNotices()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                Notice[] notices = await rpcClient.GetNoticesAsync();
            });
        }

        [TestMethod]
        public Task GetNoticesPublicOnly()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                Notice[] notices = await rpcClient.GetNoticesAsync(0, true);
            });
        }

        [TestMethod]
        public Task GetOldResults()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                OldResult[] oldResults = await rpcClient.GetOldResultsAsync();
            });
        }

        [TestMethod]
        public Task GetProjectConfig()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                ProjectConfig projectConfig = await rpcClient.GetProjectConfigAsync("http://boinc.bakerlab.org/rosetta/", CancellationToken.None);

                Assert.AreNotEqual(string.Empty, projectConfig.Name);
            });
        }

        [TestMethod]
        public Task GetProjectInitStatus()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                ProjectInitStatus projectInitStatus = await rpcClient.GetProjectInitStatusAsync();
            });
        }

        [TestMethod]
        public Task GetProjectStatus()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                Project[] projects = await rpcClient.GetProjectStatusAsync();

                Assert.AreNotEqual(projects.Length, 0);

                foreach (Project project in projects)
                    Assert.AreNotEqual(string.Empty, project.ProjectName);
            });
        }

        [TestMethod]
        public Task GetProxySettings()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                ProxyInfo proxyInfo = await rpcClient.GetProxySettingsAsync();
            });
        }

        [TestMethod]
        public Task GetResults()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                Result[] results = await rpcClient.GetResultsAsync();

                Assert.AreNotEqual(results.Length, 0);

                foreach (Result result in results)
                    Assert.AreNotEqual(string.Empty, result.Name);
            });
        }

        [TestMethod]
        public Task GetScreensaverTasks()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                Tuple<Result[], SuspendReason> screensaverTasks = await rpcClient.GetScreensaverTasksAsync();
            });
        }

        [TestMethod]
        public Task GetState()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                CoreClientState coreClientState = await rpcClient.GetStateAsync();

                Assert.AreNotEqual(coreClientState.Apps.Count, 0);
                Assert.AreNotEqual(coreClientState.AppVersions.Count, 0);
                Assert.AreNotEqual(coreClientState.Platforms.Count, 0);
                Assert.AreNotEqual(coreClientState.Projects.Count, 0);
                Assert.AreNotEqual(coreClientState.Results.Count, 0);
                Assert.AreNotEqual(coreClientState.Workunits.Count, 0);
            });
        }

        [TestMethod]
        public Task GetStatistics()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                ProjectStatistics[] statistics = await rpcClient.GetStatisticsAsync();

                Assert.AreNotEqual(statistics.Length, 0);
                Assert.AreNotEqual(string.Empty, statistics[0].MasterUrl);
            });
        }

        [TestMethod]
        public Task LookupAccount()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                AccountInfo accountInfo = await rpcClient.LookupAccountAsync("http://boinc.bakerlab.org/rosetta/", "non@existing.mail", "password", CancellationToken.None);

                Assert.AreEqual(ErrorCode.DBNotFound, accountInfo.ErrorCode);
            });
        }

        [TestMethod]
        public Task LookupAccountLdap()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                AccountInfo accountInfo = await rpcClient.LookupAccountLdapAsync("http://boinc.bakerlab.org/rosetta/", "@nonexistinguid", "password", CancellationToken.None);

                Assert.AreEqual(ErrorCode.DBNotFound, accountInfo.ErrorCode);
            });
        }

        [TestMethod]
        public Task NetworkAvailable()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                await rpcClient.NetworkAvailableAsync();
            });
        }

        [TestMethod]
        public Task ProjectAttach()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                ProjectAttachReply reply = await rpcClient.ProjectAttachAsync("http://boinc.bakerlab.org/rosetta/", "invalid_authenticator", "Rosetta@home", CancellationToken.None);

                Assert.AreEqual(ErrorCode.Success, reply.ErrorCode);
            });
        }

        [TestMethod]
        public Task ReadCoreClientConfig()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                await rpcClient.ReadCoreClientConfigAsync();
            });
        }

        [TestMethod]
        public Task ReadGlobalPreferencesOverride()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                await rpcClient.ReadGlobalPreferencesOverrideAsync();
            });
        }

        [TestMethod]
        public Task RunBenchmarksAsync()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                await rpcClient.RunBenchmarksAsync();
            });
        }

        [TestMethod]
        public Task SetCoreClientConfig()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                XElement coreClientConfig = await rpcClient.GetCoreClientConfigAsync();

                await rpcClient.SetCoreClientConfigAsync(coreClientConfig);
            });
        }

        [TestMethod]
        public Task SetGlobalPreferencesOverride()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                await rpcClient.SetGlobalPreferencesOverrideAsync(new XElement("global_preferences"));
            });
        }

        [TestMethod]
        public Task SetGpuMode()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                await rpcClient.SetGpuModeAsync(Mode.Always, TimeSpan.FromSeconds(5));
            });
        }

        [TestMethod]
        public Task SetRunModeAsync()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                await rpcClient.SetRunModeAsync(Mode.Always, TimeSpan.FromSeconds(5));
            });
        }

        [TestMethod]
        public Task SetNetworkModeAsync()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                await rpcClient.SetNetworkModeAsync(Mode.Always, TimeSpan.FromSeconds(5));
            });
        }

        [TestMethod]
        public Task SetLanguage()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                await rpcClient.SetLanguageAsync("de-DE");
            });
        }

        [TestMethod]
        public Task SetProxySettings()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                ProxyInfo proxyInfo = await rpcClient.GetProxySettingsAsync();
                await rpcClient.SetProxySettingsAsync(proxyInfo);
            });
        }

        [TestMethod]
        public Task GetAppConfig()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                try 
                {
                    XElement appConfig = await rpcClient.GetAppConfigAsync("http://boinc.bakerlab.org/rosetta/");
                    Assert.AreEqual("app_config", appConfig.Name);    
                }
                catch (RpcFailureException ex)
                {
                    // An RpcFailureException will be thrown if no app_config.xml exists for the project
                    Assert.AreEqual("app_config.xml not found", ex.Message);
                }  
            });
        }

        [TestMethod]
        public Task SetAppConfig()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                XElement appConfig = new XElement("app_config",
                    new XElement("dummy", 42));
                await rpcClient.SetAppConfigAsync("http://boinc.bakerlab.org/rosetta/", appConfig);
            });
        }

        [TestMethod]
        public Task RunGraphicsApp()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                try
                {
                    await rpcClient.RunGraphicsAppAsync(-1, true, "");
                }
                catch (RpcFailureException ex)
                {
                    Assert.AreEqual("run_graphics_app RPC is currently available only on Mac OS", ex.Message);
                }  
            });
        }

        [TestMethod]
        public Task StopGraphicsApp()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                try
                {
                    await rpcClient.StopGraphicsAppAsync(1234567, "");
                }
                catch (RpcFailureException ex)
                {
                    Assert.AreEqual("run_graphics_app RPC is currently available only on Mac OS", ex.Message);
                }  
            });
        }

        [TestMethod]
        public Task TestGraphicsApp()
        {
            return ConnectAndAuthorize(async rpcClient =>
            {
                try
                {
                    await rpcClient.TestGraphicsAppAsync(1234567);
                }
                catch (RpcFailureException ex)
                {
                    Assert.AreEqual("run_graphics_app RPC is currently available only on Mac OS", ex.Message);
                }  
            });
        }
    }
}
