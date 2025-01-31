using System;
using System.Linq;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Common.Tools.GitReleaseManager;
using Cake.Common.Tools.GitReleaseManager.Create;
using Cake.Frosting;
using Common.Utilities;

namespace Release.Tasks
{
    [TaskName(nameof(PublishRelease))]
    [TaskDescription("Publish release")]
    [IsDependentOn(typeof(PublishReleaseInternal))]

    public class PublishRelease : FrostingTask<BuildContext>
    {
    }

    [TaskName(nameof(PublishReleaseInternal))]
    [TaskDescription("Publish release")]
    public class PublishReleaseInternal : FrostingTask<BuildContext>
    {
        public override bool ShouldRun(BuildContext context)
        {
            var shouldRun = true;
            shouldRun &= context.ShouldRun(context.IsGitHubActionsBuild, $"{nameof(PublishRelease)} works only on GitHub Actions.");
            shouldRun &= context.ShouldRun(context.IsStableRelease, $"{nameof(PublishRelease)} works only for releases.");

            return shouldRun;
        }

        public override void Run(BuildContext context)
        {
            var token = context.Credentials?.GitHub?.Token;
            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("Could not resolve GitHub Token.");
            }

            var tarGzFiles = context.GetFiles(Paths.Native + "/*.tar.gz").Select(x => x.ToString()).ToList();
            context.Information("zip count: " + tarGzFiles.Count);

            var assets = string.Join(",", tarGzFiles);

            var milestone = context.Version?.Milestone;

            if (milestone is null) return;

            context.GitReleaseManagerCreate(token, Constants.RepoOwner, Constants.Repository, new GitReleaseManagerCreateSettings
            {
                Milestone = milestone,
                Name = milestone,
                Prerelease = false,
                TargetCommitish = "main"
            });

            context.GitReleaseManagerAddAssets(token, Constants.RepoOwner, Constants.Repository, milestone, assets);
            context.GitReleaseManagerClose(token, Constants.RepoOwner, Constants.Repository, milestone);
        }
    }
}
