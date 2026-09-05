using System.Diagnostics;
using System.Text.RegularExpressions;
using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// Living Spec matrix for Issue Set M6-09 AC 1–16 (MVP production acceptance).
/// Docs + packaging dry-run only; no live CHR; does not create git tags.
/// </summary>
public sealed class MvpReleaseAcceptanceLivingSpecTests
{
    private static string RepoRoot
    {
        get
        {
            DirectoryInfo? dir = new(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "MikroTikFirewallController.sln")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new InvalidOperationException("Repository root not found from test base directory.");
        }
    }

    private static string Read(params string[] relativeParts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot }.Concat(relativeParts).ToArray()));

    private static void AssertFile(params string[] relativeParts)
    {
        string path = Path.Combine(new[] { RepoRoot }.Concat(relativeParts).ToArray());
        Assert.True(File.Exists(path), $"Missing required file: {string.Join('/', relativeParts)}");
    }

    // ── AC 1 ──────────────────────────────────────────────────────────────────────

    /// <summary>M0–M6 + N1-07 closed in ROADMAP / ISSUES; MVP CLOSED; post-MVP M7 closed; §3.C NEXT advances.</summary>
    [Fact]
    public void Ac1M0ThroughM6IssuesAreClosedInRoadmap()
    {
        string roadmap = Read("ROADMAP.md");
        string issues = Read("ISSUES.md");

        Assert.Contains("M6 CLOSED", roadmap, StringComparison.Ordinal);
        Assert.Contains("MVP CLOSED", roadmap, StringComparison.Ordinal);
        Assert.Contains("M6-09", roadmap, StringComparison.Ordinal);
        Assert.Contains("N1-07", roadmap, StringComparison.Ordinal);
        Assert.Contains("M7.1 CLOSED", roadmap, StringComparison.Ordinal);
        Assert.Contains("M7.2 CLOSED", roadmap, StringComparison.Ordinal);
        Assert.Contains("M7.3 CLOSED", roadmap, StringComparison.Ordinal);
        Assert.Contains("M7.4 CLOSED", roadmap, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-20 (#439)", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-06", roadmap, StringComparison.Ordinal);
        Assert.Contains("SEC-06", roadmap, StringComparison.Ordinal);
        Assert.Contains("SEC-11", roadmap, StringComparison.Ordinal);
        Assert.Contains("SEC-12", roadmap, StringComparison.Ordinal);
        Assert.Contains("SEC-13", roadmap, StringComparison.Ordinal);
        Assert.Contains("SEC-14", roadmap, StringComparison.Ordinal);
        Assert.Contains("SEC-15", roadmap, StringComparison.Ordinal);
        Assert.Contains("W5-03", roadmap, StringComparison.Ordinal);
        Assert.Contains("W6-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W5-02", roadmap, StringComparison.Ordinal);
        Assert.Contains("W5-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("CONT-02", roadmap, StringComparison.Ordinal);

        // Logical ID → GitHub mapping lives in ISSUES.md after ROADMAP §3 strikethrough purge.
        foreach ((string id, string issue) in new[]
                 {
                     ("M6-09", "#108"), ("N1-07", "#109"),
                     ("M7.1-01", "#110"), ("M7.1-02", "#111"), ("M7.1-03", "#112"),
                     ("M7.1-04", "#113"), ("M7.1-05", "#114"), ("M7.1-06", "#115"),
                     ("M7.1-07", "#116"), ("M7.1-08", "#117"), ("M7.1-09", "#118"),
                     ("M7.1-10", "#119"), ("M7.1-11", "#120"),
                     ("M7.2-01", "#121"), ("M7.2-04", "#124"),
                     ("M7.3-05", "#129"), ("M7.4-01", "#131"), ("M7.4-06", "#136"),
                 })
        {
            Assert.Contains(id, issues, StringComparison.Ordinal);
            Assert.Contains(issue, issues, StringComparison.Ordinal);
            Assert.Contains(id, roadmap, StringComparison.Ordinal);
        }

        foreach (string doneMarker in new[]
                 {
                     "M7.1-03 DONE", "M7.1-04 DONE", "M7.1-05 DONE", "M7.1-06 DONE", "M7.1-07 DONE",
                     "M7.1-08 DONE", "M7.1-09 DONE", "M7.1-10 DONE", "M7.1-11 DONE",
                     "M7.2-01 DONE", "M7.2-02 DONE", "M7.2-03 DONE", "M7.2-04 DONE",
                     "M7.3-05 DONE",
                     "M7.4-01 DONE", "M7.4-02 DONE", "M7.4-03 DONE", "M7.4-04 DONE",
                     "M7.4-05 DONE", "M7.4-06 DONE",
                 })
        {
            Assert.Contains(doneMarker, roadmap, StringComparison.Ordinal);
        }

        // Prior M6 E2E issues must appear as DONE in §2.2 / queue strikethroughs.
        foreach (string id in new[]
                 {
                     "M6-01", "M6-02", "M6-03", "M6-04", "M6-05", "M6-06", "M6-07", "M6-08", "M6-09", "N1-07",
                 })
        {
            Assert.Contains(id, roadmap, StringComparison.Ordinal);
            Assert.Contains(id, issues, StringComparison.Ordinal);
        }

        foreach (string closedMilestone in new[]
                 {
                     "M0 Bootstrap", "M1 Read-only", "M2 Policy", "M3 Compiler", "M5 Onboarding", "M4 Safe deploy",
                 })
        {
            Assert.Contains(closedMilestone.Split(' ')[0], roadmap, StringComparison.Ordinal);
        }

        string acceptance = Read("docs", "release", "mvp-acceptance.md");
        Assert.Contains("#100", acceptance, StringComparison.Ordinal);
        Assert.Contains("#107", acceptance, StringComparison.Ordinal);
        Assert.Contains("gh issue", acceptance, StringComparison.Ordinal);
        Assert.Contains("N1-07", acceptance, StringComparison.Ordinal);
        Assert.Contains("M6 CLOSED", acceptance, StringComparison.Ordinal);
        Assert.Contains("MVP CLOSED:** **yes**", acceptance, StringComparison.Ordinal);
        Assert.Contains("M7.4 CLOSED", acceptance, StringComparison.Ordinal);
        Assert.Contains("v0.2.0", acceptance, StringComparison.Ordinal);
        Assert.Contains("Acceptance review", acceptance, StringComparison.OrdinalIgnoreCase);
    }

    // ── AC 2 ──────────────────────────────────────────────────────────────────────

    /// <summary>Release gates checklist documents every required gate.</summary>
    [Fact]
    public void Ac2ReleaseGatesChecklistExists()
    {
        AssertFile("docs", "release", "release-gates.md");
        string gates = Read("docs", "release", "release-gates.md");
        foreach (string needle in new[]
                 {
                     "Fault-injection",
                     "Security",
                     "Backup/restore",
                     "Dependency scan",
                     "Controller package",
                     "Desktop",
                     "migrations bundle",
                     "SBOM",
                     "Acceptance review",
                     "git tag",
                     "Live CHR",
                 })
        {
            Assert.Contains(needle, gates, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── AC 3 ──────────────────────────────────────────────────────────────────────

    /// <summary>CHR matrix DoD substitute: E2E Living Spec suites exist while live CHR remains OFF.</summary>
    [Fact]
    public void Ac3ChrMatrixSubstitutedByE2ELivingSpecs()
    {
        AssertFile("tests", "Mfc.UnitTests", "E2E", "StandaloneDualStackE2ELivingSpecTests.cs");
        AssertFile("tests", "Mfc.UnitTests", "E2E", "MultiWanE2ELivingSpecTests.cs");
        AssertFile("tests", "Mfc.UnitTests", "E2E", "VrrpCrsE2ELivingSpecTests.cs");
        AssertFile("tests", "Mfc.UnitTests", "E2E", "RoutingAssuranceChrAcceptanceLivingSpecTests.cs");

        string acceptance = Read("docs", "release", "mvp-acceptance.md");
        Assert.Contains("Live CHR OFF", acceptance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("StandaloneDualStackE2ELivingSpecTests", acceptance, StringComparison.Ordinal);
        Assert.Contains("RoutingAssuranceChrAcceptanceLivingSpecTests", acceptance, StringComparison.Ordinal);
        Assert.Contains("optional", acceptance, StringComparison.OrdinalIgnoreCase);

        string limitations = Read("docs", "release", "known-limitations.md");
        Assert.Contains("Live CHR matrix is **OFF**", limitations, StringComparison.Ordinal);
    }

    // ── AC 4 ──────────────────────────────────────────────────────────────────────

    /// <summary>Physical CRS DoD substitute: VRRP/CRS Living Spec + crs-switch topology fixture.</summary>
    [Fact]
    public void Ac4PhysicalCrsSubstitutedByScriptedFixture()
    {
        AssertFile("tests", "Mfc.UnitTests", "E2E", "VrrpCrsE2ELivingSpecTests.cs");
        Assert.True(
            Directory.Exists(Path.Combine(RepoRoot, "testlab", "chr", "topologies", "crs-switch")),
            "Missing testlab/chr/topologies/crs-switch");

        string crsSpec = Read("tests", "Mfc.UnitTests", "E2E", "VrrpCrsE2ELivingSpecTests.cs");
        Assert.Contains("Ac11", crsSpec, StringComparison.Ordinal);
        Assert.Contains("Crs", crsSpec, StringComparison.OrdinalIgnoreCase);
    }

    // ── AC 5 ──────────────────────────────────────────────────────────────────────

    /// <summary>Fault-injection suite artifacts exist (protocol + acceptance).</summary>
    [Fact]
    public void Ac5FaultInjectionSuiteExists()
    {
        Assert.True(
            Directory.Exists(Path.Combine(RepoRoot, "tests", "Mfc.UnitTests", "RouterOs", "FaultInjection")),
            "Missing unit FaultInjection suite");
        Assert.True(
            Directory.Exists(Path.Combine(RepoRoot, "tests", "Mfc.IntegrationTests", "Acceptance", "FaultInjection")),
            "Missing integration FaultInjection suite");
        AssertFile(
            "tests",
            "Mfc.UnitTests",
            "Deployment",
            "DeploymentFaultSecurityAcceptanceLivingSpecTests.cs");
    }

    // ── AC 6 ──────────────────────────────────────────────────────────────────────

    /// <summary>Security suite (M6-08 Living Spec) exists.</summary>
    [Fact]
    public void Ac6SecuritySuiteExists()
    {
        AssertFile("tests", "Mfc.UnitTests", "Security", "SecurityBackupRestoreLivingSpecTests.cs");
        string text = Read("tests", "Mfc.UnitTests", "Security", "SecurityBackupRestoreLivingSpecTests.cs");
        Assert.Contains("Ac1", text, StringComparison.Ordinal);
        Assert.Contains("Ac10", text, StringComparison.Ordinal);
    }

    // ── AC 7 ──────────────────────────────────────────────────────────────────────

    /// <summary>Backup/restore suite (M6-08 Integration) exists.</summary>
    [Fact]
    public void Ac7BackupRestoreSuiteExists()
    {
        AssertFile(
            "tests",
            "Mfc.IntegrationTests",
            "Security",
            "SecurityBackupRestoreAcceptanceTests.cs");
        string text = Read(
            "tests",
            "Mfc.IntegrationTests",
            "Security",
            "SecurityBackupRestoreAcceptanceTests.cs");
        Assert.Contains("Ac11", text, StringComparison.Ordinal);
        Assert.Contains("Ac14", text, StringComparison.Ordinal);
        Assert.Contains("pg_dump", text, StringComparison.OrdinalIgnoreCase);
    }

    // ── AC 8 ──────────────────────────────────────────────────────────────────────

    /// <summary>Dependency scan script + CI/docs policy for unresolved Critical exist.</summary>
    [Fact]
    public void Ac8DependencyScanPolicyAndScriptExist()
    {
        AssertFile("scripts", "release", "run-dependency-scan.sh");
        string script = Read("scripts", "release", "run-dependency-scan.sh");
        Assert.Contains("--vulnerable", script, StringComparison.Ordinal);
        Assert.Contains("Severity", script, StringComparison.Ordinal);

        string ci = Read(".github", "workflows", "ci.yml");
        Assert.Contains("Package vulnerability scan", ci, StringComparison.Ordinal);
        Assert.Contains("--vulnerable", ci, StringComparison.Ordinal);

        string packaging = Read("docs", "release", "packaging.md");
        Assert.Contains("dependency-scan", packaging, StringComparison.OrdinalIgnoreCase);
    }

    // ── AC 9–13 packaging dry-run ─────────────────────────────────────────────────

    /// <summary>Controller package script produces artifact path under OUT_DIR (dry-run).</summary>
    [Fact]
    public void Ac9ControllerPackageCreatedInDryRun()
    {
        string outDir = NewTempOutDir();
        try
        {
            RunReleaseScript("package-controller.sh", outDir, dryRun: true);
            Assert.True(Directory.Exists(Path.Combine(outDir, "controller")));
            Assert.True(File.Exists(Path.Combine(outDir, "controller.artifact-path.txt")));
            string path = File.ReadAllText(Path.Combine(outDir, "controller.artifact-path.txt")).Trim();
            Assert.True(Directory.Exists(path), $"controller artifact path missing: {path}");
        }
        finally
        {
            TryDelete(outDir);
        }
    }

    /// <summary>Desktop installer/publish archive created (dry-run zip/tar).</summary>
    [Fact]
    public void Ac10DesktopInstallerCreatedInDryRun()
    {
        string outDir = NewTempOutDir();
        try
        {
            RunReleaseScript("package-desktop.sh", outDir, dryRun: true);
            Assert.True(Directory.Exists(Path.Combine(outDir, "desktop")));
            Assert.True(File.Exists(Path.Combine(outDir, "desktop.artifact-path.txt")));
            string archive = File.ReadAllText(Path.Combine(outDir, "desktop.artifact-path.txt")).Trim();
            Assert.True(File.Exists(archive), $"desktop archive missing: {archive}");
            string packaging = Read("docs", "release", "packaging.md");
            Assert.Contains("zip", packaging, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("installer substitute", packaging, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDelete(outDir);
        }
    }

    /// <summary>Migration bundle script produces bundle path (dry-run).</summary>
    [Fact]
    public void Ac11MigrationBundleCreatedInDryRun()
    {
        string outDir = NewTempOutDir();
        try
        {
            RunReleaseScript("create-migration-bundle.sh", outDir, dryRun: true);
            string bundle = Path.Combine(outDir, "migrations", "mfc-ef-migrations");
            Assert.True(File.Exists(bundle), "migration bundle missing");
            Assert.True(File.Exists(Path.Combine(outDir, "migrations.artifact-path.txt")));
        }
        finally
        {
            TryDelete(outDir);
        }
    }

    /// <summary>SBOM and SHA-256 checksums created (dry-run).</summary>
    [Fact]
    public void Ac12SbomAndSha256ChecksumsCreatedInDryRun()
    {
        string outDir = NewTempOutDir();
        try
        {
            // Seed a file so checksums are non-empty.
            Directory.CreateDirectory(Path.Combine(outDir, "controller"));
            File.WriteAllText(Path.Combine(outDir, "controller", "seed.txt"), "m6-09");
            RunReleaseScript("generate-sbom-and-checksums.sh", outDir, dryRun: true);
            Assert.True(File.Exists(Path.Combine(outDir, "sbom.cdx.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "SHA256SUMS")));
            string sums = File.ReadAllText(Path.Combine(outDir, "SHA256SUMS"));
            Assert.Contains("seed.txt", sums, StringComparison.Ordinal);
            string sbom = File.ReadAllText(Path.Combine(outDir, "sbom.cdx.json"));
            Assert.Contains("CycloneDX", sbom, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(outDir);
        }
    }

    /// <summary>MVP “signed” artifacts: checksums + attestation / RELEASE_SIGNING policy.</summary>
    [Fact]
    public void Ac13ReleaseArtifactsSignedViaChecksumAttestation()
    {
        AssertFile("docs", "release", "RELEASE_SIGNING.md");
        string signing = Read("docs", "release", "RELEASE_SIGNING.md");
        Assert.Contains("SHA256SUMS", signing, StringComparison.Ordinal);
        Assert.Contains("CI signing gate", signing, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("v0.2.0", signing, StringComparison.Ordinal);
        Assert.Contains("Acceptance review signed off", signing, StringComparison.OrdinalIgnoreCase);

        string outDir = NewTempOutDir();
        try
        {
            File.WriteAllText(Path.Combine(outDir, "artifact.bin"), "payload");
            RunReleaseScript("generate-sbom-and-checksums.sh", outDir, dryRun: true);
            Assert.True(File.Exists(Path.Combine(outDir, "SHA256SUMS")));
            Assert.True(File.Exists(Path.Combine(outDir, "SHA256SUMS.asc")));
        }
        finally
        {
            TryDelete(outDir);
        }
    }

    // ── AC 14 ─────────────────────────────────────────────────────────────────────

    /// <summary>Known limitations match actual MVP / M6 scope.</summary>
    [Fact]
    public void Ac14KnownLimitationsMatchActualScope()
    {
        AssertFile("docs", "release", "known-limitations.md");
        string text = Read("docs", "release", "known-limitations.md");
        foreach (string needle in new[]
                 {
                     "N1-07",
                     "MVP CLOSED",
                     "Live CHR",
                     "physical CRS",
                     "zip",
                     "SHA256SUMS",
                     "M7",
                     "auto-fix drift",
                 })
        {
            Assert.Contains(needle, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── AC 15 ─────────────────────────────────────────────────────────────────────

    /// <summary>Release scripts do not mutate the git work tree when OUT_DIR is external.</summary>
    [Fact]
    public void Ac15PackagingDoesNotDirtyGitWorkTree()
    {
        string gates = Read("docs", "release", "release-gates.md");
        Assert.Contains("Working tree clean", gates, StringComparison.OrdinalIgnoreCase);

        string before = GitPorcelain();
        string outDir = NewTempOutDir();
        try
        {
            RunReleaseScript("package-controller.sh", outDir, dryRun: true);
            RunReleaseScript("package-desktop.sh", outDir, dryRun: true);
            RunReleaseScript("create-migration-bundle.sh", outDir, dryRun: true);
            RunReleaseScript("run-dependency-scan.sh", outDir, dryRun: true);
            RunReleaseScript("generate-sbom-and-checksums.sh", outDir, dryRun: true);
            string after = GitPorcelain();
            Assert.Equal(before, after);
        }
        finally
        {
            TryDelete(outDir);
        }
    }

    // ── AC 16 ─────────────────────────────────────────────────────────────────────

    /// <summary>Release tag is gated; scripts and docs must not create tags.</summary>
    [Fact]
    public void Ac16ReleaseTagOnlyAfterAcceptanceReview()
    {
        string acceptance = Read("docs", "release", "mvp-acceptance.md");
        Assert.Contains("Acceptance review", acceptance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("v0.2.0", acceptance, StringComparison.Ordinal);

        string gates = Read("docs", "release", "release-gates.md");
        Assert.Contains("Acceptance review", gates, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("git tag", gates, StringComparison.OrdinalIgnoreCase);

        foreach (string scriptName in new[]
                 {
                     "package-controller.sh",
                     "package-desktop.sh",
                     "create-migration-bundle.sh",
                     "generate-sbom-and-checksums.sh",
                     "run-dependency-scan.sh",
                 })
        {
            string script = Read("scripts", "release", scriptName);
            Assert.DoesNotContain("git tag", script, StringComparison.Ordinal);
            Assert.DoesNotContain("git tag ", script, StringComparison.Ordinal);
        }
    }

    /// <summary>Acceptance package documents and ops manuals exist.</summary>
    [Fact]
    public void AcceptancePackageDocumentsExist()
    {
        string[] relativePaths =
        [
            "docs/release/mvp-acceptance.md",
            "docs/release/release-gates.md",
            "docs/release/known-limitations.md",
            "docs/release/packaging.md",
            "docs/release/RELEASE_SIGNING.md",
            "docs/operations/installation.md",
            "docs/operations/prerequisite-checklist.md",
            "docs/operations/operations-manual.md",
            "docs/operations/recovery.md",
            "docs/development/support-manifest.md",
            "docs/development/testing.md",
            "CHANGELOG.md",
            "ROADMAP.md",
        ];

        foreach (string relative in relativePaths)
        {
            Assert.True(
                File.Exists(Path.Combine(RepoRoot, relative)),
                $"Missing documentation file: {relative}");
        }
    }

    private static string NewTempOutDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "mfc-m6-09-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup for temp artifacts.
        }
    }

    private static void RunReleaseScript(string scriptFileName, string outDir, bool dryRun)
    {
        string script = Path.Combine(RepoRoot, "scripts", "release", scriptFileName);
        Assert.True(File.Exists(script), $"Missing script: {script}");

        ProcessStartInfo psi = new()
        {
            FileName = "/bin/bash",
            Arguments = $"\"{script}\"",
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["OUT_DIR"] = outDir;
        psi.Environment["MFC_RELEASE_DRY_RUN"] = dryRun ? "1" : "0";
        psi.Environment["PATH"] = Environment.GetEnvironmentVariable("PATH") ?? "/usr/bin:/bin";

        using Process proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start bash.");
        string stdout = proc.StandardOutput.ReadToEnd();
        string stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(120_000);
        Assert.True(
            proc.ExitCode == 0,
            $"{scriptFileName} failed ({proc.ExitCode}).\nstdout:\n{stdout}\nstderr:\n{stderr}");
    }

    private static string GitPorcelain()
    {
        ProcessStartInfo psi = new()
        {
            FileName = "git",
            Arguments = "status --porcelain",
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using Process proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start git.");
        string stdout = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(30_000);
        Assert.Equal(0, proc.ExitCode);
        // Ignore this test assembly's own uncommitted files only by normalizing blank; compare raw.
        return Regex.Replace(stdout, "\r\n", "\n");
    }
}
