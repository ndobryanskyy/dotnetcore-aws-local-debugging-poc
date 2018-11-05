using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using AWSLambda.Internal.Bootstrap;
using AWSLambda.Internal.Bootstrap.Context;

namespace MockLambdaRuntime
{
    class Program
    {
        /// Task root of lambda task
        private static readonly string lambdaTaskRoot 
            = EnvHelper.GetOrDefault("LAMBDA_TASK_ROOT", "/var/task");

        private static bool _shouldWaitForDebugger;

        /// Program entry point
        static void Main(string[] args)
        {
            AssemblyLoadContext.Default.Resolving += OnAssemblyResolving;

            var positionalArgs = new List<string>(args.Length);
            
            foreach (var arg in args)
            {
                // Handle the flags
                if (arg.StartsWith("-"))
                {
                    if (MemoryExtensions.Equals(arg.AsSpan(1), "d", StringComparison.Ordinal))
                    {
                        _shouldWaitForDebugger = true;
                    }

                    continue;
                }

                positionalArgs.Add(arg);
            }
            
            var handler = GetFunctionHandler(positionalArgs);
            var body = GetEventBody(positionalArgs);

            Console.WriteLine(body);

            var lambdaContext = new MockLambdaContext(handler, body);

            var userCodeLoader = new UserCodeLoader(handler, InternalLogger.NO_OP_LOGGER);
            userCodeLoader.Init(Console.Error.WriteLine);

            var lambdaContextInternal = new LambdaContextInternal(lambdaContext.RemainingTime,
                                                                  LogAction, new Lazy<CognitoClientContextInternal>(),
                                                                  lambdaContext.RequestId,
                                                                  new Lazy<string>(lambdaContext.Arn),
                                                                  new Lazy<string>(string.Empty),
                                                                  new Lazy<string>(string.Empty),
                                                                  Environment.GetEnvironmentVariables());

            Exception lambdaException = null;

            if (_shouldWaitForDebugger)
            {
                if (!Debugger.IsAttached)
                {
                    int? processId = null;
                    try
                    {
                        processId = Process.GetCurrentProcess().Id;
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine($"Filed to retrieve PID: {ex}");
                    }

                    Console.WriteLine($"Runtime started, waiting for debugger{(processId != null ? " to attach to " + processId + " PID" : string.Empty)}");
                    Console.WriteLine("Press any key after attaching to continue...");
                    Console.Read();

                    if (!Debugger.IsAttached)
                    {
                        Console.Error.WriteLine("Debugger failed to attach, terminating");
                        return;
                    }
                }
            }

            LogRequestStart(lambdaContext);
            try
            {
                userCodeLoader.Invoke(lambdaContext.InputStream, lambdaContext.OutputStream, lambdaContextInternal);
            }
            catch (Exception ex)
            {
                lambdaException = ex;
            }
            LogRequestEnd(lambdaContext);

            if (lambdaException == null)
            {
                Console.WriteLine(lambdaContext.OutputText);
            }
            else
            {
                Console.Error.WriteLine(lambdaException);
            }
        }

        /// Called when an assembly could not be resolved
        private static Assembly OnAssemblyResolving(AssemblyLoadContext context, AssemblyName assembly)
        {
            return context.LoadFromAssemblyPath(Path.Combine(lambdaTaskRoot, $"{assembly.Name}.dll"));
        }

        /// Try to log everything to stderr except the function result
        private static void LogAction(string text)
        {
            Console.Error.WriteLine(text);
        }

        static void LogRequestStart(MockLambdaContext context)
        {
            Console.Error.WriteLine($"START RequestId: {context.RequestId} Version: {context.FunctionVersion}");
        }

        static void LogRequestEnd(MockLambdaContext context)
        {
            Console.Error.WriteLine($"END  RequestId: {context.RequestId}");

            Console.Error.WriteLine($"REPORT RequestId {context.RequestId}\t" +
                                    $"Duration: {context.Duration} ms\t" +
                                    $"Billed Duration: {context.BilledDuration} ms\t" +
                                    $"Memory Size {context.MemorySize} MB\t" +
                                    $"Max Memory Used: {context.MemoryUsed / (1024 * 1024)} MB");
        }

        /// Gets the function handler from arguments or environment
        static string GetFunctionHandler(IReadOnlyList<string> args)
        {
            return args.Count > 0 ? args[0] : EnvHelper.GetOrDefault("AWS_LAMBDA_FUNCTION_HANDLER", string.Empty);
        }

        /// Gets the event body from arguments or environment
        static string GetEventBody(IReadOnlyList<string> args)
        {
            return args.Count > 1 ? args[1] : (Environment.GetEnvironmentVariable("AWS_LAMBDA_EVENT_BODY") ??
              (Environment.GetEnvironmentVariable("DOCKER_LAMBDA_USE_STDIN") != null ? Console.In.ReadToEnd() : "'{}'"));
        }
    }

    class EnvHelper
    {
        /// Gets the given environment variable with a fallback if it doesn't exist
        public static string GetOrDefault(string name, string fallback)
        {
            return Environment.GetEnvironmentVariable(name) ?? fallback;
        }
    }
}
