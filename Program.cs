// define stack resources
using System.Threading.Tasks;
using Pulumi;
class AzureStack : Pulumi.Stack
{
    public AzureStack()
    {
        // Kreirajte VM resurs
        new VMWithPrivateIPAddress();

        // Kreirajte Azure Function resurs
        new AzureFunctionToVM();
    }
}

class Program
{
    static Task<int> Main(string[] args)
    {

        return Deployment.RunAsync(() =>
        {
            // all resources will be created in one stack azure-pulumi
            var oneStack = new AzureStack();
        });
    }

}