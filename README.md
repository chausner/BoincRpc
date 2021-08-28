# BoincRpc
An asynchronous .NET implementation of the [BOINC GUI RPC protocol](http://boinc.berkeley.edu/trac/wiki/GuiRpc).

The implementation is up-to-date as of BOINC 7.18.1 and (almost) all RPC structures and commands are fully supported.
The library is compatible with [.NET Standard 2.0](https://docs.microsoft.com/en-us/dotnet/standard/net-standard).

[![NuGet](https://img.shields.io/nuget/v/BoincRpc.svg)](https://www.nuget.org/packages/BoincRpc/)
[![license](https://img.shields.io/github/license/chausner/BoincRpc.svg)](https://github.com/chausner/BoincRpc/blob/master/LICENSE)

Usage
-----
Usage of the library should be largely self-explanatory.
All functionality is provided via the `RpcClient` class.

The following snippet shows how to connect and authenticate to a BOINC client and print a list of all current tasks: 
```csharp
using (RpcClient rpcClient = new RpcClient())
{
    await rpcClient.ConnectAsync("localhost", 31416);

    bool authorized = await rpcClient.AuthorizeAsync("57dd16bbf477ff9a76141f1575d4a44c");

    if (authorized)
    {
        foreach (Result result in await rpcClient.GetResultsAsync())
        {
            Console.WriteLine("{0} ({1}): {2:F2}% complete", result.WorkunitName, result.ProjectUrl, result.FractionDone * 100);
        }
    }
}
```

For information on the RPC commands, see the [BOINC wiki](http://boinc.berkeley.edu/trac/wiki/GuiRpc).

License
-------
LGPL 3, see [LICENSE](LICENSE)
