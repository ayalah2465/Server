using System.Xml.Linq;

namespace ConcertBooking.Application.Tests;

public class ArchitectureTests
{
    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ConcertBooking.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Solution root was not found.");
    }

    [Theory]
    [InlineData("Domain", "")]
    [InlineData("Infrastructure", "Application")]
    [InlineData("Application", "Domain")]
    [InlineData("API", "Application,Infrastructure")]
    public void Project_references_follow_layer_boundaries(string layer, string expected)
    {
        var project = LoadProject(layer);
        var references = project.Descendants("ProjectReference")
            .Select(node => node.Attribute("Include")!.Value.Replace('\\', '/'))
            .Select(path => Path.GetFileNameWithoutExtension(path).Replace("ConcertBooking.", ""))
            .OrderBy(name => name).ToArray();

        Assert.Equal(expected.Split(',', StringSplitOptions.RemoveEmptyEntries), references);
    }

    [Fact]
    public void Domain_has_no_external_package_dependencies()
    {
        Assert.Empty(LoadProject("Domain").Descendants("PackageReference"));
    }

    private static XDocument LoadProject(string layer) => XDocument.Load(
        Path.Combine(FindRoot(), "src", $"ConcertBooking.{layer}", $"ConcertBooking.{layer}.csproj"));
}
