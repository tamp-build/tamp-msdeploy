using System.Linq;
using Bogus;
using Tamp;
using Tamp.MsDeploy;
using Xunit;

namespace Tamp.MsDeploy.Tests;

public sealed class MsDeployTests
{
    private static readonly string FakeToolPath = OperatingSystem.IsWindows()
        ? "C:\\fake\\msdeploy.exe"
        : "/fake/msdeploy";

    private static Tool FakeTool() => new(AbsolutePath.Create(FakeToolPath));

    private static Secret FakePw(string name = "AGENT_PW") => new(name, "p@ssw0rd!");

    // ---- Provider rendering ----

    [Fact]
    public void ContentPath_Provider_Renders_As_Single_Kv()
    {
        var p = MsDeployProvider.ContentPath("C:\\publish");
        Assert.Equal("contentPath=C:\\publish", p.Render(null));
    }

    [Fact]
    public void IisApp_Provider_Renders_Kind()
    {
        var p = MsDeployProvider.IisApp("MySite");
        Assert.Equal("iisApp=MySite", p.Render(null));
    }

    [Fact]
    public void Package_And_ArchiveDir_Providers_Render()
    {
        Assert.Equal("package=artifacts/app.zip", MsDeployProvider.Package("artifacts/app.zip").Render(null));
        Assert.Equal("archiveDir=artifacts/archive", MsDeployProvider.ArchiveDir("artifacts/archive").Render(null));
    }

    [Fact]
    public void Custom_Provider_Renders_Verbatim()
    {
        var p = MsDeployProvider.Custom("dbDacFx", "C:\\db.dacpac");
        Assert.Equal("dbDacFx=C:\\db.dacpac", p.Render(null));
    }

    [Fact]
    public void Provider_WithComputerName_Appends_Comma_Field()
    {
        var p = MsDeployProvider.IisApp("MySite").WithComputerName("https://web1.example.com:8172/msdeploy.axd?site=MySite");
        Assert.Equal("iisApp=MySite,computerName=https://web1.example.com:8172/msdeploy.axd?site=MySite", p.Render(null));
    }

    [Fact]
    public void Provider_WithCredentials_Appends_User_Password_AuthType()
    {
        var pw = FakePw();
        var p = MsDeployProvider.IisApp("MySite")
            .WithComputerName("https://web1.example.com:8172/msdeploy.axd?site=MySite")
            .WithCredentials("deploy-bot", pw);

        Assert.Equal(
            "iisApp=MySite,computerName=https://web1.example.com:8172/msdeploy.axd?site=MySite,userName=deploy-bot,password=p@ssw0rd!,authType=Basic",
            p.Render(pw.Reveal()));
    }

    [Fact]
    public void Provider_Render_With_Null_Reveal_Omits_Password_Field()
    {
        // Plan-emission shape: when the runner is rendering for logs / dry-run, it should
        // pass null for the revealed password and get a token with the password=... segment
        // omitted (the secret stays on CommandPlan.Secrets where redaction is centralized).
        var pw = FakePw();
        var p = MsDeployProvider.IisApp("MySite").WithCredentials("u", pw);

        var rendered = p.Render(null);
        Assert.DoesNotContain("password=", rendered);
        Assert.Contains("userName=u", rendered);
        Assert.Contains("authType=Basic", rendered);
    }

    [Theory]
    [InlineData(MsDeployAuthType.Basic, "Basic")]
    [InlineData(MsDeployAuthType.Ntlm, "NTLM")]
    public void Provider_AuthType_Token(MsDeployAuthType auth, string expected)
    {
        var p = MsDeployProvider.IisApp("MySite").WithCredentials("u", FakePw(), auth);
        Assert.Contains($"authType={expected}", p.Render(null));
    }

    [Fact]
    public void Provider_Default_AuthType_Omits_Token()
    {
        var p = MsDeployProvider.IisApp("MySite") with { UserName = "u", Password = FakePw() };
        Assert.DoesNotContain("authType=", p.Render(null));
    }

    [Fact]
    public void Provider_ExtraSettings_Append()
    {
        var p = MsDeployProvider.ContentPath("C:\\publish")
            .WithExtraSetting("includeAcls=false")
            .WithExtraSetting("encryptPassword=true");

        Assert.Equal("contentPath=C:\\publish,includeAcls=false,encryptPassword=true", p.Render(null));
    }

    // ---- Sync verb ----

    [Fact]
    public void Sync_Emits_Verb_Source_Dest()
    {
        var plan = MsDeploy.Sync(FakeTool(), s => s
            .SetSource(MsDeployProvider.ContentPath("publish"))
            .SetDestination(MsDeployProvider.ContentPath("C:\\inetpub\\app")));

        Assert.Equal(new[] { "-verb:sync", "-source:contentPath=publish", "-dest:contentPath=C:\\inetpub\\app" },
            plan.Arguments.Take(3));
    }

    [Fact]
    public void Sync_Without_Source_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => MsDeploy.Sync(FakeTool(), s => s
            .SetDestination(MsDeployProvider.ContentPath("C:\\dest"))));
        Assert.Contains("Source", ex.Message);
    }

    [Fact]
    public void Sync_Without_Destination_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => MsDeploy.Sync(FakeTool(), s => s
            .SetSource(MsDeployProvider.ContentPath("publish"))));
        Assert.Contains("Destination", ex.Message);
    }

    // ---- Skip rules ----

    [Fact]
    public void Sync_Skip_FilePath_Emits_Skip_Token()
    {
        var plan = MsDeploy.Sync(FakeTool(), s => s
            .SetSource(MsDeployProvider.ContentPath("publish"))
            .SetDestination(MsDeployProvider.ContentPath("C:\\dest"))
            .AddSkipRule(MsDeploySkipRule.FilePath(".*\\\\web\\.config$")));

        Assert.Contains("-skip:objectName=filePath,absolutePath=.*\\\\web\\.config$", plan.Arguments);
    }

    [Fact]
    public void Sync_Skip_DirPath_Emits_Skip_Token()
    {
        var plan = MsDeploy.Sync(FakeTool(), s => s
            .SetSource(MsDeployProvider.ContentPath("publish"))
            .SetDestination(MsDeployProvider.ContentPath("C:\\dest"))
            .AddSkipRule(MsDeploySkipRule.DirPath(".*\\\\App_Data$")));

        Assert.Contains("-skip:objectName=dirPath,absolutePath=.*\\\\App_Data$", plan.Arguments);
    }

    [Fact]
    public void Sync_Multiple_Skip_Rules_All_Emit()
    {
        var plan = MsDeploy.Sync(FakeTool(), s => s
            .SetSource(MsDeployProvider.ContentPath("publish"))
            .SetDestination(MsDeployProvider.ContentPath("C:\\dest"))
            .AddSkipRule(MsDeploySkipRule.FilePath(".*\\\\web\\.config$"))
            .AddSkipRule(MsDeploySkipRule.DirPath(".*\\\\App_Data$"))
            .AddSkipRule(new MsDeploySkipRule { ObjectName = "setAcl" }));

        var skips = plan.Arguments.Where(a => a.StartsWith("-skip:", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, skips.Count);
    }

    [Fact]
    public void Empty_SkipRule_Throws_On_Render()
    {
        var bad = new MsDeploySkipRule();
        Assert.Throws<InvalidOperationException>(() => MsDeploy.Sync(FakeTool(), s => s
            .SetSource(MsDeployProvider.ContentPath("publish"))
            .SetDestination(MsDeployProvider.ContentPath("C:\\dest"))
            .AddSkipRule(bad)));
    }

    // ---- Cross-cutting flags ----

    [Fact]
    public void AllowUntrusted_Emits_Flag()
    {
        var plan = MsDeploy.Sync(FakeTool(), s => s
            .SetSource(MsDeployProvider.ContentPath("p"))
            .SetDestination(MsDeployProvider.ContentPath("d"))
            .SetAllowUntrusted());
        Assert.Contains("-allowUntrusted", plan.Arguments);
    }

    [Fact]
    public void Verbose_And_WhatIf_Emit_Flags()
    {
        var plan = MsDeploy.Sync(FakeTool(), s => s
            .SetSource(MsDeployProvider.ContentPath("p"))
            .SetDestination(MsDeployProvider.ContentPath("d"))
            .SetVerbose()
            .SetWhatIf());
        Assert.Contains("-verbose", plan.Arguments);
        Assert.Contains("-whatif", plan.Arguments);
    }

    [Fact]
    public void RetryAttempts_And_Interval_Emit_With_Values()
    {
        var plan = MsDeploy.Sync(FakeTool(), s => s
            .SetSource(MsDeployProvider.ContentPath("p"))
            .SetDestination(MsDeployProvider.ContentPath("d"))
            .SetRetryAttempts(5)
            .SetRetryIntervalMs(2000));
        Assert.Contains("-retryAttempts:5", plan.Arguments);
        Assert.Contains("-retryInterval:2000", plan.Arguments);
    }

    [Fact]
    public void DisableLink_And_EnableLink_Emit_Repeatable()
    {
        var plan = MsDeploy.Sync(FakeTool(), s => s
            .SetSource(MsDeployProvider.ContentPath("p"))
            .SetDestination(MsDeployProvider.ContentPath("d"))
            .AddDisableLink("AppPoolExtension")
            .AddDisableLink("ContentExtension")
            .AddEnableLink("CertificateExtension"));
        Assert.Contains("-disableLink:AppPoolExtension", plan.Arguments);
        Assert.Contains("-disableLink:ContentExtension", plan.Arguments);
        Assert.Contains("-enableLink:CertificateExtension", plan.Arguments);
    }

    [Fact]
    public void UseChecksum_Emits_Flag()
    {
        var plan = MsDeploy.Sync(FakeTool(), s => s
            .SetSource(MsDeployProvider.ContentPath("p"))
            .SetDestination(MsDeployProvider.ContentPath("d"))
            .SetUseChecksum());
        Assert.Contains("-useCheckSum", plan.Arguments);
    }

    // ---- Secret tracking ----

    [Fact]
    public void Sync_Destination_Password_Tracked_As_Secret()
    {
        var pw = FakePw("DEST_PW");
        var plan = MsDeploy.Sync(FakeTool(), s => s
            .SetSource(MsDeployProvider.ContentPath("publish"))
            .SetDestination(MsDeployProvider.IisApp("MySite")
                .WithComputerName("https://web1.example.com:8172/msdeploy.axd")
                .WithCredentials("deploy-bot", pw)));

        Assert.Contains(plan.Secrets, x => ReferenceEquals(x, pw));
    }

    [Fact]
    public void Sync_Both_Source_And_Dest_Passwords_Tracked_Independently()
    {
        var srcPw = FakePw("SRC_PW");
        var dstPw = FakePw("DST_PW");
        var plan = MsDeploy.Sync(FakeTool(), s => s
            .SetSource(MsDeployProvider.IisApp("SrcSite")
                .WithComputerName("https://src.example.com")
                .WithCredentials("u1", srcPw))
            .SetDestination(MsDeployProvider.IisApp("DstSite")
                .WithComputerName("https://dst.example.com")
                .WithCredentials("u2", dstPw)));

        Assert.Equal(2, plan.Secrets.Count);
        Assert.Contains(plan.Secrets, x => ReferenceEquals(x, srcPw));
        Assert.Contains(plan.Secrets, x => ReferenceEquals(x, dstPw));
    }

    [Fact]
    public void Sync_No_Passwords_Means_No_Secrets()
    {
        var plan = MsDeploy.Sync(FakeTool(), s => s
            .SetSource(MsDeployProvider.ContentPath("publish"))
            .SetDestination(MsDeployProvider.ContentPath("C:\\dest")));

        Assert.Empty(plan.Secrets);
    }

    // ---- Dump verb ----

    [Fact]
    public void Dump_Emits_Verb_And_Source()
    {
        var plan = MsDeploy.Dump(FakeTool(), s => s
            .SetSource(MsDeployProvider.IisApp("MySite")
                .WithComputerName("https://web1.example.com:8172/msdeploy.axd?site=MySite")));

        Assert.Equal("-verb:dump", plan.Arguments[0]);
        Assert.Contains(plan.Arguments, a => a.StartsWith("-source:iisApp=MySite", StringComparison.Ordinal));
        Assert.DoesNotContain(plan.Arguments, a => a.StartsWith("-dest:", StringComparison.Ordinal));
    }

    [Fact]
    public void Dump_Without_Source_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => MsDeploy.Dump(FakeTool(), _ => { }));
    }

    [Fact]
    public void Dump_Password_Tracked_As_Secret()
    {
        var pw = FakePw();
        var plan = MsDeploy.Dump(FakeTool(), s => s
            .SetSource(MsDeployProvider.IisApp("MySite")
                .WithComputerName("https://web1.example.com:8172/msdeploy.axd")
                .WithCredentials("u", pw)));
        Assert.Contains(plan.Secrets, x => ReferenceEquals(x, pw));
    }

    // ---- Object-init parity ----

    [Fact]
    public void Sync_ObjectInit_Equivalent_To_Fluent()
    {
        var pw = FakePw();
        var fluent = MsDeploy.Sync(FakeTool(), s => s
            .SetSource(MsDeployProvider.ContentPath("publish"))
            .SetDestination(MsDeployProvider.IisApp("MySite")
                .WithComputerName("https://web1.example.com:8172/msdeploy.axd")
                .WithCredentials("u", pw))
            .AddSkipRule(MsDeploySkipRule.FilePath(".*\\\\web\\.config$"))
            .SetAllowUntrusted()
            .SetRetryAttempts(3));

        var settings = new SyncSettings
        {
            Source = MsDeployProvider.ContentPath("publish"),
            Destination = MsDeployProvider.IisApp("MySite")
                .WithComputerName("https://web1.example.com:8172/msdeploy.axd")
                .WithCredentials("u", pw),
            AllowUntrusted = true,
            RetryAttempts = 3,
        };
        settings.SkipRules.Add(MsDeploySkipRule.FilePath(".*\\\\web\\.config$"));
        var objInit = MsDeploy.Sync(FakeTool(), settings);

        Assert.Equal(fluent.Arguments, objInit.Arguments);
    }

    // ---- Working directory / env / executable ----

    [Fact]
    public void Executable_Is_Tool_Path()
    {
        var plan = MsDeploy.Sync(FakeTool(), s => s
            .SetSource(MsDeployProvider.ContentPath("p"))
            .SetDestination(MsDeployProvider.ContentPath("d")));
        Assert.Equal(FakeToolPath, plan.Executable);
    }

    [Fact]
    public void Working_Directory_Propagates()
    {
        var plan = MsDeploy.Sync(FakeTool(), s => s
            .SetSource(MsDeployProvider.ContentPath("p"))
            .SetDestination(MsDeployProvider.ContentPath("d"))
            .SetWorkingDirectory("/tmp/build"));
        Assert.Equal("/tmp/build", plan.WorkingDirectory);
    }

    [Fact]
    public void Environment_Variables_Propagate()
    {
        var plan = MsDeploy.Sync(FakeTool(), s => s
            .SetSource(MsDeployProvider.ContentPath("p"))
            .SetDestination(MsDeployProvider.ContentPath("d"))
            .SetEnvironmentVariable("MSDEPLOY_PRESERVE_TIMESTAMPS", "true"));
        Assert.Equal("true", plan.Environment["MSDEPLOY_PRESERVE_TIMESTAMPS"]);
    }

    // ---- Realistic composition ----

    [Fact]
    public void Realistic_Remote_IIS_Sync_Shape()
    {
        var pw = FakePw("AGENT_PW");
        var plan = MsDeploy.Sync(FakeTool(), s => s
            .SetSource(MsDeployProvider.ContentPath("artifacts/publish"))
            .SetDestination(MsDeployProvider.IisApp("WebApp")
                .WithComputerName("https://web1.example.com:8172/msdeploy.axd?site=WebApp")
                .WithCredentials("deploy-bot", pw))
            .AddSkipRule(MsDeploySkipRule.FilePath(".*\\\\web\\.config$"))
            .AddSkipRule(MsDeploySkipRule.DirPath(".*\\\\App_Data$"))
            .AddDisableLink("AppPoolExtension")
            .SetAllowUntrusted()
            .SetRetryAttempts(3)
            .SetRetryIntervalMs(2000)
            .SetVerbose());

        // Verb + endpoints come first
        Assert.Equal("-verb:sync", plan.Arguments[0]);
        Assert.StartsWith("-source:contentPath=artifacts/publish", plan.Arguments[1]);
        Assert.StartsWith("-dest:iisApp=WebApp", plan.Arguments[2]);
        Assert.Contains("computerName=https://web1.example.com:8172/msdeploy.axd?site=WebApp", plan.Arguments[2]);
        Assert.Contains("userName=deploy-bot", plan.Arguments[2]);
        Assert.Contains("password=p@ssw0rd!", plan.Arguments[2]);
        Assert.Contains("authType=Basic", plan.Arguments[2]);

        // Critical flags all present
        Assert.Contains("-disableLink:AppPoolExtension", plan.Arguments);
        Assert.Contains("-allowUntrusted", plan.Arguments);
        Assert.Contains("-verbose", plan.Arguments);
        Assert.Contains("-retryAttempts:3", plan.Arguments);
        Assert.Contains("-retryInterval:2000", plan.Arguments);

        // Both skip rules
        Assert.Equal(2, plan.Arguments.Count(a => a.StartsWith("-skip:", StringComparison.Ordinal)));

        // Password tracked
        Assert.Contains(plan.Secrets, x => ReferenceEquals(x, pw));
    }

    // ---- Boundary / fuzz ----

    [Theory]
    [InlineData("artifacts/path with spaces/publish")]
    [InlineData("artifacts/Δ-π/publish")]
    [InlineData("artifacts/sub'dir/publish")]
    public void Provider_Path_Roundtrips_Verbatim(string path)
    {
        var p = MsDeployProvider.ContentPath(path);
        Assert.Equal($"contentPath={path}", p.Render(null));
    }

    [Fact]
    public void Bulk_Skip_Rules_All_Emit()
    {
        var faker = new Faker();
        var rules = Enumerable.Range(0, 30)
            .Select(_ => MsDeploySkipRule.FilePath($".*{faker.Random.AlphaNumeric(8)}.*"))
            .ToList();

        var plan = MsDeploy.Sync(FakeTool(), s =>
        {
            s.SetSource(MsDeployProvider.ContentPath("p")).SetDestination(MsDeployProvider.ContentPath("d"));
            foreach (var r in rules) s.AddSkipRule(r);
        });

        Assert.Equal(rules.Count, plan.Arguments.Count(a => a.StartsWith("-skip:", StringComparison.Ordinal)));
    }
}
