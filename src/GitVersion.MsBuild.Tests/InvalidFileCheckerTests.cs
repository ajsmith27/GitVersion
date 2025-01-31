using System;
using System.IO;
using GitVersion.Core.Tests.Helpers;
using GitVersion.MsBuild.Tests.Mocks;
using Microsoft.Build.Framework;
using NUnit.Framework;

namespace GitVersion.MsBuild.Tests
{
    [TestFixture]
    public class InvalidFileCheckerTests : TestBase
    {
        private string projectDirectory;
        private string projectFile;

        [SetUp]
        public void CreateTemporaryProject()
        {
            this.projectDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            this.projectFile = Path.Combine(this.projectDirectory, "Fake.csproj");

            Directory.CreateDirectory(this.projectDirectory);

            File.Create(this.projectFile).Close();
        }

        [TearDown]
        public void Cleanup() => Directory.Delete(this.projectDirectory, true);

        [Test]
        public void VerifyIgnoreNonAssemblyInfoFile()
        {
            using (var writer = File.CreateText(Path.Combine(this.projectDirectory, "SomeOtherFile.cs")))
            {
                writer.Write(@"
using System;
using System.Reflection;

[assembly: AssemblyVersion(""1.0.0.0"")]
");
            }

            FileHelper.CheckForInvalidFiles(new ITaskItem[] { new MockTaskItem { ItemSpec = "SomeOtherFile.cs" } }, this.projectFile);
        }

        [Test]
        public void VerifyAttributeFoundCSharp([Values("AssemblyVersion", "AssemblyFileVersion", "AssemblyInformationalVersion", "System.Reflection.AssemblyVersion")] string attribute)
        {
            using (var writer = File.CreateText(Path.Combine(this.projectDirectory, "AssemblyInfo.cs")))
            {
                writer.Write(@"
using System;
using System.Reflection;

[assembly:{0}(""1.0.0.0"")]
", attribute);
            }

            var ex = Assert.Throws<WarningException>(() => FileHelper.CheckForInvalidFiles(new ITaskItem[] { new MockTaskItem { ItemSpec = "AssemblyInfo.cs" } }, this.projectFile), attribute);
            Assert.That(ex.Message, Is.EqualTo("File contains assembly version attributes which conflict with the attributes generated by GitVersion AssemblyInfo.cs"));
        }

        [Test]
        public void VerifyUnformattedAttributeFoundCSharp([Values("AssemblyVersion", "AssemblyFileVersion", "AssemblyInformationalVersion", "System . Reflection   .   AssemblyVersion")] string attribute)
        {
            using (var writer = File.CreateText(Path.Combine(this.projectDirectory, "AssemblyInfo.cs")))
            {
                writer.Write(@"
using System;
using System.Reflection;

[  assembly   :
{0}     ( ""1.0.0.0"")]
", attribute);
            }

            var ex = Assert.Throws<WarningException>(() => FileHelper.CheckForInvalidFiles(new ITaskItem[] { new MockTaskItem { ItemSpec = "AssemblyInfo.cs" } }, this.projectFile), attribute);
            Assert.That(ex.Message, Is.EqualTo("File contains assembly version attributes which conflict with the attributes generated by GitVersion AssemblyInfo.cs"));
        }

        [Test]
        public void VerifyCommentWorksCSharp([Values("AssemblyVersion", "AssemblyFileVersion", "AssemblyInformationalVersion")] string attribute)
        {
            using (var writer = File.CreateText(Path.Combine(this.projectDirectory, "AssemblyInfo.cs")))
            {
                writer.Write(@"
using System;
using System.Reflection;

//[assembly: {0}(""1.0.0.0"")]
", attribute);
            }

            FileHelper.CheckForInvalidFiles(new ITaskItem[] { new MockTaskItem { ItemSpec = "AssemblyInfo.cs" } }, this.projectFile);
        }

        [Test]
        public void VerifyCommentWithNoNewLineAtEndWorksCSharp([Values("AssemblyVersion", "AssemblyFileVersion", "AssemblyInformationalVersion")] string attribute)
        {
            using (var writer = File.CreateText(Path.Combine(this.projectDirectory, "AssemblyInfo.cs")))
            {
                writer.Write(@"
using System;
using System.Reflection;

//[assembly: {0}(""1.0.0.0"")]", attribute);
            }

            FileHelper.CheckForInvalidFiles(new ITaskItem[] { new MockTaskItem { ItemSpec = "AssemblyInfo.cs" } }, this.projectFile);
        }

        [Test]
        public void VerifyStringWorksCSharp([Values("AssemblyVersion", "AssemblyFileVersion", "AssemblyInformationalVersion")] string attribute)
        {
            using (var writer = File.CreateText(Path.Combine(this.projectDirectory, "AssemblyInfo.cs")))
            {
                writer.Write(@"
using System;
using System.Reflection;

public class Temp
{{
    static const string Foo = ""[assembly: {0}(""""1.0.0.0"""")]"";
}}
", attribute);
            }

            FileHelper.CheckForInvalidFiles(new ITaskItem[] { new MockTaskItem { ItemSpec = "AssemblyInfo.cs" } }, this.projectFile);
        }

        [Test]
        public void VerifyIdentifierWorksCSharp([Values("AssemblyVersion", "AssemblyFileVersion", "AssemblyInformationalVersion")] string attribute)
        {
            using (var writer = File.CreateText(Path.Combine(this.projectDirectory, "AssemblyInfo.cs")))
            {
                writer.Write(@"
using System;
using System.Reflection;

public class {0}
{{
}}
", attribute);
            }

            FileHelper.CheckForInvalidFiles(new ITaskItem[] { new MockTaskItem { ItemSpec = "AssemblyInfo.cs" } }, this.projectFile);
        }

        [Test]
        public void VerifyAttributeFoundVisualBasic([Values("AssemblyVersion", "AssemblyFileVersion", "AssemblyInformationalVersion", "System.Reflection.AssemblyVersion")] string attribute)
        {
            using (var writer = File.CreateText(Path.Combine(this.projectDirectory, "AssemblyInfo.vb")))
            {
                writer.Write(@"
Imports System
Imports System.Reflection

<Assembly:{0}(""1.0.0.0"")>
", attribute);
            }

            var ex = Assert.Throws<WarningException>(() => FileHelper.CheckForInvalidFiles(new ITaskItem[] { new MockTaskItem { ItemSpec = "AssemblyInfo.vb" } }, this.projectFile), attribute);
            Assert.That(ex.Message, Is.EqualTo("File contains assembly version attributes which conflict with the attributes generated by GitVersion AssemblyInfo.vb"));
        }

        [Test]
        public void VerifyUnformattedAttributeFoundVisualBasic([Values("AssemblyVersion", "AssemblyFileVersion", "AssemblyInformationalVersion", "System . Reflection   .   AssemblyVersion")] string attribute)
        {
            using (var writer = File.CreateText(Path.Combine(this.projectDirectory, "AssemblyInfo.vb")))
            {
                writer.Write(@"
Imports System
Imports System.Reflection

<  Assembly   :
{0}     ( ""1.0.0.0"")>
", attribute);
            }

            var ex = Assert.Throws<WarningException>(() => FileHelper.CheckForInvalidFiles(new ITaskItem[] { new MockTaskItem { ItemSpec = "AssemblyInfo.vb" } }, this.projectFile), attribute);
            Assert.That(ex.Message, Is.EqualTo("File contains assembly version attributes which conflict with the attributes generated by GitVersion AssemblyInfo.vb"));
        }

        [Test]
        public void VerifyCommentWorksVisualBasic([Values("AssemblyVersion", "AssemblyFileVersion", "AssemblyInformationalVersion")] string attribute)
        {
            using (var writer = File.CreateText(Path.Combine(this.projectDirectory, "AssemblyInfo.vb")))
            {
                writer.Write(@"
Imports System
Imports System.Reflection

'<Assembly: {0}(""1.0.0.0"")>
", attribute);
            }

            FileHelper.CheckForInvalidFiles(new ITaskItem[] { new MockTaskItem { ItemSpec = "AssemblyInfo.vb" } }, this.projectFile);
        }

        [Test]
        public void VerifyCommentWithNoNewLineAtEndWorksVisualBasic([Values("AssemblyVersion", "AssemblyFileVersion", "AssemblyInformationalVersion")] string attribute)
        {
            using (var writer = File.CreateText(Path.Combine(this.projectDirectory, "AssemblyInfo.vb")))
            {
                writer.Write(@"
Imports System
Imports System.Reflection

'<Assembly: {0}(""1.0.0.0"")>", attribute);
            }

            FileHelper.CheckForInvalidFiles(new ITaskItem[] { new MockTaskItem { ItemSpec = "AssemblyInfo.vb" } }, this.projectFile);
        }

        [Test]
        public void VerifyStringWorksVisualBasic([Values("AssemblyVersion", "AssemblyFileVersion", "AssemblyInformationalVersion")] string attribute)
        {
            using (var writer = File.CreateText(Path.Combine(this.projectDirectory, "AssemblyInfo.vb")))
            {
                writer.Write(@"
Imports System
Imports System.Reflection

Public Class Temp
    static const string Foo = ""<Assembly: {0}(""""1.0.0.0"""")>"";
End Class
", attribute);
            }

            FileHelper.CheckForInvalidFiles(new ITaskItem[] { new MockTaskItem { ItemSpec = "AssemblyInfo.vb" } }, this.projectFile);
        }

        [Test]
        public void VerifyIdentifierWorksVisualBasic([Values("AssemblyVersion", "AssemblyFileVersion", "AssemblyInformationalVersion")] string attribute)
        {
            using (var writer = File.CreateText(Path.Combine(this.projectDirectory, "AssemblyInfo.vb")))
            {
                writer.Write(@"
Imports System
Imports System.Reflection

Public Class {0}
End Class
", attribute);
            }

            FileHelper.CheckForInvalidFiles(new ITaskItem[] { new MockTaskItem { ItemSpec = "AssemblyInfo.vb" } }, this.projectFile);
        }
    }
}
