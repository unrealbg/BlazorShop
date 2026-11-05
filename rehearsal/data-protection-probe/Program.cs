// Rehearsal-only probe: proves that a backed-up and restored ASP.NET Core Data Protection key ring
// can still unprotect a synthetic value that was protected before the backup. The payload is read from
// stdin and the result is written to stdout, so no key material or payload is written outside the
// mounted key directory.
using System.Security.Cryptography;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

const string ApplicationName = "BlazorShop.StagingRehearsal.DataProtectionProbe";
const string Purpose = "BlazorShop.StagingRehearsal.PersistentVolumeRestore.v1";

if (args.Length != 2 || (args[0] != "protect" && args[0] != "unprotect"))
{
    Console.Error.WriteLine("Usage: protect|unprotect <key-directory> (payload on stdin, result on stdout)");
    return 2;
}

var keyDirectory = new DirectoryInfo(args[1]);
if (!keyDirectory.Exists)
{
    Console.Error.WriteLine($"Key directory '{keyDirectory.FullName}' does not exist.");
    return 2;
}

var services = new ServiceCollection();
services.AddLogging();
var dataProtection = services.AddDataProtection()
    .SetApplicationName(ApplicationName)
    .PersistKeysToFileSystem(keyDirectory);

var unprotect = args[0] == "unprotect";
if (unprotect)
{
    // Unprotect must only use the restored key ring; never create a key that could mask a lost ring.
    dataProtection.DisableAutomaticKeyGeneration();
}

using var provider = services.BuildServiceProvider();
var protector = provider.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose);
var input = (await Console.In.ReadToEndAsync()).Trim();
if (input.Length == 0)
{
    Console.Error.WriteLine("No payload was provided on stdin.");
    return 2;
}

if (!unprotect)
{
    Console.Out.Write(protector.Protect(input));
    return 0;
}

try
{
    Console.Out.Write(protector.Unprotect(input));
    return 0;
}
catch (CryptographicException exception)
{
    Console.Error.WriteLine($"Unprotect failed: {exception.GetType().Name}.");
    return 3;
}
