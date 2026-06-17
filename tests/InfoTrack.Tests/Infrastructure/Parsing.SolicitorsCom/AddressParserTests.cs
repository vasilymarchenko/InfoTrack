using InfoTrack.Infrastructure.Parsing.SolicitorsCom;

namespace InfoTrack.Tests.Infrastructure.Parsing.SolicitorsCom;

public class AddressParserTests
{
    // ── Postcode extraction ────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Parse_NullOrEmptyAddress_ReturnsBothNull(string? address)
    {
        var (town, postcode) = AddressParser.Parse(address);
        Assert.Null(town);
        Assert.Null(postcode);
    }

    [Theory]
    [InlineData("141 High Street, Southgate, London N14 6BP", "N14 6BP")]
    [InlineData("10 Victoria Square, Birmingham B1 1BD", "B1 1BD")]
    [InlineData("1 Harbour Exchange Square, London E14 9GE", "E14 9GE")]
    public void Parse_RealAddress_ExtractsCorrectPostcode(string address, string expectedPostcode)
    {
        var (_, postcode) = AddressParser.Parse(address);
        Assert.Equal(expectedPostcode, postcode);
    }

    [Fact]
    public void Parse_PostcodeWithExtraWhitespace_NormalisedWithSingleSpace()
    {
        var (_, postcode) = AddressParser.Parse("10 Main St, London  E1  1AA");
        Assert.Equal("E1 1AA", postcode);
    }

    [Fact]
    public void Parse_NoPostcodeInAddress_PostcodeIsNull()
    {
        var (_, postcode) = AddressParser.Parse("Stoneham House, 17 Chequer Street, St Albans");
        Assert.Null(postcode);
    }

    [Fact]
    public void Parse_PostcodeUppercased()
    {
        var (_, postcode) = AddressParser.Parse("10 main street, london sw1a 1aa");
        Assert.Equal("SW1A 1AA", postcode);
    }

    // ── Town extraction ────────────────────────────────────────────────────────

    [Theory]
    // Town is the segment immediately BEFORE the postcode-containing segment.
    // "141 High Street, Southgate, London N14 6BP" → segment before "London N14 6BP" = "Southgate"
    // "33 St. Pauls Street, Leeds, Yorkshire LS1 2JJ" → segment before "Yorkshire LS1 2JJ" = "Leeds"
    // "1 The Mews, City Centre, Leeds LS1 1AB" → segment before "Leeds LS1 1AB" = "City Centre"
    [InlineData("141 High Street, Southgate, London N14 6BP", "Southgate")]
    [InlineData("33 St. Pauls Street, Leeds, Yorkshire LS1 2JJ", "Leeds")]
    [InlineData("1 The Mews, City Centre, Leeds LS1 1AB", "City Centre")]
    public void Parse_RealAddress_ExtractsTownBeforePostcode(string address, string expectedTown)
    {
        var (town, _) = AddressParser.Parse(address);
        Assert.Equal(expectedTown, town);
    }

    [Fact]
    public void Parse_PostcodeOnSeparateSegment_TownIsSegmentBefore()
    {
        var (town, _) = AddressParser.Parse("10 Main St, London, E1 1AA");
        Assert.Equal("London", town);
    }

    [Fact]
    public void Parse_NoPostcode_FallsBackToLastSegment()
    {
        var (town, postcode) = AddressParser.Parse("10 Main St, Somewhere");
        Assert.Null(postcode);
        Assert.Equal("Somewhere", town);
    }

    [Fact]
    public void Parse_TaglineAddressWithNoPostcode_PostcodeNull()
    {
        // QualitySolicitors-style tagline instead of real address
        var (_, postcode) = AddressParser.Parse("Quality conveyancing services for you");
        Assert.Null(postcode);
    }

    [Fact]
    public void Parse_PostcodeOnlySegment_TownFromPreviousSegment()
    {
        var (town, postcode) = AddressParser.Parse("100 High Road, Bradford, BD1 1AA");
        Assert.Equal("BD1 1AA", postcode);
        Assert.Equal("Bradford", town);
    }
}
