using System;
using System.Collections.Generic;
using GitTools.Testing;
using GitVersion.Core.Tests.Helpers;
using GitVersion.Core.Tests.IntegrationTests;
using GitVersion.Model.Configuration;
using GitVersion.VersionCalculation;
using LibGit2Sharp;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;

namespace GitVersion.Core.Tests.VersionCalculation
{
    public class NextVersionCalculatorTests : TestBase
    {
        [Test]
        public void ShouldIncrementVersionBasedOnConfig()
        {
            var semanticVersionBuildMetaData = new SemanticVersionBuildMetaData("ef7d0d7e1e700f1c7c9fa01ea6791bb778a5c37c", 1, MainBranch, "b1a34edbd80e141f7cc046c074f109be7d022074", "b1a34e", DateTimeOffset.Now, 0);

            var contextBuilder = new GitVersionContextBuilder();

            contextBuilder
                .OverrideServices(services =>
                {
                    var testBaseVersionCalculator = new TestBaseVersionCalculator(true, new SemanticVersion(1), GitToolsTestingExtensions.CreateMockCommit());
                    services.AddSingleton<IBaseVersionCalculator>(testBaseVersionCalculator);
                    services.AddSingleton<IMainlineVersionCalculator>(new TestMainlineVersionCalculator(semanticVersionBuildMetaData));
                })
                .WithConfig(new Config())
                .Build();

            var nextVersionCalculator = contextBuilder.ServicesProvider.GetService<INextVersionCalculator>();
            nextVersionCalculator.ShouldNotBeNull();

            var version = nextVersionCalculator.FindVersion();

            version.ToString().ShouldBe("1.0.1");
        }

        [Test]
        public void DoesNotIncrementWhenBaseVersionSaysNotTo()
        {
            var semanticVersionBuildMetaData = new SemanticVersionBuildMetaData("ef7d0d7e1e700f1c7c9fa01ea6791bb778a5c37c", 1, MainBranch, "b1a34edbd80e141f7cc046c074f109be7d022074", "b1a34e", DateTimeOffset.Now, 0);

            var contextBuilder = new GitVersionContextBuilder();

            contextBuilder
                .OverrideServices(services =>
                {
                    var testBaseVersionCalculator = new TestBaseVersionCalculator(false, new SemanticVersion(1), GitToolsTestingExtensions.CreateMockCommit());
                    services.AddSingleton<IBaseVersionCalculator>(testBaseVersionCalculator);
                    services.AddSingleton<IMainlineVersionCalculator>(new TestMainlineVersionCalculator(semanticVersionBuildMetaData));
                })
                .WithConfig(new Config())
                .Build();

            var nextVersionCalculator = contextBuilder.ServicesProvider.GetService<INextVersionCalculator>();

            nextVersionCalculator.ShouldNotBeNull();

            var version = nextVersionCalculator.FindVersion();

            version.ToString().ShouldBe("1.0.0");
        }

        [Test]
        public void AppliesBranchPreReleaseTag()
        {
            var semanticVersionBuildMetaData = new SemanticVersionBuildMetaData("ef7d0d7e1e700f1c7c9fa01ea6791bb778a5c37c", 2, "develop", "b1a34edbd80e141f7cc046c074f109be7d022074", "b1a34e", DateTimeOffset.Now, 0);
            var contextBuilder = new GitVersionContextBuilder();

            contextBuilder
                .OverrideServices(services =>
                {
                    var testBaseVersionCalculator = new TestBaseVersionCalculator(false, new SemanticVersion(1), GitToolsTestingExtensions.CreateMockCommit());
                    services.AddSingleton<IBaseVersionCalculator>(testBaseVersionCalculator);
                    services.AddSingleton<IMainlineVersionCalculator>(new TestMainlineVersionCalculator(semanticVersionBuildMetaData));
                })
                .WithDevelopBranch()
                .Build();

            var nextVersionCalculator = contextBuilder.ServicesProvider.GetService<INextVersionCalculator>();
            nextVersionCalculator.ShouldNotBeNull();

            var version = nextVersionCalculator.FindVersion();

            version.ToString("f").ShouldBe("1.0.0-alpha.1+2");
        }

        [Test]
        public void PreReleaseTagCanUseBranchName()
        {
            var config = new Config
            {
                NextVersion = "1.0.0",
                Branches = new Dictionary<string, BranchConfig>
                {
                    {
                        "custom", new BranchConfig
                        {
                            Regex = "custom/",
                            Tag = "useBranchName",
                            SourceBranches = new HashSet<string>()
                        }
                    }
                }
            };

            using var fixture = new EmptyRepositoryFixture();
            fixture.MakeACommit();
            fixture.BranchTo("develop");
            fixture.MakeACommit();
            fixture.BranchTo("custom/foo");
            fixture.MakeACommit();

            fixture.AssertFullSemver("1.0.0-foo.1+2", config);
        }

        [Test]
        public void PreReleaseVersionMainline()
        {
            var config = new Config
            {
                VersioningMode = VersioningMode.Mainline,
                NextVersion = "1.0.0"
            };

            using var fixture = new EmptyRepositoryFixture();
            fixture.MakeACommit();
            fixture.BranchTo("foo");
            fixture.MakeACommit();

            fixture.AssertFullSemver("1.0.0-foo.1", config);
        }

        [Test]
        public void MergeIntoMainline()
        {
            var config = new Config
            {
                VersioningMode = VersioningMode.Mainline,
                NextVersion = "1.0.0"
            };

            using var fixture = new EmptyRepositoryFixture();
            fixture.MakeACommit();
            fixture.BranchTo("foo");
            fixture.MakeACommit();
            fixture.Checkout(MainBranch);
            fixture.MergeNoFF("foo");

            fixture.AssertFullSemver("1.0.0", config);
        }

        [Test]
        public void MergeFeatureIntoMainline()
        {
            var config = new Config
            {
                VersioningMode = VersioningMode.Mainline
            };

            using var fixture = new EmptyRepositoryFixture();
            fixture.MakeACommit();
            fixture.ApplyTag("1.0.0");
            fixture.AssertFullSemver("1.0.0", config);

            fixture.BranchTo("feature/foo");
            fixture.MakeACommit();
            fixture.AssertFullSemver("1.0.1-foo.1", config);
            fixture.ApplyTag("1.0.1-foo.1");

            fixture.Checkout(MainBranch);
            fixture.MergeNoFF("feature/foo");
            fixture.AssertFullSemver("1.0.1", config);
        }

        [Test]
        public void MergeFeatureIntoMainlineWithMinorIncrement()
        {
            var config = new Config
            {
                VersioningMode = VersioningMode.Mainline,
                Branches = new Dictionary<string, BranchConfig>()
                {
                    { "feature", new BranchConfig { Increment = IncrementStrategy.Minor } }
                },
                Ignore = new IgnoreConfig() { ShAs = new List<string>() },
                MergeMessageFormats = new Dictionary<string, string>()
            };

            using var fixture = new EmptyRepositoryFixture();
            fixture.MakeACommit();
            fixture.ApplyTag("1.0.0");
            fixture.AssertFullSemver("1.0.0", config);

            fixture.BranchTo("feature/foo");
            fixture.MakeACommit();
            fixture.AssertFullSemver("1.1.0-foo.1", config);
            fixture.ApplyTag("1.1.0-foo.1");

            fixture.Checkout(MainBranch);
            fixture.MergeNoFF("feature/foo");
            fixture.AssertFullSemver("1.1.0", config);
        }

        [Test]
        public void MergeFeatureIntoMainlineWithMinorIncrementAndThenMergeHotfix()
        {
            var config = new Config
            {
                VersioningMode = VersioningMode.Mainline,
                Branches = new Dictionary<string, BranchConfig>()
                {
                    { "feature", new BranchConfig { Increment = IncrementStrategy.Minor } }
                },
                Ignore = new IgnoreConfig() { ShAs = new List<string>() },
                MergeMessageFormats = new Dictionary<string, string>()
            };

            using var fixture = new EmptyRepositoryFixture();
            fixture.MakeACommit();
            fixture.ApplyTag("1.0.0");
            fixture.AssertFullSemver("1.0.0", config);

            fixture.BranchTo("feature/foo");
            fixture.MakeACommit();
            fixture.AssertFullSemver("1.1.0-foo.1", config);
            fixture.ApplyTag("1.1.0-foo.1");

            fixture.Checkout(MainBranch);
            fixture.MergeNoFF("feature/foo");
            fixture.AssertFullSemver("1.1.0", config);
            fixture.ApplyTag("1.1.0");

            fixture.BranchTo("hotfix/bar");
            fixture.MakeACommit();
            fixture.AssertFullSemver("1.1.1-beta.1", config);
            fixture.ApplyTag("1.1.1-beta.1");

            fixture.Checkout(MainBranch);
            fixture.MergeNoFF("hotfix/bar");
            fixture.AssertFullSemver("1.1.1", config);
        }

        [Test]
        public void PreReleaseTagCanUseBranchNameVariable()
        {
            var config = new Config
            {
                NextVersion = "1.0.0",
                Branches = new Dictionary<string, BranchConfig>
                {
                    {
                        "custom", new BranchConfig
                        {
                            Regex = "custom/",
                            Tag = "alpha.{BranchName}",
                            SourceBranches = new HashSet<string>()
                        }
                    }
                }
            };

            using var fixture = new EmptyRepositoryFixture();
            fixture.MakeACommit();
            fixture.BranchTo("develop");
            fixture.MakeACommit();
            fixture.BranchTo("custom/foo");
            fixture.MakeACommit();

            fixture.AssertFullSemver("1.0.0-alpha.foo.1+2", config);
        }

        [Test]
        public void PreReleaseNumberShouldBeScopeToPreReleaseLabelInContinuousDelivery()
        {
            var config = new Config
            {
                VersioningMode = VersioningMode.ContinuousDelivery,
                Branches = new Dictionary<string, BranchConfig>
                {
                    {
                        MainBranch, new BranchConfig
                        {
                            Tag = "beta"
                        }
                    },
                }
            };

            using var fixture = new EmptyRepositoryFixture();
            fixture.Repository.MakeACommit();

            fixture.Repository.CreateBranch("feature/test");
            Commands.Checkout(fixture.Repository, "feature/test");
            fixture.Repository.MakeATaggedCommit("0.1.0-test.1");
            fixture.Repository.MakeACommit();

            fixture.AssertFullSemver("0.1.0-test.2+2", config);

            Commands.Checkout(fixture.Repository, MainBranch);
            fixture.Repository.Merge("feature/test", Generate.SignatureNow());

            fixture.AssertFullSemver("0.1.0-beta.1+2", config);
        }

        [Test]
        public void GetNextVersionOnNonMainlineBranchWithoutCommitsShouldWorkNormally()
        {
            var config = new Config
            {
                VersioningMode = VersioningMode.Mainline,
                NextVersion = "1.0.0"
            };

            using var fixture = new EmptyRepositoryFixture();
            fixture.MakeACommit("initial commit");
            fixture.BranchTo("feature/f1");
            fixture.AssertFullSemver("1.0.0-f1.0", config);
        }
    }
}
