using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace BoincRpc
{
    // RPC XML structures in https://github.com/BOINC/boinc/blob/master/client/gui_rpc_server_ops.cpp

    public class AccountManagerInfo
    {
        public string Name { get; }
        public string Url { get; }
        public bool HaveCredentials { get; }
        public bool CookieRequired { get; }
        public string CookieFailureUrl { get; }

        internal AccountManagerInfo(XElement element)
        {
            Name = element.ElementString("acct_mgr_name");
            Url = element.ElementString("acct_mgr_url");
            HaveCredentials = element.ElementBoolean("have_credentials");
            CookieRequired = element.ElementBoolean("cookie_required");
            CookieFailureUrl = element.ElementString("cookie_failure_url");
        }

        public override string ToString()
        {
            return $"{Name} ({Url})";
        }
    }

    public class AccountManagerRpcReply
    {
        public ErrorCode ErrorCode { get; }
        public IReadOnlyList<string> Messages { get; }

        internal AccountManagerRpcReply(XElement element)
        {            
            ErrorCode = (ErrorCode)element.ElementInt("error_num");
            Messages = element.Elements("message").Select(e => (string)e).ToArray();
        }

        public override string ToString()
        {
            return $"ErrorCode: {ErrorCode}";
        }
    }

    public class ProjectAttachReply
    {
        public ErrorCode ErrorCode { get; }
        public IReadOnlyList<string> Messages { get; }

        internal ProjectAttachReply(XElement element)
        {
            ErrorCode = (ErrorCode)element.ElementInt("error_num");
            Messages = element.Elements("message").Select(e => (string)e).ToArray();
        }

        public override string ToString()
        {
            return $"ErrorCode: {ErrorCode}";
        }
    }

    public class ProjectInitStatus
    {
        public string Name { get; }
        // public string TeamName { get; } // appears unused in client
        public string Url { get; }
        public bool HasAccountKey { get; }
        public bool Embedded { get; }

        internal ProjectInitStatus(XElement element)
        {
            Name = element.ElementString("name");
            // TeamName = element.ElementString("team_name");
            Url = element.ElementString("url");
            HasAccountKey = element.ElementBoolean("has_account_key");
            Embedded = element.ElementBoolean("embedded");
        }

        public override string ToString()
        {
            return $"{Name} ({Url})";
        }
    }

    public class ProjectConfig
    {
        public ErrorCode ErrorCode { get; }
        public string Name { get; }
        public string MasterUrl { get; }
        public string LocalRevision { get; }
        public int MinimumPasswordLength { get; }
        public bool AccountManager { get; }
        public bool UsesUsername { get; }
        public bool AccountCreationDisabled { get; }
        public bool ClientAccountCreationDisabled { get; }
        public string ErrorMessage { get; }
        public string TermsOfUse { get; }
        public int MinimumClientVersion { get; }
        public bool WebStopped { get; }
        public bool SchedulerStopped { get; }
        public IReadOnlyList<string> Platforms { get; }

        internal ProjectConfig(XElement element)
        {
            ErrorCode = (ErrorCode)element.ElementInt("error_num");
            Name = element.ElementString("name");
            MasterUrl = element.ElementString("master_url");
            LocalRevision = element.ElementString("local_revision");
            MinimumPasswordLength = element.ElementInt("min_passwd_length");
            AccountManager = element.ElementBoolean("account_manager");
            UsesUsername = element.ElementBoolean("uses_username");
            AccountCreationDisabled = element.ElementBoolean("account_creation_disabled");
            ClientAccountCreationDisabled = element.ElementBoolean("client_account_creation_disabled");
            ErrorMessage = element.ElementString("error_msg");
            TermsOfUse = element.ElementString("terms_of_use");
            MinimumClientVersion = element.ElementInt("min_client_version");
            WebStopped = element.ElementBoolean("web_stopped");
            SchedulerStopped = element.ElementBoolean("sched_stopped");
            Platforms = element.Descendants("platform_name").Select(e => (string)e).ToArray();
        }

        public override string ToString()
        {
            if (ErrorCode == ErrorCode.Success)
                return $"{Name} ({MasterUrl})";
            else
                return $"ErrorCode: {ErrorCode}";
        }
    }

    public class AccountInfo
    {
        public ErrorCode ErrorCode { get; }
        public string ErrorMessage { get; }
        public string Authenticator { get; }

        internal AccountInfo(XElement element)
        {
            ErrorCode = (ErrorCode)element.ElementInt("error_num");
            ErrorMessage = element.ElementString("error_msg");
            Authenticator = element.ElementString("authenticator");
        }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(ErrorMessage))
                return $"ErrorCode: {ErrorCode}";
            else
                return $"ErrorCode: {ErrorCode}, ErrorMessage: {ErrorMessage}";
        }
    }

    public class VersionInfo
    {
        public int Major { get; }
        public int Minor { get; }
        public int Release { get; }

        public VersionInfo(int major, int minor, int release)
        {
            if (major < 0)
                throw new ArgumentOutOfRangeException(nameof(major));
            if (minor < 0)
                throw new ArgumentOutOfRangeException(nameof(minor));
            if (release < 0)
                throw new ArgumentOutOfRangeException(nameof(release));

            Major = major;
            Minor = minor;
            Release = release;
        }

        internal VersionInfo(XElement element)
        {
            Major = element.ElementInt("major");
            Minor = element.ElementInt("minor");
            Release = element.ElementInt("release");
        }

        public override bool Equals(object obj)
        {
            VersionInfo other = obj as VersionInfo;

            if (other == null)
                return false;

            return other.Major == Major && other.Minor == Minor && other.Release == Release;
        }

        public override int GetHashCode()
        {
            return Major ^ Minor ^ Release;
        }

        public override string ToString()
        {
            return $"{Major}.{Minor}.{Release}";
        }
    }

    public class DiskUsage
    {
        public double Total { get; }
        public double Free { get; }
        public double Boinc { get; }
        public double Allowed { get; }
        public IReadOnlyList<Project> Projects { get; }

        internal DiskUsage(XElement element)
        {
            Total = element.ElementDouble("d_total");
            Free = element.ElementDouble("d_free");
            Boinc = element.ElementDouble("d_boinc");
            Allowed = element.ElementDouble("d_allowed");
            Projects = element.Elements("project").Select(e => new Project(e)).ToArray();
        }

        public override string ToString()
        {
            return $"Total: {Total}, Free: {Free}, BOINC: {Boinc}";
        }
    }

    public class GuiUrl
    {
        public string Name { get; }
        public string Description { get; }
        public string Url { get; }

        internal GuiUrl(XElement element)
        {
            Name = element.ElementString("name");
            Description = element.ElementString("description");
            Url = element.ElementString("url");
        }

        public override string ToString()
        {
            return $"{Name} ({Url}): {Description}";
        }
    }

    public class DailyStatistics
    {
        public DateTime Day { get; }
        public double UserTotalCredit { get; }
        public double UserAverageCredit { get; }
        public double HostTotalCredit { get; }
        public double HostAverageCredit { get; }

        internal DailyStatistics(XElement element)
        {
            Day = element.ElementDateTimeOffset("day").DateTime;
            UserTotalCredit = element.ElementDouble("user_total_credit");
            UserAverageCredit = element.ElementDouble("user_expavg_credit");
            HostTotalCredit = element.ElementDouble("host_total_credit");
            HostAverageCredit = element.ElementDouble("host_expavg_credit");
        }

        public override string ToString()
        {
            return $"Day: {Day}, User total credit: {UserTotalCredit}, User average credit: {UserAverageCredit}";
        }
    }

    public class ProjectStatistics
    {
        public string MasterUrl { get; }
        public IReadOnlyList<DailyStatistics> Statistics { get; }

        internal ProjectStatistics(XElement element)
        {
            MasterUrl = element.ElementString("master_url");
            Statistics = element.Elements("daily_statistics").Select(e => new DailyStatistics(e)).ToArray();
        }

        public override string ToString()
        {
            return $"{MasterUrl}: {Statistics.Count} statistic(s)";
        }
    }

    public class ProjectListEntry
    {
        public string Name { get; }
        public string Url { get; }
        public string WebUrl { get; }
        public string GeneralArea { get; }
        public string SpecificArea { get; }
        public string Description { get; }
        public string Home { get; }
        public string Image { get; }
        public IReadOnlyList<string> Platforms { get; }

        internal ProjectListEntry(XElement element)
        {
            Name = element.ElementString("name");
            Url = element.ElementString("url");
            WebUrl = element.ElementString("web_url");
            GeneralArea = element.ElementString("general_area");
            SpecificArea = element.ElementString("specific_area");
            Description = element.ElementString("description");
            Home = element.ElementString("home");
            Image = element.ElementString("image");
            Platforms = element.Elements("platforms").SelectMany(e => e.Elements("name")).Select(e => (string)e).ToArray();
        }

        public override string ToString()
        {
            return $"{Name} ({Url}): {Description}";
        }
    }

    public class AccountManagerListEntry
    {
        public string Name { get; }
        public string Url { get; }
        public string Description { get; }
        public string Image { get; }

        internal AccountManagerListEntry(XElement element)
        {
            Name = element.ElementString("name");
            Url = element.ElementString("url");
            Description = element.ElementString("description");
            Image = element.ElementString("image");
        }

        public override string ToString()
        {
            return $"{Name} ({Url}): {Description}";
        }
    }

    public class Project
    {
        public string MasterUrl { get; private set; }
        public string ProjectName { get; private set; }
        public string UserName { get; private set; }
        public string TeamName { get; private set; }
        public int HostID { get; private set; }
        public IReadOnlyList<GuiUrl> GuiUrls { get; private set; }
        public double ResourceShare { get; private set; }
        public double UserTotalCredit { get; private set; }
        public double UserAverageCredit { get; private set; }
        public double HostTotalCredit { get; private set; }
        public double HostAverageCredit { get; private set; }
        public double DiskUsage { get; private set; }
        public int RpcFailures { get; private set; }
        public int MasterFetchFailures { get; private set; }
        public DateTimeOffset MinRpcTime { get; private set; }
        public TimeSpan DownloadBackoff { get; private set; }
        public TimeSpan UploadBackoff { get; private set; }
        public double ShortTermDebt { get; private set; }
        public double LongTermDebt { get; private set; }
        public DateTimeOffset CpuBackoffTime { get; private set; }
        public TimeSpan CpuBackoffInterval { get; private set; }
        public double CudaDebt { get; private set; }
        public double CudaShortTermDebt { get; private set; }
        public DateTimeOffset CudaBackoffTime { get; private set; }
        public TimeSpan CudaBackoffInterval { get; private set; }
        public double AtiDebt { get; private set; }
        public double AtiShortTermDebt { get; private set; }
        public DateTimeOffset AtiBackoffTime { get; private set; }
        public TimeSpan AtiBackoffInterval { get; private set; }
        public double DurationCorrectionFactor { get; private set; }
        public bool MasterUrlFetchPending { get; private set; }
        public RpcReason SchedulerRpcPending { get; private set; }
        public bool SchedulerRpcInProgress { get; private set; }
        public bool NonCpuIntensive { get; private set; }
        public bool Suspended { get; private set; }
        public bool DontRequestMoreWork { get; private set; }
        public bool AttachedViaAccountManager { get; private set; }
        public bool DetachWhenDone { get; private set; }
        public bool Ended { get; private set; }
        public DateTimeOffset ProjectFilesDownloadedTime { get; private set; }
        public DateTimeOffset LastRpcTime { get; private set; }

        //public List<DailyStatistics> Statistics { get; private set; }

        internal Project(XElement element)
        {
            MasterUrl = element.ElementString("master_url");
            ResourceShare = element.ElementDouble("resource_share");
            ProjectName = element.ElementString("project_name");
            UserName = element.ElementString("user_name");
            TeamName = element.ElementString("team_name");
            HostID = element.ElementInt("hostid");
            GuiUrls = element.Elements("gui_urls").SelectMany(e => e.Elements("gui_url")).Select(e => new GuiUrl(e)).ToArray();
            UserTotalCredit = element.ElementDouble("user_total_credit");
            UserAverageCredit = element.ElementDouble("user_expavg_credit");
            HostTotalCredit = element.ElementDouble("host_total_credit");
            HostAverageCredit = element.ElementDouble("host_expavg_credit");
            DiskUsage = element.ElementDouble("disk_usage");
            RpcFailures = element.ElementInt("nrpc_failures");
            MasterFetchFailures = element.ElementInt("master_fetch_failures");
            MinRpcTime = element.ElementDateTimeOffset("min_rpc_time");
            DownloadBackoff = element.ElementTimeSpan("download_backoff");
            UploadBackoff = element.ElementTimeSpan("upload_backoff");
            ShortTermDebt = element.ElementDouble("short_term_debt");
            LongTermDebt = element.ElementDouble("long_term_debt");
            CpuBackoffTime = element.ElementDateTimeOffset("cpu_backoff_time");
            CpuBackoffInterval = element.ElementTimeSpan("cpu_backoff_interval");
            CudaDebt = element.ElementDouble("cuda_debt");
            CudaShortTermDebt = element.ElementDouble("cuda_short_term_debt");
            CudaBackoffTime = element.ElementDateTimeOffset("cuda_backoff_time");
            CudaBackoffInterval = element.ElementTimeSpan("cuda_backoff_interval");
            AtiDebt = element.ElementDouble("ati_debt");
            AtiShortTermDebt = element.ElementDouble("ati_short_term_debt");
            AtiBackoffTime = element.ElementDateTimeOffset("ati_backoff_time");
            AtiBackoffInterval = element.ElementTimeSpan("ati_backoff_interval");
            DurationCorrectionFactor = element.ElementDouble("duration_correction_factor");
            MasterUrlFetchPending = element.ContainsElement("master_url_fetch_pending");
            SchedulerRpcPending = (RpcReason)element.ElementInt("sched_rpc_pending");
            NonCpuIntensive = element.ContainsElement("non_cpu_intensive");
            Suspended = element.ContainsElement("suspended_via_gui");
            DontRequestMoreWork = element.ContainsElement("dont_request_more_work");
            Ended = element.ContainsElement("ended");
            SchedulerRpcInProgress = element.ContainsElement("scheduler_rpc_in_progress");
            AttachedViaAccountManager = element.ContainsElement("attached_via_acct_mgr");
            DetachWhenDone = element.ContainsElement("detach_when_done");
            ProjectFilesDownloadedTime = element.ElementDateTimeOffset("project_files_downloaded_time");
            LastRpcTime = element.ElementDateTimeOffset("last_rpc_time");
        }

        public override string ToString()
        {
            return $"{ProjectName} ({MasterUrl})";
        }
    }

    public class App
    {
        public string Name { get; }
        public string UserFriendlyName { get; }

        public string ProjectUrl { get; }

        internal App(XElement element, Project project)
        {
            ProjectUrl = project.MasterUrl;
            Name = element.ElementString("name");
            UserFriendlyName = element.ElementString("user_friendly_name");
        }

        public override string ToString()
        {
            return $"{Name} ({ProjectUrl}): {UserFriendlyName}";
        }
    }

    public class AppVersion
    {
        public string AppName { get; }
        public int VersionNumber { get; }
        public string PlanClass { get; }

        public string ProjectUrl { get; }

        internal AppVersion(XElement element, Project project)
        {
            ProjectUrl = project.MasterUrl;

            AppName = element.ElementString("app_name");
            VersionNumber = element.ElementInt("version_num");
            PlanClass = element.ElementString("plan_class");
        }

        public override string ToString()
        {
            return $"{AppName} {VersionNumber} (Project: {ProjectUrl})";
        }
    }

    public class Workunit
    {
        public string Name { get; }
        public string AppName { get; }
        public int VersionNumber { get; }
        public double RscFpOpsEst { get; }
        public double RscFpOpsBound { get; }
        public double RscMemoryBound { get; }
        public double RscDiskBound { get; }

        public string ProjectUrl { get; }
        public IReadOnlyList<Keyword> JobKeywords { get; }

        internal Workunit(XElement element, Project project)
        {
            ProjectUrl = project.MasterUrl;

            Name = element.ElementString("name");
            AppName = element.ElementString("app_name");
            VersionNumber = element.ElementInt("version_num");
            RscFpOpsEst = element.ElementDouble("rsc_fpops_est");
            RscFpOpsBound = element.ElementDouble("rsc_fpops_bound");
            RscMemoryBound = element.ElementDouble("rsc_memory_bound");
            RscDiskBound = element.ElementDouble("rsc_disk_bound");

            JobKeywords = element.Element("job_keywords")?.Elements("keyword").Select(e => new Keyword(e)).ToArray();
        }

        public override string ToString()
        {
            return $"{Name} (App: {AppName} {VersionNumber}, Project: {ProjectUrl})";
        }
    }

    public class Keyword
    {
        public int ID { get; }
        public string Name { get; }
        public string Description { get; }
        public int Parent { get; }
        public int Level { get; }
        public int Category { get; }

        internal Keyword(XElement element)
        {
            ID = element.ElementInt("id");
            Name = element.ElementString("name");
            Description = element.ElementString("description");
            Parent = element.ElementInt("parent");
            Level = element.ElementInt("level");
            Category = element.ElementInt("category");
        }

        public override string ToString()
        {
            return $"{Name} ({Description})";
        }
    }

    public class Result
    {
        public string Name { get; private set; }
        public string WorkunitName { get; private set; }
        public string ProjectUrl { get; private set; }
        public int VersionNumber { get; private set; }
        public string PlanClass { get; private set; }
        public DateTimeOffset ReportDeadline { get; private set; }
        public DateTimeOffset ReceivedTime { get; private set; }
        public bool ReadyToReport { get; private set; }
        public bool GotServerAck { get; private set; }
        public TimeSpan FinalCpuTime { get; private set; }
        public TimeSpan FinalElapsedTime { get; private set; }
        public ResultState State { get; private set; }
        public SchedulerState SchedulerState { get; private set; }
        public int ExitStatus { get; private set; }
        public int Signal { get; private set; }
        public string StderrOut { get; private set; }
        public bool Suspended { get; private set; }
        public bool ProjectSuspended { get; private set; }
        public bool ReportImmediately { get; private set; }
        public bool CoprocessorMissing { get; private set; }
        public bool SchedulerWait { get; private set; }
        public string SchedulerWaitReason { get; private set; }
        public bool NetworkWait { get; private set; }
        public bool ActiveTask { get; private set; }
        public TaskState ActiveTaskState { get; private set; }
        public int AppVersionNumber { get; private set; }
        public int Slot { get; private set; }
        public int Pid { get; private set; }
        public TimeSpan CheckpointCpuTime { get; private set; }
        public TimeSpan CurrentCpuTime { get; private set; }
        public double FractionDone { get; private set; }
        public TimeSpan ElapsedTime { get; private set; }
        public double SwapSize { get; private set; }
        public double WorkingSetSizeSmoothed { get; private set; }
        public TimeSpan EstimatedCpuTimeRemaining { get; private set; }
        public bool SupportsGraphics { get; private set; }
        public int GraphicsModeAcked { get; private set; }
        public bool TooLarge { get; private set; }
        public bool NeedsSharedMemory { get; private set; }
        public bool EdfScheduled { get; private set; } // no longer there ?
        public string GraphicsExecPath { get; private set; }
        public string SlotPath { get; private set; }
        public string Resources { get; private set; }

        internal Result(XElement element)
        {
            Name = element.ElementString("name");
            WorkunitName = element.ElementString("wu_name");
            ProjectUrl = element.ElementString("project_url");
            VersionNumber = element.ElementInt("version_num");
            PlanClass = element.ElementString("plan_class");
            ReportDeadline = element.ElementDateTimeOffset("report_deadline");
            ReceivedTime = element.ElementDateTimeOffset("received_time");
            ReadyToReport = element.ContainsElement("ready_to_report");
            GotServerAck = element.ContainsElement("got_server_ack");
            FinalCpuTime = element.ElementTimeSpan("final_cpu_time");
            FinalElapsedTime = element.ElementTimeSpan("final_elapsed_time");
            State = (ResultState)element.ElementInt("state");
            ExitStatus = element.ElementInt("exit_status");
            Signal = element.ElementInt("signal");
            StderrOut = element.ElementString("stderr_out");
            Suspended = element.ContainsElement("suspended_via_gui");
            ProjectSuspended = element.ContainsElement("project_suspended_via_gui");
            ReportImmediately = element.ContainsElement("report_immediately");
            CoprocessorMissing = element.ContainsElement("coproc_missing");
            SchedulerWait = element.ContainsElement("scheduler_wait");
            SchedulerWaitReason = element.ElementString("scheduler_wait_reason");
            NetworkWait = element.ContainsElement("network_wait");
            ActiveTask = element.ContainsElement("active_task");
            EstimatedCpuTimeRemaining = element.ElementTimeSpan("estimated_cpu_time_remaining");
            SupportsGraphics = element.ContainsElement("supports_graphics");
            GraphicsModeAcked = element.ElementInt("graphics_mode_acked");
            TooLarge = element.ContainsElement("too_large");
            NeedsSharedMemory = element.ContainsElement("needs_shmem");
            EdfScheduled = element.ContainsElement("edf_scheduled");
            Resources = element.ElementString("resources");

            XElement activeTaskElement = element.Element("active_task");

            if (activeTaskElement != null)
            {
                ActiveTaskState = (TaskState)activeTaskElement.ElementInt("active_task_state");
                AppVersionNumber = activeTaskElement.ElementInt("app_version_num");
                Slot = activeTaskElement.ElementInt("slot");
                Pid = activeTaskElement.ElementInt("pid");
                SchedulerState = (SchedulerState)activeTaskElement.ElementInt("scheduler_state");
                CheckpointCpuTime = activeTaskElement.ElementTimeSpan("checkpoint_cpu_time");
                CurrentCpuTime = activeTaskElement.ElementTimeSpan("current_cpu_time");
                FractionDone = activeTaskElement.ElementDouble("fraction_done");
                ElapsedTime = activeTaskElement.ElementTimeSpan("elapsed_time");
                SwapSize = activeTaskElement.ElementDouble("swap_size");
                WorkingSetSizeSmoothed = activeTaskElement.ElementDouble("working_set_size_smoothed");
                GraphicsExecPath = activeTaskElement.ElementString("graphics_exec_path");
                SlotPath = activeTaskElement.ElementString("slot_path");
            }
        }

        public override string ToString()
        {
            return $"{Name} (Workunit: {WorkunitName}, Project: {ProjectUrl})";
        }
    }

    public class FileTransfer
    {
        public string Name { get; private set; }
        public string ProjectUrl { get; private set; }
        public string ProjectName { get; private set; }
        public double NumberOfBytes { get; private set; }
        public bool Uploaded { get; private set; }
        public bool Sticky { get; private set; }
        public bool PersistentTransferActive { get; private set; }
        public bool TransferActive { get; private set; }
        public int NumberOfRetries { get; private set; }
        public DateTimeOffset FirstRequestTime { get; private set; }
        public DateTimeOffset NextRequestTime { get; private set; }
        public ErrorCode Status { get; private set; }
        public TimeSpan TimeSoFar { get; private set; }
        public double LastBytesTransferred { get; private set; }
        public bool IsUpload { get; private set; }
        public double BytesTransferred { get; private set; }
        public double FileOffset { get; private set; }
        public double TransferSpeed { get; private set; }
        public string Url { get; private set; }
        public string Hostname { get; private set; }
        public TimeSpan ProjectBackoff { get; private set; }

        internal FileTransfer(XElement element)
        {
            Name = element.ElementString("name");
            ProjectUrl = element.ElementString("project_url");
            ProjectName = element.ElementString("project_name");
            NumberOfBytes = element.ElementDouble("nbytes");
            IsUpload = element.ContainsElement("generated_locally"); // deprecated, for backwards compatibility
            Uploaded = element.ContainsElement("uploaded");
            Sticky = element.ContainsElement("sticky");
            PersistentTransferActive = element.ContainsElement("persistent_file_xfer");
            TransferActive = element.ContainsElement("file_xfer");           
            Status = (ErrorCode)element.ElementInt("status");
            Hostname = element.ElementString("hostname");
            ProjectBackoff = element.ElementTimeSpan("project_backoff");

            XElement persistentFileXfer = element.Element("persistent_file_xfer");

            if (persistentFileXfer != null)
            {
                NumberOfRetries = persistentFileXfer.ElementInt("num_retries");
                FirstRequestTime = persistentFileXfer.ElementDateTimeOffset("first_request_time");
                NextRequestTime = persistentFileXfer.ElementDateTimeOffset("next_request_time");
                TimeSoFar = persistentFileXfer.ElementTimeSpan("time_so_far");
                LastBytesTransferred = persistentFileXfer.ElementDouble("last_bytes_xferred");
                IsUpload = IsUpload || persistentFileXfer.ElementBoolean("is_upload");
            }

            XElement fileXfer = element.Element("file_xfer");

            if (fileXfer != null)
            {
                BytesTransferred = fileXfer.ElementDouble("bytes_xferred");
                FileOffset = fileXfer.ElementDouble("file_offset");
                TransferSpeed = fileXfer.ElementDouble("xfer_speed");
                Url = fileXfer.ElementString("url");
            }
        }

        public override string ToString()
        {
            return $"{Name} (Project: {ProjectUrl}): {Url}";
        }
    }

    public class Message
    {
        public string Project { get; }
        public MessagePriority Priority { get; }
        public int SequenceNumber { get; }
        public DateTimeOffset Timestamp { get; }
        public string Body { get; }

        internal Message(XElement element)
        {
            Project = element.ElementString("project");
            Priority = (MessagePriority)element.ElementInt("pri");
            SequenceNumber = element.ElementInt("seqno");
            Timestamp = element.ElementDateTimeOffset("time");
            Body = element.ElementString("body").Trim('\n');
        }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(Project))
                return $"{Timestamp}: {Body}";
            else
                return $"{Timestamp} (Project: {Project}): {Body}";
        }
    }

    public class Notice
    {
        public string Title { get; }
        public string Description { get; }
        public DateTimeOffset CreateTime { get; }
        public DateTimeOffset ArrivalTime { get; }
        public bool IsPrivate { get; }
        public string ProjectName { get; }
        public string Category { get; }
        public string Link { get; }
        public int SequenceNumber { get; }

        internal Notice(XElement element)
        {
            Title = element.ElementString("title");
            Description = element.ElementString("description").Trim('\n');
            CreateTime = element.ElementDateTimeOffset("create_time");
            ArrivalTime = element.ElementDateTimeOffset("arrival_time");
            IsPrivate = element.ElementBoolean("is_private");
            ProjectName = element.ElementString("project_name");
            Category = element.ElementString("category");
            Link = element.ElementString("link");
            SequenceNumber = element.ElementInt("seqno");
        }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(ProjectName))
                return $"{Title}: {Description}";
            else
                return $"{Title} (Project: {ProjectName}): {Description}";
        }
    }

    public class ProxyInfo
    {
        public bool UseHttpProxy { get; set; }
        public bool UseSocksProxy { get; set; }
        public bool UseHttpAuthentication { get; set; }
        public string HttpServerName { get; set; }
        public int HttpServerPort { get; set; }
        public string SocksServerName { get; set; }
        public int SocksServerPort { get; set; }
        public string HttpUserName { get; set; }
        public string HttpUserPassword { get; set; }
        public string Socks5UserName { get; set; }
        public string Socks5UserPassword { get; set; }
        public bool Socks5RemoteDns { get; set; }
        public string NoProxyHosts { get; set; }

        public ProxyInfo()
        {
        }

        internal ProxyInfo(XElement element)
        {
            UseHttpProxy = element.ContainsElement("use_http_proxy");
            UseSocksProxy = element.ContainsElement("use_socks_proxy");
            UseHttpAuthentication = element.ContainsElement("use_http_auth");
            HttpServerName = element.ElementString("http_server_name");
            HttpServerPort = element.ElementInt("http_server_port");
            SocksServerName = element.ElementString("socks_server_name");
            SocksServerPort = element.ElementInt("socks_server_port");
            HttpUserName = element.ElementString("http_user_name");
            HttpUserPassword = element.ElementString("http_user_passwd");
            Socks5UserName = element.ElementString("socks5_user_name");
            Socks5UserPassword = element.ElementString("socks5_user_passwd");
            Socks5RemoteDns = element.ElementInt("socks5_remote_dns") != 0;
            NoProxyHosts = element.ElementString("no_proxy");
        }
    }

    // in client\result.cpp
    public class OldResult
    { 
        public string ProjectUrl { get; }
        public string ResultName { get; }
        public string AppName { get; }
        public int ExitStatus { get; }
        public TimeSpan ElapsedTime { get; }
        public TimeSpan CpuTime { get; }
        public DateTimeOffset CompletedTime { get; }
        public DateTimeOffset CreateTime { get; }

        internal OldResult(XElement element)
        {
            ProjectUrl = element.ElementString("project_url");
            ResultName = element.ElementString("result_name");
            AppName = element.ElementString("app_name");
            ExitStatus = element.ElementInt("exit_status");
            ElapsedTime = element.ElementTimeSpan("elapsed_time");
            CpuTime = element.ElementTimeSpan("cpu_time");
            CompletedTime = element.ElementDateTimeOffset("completed_time");
            CreateTime = element.ElementDateTimeOffset("create_time");
        }

        public override string ToString()
        {
            return $"{ResultName} ({ProjectUrl})";
        }
    }

    // in client\net_stats.cpp
    public class DailyTransferStatistics
    {
        public DateTime Day { get; }
        public double BytesUploaded { get; }
        public double BytesDownloaded { get; }

        internal DailyTransferStatistics(XElement element)
        {
            Day = new DateTime(1970, 1, 1).AddDays(element.ElementInt("when"));
            BytesUploaded = element.ElementDouble("up");
            BytesDownloaded = element.ElementDouble("down");
        }
    }

    public struct StartEndTime
    {
        public double StartHour { get; }
        public double EndHour { get; }

        public StartEndTime(double startHour, double endHour)
        {
            StartHour = startHour;
            EndHour = endHour;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;

            return StartHour == ((StartEndTime)obj).StartHour && EndHour == ((StartEndTime)obj).EndHour;
        }

        public override int GetHashCode()
        {
            return StartHour.GetHashCode() ^ EndHour.GetHashCode();
        }
        
        public static bool operator ==(StartEndTime obj1, object obj2)
        {
            return obj1.Equals(obj2);
        }

        public static bool operator !=(StartEndTime obj1, object obj2)
        {
            return !obj1.Equals(obj2);
        }

        public override string ToString()
        {
            return $"{TimeSpan.FromHours(StartHour)} to {TimeSpan.FromHours(EndHour)}";
        }
    }

    public class TimesPreferences
    {
        public StartEndTime StartEndTime { get; }
        public IReadOnlyDictionary<DayOfWeek, StartEndTime> WeeklyPreferences { get; }

        public TimesPreferences(StartEndTime startEndTime, IReadOnlyDictionary<DayOfWeek, StartEndTime> weeklyPreferences)
        {
            StartEndTime = startEndTime;
            WeeklyPreferences = weeklyPreferences;
        }
    }

    // in https://github.com/BOINC/boinc/blob/master/lib/prefs.cpp
    public class GlobalPreferences
    {
        public string SourceProject { get; }
        public DateTimeOffset ModifiedTime { get; }
        public double BatteryChargeMinPercentage { get; }
        public double BatteryMaxTemperature { get; }
        public bool RunOnBatteries { get; }
        public bool RunIfUserActive { get; }
        public bool RunGpuIfUserActive { get; }
        public double SuspendIfNoRecentInput { get; }
        public double SuspendCpuUsage { get; }
        public TimesPreferences CpuTimes { get; }
        public TimesPreferences NetworkTimes { get; } 
        public bool LeaveAppsInMemory { get; }
        public bool ConfirmBeforeConnecting { get; }
        public bool HangupIfDialed { get; }
        public bool DontVerifyImages { get; }
        public double WorkBufferMinDays { get; }
        public double WorkBufferAdditionalDays { get; }
        public double MaxNumberOfCpusPercentage { get; }
        public TimeSpan CpuSchedulingPeriod { get; }
        public double DiskInterval { get; }
        public double DiskMaxUsedGB { get; }
        public double DiskMaxUsedPercentage { get; }
        public double DiskMinFreeGB { get; }
        public double VMMaxUsedPercentage { get; }
        public double RamMaxUsedBusyPercentage { get; }
        public double RamMaxUsedIdlePercentage { get; }
        public double IdleTimeToRun { get; }
        public double MaxBytesPerSecondUp { get; }
        public double MaxBytesPerSecondDown { get; }
        public double CpuUsageLimit { get; }
        public double DailyTransferLimitMB { get; }
        public double DailyTransferPeriodDays { get; }
        public bool OverrideFilePresent { get; }
        public bool NetworkWifiOnly { get; }
        public int MaxNumberOfCpus { get; }

        internal GlobalPreferences(XElement element)
        {
            SourceProject = element.ElementString("source_project");
            ModifiedTime = element.ElementDateTimeOffset("mod_time");
            BatteryChargeMinPercentage = element.ElementDouble("battery_charge_min_pct");
            BatteryMaxTemperature = element.ElementDouble("battery_max_temperature");
            RunOnBatteries = element.ContainsElement("run_on_batteries");
            RunIfUserActive = element.ContainsElement("run_if_user_active");
            RunGpuIfUserActive = element.ContainsElement("run_gpu_if_user_active");
            SuspendIfNoRecentInput = element.ElementDouble("suspend_if_no_recent_input");
            SuspendCpuUsage = element.ElementDouble("suspend_cpu_usage");            
            LeaveAppsInMemory = element.ContainsElement("leave_apps_in_memory");
            ConfirmBeforeConnecting = element.ContainsElement("confirm_before_connecting");
            HangupIfDialed = element.ContainsElement("hangup_if_dialed");
            DontVerifyImages = element.ContainsElement("dont_verify_images");
            WorkBufferMinDays = element.ElementDouble("work_buf_min_days");
            WorkBufferAdditionalDays = element.ElementDouble("work_buf_additional_days");
            MaxNumberOfCpusPercentage = element.ElementDouble("max_ncpus_pct");
            CpuSchedulingPeriod = TimeSpan.FromMinutes(element.ElementDouble("cpu_scheduling_period_minutes"));
            DiskInterval = element.ElementDouble("disk_interval");
            DiskMaxUsedGB = element.ElementDouble("disk_max_used_gb");
            DiskMaxUsedPercentage = element.ElementDouble("disk_max_used_pct");
            DiskMinFreeGB = element.ElementDouble("disk_min_free_gb");
            VMMaxUsedPercentage = element.ElementDouble("vm_max_used_pct");
            RamMaxUsedBusyPercentage = element.ElementDouble("ram_max_used_busy_pct");
            RamMaxUsedIdlePercentage = element.ElementDouble("ram_max_used_idle_pct");
            IdleTimeToRun = element.ElementDouble("idle_time_to_run");
            MaxBytesPerSecondUp = element.ElementDouble("max_bytes_sec_up");
            MaxBytesPerSecondDown = element.ElementDouble("max_bytes_sec_down");
            CpuUsageLimit = element.ElementDouble("cpu_usage_limit");
            DailyTransferLimitMB = element.ElementDouble("daily_xfer_limit_mb");
            DailyTransferPeriodDays = element.ElementDouble("daily_xfer_period_days");
            OverrideFilePresent = element.ContainsElement("override_file_present");
            NetworkWifiOnly = element.ContainsElement("network_wifi_only");
            MaxNumberOfCpus = element.ElementInt("max_cpus");

            StartEndTime cpuStartEndTime = new StartEndTime(element.ElementDouble("start_hour"), element.ElementDouble("end_hour"));
            StartEndTime networkStartEndTime = new StartEndTime(element.ElementDouble("net_start_hour"), element.ElementDouble("net_end_hour"));
            
            Dictionary<DayOfWeek, StartEndTime> weeklyCpuPreferences = new Dictionary<DayOfWeek, StartEndTime>();
            Dictionary<DayOfWeek, StartEndTime> weeklyNetworkPreferences = new Dictionary<DayOfWeek, StartEndTime>();

            foreach (XElement dayPrefsElement in element.Elements("day_prefs"))
            {
                DayOfWeek dayOfWeek = (DayOfWeek)dayPrefsElement.ElementInt("day_of_week");

                if (dayPrefsElement.ContainsElement("start_hour") && dayPrefsElement.ContainsElement("end_hour"))
                    weeklyCpuPreferences[dayOfWeek] = new StartEndTime(dayPrefsElement.ElementDouble("start_hour"), dayPrefsElement.ElementDouble("end_hour"));

                if (dayPrefsElement.ContainsElement("net_start_hour") && dayPrefsElement.ContainsElement("net_end_hour"))
                    weeklyNetworkPreferences[dayOfWeek] = new StartEndTime(dayPrefsElement.ElementDouble("net_start_hour"), dayPrefsElement.ElementDouble("net_end_hour"));
            }

            CpuTimes = new TimesPreferences(cpuStartEndTime, weeklyCpuPreferences);
            NetworkTimes = new TimesPreferences(networkStartEndTime, weeklyNetworkPreferences);
        }
    }

    // see CC_STATE::parse in https://github.com/BOINC/boinc/blob/master/lib/gui_rpc_client_ops.cpp
    public class CoreClientState
    {
        public IReadOnlyList<Project> Projects { get; }
        public IReadOnlyList<App> Apps { get; }
        public IReadOnlyList<AppVersion> AppVersions { get; }
        public IReadOnlyList<Workunit> Workunits { get; }
        public IReadOnlyList<Result> Results { get; }
        public IReadOnlyList<string> Platforms { get; }
        public GlobalPreferences GlobalPreferences { get; }
        public HostInfo HostInfo { get; }
        public TimeStatistics TimeStatistics { get; }
        public bool ExecutingAsDaemon { get; }
        public bool HaveCuda { get; }
        public bool HaveAti { get; }

        internal CoreClientState(XElement element)
        {
            List<Project> projects = new List<Project>();
            List<App> apps = new List<App>();
            List<AppVersion> appVersions = new List<AppVersion>();
            List<Workunit> workunits = new List<Workunit>();
            List<Result> results = new List<Result>();

            ExecutingAsDaemon = element.ContainsElement("executing_as_daemon");
            HaveCuda = element.ContainsElement("have_cuda");
            HaveAti = element.ContainsElement("have_ati");
            Platforms = element.Elements("platform").Select(e => (string)e).ToArray();

            XElement globalPreferences = element.Element("global_preferences");
            if (globalPreferences != null)
                GlobalPreferences = new GlobalPreferences(globalPreferences);

            HostInfo = new HostInfo(element.Element("host_info"));
            TimeStatistics = new TimeStatistics(element.Element("time_stats"));

            foreach (XElement el in element.Elements())
            {
                switch (el.Name.ToString())
                {
                    case "project":
                        projects.Add(new Project(el));
                        break;
                    case "app":
                        App app = new App(el, projects.Last());
                        apps.Add(app);
                        break;
                    case "app_version":
                        AppVersion appVersion = new AppVersion(el, projects.Last());
                        appVersions.Add(appVersion);
                        break;
                    case "workunit":
                        Workunit workunit = new Workunit(el, projects.Last());
                        workunits.Add(workunit);
                        break;
                    case "result":
                        Result result = new Result(el);
                        results.Add(result);
                        break;
                }
            }

            Projects = projects;
            Apps = apps;
            AppVersions = appVersions;
            Workunits = workunits;
            Results = results;
        }

        public override string ToString()
        {
            return $"{Projects.Count} project(s), {Apps.Count} app(s), {Workunits.Count} workunit(s), {Results.Count} result(s)";
        }
    }

    // see TIME_STATS::parse in https://github.com/BOINC/boinc/blob/master/lib/gui_rpc_client_ops.cpp
    public class TimeStatistics
    {
        public DateTimeOffset Now { get; }
        public double OnFraction { get; }
        public double ConnectedFraction { get; }
        public double CpuAndNetworkAvailableFraction { get; }
        public double ActiveFraction { get; }
        public double GpuActiveFraction { get; }
        public DateTimeOffset ClientStartTime { get; }
        public TimeSpan PreviousUptime { get; }
        public TimeSpan SessionActiveDuration { get; }
        public TimeSpan SessionGpuActiveDuration { get; }
        public DateTimeOffset TotalStartTime { get; }
        public TimeSpan TotalDuration { get; }
        public TimeSpan TotalActiveDuration { get; }
        public TimeSpan TotalGpuActiveDuration { get; }
   
        internal TimeStatistics(XElement element)
        {
            Now = element.ElementDateTimeOffset("now");
            OnFraction = element.ElementDouble("on_frac");
            ConnectedFraction = element.ElementDouble("connected_frac");
            CpuAndNetworkAvailableFraction = element.ElementDouble("cpu_and_network_available_frac");
            ActiveFraction = element.ElementDouble("active_frac");
            GpuActiveFraction = element.ElementDouble("gpu_active_frac");
            ClientStartTime = element.ElementDateTimeOffset("client_start_time");
            PreviousUptime = element.ElementTimeSpan("previous_uptime");
            SessionActiveDuration = element.ElementTimeSpan("session_active_duration");
            SessionGpuActiveDuration = element.ElementTimeSpan("session_gpu_active_duration");
            TotalStartTime = element.ElementDateTimeOffset("total_start_time");
            TotalDuration = element.ElementTimeSpan("total_duration");
            TotalActiveDuration = element.ElementTimeSpan("total_active_duration");
            TotalGpuActiveDuration = element.ElementTimeSpan("total_gpu_active_duration");
        }
    }

    // see handle_get_cc_status in https://github.com/BOINC/boinc/blob/master/client/gui_rpc_server_ops.cpp
    public class CoreClientStatus
    {
        public NetworkStatus NetworkStatus { get; }
        public bool AmsPasswordError { get; }
        public bool ManagerMustQuit { get; }
        public SuspendReason TaskSuspendReason { get; }
        public Mode TaskMode { get; }
        public Mode TaskModePerm { get; }
        public TimeSpan TaskModeDelay { get; }
        public SuspendReason GpuSuspendReason { get; }
        public Mode GpuMode { get; }
        public Mode GpuModePerm { get; }
        public TimeSpan GpuModeDelay { get; }
        public SuspendReason NetworkSuspendReason { get; }
        public Mode NetworkMode { get; }
        public Mode NetworkModePerm { get; }
        public TimeSpan NetworkModeDelay { get; }
        public bool DisallowAttach { get; }
        public bool SimpleGuiOnly { get; }
        public int MaxEventLogLines { get; }

        internal CoreClientStatus(XElement element)
        {
            NetworkStatus = (NetworkStatus)element.ElementInt("network_status");
            AmsPasswordError = element.ElementBoolean("ams_password_error");
            ManagerMustQuit = element.ElementBoolean("manager_must_quit");
            TaskSuspendReason = (SuspendReason)element.ElementInt("task_suspend_reason");
            TaskMode = (Mode)element.ElementInt("task_mode");
            TaskModePerm = (Mode)element.ElementInt("task_mode_perm");
            TaskModeDelay = element.ElementTimeSpan("task_mode_delay");
            GpuSuspendReason = (SuspendReason)element.ElementInt("gpu_suspend_reason");
            GpuMode = (Mode)element.ElementInt("gpu_mode");
            GpuModePerm = (Mode)element.ElementInt("gpu_mode_perm");
            GpuModeDelay = element.ElementTimeSpan("gpu_mode_delay");
            NetworkSuspendReason = (SuspendReason)element.ElementInt("network_suspend_reason");
            NetworkMode = (Mode)element.ElementInt("network_mode");
            NetworkModePerm = (Mode)element.ElementInt("network_mode_perm");
            NetworkModeDelay = element.ElementTimeSpan("network_mode_delay");
            DisallowAttach = element.ElementBoolean("disallow_attach");
            SimpleGuiOnly = element.ElementBoolean("simple_gui_only");
            MaxEventLogLines = element.ElementInt("max_event_log_lines");
        }
    }

    // see HOST_INFO::write in https://github.com/BOINC/boinc/blob/master/lib/hostinfo.cpp
    public class HostInfo
    {
        public TimeSpan TimeZone { get; }
        public string DomainName { get; }
        public string SerialNumber { get; }
        public string IPAddress { get; }
        public string HostCPID { get; }
        public int NumberOfCpus { get; }
        public string ProcessorVendor { get; }
        public string ProcessorModel { get; }
        public string ProcessorFeatures { get; }
        public double FloatingPointOperations { get; }
        public double IntegerOperations { get; }
        public double MemoryBandwidth { get; }
        public DateTimeOffset LastBenchmark { get; }
        public bool VMExtensionsDisabled { get; }
        public double MemorySize { get; }
        public double CacheSize { get; }
        public double SwapSize { get; }
        public double TotalDiskSpace { get; }
        public double FreeDiskSpace { get; }
        public string OSName { get; }
        public string OSVersion { get; }
        public int NumberOfUsableCoprocessors { get; }
        public bool WslAvailable { get; }
        public string ProductName { get; }
        public string MacAddress { get; }
        public string VirtualBoxVersion { get; }

        public IReadOnlyList<Wsl> Wsls { get; }

        // TODO: add coproc properties
        // TODO: add opencl_cpu_prop

        internal HostInfo(XElement element)
        {
            TimeZone = TimeSpan.FromSeconds(element.ElementInt("timezone"));
            DomainName = element.ElementString("domain_name");
            IPAddress = element.ElementString("ip_addr");
            HostCPID = element.ElementString("host_cpid");
            NumberOfCpus = element.ElementInt("p_ncpus");
            ProcessorVendor = element.ElementString("p_vendor");
            ProcessorModel = element.ElementString("p_model");
            ProcessorFeatures = element.ElementString("p_features");
            FloatingPointOperations = element.ElementDouble("p_fpops");
            IntegerOperations = element.ElementDouble("p_iops");
            MemoryBandwidth = element.ElementDouble("p_membw");
            LastBenchmark = element.ElementDateTimeOffset("p_calculated");
            VMExtensionsDisabled = element.ElementBoolean("p_vm_extensions_disabled");
            MemorySize = element.ElementDouble("m_nbytes");
            CacheSize = element.ElementDouble("m_cache");
            SwapSize = element.ElementDouble("m_swap");
            TotalDiskSpace = element.ElementDouble("d_total");
            FreeDiskSpace = element.ElementDouble("d_free");
            OSName = element.ElementString("os_name");
            OSVersion = element.ElementString("os_version");
            NumberOfUsableCoprocessors = element.ElementInt("n_usable_coprocs");
            WslAvailable = element.ElementBoolean("wsl_available");
            ProductName = element.ElementString("product_name");
            MacAddress = element.ElementString("mac_address");
            VirtualBoxVersion = element.ElementString("virtualbox_version");

            Wsls = element.Element("wsl")?.Elements("distro").Select(e => new Wsl(e)).ToArray();
        }

        public override string ToString()
        {
            return $"DomainName: {DomainName}, IP: {IPAddress}, OSName: {OSName}, OSVersion: {OSVersion}";
        }
    }

    // see WSL::write_xml in https://github.com/BOINC/boinc/blob/master/lib/wslinfo.cpp
    public class Wsl
    {
        public string DistroName { get; }
        public string Name { get; }
        public string Version { get; }
        public bool IsDefault { get; }

        internal Wsl(XElement element)
        {
            DistroName = element.ElementString("distro_name");
            Name = element.ElementString("name");
            Version = element.ElementString("version");
            IsDefault = element.ElementInt("is_default") != 0;
        }

        public override string ToString()
        {
            return $"{Name} ({DistroName}, {Version}{(IsDefault ? ", default" : "")})";
        }
    }

    public class NewerVersionInfo
    {
        public string NewerVersion { get; }
        public string DownloadUrl { get; }

        internal NewerVersionInfo(XElement element)
        {
            NewerVersion = element.ElementString("newer_version");
            DownloadUrl = element.ElementString("download_url");
        }

        public override string ToString()
        {
            return $"{NewerVersion} ({DownloadUrl})";
        }
    }
}