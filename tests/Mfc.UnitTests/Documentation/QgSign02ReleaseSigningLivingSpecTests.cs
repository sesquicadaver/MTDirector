using System.Diagnostics;
using Xunit;

namespace Mfc.UnitTests.Documentation;

/// <summary>
/// QG-SIGN-02: opt-in cryptographic signing gate (GPG and/or Sigstore/cosign) beyond
/// QG-SIGN-01 cleartext SHA256SUMS. Default PR CI must not require org secrets.
/// </summary>
public sealed class QgSign02ReleaseSigningLivingSpecTests
{
    [Fact]
    public void Ac1ReleaseSigningDocumentsOptInCryptoGateBeyondCleartext()
    {
        string signing = File.ReadAllText(Path.Combine(RepoRoot(), "docs/release/RELEASE_SIGNING.md"));

        Assert.Contains("QG-SIGN-02", signing, StringComparison.Ordinal);
        Assert.Contains("sign-sha256sums-crypto.sh", signing, StringComparison.Ordinal);
        Assert.Contains("MFC_RELEASE_SIGN_SELFTEST", signing, StringComparison.Ordinal);
        Assert.Contains("MFC_RELEASE_COSIGN", signing, StringComparison.Ordinal);
        Assert.Contains("opt-in", signing, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workflow_dispatch", signing, StringComparison.Ordinal);
        Assert.Contains("org secrets on every PR", signing, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does **not** require", signing, StringComparison.Ordinal);
        Assert.DoesNotContain("cryptographic signing is enabled by default", signing, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ac2SigningGateChecklistDocumentsQgSign02()
    {
        string gate = File.ReadAllText(Path.Combine(RepoRoot(), "docs/development/signing-gate.md"));

        Assert.Contains("QG-SIGN-02", gate, StringComparison.Ordinal);
        Assert.Contains("sign-sha256sums-crypto.sh", gate, StringComparison.Ordinal);
        Assert.Contains("QgSign02ReleaseSigningLivingSpecTests", gate, StringComparison.Ordinal);
        Assert.Contains("workflow_dispatch", gate, StringComparison.Ordinal);
        Assert.Contains("pull_request", gate, StringComparison.Ordinal);
        Assert.Contains("QG-SIGN-01", gate, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac3ReleaseSigningWorkflowIsDispatchOnlyNotPullRequest()
    {
        string wf = File.ReadAllText(Path.Combine(RepoRoot(), ".github/workflows/release-signing.yml"));
        string ci = File.ReadAllText(Path.Combine(RepoRoot(), ".github/workflows/ci.yml"));

        Assert.Contains("workflow_dispatch", wf, StringComparison.Ordinal);
        Assert.Contains("sign-sha256sums-crypto.sh", wf, StringComparison.Ordinal);
        Assert.Contains("MFC_RELEASE_SIGN_SELFTEST", wf, StringComparison.Ordinal);
        Assert.Contains("secrets.MFC_RELEASE_GPG_KEY_ID", wf, StringComparison.Ordinal);
        Assert.Contains("secrets.COSIGN_PRIVATE_KEY", wf, StringComparison.Ordinal);
        Assert.DoesNotContain("pull_request:", wf, StringComparison.Ordinal);
        Assert.DoesNotContain("sign-sha256sums-crypto.sh", ci, StringComparison.Ordinal);
        Assert.DoesNotContain("COSIGN_PRIVATE_KEY", ci, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac4CryptoGateScriptSelfTestProducesStatusWithoutSecrets()
    {
        string root = RepoRoot();
        string script = Path.Combine(root, "scripts/release/sign-sha256sums-crypto.sh");
        Assert.True(File.Exists(script));

        string outDir = Path.Combine(Path.GetTempPath(), "mfc-qg-sign-02-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);
        try
        {
            File.WriteAllText(Path.Combine(outDir, "SHA256SUMS"), "deadbeef  artifact.bin\n");

            var psi = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add(script);
            psi.Environment["OUT_DIR"] = outDir;
            psi.Environment["MFC_RELEASE_SIGN_SELFTEST"] = "1";

            using Process proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start self-test.");
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit(60_000);
            Assert.True(proc.ExitCode == 0, $"self-test failed ({proc.ExitCode}): {stdout}\n{stderr}");

            string statusPath = Path.Combine(outDir, "SHA256SUMS.crypto-gate.json");
            Assert.True(File.Exists(statusPath));
            string status = File.ReadAllText(statusPath);
            Assert.Contains("\"mode\": \"selftest\"", status, StringComparison.Ordinal);
            Assert.Contains("qg-sign-02", status, StringComparison.Ordinal);
            Assert.Contains("\"defaultPrCiRequiresSecrets\": false", status, StringComparison.Ordinal);

            // Cleartext SHA256SUMS must remain untouched by self-test.
            Assert.Equal("deadbeef  artifact.bin\n", File.ReadAllText(Path.Combine(outDir, "SHA256SUMS")));
        }
        finally
        {
            try
            {
                Directory.Delete(outDir, recursive: true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }

    [Fact]
    public void Ac5DocsMatrixDocumentsQgSign02()
    {
        string root = RepoRoot();
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string packaging = File.ReadAllText(Path.Combine(root, "docs/release/packaging.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));

        Assert.Contains("QG-SIGN-02", testing, StringComparison.Ordinal);
        Assert.Contains("Living Specification — QG-SIGN-02", testing, StringComparison.Ordinal);
        Assert.Contains("QgSign02ReleaseSigningLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("sign-sha256sums-crypto.sh", packaging, StringComparison.Ordinal);
        Assert.Contains("MFC_RELEASE_SIGN_SELFTEST", packaging, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-310 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("QG-SIGN-02 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains(
            "W7-310 | [#1026](https://github.com/sesquicadaver/MTDirector/issues/1026) | QG-SIGN-02 — Opt-in cryptographic signing gate (GPG/Sigstore) beyond QG-SIGN-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-366 (#1138)", roadmap, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ROADMAP.md")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
