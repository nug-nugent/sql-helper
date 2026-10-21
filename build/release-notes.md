## Installing

**SSMS 22 and Visual Studio 2022 / 2026:** download `SqlHelper.VisualStudio.vsix` below, close SSMS and Visual Studio, then double-click the file and tick the products to install into. It upgrades any earlier version and keeps your settings. The installer shows "Digital signature: None" because the extension isn't code-signed; that's expected.

Once installed, use **Extensions > SQL Helper**: **Format SQL** (Ctrl+Alt+F) and **Add Header** (Ctrl+Alt+H). Your initials for headers come from your Windows account name; check them under **Tools > Options > SQL Helper > General**.

**Automatic updates in Visual Studio:** add `https://github.com/nug-nugent/sql-helper/releases/latest/download/atom.xml` under **Tools > Options > Environment > Extensions > Additional Extension Galleries**, and new releases appear under **Extensions > Manage Extensions > Updates**. SSMS doesn't check galleries for updates, so install each new release there by running the `.vsix`.

If the extension doesn't appear after installing, close Visual Studio and run `devenv.exe /updateconfiguration` once.

**Command line:** unzip `sqlhelper-<version>.zip` anywhere and run `sqlhelper format script.sql` or `sqlhelper header proc.sql --initials XX`. Needs the [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0).