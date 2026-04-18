using AutoFixture;
using AutoFixture.AutoNSubstitute;

namespace Nexus.Core.Tests.Common;

/// <summary>
/// shared base class for all unit tests.
/// Provides a pre-configured Fixture with AutoNSubstitute support for seamless mocking.
/// </summary>
public abstract class BaseTest
{
    protected IFixture Fixture { get; }

    protected BaseTest()
    {
        Fixture = new Fixture().Customize(new AutoNSubstituteCustomization());
    }

    /// <summary>
    /// helper to create a mock of type T that is automatically managed by the Fixture.
    /// </summary>
    protected T CreateMock<T>() where T : class => Fixture.Create<T>();
}
