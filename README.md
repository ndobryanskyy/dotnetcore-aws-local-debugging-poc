# Debugging .NET Core AWS Lambda function in VS Code

## Setup and prerequisites

### Step 0 - Setup your machine
You will need [Docker](https://www.docker.com/) 🐋 and [VS Code](https://code.visualstudio.com/) with installed [C# extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode.csharp) to run this sample.

<br/>

### Step 1 - Build your own [AWS dotnetcore2.1 Lambda environment](https://github.com/lambci/docker-lambda/blob/master/dotnetcore2.1/run/Dockerfile)

From repository root run these commands

```sh
cd AWSDocker
docker build -q -t me/lambci-dotnetcore2.1 .
cd ../Lambda
# You need VS Code available from PATH for that - this  will open IDE
code .
```
<br/>

### Step 2 - Invoke the function

As you've opened up the Visual Studio Code, either from above instructions or UI (`/Lambda` folder) go ahead and pop open integrated terminal of IDE.

![Integrated Terminal](/docs/screenshots/integrated-terminal.png?raw=true "Integrated Terminal")

From this terminal we will simulate the SAM CLI and run the container with AWS Lambda environment to execute our function.
```sh
# Build 
docker run --rm --mount src=$(pwd),dst=/var/task,type=bind lambci/lambda:build-dotnetcore2.1 dotnet publish -c Debug -o out

# Also you can opt out to build lambda locally if you have dotnet sdk installed on you machine
# dotnet publish -c Debug -o out

# Invoke AWS Lambda
docker run -i --rm --name debugger --mount src=$(pwd)/out,dst=/var/task,type=bind,readonly me/lambci-dotnetcore2.1 Lambda::Lambda.TestFunction::Handler -d "'Debugger Works!'"
```

![Function invocation](/docs/screenshots/function-invocation.png?raw=true "Function invocation")

After doing that you'll see the output of the runtime: 
`Press any key after attaching to continue...` we will get  back here later

<br/>

### Step 3 - Attaching

This means,that we are _almost there_. Now we need to __attach__ to the process inside the running Docker container. Luckily this is very easy - just go to the debugging section of  Visual Studio code and choose __.NET Core Remote Attach__  configuration there. Put some breakpoint in the `TestFunction.cs` `Handler` method and start debugging.

At this point VS Code should go into the debug mode turning orange and all. Now get back to the integrated terminal and finally press any key to start actual Lambda invocation. At this point you'll see, that __breakpoint is actually hit__, yay 🎉!

![Debugger works!](/docs/screenshots/debugger-works.gif?raw=true "Debugger works!")
# Detailed explanation

## Debugger

The most tricky part about .NET Core remote debugging is that debugger must be installed on the __target__ rather than host machine. In our situation Docker container with AWS Lambda environment is that __target machine__. That said, the first challenge is to get `vsdbg` (our debugger) to target machine somehow.

### Options
 *	Include `vsdbg` installation step to the Dockerfile. Making our runtime environment sort of “debugging ready” by default, thus providing seamless experience to SAM users.
 * Create some additional step in SAM CLI which will check some SAMs internal folder for debugger presence, if no debugger was found run Docker container of runtime with our _install vsdbg_ script and mounted folder to get the output to host machine. After the installation SAM will reuse this debugger.
 * Tell the user to do the same steps of the previous option locally and then supply __debuggerPath__ SAM flag. The most inconvenient option as for me, but provides full control over the debugger. _Note: VS Code and Visual Studio 2017 both use vsdbg as remote debugger_

I think the easiest is the first one. Second option requires the biggest effort, but provides consistent UX, and - no need to  touch Dockerfile. The third one is the most flexible of all, but the UX is - _meh_.
 
In this POC I've used the first method. And AWS container simply downloads and installs `vsdbg`.
 
 ```sh
 curl -sSL https://aka.ms/getvsdbgsh | bash /dev/stdin -v latest -l /vsdbg
 ```
 
### Function invoker - Program.cs
Now, as we have debugger on the target machine, all we need to do is talk to it via VS Code and let him attach to our process in the Docker. For all of this to work we need two things:
  * Publish our lambda function in __Debug__ configuration;
  * Halt lambda execution until the debugger is attached.
  
As for the first point it is straightforward and all, we are just passing `-c Debug` flag to `dotnet publish` command, and that is pretty much it.
 
For the second point we need some mechanism to wait till the attachment occurs. Sadly `dotnet` does __not__ support this out of the box. Track the issue [here](https://github.com/Microsoft/vscode/issues/32726) and [here](https://github.com/Microsoft/vscode/issues/38327). So  I've started to investigate possible ways of accomplishing this __wait__ and found, that `corerun` ([source](https://github.com/dotnet/coreclr/blob/ef93a727984dbc5b8925a0c2d723be6580d20460/src/coreclr/hosts/corerun/corerun.cpp#L627)) supports waiting for the debugger to attach out of the box with \d flag. I've thrown in the link to the source intentionally, because it actually uses simple `getchar()` to support this feature - go check  it out. And so does my modified version of Program.cs look at the [source](https://github.com/ndobryanskyy/dotnetcore-aws-local-debugging-poc/blob/c09aaa2453a792d3d14a1ab8e9667083c9594d65/AWSDocker/MockBootstraps/Program.cs#L61) - it waits until you press any button - effectively write something into the stdin. All you need to do is supply `-d` flag (we can discuss the flag and details).

### Attaching
Now, as the Docker container with our Bootsrapper and Lambda code is up and waits for `any key` it is the perfect time to attach to it via VS Code. Inspect [launch.json](https://github.com/ndobryanskyy/dotnetcore-aws-local-debugging-poc/blob/master/Lambda/.vscode/launch.json) the notable part is - `pipeArgs": [ "exec -i debugger"]` where __"debugger"__ is the name of the container with our Lambda running. We've used __docker__ as our pipe program directly. As for the __processId__, luckily the entrypoint process of the docker continer always gets the PID of 1. With this set up, everything will work like a charm!
  
### Conclusion
We need to have a clean way to get the debugger on the __target__ machine (Docker container). We can choose any option we like for that and stick to it.
With a small hack (`Console.Read()`) we've implemented the mechanism to wait for debugger to attach.
With debugger on board of the target machine and VS Code wonderful pipeTransport setup it becomes super easy to attach to Docker container's process which is our AWS Lambda runtime.
Then we just happily `Press any key to continue...` and the magic happens ✨✨✨
