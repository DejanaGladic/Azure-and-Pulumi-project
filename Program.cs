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
    static Task<int> Main() => Deployment.RunAsync<AzureStack>();

}