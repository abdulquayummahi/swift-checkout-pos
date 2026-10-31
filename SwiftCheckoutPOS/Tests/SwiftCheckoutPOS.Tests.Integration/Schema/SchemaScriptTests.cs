using SwiftCheckoutPOS.Tests.Integration.Infrastructure;

namespace SwiftCheckoutPOS.Tests.Integration.Schema;

public sealed class SchemaScriptTests
{
    [Fact]
    [Trait("Category", "SchemaReview")]
    public void CurrentModelMatchesReviewedSql()
    {
        var guard = TestConnectionGuard.FromEnvironment();
        var generated = SchemaScript.Generate(guard); // metadata only; never opens SQL
        if (Environment.GetEnvironmentVariable("SWIFTCHECKOUTPOS_GENERATE_TEST_SCHEMA") == "1")
        {
            File.WriteAllText(SchemaScript.SourcePath(), generated, new System.Text.UTF8Encoding(false));
            return; // explicit generation is NOT evidence that SQL has been reviewed/applied
        }
        Assert.Equal(generated, SchemaScript.ReadReviewed(guard));
    }
}
