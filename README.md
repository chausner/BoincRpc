# BoincRpc
An asynchronous .NET Core implementation of the [BOINC GUI RPC protocol](http://boinc.berkeley.edu/trac/wiki/GuiRpc).

The implementation is up-to-date as of BOINC 7.18.1 and (almost) all RPC structures and commands are fully supported.

Usage
-----
RPC client usage should be largely self-explanatory. For information on the RPC commands, see the [BOINC wiki](http://boinc.berkeley.edu/trac/wiki/GuiRpc).

Connecting, authorizing and printing a list of all tasks:
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

License
-------
LGPL 3, see LICENSE
