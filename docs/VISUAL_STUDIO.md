# Visual Studio 2022

The solution is a standard .NET 8 C# solution and opens directly in Visual Studio 2022
17.8 or newer. VS Code is not required.

1. Install the **ASP.NET and web development** workload, .NET 8 SDK and Docker Desktop.
2. Clone the server repository and run `scripts/setup-local.ps1` from PowerShell. The
   script is optional; the manual commands remain in the main README.
3. Open `ConcertBooking.sln` through **File > Open > Project/Solution**.
4. In Solution Explorer, right-click `ConcertBooking.API` and choose
   **Set as Startup Project**.
5. Select the `http` launch profile and press **F5**. Open `/swagger` if the browser
   does not open automatically.
6. Run all tests through **Test > Run All Tests**. PostgreSQL integration tests also
   require `CONCERT_TEST_CONNECTION_STRING`; the setup script defines it only for its
   own test run, so set it in the test process when running those tests later.

The solution folders and project references are preserved exactly as used by the CLI.
Opening it in Visual Studio does not copy, convert or rewrite the application architecture.
Visual Studio may create `.vs`, `*.user` and other personal files; Git ignores them.

The React client remains a separate repository, as required by the assignment. Visual
Studio can open its folder, but it is normally run with `npm install` and `npm run dev`.
