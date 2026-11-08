# LiteSS

A very small Windows screenshot tool written in C# (WinForms, .NET 8).
## Requirements

- Windows
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Build and run

```powershell
dotnet run
```

To produce a release build:

```powershell
dotnet publish -c Release
```

If you enable **Launch at startup**, the registered command points at the executable that is
currently running. Launch the built exe (or a published copy) when you enable it so Windows
starts that copy at sign-in.
