# spin-picture-analysis

Example repository to recognize a picture and send a notification.

## Building

```console
export PLATFORM=linux-x64
mkdir packages
curl -LO --output-dir packages https://github.com/dicej/spin-dotnet-sdk/releases/download/canary/Fermyon.Spin.SDK.0.1.0-dev.nupkg
curl -LO --output-dir packages https://github.com/dicej/spin-dotnet-sdk/releases/download/canary/Microsoft.DotNet.ILCompiler.LLVM.9.0.0-dev.nupkg
curl -LO --output-dir packages https://github.com/dicej/spin-dotnet-sdk/releases/download/canary/runtime.wasi-wasm.Microsoft.DotNet.ILCompiler.LLVM.9.0.0-dev.nupkg
curl -LO --output-dir packages https://github.com/dicej/spin-dotnet-sdk/releases/download/canary/runtime.$PLATFORM.Microsoft.DotNet.ILCompiler.LLVM.9.0.0-dev.nupkg
curl -LO https://github.com/dicej/spin-dotnet-sdk/releases/download/canary/aspnetcore-wasi.zip
unzip aspnetcore-wasi.zip
rm aspnetcore-wasi.zip
dapr run -f .
```

Once all of your services are running, run:

```console
curl -XPOST localhost:9000/api/v1.0/computervision -H 'Content-Type: application/json' --data '{"fileReference": "foo"}' -i
```

Or:

```
curl localhost:9001/api/v1.0/File/hello.txt
```

## Notable changes

Dapr's `InvokeBindingAsync` uses the gRPC protocol to invoke output bindings. Due to the lack of
HTTP/2 or `System.Net.Sockets` support, we cannot use this protocol at this time. Fortunately, dapr
[exposes output bindings via the HTTP
API](https://docs.dapr.io/developing-applications/building-blocks/bindings/howto-bindings/).
