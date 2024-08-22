# spin-picture-analysis

Example repository to recognize a picture and send a notification.

# Building

```console
export PLATFORM=linux-x64
mkdir packages
curl -LO --output-dir packages https://github.com/dicej/spin-dotnet-sdk/releases/download/canary/Fermyon.Spin.SDK.0.1.0-dev.nupkg
curl -LO --output-dir packages https://github.com/dicej/spin-dotnet-sdk/releases/download/canary/Microsoft.DotNet.ILCompiler.LLVM.9.0.0-dev.nupkg
curl -LO --output-dir packages https://github.com/dicej/spin-dotnet-sdk/releases/download/canary/runtime.wasi-wasm.Microsoft.DotNet.ILCompiler.LLVM.9.0.0-dev.nupkg
curl -LO --output-dir packages https://github.com/dicej/spin-dotnet-sdk/releases/download/canary/runtime.$platform.Microsoft.DotNet.ILCompiler.LLVM.9.0.0-dev.nupkg
curl -LO --output-dir . https://github.com/dicej/spin-dotnet-sdk/releases/download/canary/aspnetcore-wasi.zip
unzip aspnetcore-wasi.zip
rm aspnetcore-wasi.zip
spin up --build
```
