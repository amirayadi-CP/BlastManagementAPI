namespace BlastManagementAPI.Domain;

/// <summary>
/// Represents a collar position in 3D space (X, Y, Z coordinates).
/// This is a value object — immutable and compared by value, not by reference.
/// </summary>
public class Position
{
    public double X { get; }
    public double Y { get; }
    public double Z { get; }

    public Position(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Position other) return false;
        return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y, Z);
    }

    public override string ToString()
    {
        return $"({X}, {Y}, {Z})";
    }
}
