namespace BlastManagementAPI.API.DTOs;

public class CreateBlastRequest
{
    public required string Name { get; init; }
}

public class AddHoleRequest
{
    public required string Name { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Z { get; init; }
    public required double Direction { get; init; }
    public required double Inclination { get; init; }
}

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
}

public class ApiErrorResponse
{
    public bool Success => false;
    public required string Message { get; init; }
    public string? Details { get; init; }
}
