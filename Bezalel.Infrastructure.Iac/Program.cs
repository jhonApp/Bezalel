using Amazon.CDK;

namespace Bezalel.Infrastructure.IaC
{
    sealed class Program
    {
        public static void Main(string[] args)
        {
            var app = new App();
            new BezalelInfrastructureIaCStack(app, "BezalelInfrastructureIaCStack", new StackProps
            {
                Env = new Amazon.CDK.Environment
                {
                    Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
                    Region = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION"),
                }
            });
            app.Synth();
        }
    }
}
