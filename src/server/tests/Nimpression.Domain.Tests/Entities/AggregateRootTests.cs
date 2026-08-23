using Nimpression.Domain.Common;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Exceptions;

namespace Nimpression.Domain.Tests.Entities;

public sealed class AggregateRootTests
{
    private sealed record TestDomainEvent(DateTimeOffset OccurredAt) : IDomainEvent;

    private sealed class TestAggregate : AggregateRoot
    {
        public TestAggregate(Guid id) : base(id)
        {
        }

        public void DoSomething()
        {
            AddDomainEvent(new TestDomainEvent(DateTimeOffset.UtcNow));
        }
    }

    [Fact]
    public void AggregateRoot_records_and_clears_domain_events()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());
        Assert.Empty(aggregate.DomainEvents);

        aggregate.DoSomething();
        Assert.Single(aggregate.DomainEvents);

        aggregate.ClearDomainEvents();
        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void DomainExceptions_preserve_properties_and_messages()
    {
        var domainEx = new DomainException("error");
        Assert.Equal("error", domainEx.Message);

        var inner = new InvalidOperationException("inner");
        var domainExWithInner = new DomainException("error", inner);
        Assert.Same(inner, domainExWithInner.InnerException);

        var validationEx = new DomainValidationException("invalid");
        Assert.Equal("invalid", validationEx.Message);

        var taskEx = new InvalidJobTaskTransitionException(JobTaskStatus.Completed, JobTaskStatus.Assigned);
        Assert.Equal(JobTaskStatus.Completed, taskEx.From);
        Assert.Equal(JobTaskStatus.Assigned, taskEx.To);
        Assert.Contains("Completed", taskEx.Message);
        Assert.Contains("Assigned", taskEx.Message);

        var fineEx = new InvalidFineTransitionException(FineStatus.Accepted, FineStatus.UnderReview);
        Assert.Equal(FineStatus.Accepted, fineEx.From);
        Assert.Equal(FineStatus.UnderReview, fineEx.To);
        Assert.Contains("Accepted", fineEx.Message);
        Assert.Contains("UnderReview", fineEx.Message);
    }
}
