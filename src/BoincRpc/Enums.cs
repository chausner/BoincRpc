namespace BoincRpc
{
    // Enum definitions in https://github.com/BOINC/boinc/blob/master/lib/common_defs.h
    
    public enum Mode
    {
        Always = 1,
        Auto,
        Never,
        Restore
    }

    public enum ProjectOperation
    {
        Reset,
        Detach,
        Update,
        Suspend,
        Resume,
        AllowMoreWork,
        NoMoreWork,
        DetachWhenDone,
        DontDetachWhenDone
    }

    public enum FileTransferOperation
    {
        Retry,
        Abort
    }

    public enum ResultOperation
    {
        Suspend,
        Resume,
        Abort
    }

    public enum SchedulerState
    {
        Uninitialized,
        Preempted,
        Scheduled
    }

    public enum ResultState
    {
        New,
        FilesDownloading,
        FilesDownloaded,
        ComputeError,
        FilesUploading,
        FilesUploaded,
        Aborted,
        UploadFailed
    }

    public enum TaskState
    {
        Uninitialized,
        Executing,
        Exited,
        WasSignaled,
        ExitUnknown,
        AbortPending,
        Aborted,
        CouldntStart,
        QuitPending,
        Suspended,
        CopyPending
    }

    public enum NetworkStatus
    {
        Online,
        WantConnection,
        WantDisconnect,
        LookupPending
    }

    public enum RpcReason
    {
        UserRequest = 1,
        ResultsDue,
        NeedWork,
        TrickleUp,
        AccountManagerRequest,
        Init,
        ProjectRequest
    }

    public enum MessagePriority
    {
        Info = 1,
        UserAlert,
        InternalError
    }

    public enum SuspendReason
    {
        None = 0,
        Batteries = 1,
        UserActive = 2,
        UserRequest = 4,
        TimeOfDay = 8,
        Benchmarks = 16,
        DiskSize = 32,
        CpuThrottle = 64,
        NoRecentInput = 128,
        InitialDelay = 256,
        ExclusiveAppRunning = 512,
        CpuUsage = 1024,
        NetworkQuotaExceeded = 2048,
        OperatingSystem = 4096,
        WiFiState = 4097,
        BatteryCharging = 4098,
        BatteryOverheated = 4099,
        NoGuiKeepAlive = 4100
    }

    public enum ExitCode
    {
        StateFileWrite = 192,
        Signal = 193,
        AbortedByClient = 194,
        ChildFailed = 195,
        DiskLimitExceeded = 196,
        TimeLimitExceeded = 197,
        MemoryLimitExceeded = 198,
        ClientExiting = 199,
        UnstartedLate = 200,
        MissingCoprocessor = 201,
        AbortedByProject = 202,
        AbortedViaGui = 203,
        Unknown = 204,
        OutOfMemory = 205
    }

    public enum ErrorCode
    {
        Success = 0,
        Select = -100,
        MAlloc = -101,
        Read = -102,
        Write = -103,
        FRead = -104,
        FWrite = -105,
        IO = -106,
        Connect = -107,
        FOpen = -108,
        Rename = -109,
        Unlink = -110,
        OpenDir = -111,
        XmlParse = -112,
        GetHostByName = -113,
        GiveupDownload = -114,
        GiveupUpload = -115,
        Null = -116,
        Neg = -117,
        BufferOverflow = -118,
        Md5Failed = -119,
        RsaFailed = -120,
        Open = -121,
        Dup2 = -122,
        NoSignature = -123,
        Thread = -124,
        SignalCatch = -125,
        BadFormat = -126,
        UploadTransient = -127,
        UploadPermanent = -128,
        IdlePeriod = -129,
        AlreadyAttached = -130,
        FileTooBig = -131,
        Getrusage = -132,
        BenchmarkFailed = -133,
        BadHexFormat = -134,
        GetAddrInfo = -135,
        DBNotFound = -136,
        DBNotUnique = -137,
        DBCantConnect = -138,
        Gets = -139,
        Scanf = -140,
        Readdir = -143,
        Shmget = -144,
        Shmctl = -145,
        Shmat = -146,
        Fork = -147,
        Exec = -148,
        NotExited = -149,
        NotImplemented = -150,
        Gethostname = -151,
        Netopen = -152,
        Socket = -153,
        Fcntl = -154,
        Authenticator = -155,
        SchedShmem = -156,
        Asyncselect = -157,
        BadResultState = -158,
        DbCantInit = -159,
        NotUnique = -160,
        NotFound = -161,
        NoExitStatus = -162,
        FileMissing = -163,
        Kill = -164,
        Semget = -165,
        Semctl = -166,
        Semop = -167,
        Ftok = -168,
        SocksUnknownFailure = -169,
        SocksRequestFailed = -170,
        SocksBadUserPass = -171,
        SocksUnknownServerVersion = -172,
        SocksUnsupported = -173,
        SocksCantReachHost = -174,
        SocksConnRefused = -175,
        TimerInit = -176,
        InvalidParam = -178,
        SignalOp = -179,
        Bind = -180,
        Listen = -181,
        Timeout = -182,
        ProjectDown = -183,
        HttpTransient = -184,
        ResultStart = -185,
        ResultDownload = -186,
        ResultUpload = -187,
        BadUsername = -188,
        InvalidUrl = -189,
        MajorVersion = -190,
        NoOption = -191,
        Mkdir = -192,
        InvalidEvent = -193,
        AlreadyRunning = -194,
        NoAppVersion = -195,
        WuUserRule = -196,
        AbortedViaGui = -197,
        InsufficientResource = -198,
        Retry = -199,
        WrongSize = -200,
        UserPermission = -201,
        ShmemName = -202,
        NoNetworkConnection = -203,
        InProgress = -204,
        BadEmailAddr = -205,
        BadPasswd = -206,
        NonUniqueEmail = -207,
        AcctCreationDisabled = -208,
        AttachFailInit = -209,
        AttachFailDownload = -210,
        AttachFailParse = -211,
        AttachFailBadKey = -212,
        AttachFailFileWrite = -213,
        AttachFailServerError = -214,
        SigningKey = -215,
        Fflush = -216,
        Fsync = -217,
        Truncate = -218,
        WrongUrl = -219,
        DupName = -220,
        Getgrnam = -222,
        Chown = -223,
        HttpPermanent = -224,
        BadFilename = -225,
        TooManyExits = -226,
        Rmdir = -227,
        Symlink = -229,
        DbConnLost = -230,
        Crypto = -231,
        AbortedOnExit = -232,
        ProcParse = -235,
        Statfs = -236,
        Pipe = -237,
        NeedHttps = -238
    }
}
