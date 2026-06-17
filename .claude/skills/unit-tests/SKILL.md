---
name: unit-tests
description: 'Use when writing or generating C# unit tests. Trigger on any request to add, generate, or improve unit tests in a .NET project.'
argument-hint: 'Describe what to test — e.g. "unit tests for SearchEndpoints", "guard tests for ResultSectionLocator"'
---

# Generate Unit Tests

## Stack

xUnit · Moq (when mocking interfaces is needed)

Assertions use plain `Assert.*` throughout.

---

## File Placement

Test files mirror the source namespace. The rule:

`InfoTrack.<Assembly>[.<Sub.Namespace>].<ClassName>` → `<Assembly>/[<Sub.Namespace>/]<ClassNameTests.cs>`

- Strip the `InfoTrack.` prefix
- The assembly segment (`Api`, `Infrastructure`, `Application`, …) maps to a top-level folder
- The sub-namespace within the assembly (if any) becomes the next folder, **preserving dots**: `Parsing.SolicitorsCom` → `Parsing.SolicitorsCom/`
- The class name becomes the filename with a `Tests` suffix

| Source class | Test file path (relative to test project root) |
|---|---|
| `InfoTrack.Api.SearchEndpoints` | `Api/SearchEndpointsTests.cs` |
| `InfoTrack.Infrastructure.Parsing.SolicitorsCom.ResultSectionLocator` | `Infrastructure/Parsing.SolicitorsCom/ResultSectionLocatorTests.cs` |
| `InfoTrack.Application.Services.SolicitorSearchService` | `Application/Services/SolicitorSearchServiceTests.cs` |

The test file namespace must match: `InfoTrack.Tests.<Assembly>[.<Sub.Namespace>]`

---

## Naming

```
UnitOfWork_StateUnderTest_ExpectedOutcome
```

---

## Theory vs Fact

Prefer `[Theory]` when the same assertion logic runs across multiple inputs. Use `[Fact]` only for single-scenario tests.

**`[Theory, InlineData]`** — same logic, different primitive inputs:

```csharp
[Theory]
[InlineData(null)]
[InlineData("")]
public void Locate_NullOrEmptyHtml_ReturnsEmpty(string? html) =>
    Assert.Equal(string.Empty, ResultSectionLocator.Locate(html!));
```

**`[Theory, MemberData]`** — same logic, complex inputs:

```csharp
public static IEnumerable<object[]> InvalidLocations() =>
[
    [null],
    [new List<string>()],
];

[Theory, MemberData(nameof(InvalidLocations))]
public void SearchAsync_InvalidLocations_ReturnsValidationProblem(List<string>? locations) { ... }
```

**`[Fact]`** — single scenario, fixture-driven tests, or spot-checks with unique per-case assertions.

---

## Test Structure

**Static or parameterless SUT** — call directly, no setup:

```csharp
[Fact]
public void Locate_NoResultSection_ReturnsEmpty()
{
    const string html = "<html><body><p>Not found.</p></body></html>";
    Assert.Equal(string.Empty, ResultSectionLocator.Locate(html));
}
```

**SUT with dependencies** — construct manually; mock interfaces with Moq:

```csharp
[Fact]
public async Task SearchAsync_ValidRequest_DelegatesToSearchService()
{
    var searchService = new Mock<ISolicitorSearchService>();
    searchService.Setup(s => s.SearchAsync(It.IsAny<SearchRequest>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new SearchResponse());

    var sut = new SomeService(searchService.Object);
    await sut.RunAsync(new SearchRequest { Locations = ["london"] }, CancellationToken.None);

    searchService.Verify(s => s.SearchAsync(It.IsAny<SearchRequest>(), It.IsAny<CancellationToken>()), Times.Once);
}
```

**Fixture-based tests** — for parsing tests that need real HTML, load it as an embedded resource. Register the file in the `.csproj` as `<EmbeddedResource>` and load it via:

```csharp
private static string LoadFixture(string fileName)
{
    var assembly = Assembly.GetExecutingAssembly();
    using var stream = assembly.GetManifestResourceStream($"InfoTrack.Tests.Fixtures.{fileName}")
        ?? throw new InvalidOperationException($"Embedded resource not found: {fileName}");
    using var reader = new StreamReader(stream);
    return reader.ReadToEnd();
}
```

Prefer inline string literals (`const string html = """..."""`) for unit-level parsing tests so the input is visible in the test itself.

---

## Before Delivering

- [ ] Test file is in the correct folder per the namespace-mirroring rule above
- [ ] Test namespace matches `InfoTrack.Tests.<Assembly>[.<Sub.Namespace>]`
- [ ] Multiple `[Fact]`s with identical logic but different inputs collapsed into a `[Theory]`
- [ ] Naming matches `UnitOfWork_StateUnderTest_ExpectedOutcome`
- [ ] Only tests things that can break — no trivial getters/setters, no duplicate cases, no coverage padding
- [ ] All public methods have at least one happy-path test
- [ ] Error and null-input cases covered where applicable
- [ ] Assertions use `Assert.*`
- [ ] Moq used only when an interface genuinely needs to be controlled or verified
