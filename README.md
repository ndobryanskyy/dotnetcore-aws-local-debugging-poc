# Debugging .NET Core AWS Lambda Function in VS Code



## Prerequisites and Setup

To run this sample you'll need:

1. [Docker](https://www.docker.com/) 🐋 
2. [VS Code](https://code.visualstudio.com/) 
3. VS Code [C# extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode.csharp).

Also you'll need to download `vsdbg` to you machine in order to mount it later to the running Docker container with the Lambda. You could easily get it  to your home directory by invoking these commands from the repository root (*or anywhere actually*):

```sh
# Script is compatible with powershell and bash

# Create directory to store debugger locally
mkdir $HOME/vsdbg

# Mount this to get built vsdbg for AWS Lambda runtime container on host machine
docker run --rm --mount type=bind,src=$HOME/vsdbg,dst=/vsdbg --entrypoint bash lambci/lambda:dotnetcore2.0 -c "curl -sSL https://aka.ms/getvsdbgsh | bash /dev/stdin -v latest -l /vsdbg"
```

And this sample needs patched version of `lambci/lambda:dotnetcore2.1` [image](https://github.com/lambci/docker-lambda/blob/master/dotnetcore2.1/run/Dockerfile) with enabled debugging support for .NET Core (it will work on 2.0 version too, I took the latest just to showcase).

From repository root run these commands:

```sh
cd AWSDocker

docker build -t me/lambda:dotnetcore2.1 .
cd ../Lambda

# You need VS Code available from PATH for that - this will open IDE
code .
```

Upon successfully running above commands, you should have patched version of AWS .NET Core  Lambda runtime image  tagged  `me/lambda:dotnetcore2.1` with debugging support and compatible `vsdbg` available on your machine under `~/vsdbg` path.

## How to run this sample



### Step 1 - Build .NET Core function 

Open `/Lambda` folder with VS Code. And hit `Ctrl + Shift + B` to get the list of supported build commands. Choose to either publish (build in a way, that it could be run by `dotnet` runtime without SDK) AWS Lambda function using build Docker container or locally  with your SDK. You can examine how these commands are setup in `tasks.json` file.

![Integrated Terminal](/docs/screenshots/integrated-terminal.png?raw=true "Integrated Terminal")

### Step 2 - Invoke the function

Now pop open VS Code integrated terminal by hitting ``Ctrl + ` ``.  We will use use it to invoke our function in a way, that aligns closely with how SAM CLI does it. Run this command:

From `powershell`: 

```powershell
# Invoke AWS Lambda

docker run --rm --mount type=bind,src=$HOME/vsdbg,dst=/vsdbg,readonly --mount type=bind,src=$PWD/out,dst=/var/task,readonly --publish 6000 me/lambda:dotnetcore2.1 Lambda::Lambda.TestFunction::Handler -d "'Debugger Works!'"
```

From `bash`:

```sh
docker run --rm --mount type=bind,src=$HOME/vsdbg,dst=/vsdbg,readonly --mount type=bind,src=$PWD/out,dst=/var/task,readonly --publish 6000 me/lambda:dotnetcore2.1 Lambda::Lambda.TestFunction::Handler -d '"Debugger Works!"'
```

*Note the difference is only in argument quoting*.

![Function invocation](/docs/screenshots/function-invocation.png?raw=true "Function invocation")

After doing that you'll see the output of the runtime: 
`Waiting for the debugger to attach...`

### Step 3 - Attaching

This means, that we are _almost there_. Now we need to __attach__ to the process inside of the running Docker container. Luckily this is very easy - just go to the debugging section of  Visual Studio code and choose __.NET Core Docker Attach__  configuration there. Put some breakpoint in the `TestFunction.cs` `Handler` method and and hit *F5* to start debugging.

At this point VS Code should go into the debug mode turning orange and all and __breakpoint is actually hit__, yay 🎉

![Debugger works!](/docs/screenshots/debugger-works.gif?raw=true "Debugger works!")



# Detailed explanation



## Debugger

The most tricky part about .NET Core remote debugging is that debugger must be installed on the __target__ rather than host machine. In our situation Docker container with AWS Lambda environment is that __target machine__. That said, the first challenge is to get `vsdbg` (our debugger) to target machine somehow.

### Options
 *	Include `vsdbg` installation step to the Dockerfile. Making our runtime environment sort of “debugging ready” by default, thus providing seamless experience to SAM users.
 * Create some additional step in SAM CLI which will check some SAMs internal folder for debugger presence, if no debugger was found run Docker container of runtime with our _install vsdbg_ script and mounted folder to get the output to host machine. After the installation SAM will reuse this debugger.
 * Tell the user to do the same steps of the previous option locally and then supply __debugger_path__ SAM flag. The most inconvenient option as for me, but provides full control over the debugger. _Note: VS Code and Visual Studio 2017 both use vsdbg as remote debugger_

I think the easiest is the first one. Second option requires the biggest effort, but provides consistent UX, and - no need to  touch `Dockerfile`. The third one is the most flexible of all

In this POC I've used the third method. When debugger is downloaded locally and then mounted to  the running container.

### Function invoker - `Program.cs`
Now, as we have debugger on the target machine, all we need to do is talk to it via VS Code and let him attach to our process in the Docker. For all of this to work we need two things:
  * Publish our lambda function in __Debug__ configuration;
  * Halt lambda execution until the debugger is attached.

As for the first point it is straightforward and all, we are just passing `-c Debug` flag to `dotnet publish` command, and that is pretty much it.

For the second point we need some mechanism to wait till the attachment occurs. Sadly `dotnet` does __not__ support this out of the box. Track the issue [here](https://github.com/Microsoft/vscode/issues/32726) and [here](https://github.com/Microsoft/vscode/issues/38327). So  I've started to investigate possible ways of accomplishing this __wait__ and found, that `corerun` ([source](https://github.com/dotnet/coreclr/blob/ef93a727984dbc5b8925a0c2d723be6580d20460/src/coreclr/hosts/corerun/corerun.cpp#L627)) supports waiting for the debugger to attach out of the box with \d flag. I've thrown in the link to the source intentionally, because it actually uses simple `getchar()` to support this feature - go check it out.

But in our case to provide *seamless* experience for the user we will use *infinite loop* which will inspect `Debugger.IsAttached` property on each iteration and timeout after 10 minutes (see [source](AWSDocker/MockBootstraps/DebuggerExtensions.cs)). Thanks @mikemorain and @sanathkr for the feedback to the initial approach and supporting the idea of spin wait.

### Attaching

This POC uses `docker` to perform .NET Core remote debugging and one very neat trick with `docker ps`  [command](https://docs.docker.com/engine/reference/commandline/ps/)  to avoid introducing any changes to SAM CLI and provide user with unified UX across runtimes. SAM CLI internally uses *published port* to provide debugging support for all of its runtimes. But .NET Core debugger is not capable of running in http mode. That is why VS Code C# extension provides `pipeTransport` configuration section to perform remote .NET debugging. User must provide `pipeProgram` which will let VS Code to talk to `vsdbg` located under `debuggerPath` on **target** machine (Docker  container in our case).

I've chosen `docker` to serve as `pipeProgram` via its `exec` command. By supplying `-i` flag we keep the `stdin` open to let VS Code perform its communication. The only unsolved part in this equation is how do we know `container name`  or `container id` to perform`exec`, because SAM CLI does not specifically set those. And the answer is: use `docker ps` with filter!

```sh
docker ps -q -f publish=<debugger_port>
```

`-q`  will make this command print only the id of the container, and `-f publish=<port>` will filter containers based on the published port,pretty neat, right?  This exact trick was used in [launch.json](Lambda/.vscode/launch.json) to get container id for the `docker exec`  command. I've used `powershell`  on windows to get this nested command working (the sample includes configuration for OSX and Linux also):

```json
"pipeTransport": {
    "pipeProgram": "powershell",
    "pipeArgs": [ 
        "-c",
        "docker exec -i $(docker ps -q -f publish=6000) ${debuggerCommand}"
    ],
    "debuggerPath": "/vsdbg/vsdbg",
    "pipeCwd": "${workspaceFolder}",
}
```

As for the `processId` - luckily entry point program always gets PID of 1 in a running container, so no remote  picker required!

### Conclusion
We need to have a clean way to get the debugger on the __target__ machine (Docker container). We can choose any option we like for that and stick to it.
Using infinite loop we've implemented Ad-hoc mechanism to wait for the debugger to attach.
With debugger on board of the target machine and VS Code wonderful `pipeTransport` setup it becomes super easy to attach to Docker container's process which is our AWS Lambda runtime.  ✨✨✨

### P.S.

This is an updated version of the POC, initial one could be found [here](https://github.com/ndobryanskyy/dotnetcore-aws-local-debugging-poc/commit/ddf364c72b5caad4182d805f2c7f31b3b93ede24)