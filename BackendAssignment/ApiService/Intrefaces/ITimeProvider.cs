using System;

namespace CleoAssignment.ApiService.Intrefaces;

public interface ITimeProvider
{
    public DateTime UtcNow { get; }
}