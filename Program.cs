﻿// define stack resources
using System.Collections.Immutable;
using System.Threading.Tasks;
using Pulumi;
class AzureStack : Stack
{
    public AzureStack()
    {
        // Kreirajte VM resurs - for now i dont use that
        var vmResource = new VMWithPrivateIPAddress();

        // Kreirajte Azure Function resurs
        var functionResource = new AzureFunctionToVM();

        // Register the outputs
        this.PrivateSshKey = vmResource.PrivateSshKey!;
        //this.FunctionEndpoint = functionResource.functionEndpoint!;
        this.WebAppName = functionResource.WebAppName;
    }

    //[Output]
    //public Output<ImmutableDictionary<string, object?>> FunctionEndpoint { get; set; }

    [Output]
    public Output<string> WebAppName { get; set; }
    
    [Output]
    public Output<string> PrivateSshKey { get; set; }
}

class Program
{
    static Task<int> Main() => Deployment.RunAsync<AzureStack>();

}