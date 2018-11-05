using System.Security.Cryptography;
using Amazon.Lambda.Core;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace Lambda
{
    /// <summary>
    /// docker run -i --rm --name debugger --mount src=$pwd/out,dst=/var/task,type=bind,readonly me/lambci-dotnetcore2.1 Lambda::Lambda.TestFunction::Handler -d "'Debugger Works!'"
    /// </summary>
    public class TestFunction
    {
        
        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public string Handler(string input, ILambdaContext context)
        {
            return input?.ToUpper();
        }
    }
}