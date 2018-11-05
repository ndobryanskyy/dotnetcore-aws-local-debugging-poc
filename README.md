# Debugging .NET Core AWS Lambda function in VS Code

## Run it first, to see how it works

---
### Setup

Build our own version of [AWS dotnetcore2.1 Lambda environment](https://github.com/lambci/docker-lambda/blob/master/dotnetcore2.1/run/Dockerfile)

From repository root run these commands first

```sh
cd AWSDocker
docker build -q -t me/lambci-dotnetcore2.1 .
cd ../Lambda
# You need VS Code available from PATH for that - this  will open IDE
code .
```

As you've opened up the Visual Studio Code, either from above instructions or UI (`/Lambda` folder) go ahead and pop open integrated terminal of IDE.

![Integrated Terminal](/docs/screenshots/integrated-terminal.png?raw=true "Integrated Terminal")

From there we will simulate the SAM CLI and run the container with AWS Lambda environment and execute our function

### Run from integrated terminal
```sh

# Build 
docker run --rm --mount src=$pwd,dst=/var/task,type=bind lambci/lambda:build-dotnetcore2.1 dotnet publish -c Debug -o out

# Also you can opt out to build lambda locally if you have dotnet sdk installed on you machine
# dotnet publish -c Debug -o out

# Invoke AWS Lambda
docker run -i --rm --name debugger --mount src=$pwd/out,dst=/var/task,type=bind,readonly me/lambci-dotnetcore2.1 Lambda::Lambda.TestFunction::Handler -d "'Debugger Works!'"
```

![Function invocation](/docs/screenshots/function-invocation.png?raw=true "Function invocation")

After doing that you'll see the output of the runtime: 
`Press any key after attaching to continue...` we will get  back here later

This means,that we are _almost there_. Now we need to __attach__ to the process inside the running Docker container. Luckily this is very easy - just go to the debugging section of  Visual Studio code and choose __.NET Core Remote Attach__  configuration there. Put some breakpoint in the `TestFunction.cs` `Handler` method and start debugging.

![Start debugger](/docs/screenshots/start-debugger.png?raw=true "Start debugger")

At this point VS Code should go into the debug mode turning orange and all. Now get back to the integrated terminal and finally press any key to start actual Lambda invocation. At this point you'll see, that __breakpoint is actually hit__, yay!

![Debugger works!](/docs/screenshots/debugger-works.gif?raw=true "Debugger works!")

##  Detailed explanation

Is on the way...